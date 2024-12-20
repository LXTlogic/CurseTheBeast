using CurseTheBeast.Utils;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CurseTheBeast.Storage;

// 不是前端那个（
public class LocalStorage : IDisposable
{
    public static readonly LocalStorage Persistent;

    static readonly string _tempDir;
    static volatile int _tempCounter;

    static LocalStorage()
    {
        _tempDir = Path.GetTempPath();
        _tempCounter = 0;

        var dir = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share"),
        };
        Persistent = new LocalStorage(Path.Combine(dir, AppInfo.Name), true);
    }

    public string WorkSpace { get; }

    readonly string _rootDir;
    readonly string _objectDir;
    readonly bool _isPersistent;
    readonly Locker? _locker;


    private LocalStorage(string dir, bool isPersistant)
    {
        _rootDir = dir;
        _objectDir = Path.Combine(dir, "obj");
        WorkSpace = Path.Combine(dir, "work-space");
        _isPersistent = isPersistant;

        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(_objectDir);
        Directory.CreateDirectory(WorkSpace);
        if (!isPersistant)
            _locker = Locker.AcquireOrWaitAsync(dir, default).Result;
    }

    public IEnumerable<FileEntry> GetWorkSpaceFiles(string? archiveEntryPrefix = null)
    {
        return Directory.EnumerateFiles(WorkSpace, "*", SearchOption.AllDirectories)
            .Select(file => new FileEntry(file)
                .WithArchiveEntryName(archiveEntryPrefix, Path.GetRelativePath(WorkSpace, file)));
    }

    public async Task<T> GetOrSaveObject<T>(string name, Func<Task<T>> objProvider, JsonTypeInfo<T>? type, CancellationToken ct = default) where T : class
    {
        var path = GetObjectPath<T>(name);
        using var locker = await Locker.AcquireOrWaitAsync(path, ct);
        var value = await GetObjectAsync<T>(name, type, ct);
        if(value == null)
        {
            value = await objProvider();
            await SaveObjectAsync(name, value, type, ct);
        }
        return value;
    }

    public async Task<T> GetOrUpdateObject<T>(string name, Func<T?, Task<T>> objProvider, JsonTypeInfo<T>? type, CancellationToken ct = default) where T : class
    {
        var path = GetObjectPath<T>(name);
        using var locker = await Locker.AcquireOrWaitAsync(path, ct);
        var value = await GetObjectAsync<T>(name, type, ct);
        value = await objProvider(value);
        await SaveObjectAsync(name, value, type, ct);
        return value;
    }

    public async Task<T?> GetObjectAsync<T>(string name, JsonTypeInfo<T>? type, CancellationToken ct = default) where T : class
    {
        var path = GetObjectPath<T>(name);
        if (!File.Exists(path))
            return null;
        try
        {
            await using var fs = File.OpenRead(path);
            await using var br = new BrotliStream(fs, CompressionMode.Decompress, true);
            if (type == null)
            {
                return (await JsonSerializer.DeserializeAsync<T>(br, cancellationToken: ct))!;
            }
            else
            {
                return (T)(await JsonSerializer.DeserializeAsync<T>(br, type, cancellationToken: ct))!;
            }
        }
        catch (JsonException)
        {
            File.Delete(path);
            return null;
        }
    }

    public async Task SaveObjectAsync<T>(string name, T obj, JsonTypeInfo<T>? type, CancellationToken ct = default)
    {
        await using var fs = File.Create(GetObjectPath<T>(name));
        await using var br = new BrotliStream(fs, CompressionLevel.Fastest, true);
        if(type == null)
            await JsonSerializer.SerializeAsync(br, obj, cancellationToken: ct);
        else
            await JsonSerializer.SerializeAsync(br, obj, type, cancellationToken: ct);
    }

    string GetObjectPath<T>(string name)
    {
        var dir = Path.Combine(_objectDir, typeof(T).Name);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    public string GetFilePath(RepoType repo, params string[] pathSegments)
    {
        return GetFilePath(repo, pathSegments as IEnumerable<string>);
    }

    public string GetFilePath(RepoType repo, IEnumerable<string> pathSegments)
    {
        var path = Path.Combine(Enumerable.Empty<string>().Append(_rootDir).Append(repo.ToString()).Concat(pathSegments).ToArray());
        var dir = Path.GetDirectoryName(path);
        if(!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        return path;
    }

    public static LocalStorage GetTempStorage()
    {
        return new LocalStorage(Path.Combine(_tempDir, $"{AppInfo.Name}-{Environment.ProcessId}-{Interlocked.Increment(ref _tempCounter)}"), false);
    }

    public static LocalStorage GetTempStorage(string name)
    {
        return new LocalStorage(Path.Combine(_tempDir, $"{AppInfo.Name}-{name}-{Random.Shared.NextInt64()}"), false);
    }

    public static void PruneUnusedTemp()
    {
        if (!Directory.Exists(_tempDir))
            return;
        foreach (var dir in Directory.GetDirectories(_tempDir, $"{AppInfo.Name}*", SearchOption.TopDirectoryOnly))
        {
            if (!Locker.TryAcquire(dir, out var locker))
                continue;
            try
            {
                Directory.Delete(dir, true);
            }
            catch(Exception)
            {


            }
            locker.Dispose();
        }
    }

    public void CleanLegacyAssetCache()
    {
        var dir = GetFilePath(RepoType.Asset);
        if (Directory.Exists(dir))
        {
            Focused.Status("清理失效的旧版缓存", ctx =>
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception) { }
            });
        }
    }

    public void Dispose()
    {
        if(!_isPersistent)
        {
            try
            {
                Directory.Delete(_rootDir, true);
            }
            catch(Exception)
            {

            }
            _locker!.Dispose();
        }
    }
}