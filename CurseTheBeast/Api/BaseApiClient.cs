using CurseTheBeast.Mirrors;
using CurseTheBeast.Services;
using CurseTheBeast.Utils;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace CurseTheBeast.Api;


public abstract class BaseApiClient : IDisposable
{
    public const string AcceptLanguage = "zh-CN,en,*";
    public const string AcceptEncoding = "br, gzip, deflate";
    public const string Accept = "application/json";

    protected readonly HttpClient _cli;
    protected int _timeout = 10000;
    protected int _tryTimes = 3;

    protected BaseApiClient()
    {
        var handler = HttpMessageHandlerFactory();
        if (handler is HttpClientHandler cliHandler)
            OnConfigureHttpClientHandler(cliHandler);
        _cli = HttpClientFactory(handler);
        OnConfigureHttpClient(_cli);
    }

    protected virtual HttpMessageHandler HttpMessageHandlerFactory()
    {
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            UseProxy = HttpConfigService.Proxy != null,
            Proxy = HttpConfigService.Proxy,
        };
        return handler;
    }

    protected virtual HttpClient HttpClientFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(_timeout),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };

        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(HttpConfigService.UserAgent);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(AcceptLanguage);
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd(AcceptEncoding);
        client.DefaultRequestHeaders.Accept.ParseAdd(Accept);

        return client;
    }

    protected virtual void OnConfigureHttpClientHandler(HttpClientHandler handler)
    {

    }

    protected virtual void OnConfigureHttpClient(HttpClient client)
    {

    }

    protected virtual async Task<TRsp> GetAsync<TRsp>(Uri uri, JsonTypeInfo? ctx, CancellationToken ct)
        => await CallJsonAsync<TRsp>(HttpMethod.Get, uri, null, ctx, ct);

    protected virtual async Task<TRsp> PostAsync<TRsp>(Uri uri, Func<HttpContent> contentProvider, JsonTypeInfo? type, CancellationToken ct)
        => await CallJsonAsync<TRsp>(HttpMethod.Post, uri, contentProvider, type, ct);

    protected virtual async Task<TRsp> PostJsonAsync<TRsp>(Uri uri, JsonNode content, JsonTypeInfo? type, CancellationToken ct)
    {
        return await CallJsonAsync<TRsp>(HttpMethod.Post, uri, () => JsonContent.Create(content, JsonNodeContext.Default.JsonNode), type, ct);
    }

    protected virtual async Task<bool> IsAvailableAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            using var rsp = await CallAsync(HttpMethod.Head, uri, null, ct);
            return true;
        }
        catch(Exception)
        {
            return false;
        }
    }

    protected virtual async Task<TRsp> CallJsonAsync<TRsp>(HttpMethod method, Uri uri, Func<HttpContent>? contentProvider, JsonTypeInfo? type, CancellationToken ct)
    {
        using var rsp = await CallAsync(method, uri, contentProvider, ct);
        using var stream = rsp.Content.ReadAsStream(ct);
        if (type == null)
            return (await JsonSerializer.DeserializeAsync<TRsp>(stream, cancellationToken: ct))!;
        else
            return (await JsonSerializer.DeserializeAsync<TRsp>(stream, (JsonTypeInfo<TRsp>)type, cancellationToken: ct))!;
    }

    protected virtual async Task<HttpResponseMessage> CallAsync(HttpMethod method, Uri uri, Func<HttpContent>? contentProvider, CancellationToken ct)
    {
        var uriList = MirrorManager.GetUrls(_cli.BaseAddress == null ? uri : new Uri(_cli.BaseAddress, uri)).ToArray();
        for (var i = 1; ; ++i)
        {
            try
            {
                using var req = new HttpRequestMessage()
                {
                    Method = method,
                    RequestUri = uriList[Math.Min(uriList.Length, i) - 1],
                    Content = contentProvider?.Invoke(),
                };
                var rsp = await _cli.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                rsp.EnsureSuccessStatusCode();
                return rsp;
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException && ct.IsCancellationRequested)
                    throw;
                if (i < uriList.Length)
                    continue;
                if (i >= _tryTimes)
                {
                    if (e is HttpRequestException hre && hre.StatusCode != null)
                        throw new Exception($"调用接口失败（{(int)hre.StatusCode}）：{uri} ，请重试几次，或打开\u68AF\u5B50再试");
                    else
                        throw new Exception($"调用接口失败：{uri} ，请重试几次，或打开\u68AF\u5B50再试", e);
                }
            }
        }
    }

    public void Dispose()
    {
        _cli.Dispose();
    }
}
