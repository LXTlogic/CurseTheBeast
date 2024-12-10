using CurseTheBeast.Storage;
using CurseTheBeast.Mirrors;
using CurseTheBeast.Services;
using CurseTheBeast.Utils;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;

namespace CurseTheBeast.Download;


public class DownloadQueue : IDisposable
{
    public const int TryTimes = 3;
    public const int ConnectionTimeout = 30;
    public const int ReadTimeout = 5;
    public const string Accept = "text/html, image/gif, image/jpeg, *; q=.2, */*; q=.2";
    public const string AcceptLanguage = "zh_CN";
    public const string Pragma = "no-cache";
    public const string Connection = "keep-alive";
    public const string AcceptEncoding = "br, gzip, deflate";

    public record TaskProgressedEventArgs(int? Total, int Received, int Progressed);

    public event Action<FileEntry>? TaskStarted;
    public event Action<FileEntry, TaskProgressedEventArgs>? TaskProgressed;
    public event Action<FileEntry>? TaskFinished;

    readonly BlockingCollection<HttpClient> _cliPool;

    public DownloadQueue()
    {
        _cliPool = new BlockingCollection<HttpClient>();
    }

    public async Task DownloadAsync(IEnumerable<FileEntry> tasks, CancellationToken ct = default)
    {
        var queue = new ConcurrentQueue<FileEntry>(tasks);
        if (queue.IsEmpty)
            return;

        var thread = Math.Min(queue.Count, HttpConfigService.Thread);
        for (var i = _cliPool.Count; i < thread; ++i)
            _cliPool.Add(getHttpClient(), ct);
        await Task.WhenAll(Enumerable.Range(0, thread)
            .Select(_ => downloadWorker(queue, ct))
            .ToArray());
    }

    async Task downloadWorker(ConcurrentQueue<FileEntry> queue, CancellationToken ct = default)
    {
        using var owner = MemoryPool<byte>.Shared.Rent(1024 * 1024);
        var buffer = owner.Memory;

        while (!ct.IsCancellationRequested)
        {
            if (!queue.TryDequeue(out var file))
                return;

            using var locker = await Locker.AcquireOrWaitAsync(file.LocalPath, ct);
            if (file.Validate())
            {
                TaskFinished?.Invoke(file);
                continue;
            }
            TaskStarted?.Invoke(file);

            await downloadFile(file, buffer, ct);

            if (!file.Unreachable && !file.ValidateTempAndApply())
                throw new Exception($"文件校验失败: {file.Urls[0]}");
            TaskFinished?.Invoke(file);
        }
    }

    async ValueTask downloadFile(FileEntry file, Memory<byte> buffer, CancellationToken ct)
    {
        if (file.Urls.Count == 0)
            throw new Exception($"文件 {file.DisplayName ?? file.LocalPath} 无Url，无法下载");

        var uriList = file.Urls.SelectMany(url => MirrorManager.GetUrls(new Uri(url))).ToArray();
        for (var i = 1; ; i++)
        {
            var cli = _cliPool.Take(ct);
            var uri = uriList[Math.Min(uriList.Length, i) - 1];
            try
            {
                using var rsp = await cli.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                rsp.EnsureSuccessStatusCode();
                using var rspStream = rsp.Content.ReadAsStream(ct);
                using var fs = File.Create(file.LocalTempPath);
                var received = 0;
                var size = rsp.Content.Headers.ContentLength == null ? file.Size : (int)rsp.Content.Headers.ContentLength.Value;
                if (size != null && size != file.Size)
                    file.WithSize(size.Value);

                while (true)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(ReadTimeout));
                    var progressed = await rspStream.ReadAsync(buffer, cts.Token);
                    if (progressed == 0)
                        break;
                    received += progressed;
                    TaskProgressed?.Invoke(file, new(size, received, progressed));
                    await fs.WriteAsync(buffer[..progressed], ct);
                }
                break;
            }
            catch (Exception ex)
            {
                file.DeleteTemp();

                if (ex is OperationCanceledException && ct.IsCancellationRequested)
                    throw;

                if (i < uriList.Length)
                    continue;

                if (ex is HttpRequestException hre)
                {
                    if (hre.StatusCode != null)
                    {
                        // 400系状态码除429外全部视为unreachable
                        if (hre.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            throw new Exception("下载请求太频繁，请稍后重试，或打开\u68AF\u5B50再试。");
                        }
                        else if (((int)hre.StatusCode.Value) / 100 == 4)
                        {
                            if (file.Required)
                                throw new Exception($"文件下载失败（${(int)hre.StatusCode}）: {uri} ，请重试几次，或打开\u68AF\u5B50再试", ex);
                            else
                                file.SetUnreachable();
                            break;
                        }
                    }
                }

                cli.Dispose();
                cli = getHttpClient();
                if (i >= TryTimes)
                    throw new Exception($"文件下载失败: {uri} ，请重试几次，或打开\u68AF\u5B50再试", ex);
                else
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            finally
            {
                _cliPool.Add(cli);
            }
        }
    }

    HttpClient getHttpClient()
    {
        var cli = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseProxy = HttpConfigService.Proxy != null,
            Proxy = HttpConfigService.Proxy,
        })
        {
            Timeout = TimeSpan.FromSeconds(ConnectionTimeout),
            DefaultRequestVersion = HttpVersion.Version20
        };
        cli.DefaultRequestHeaders.TryAddWithoutValidation("Accept", Accept);
        cli.DefaultRequestHeaders.UserAgent.ParseAdd(HttpConfigService.UserAgent);
        cli.DefaultRequestHeaders.AcceptLanguage.ParseAdd(AcceptLanguage);
        cli.DefaultRequestHeaders.CacheControl = new() { NoCache = true };
        cli.DefaultRequestHeaders.Pragma.ParseAdd(Pragma);
        cli.DefaultRequestHeaders.Connection.ParseAdd(Connection);
        cli.DefaultRequestHeaders.AcceptEncoding.ParseAdd(AcceptEncoding);
        return cli;
    }

    public void Dispose()
    {
        while (_cliPool.TryTake(out var cli))
            cli.Dispose();
    }
}
