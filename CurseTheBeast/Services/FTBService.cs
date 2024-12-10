using CurseTheBeast.Api.FTB;
using CurseTheBeast.Api.FTB.Model;
using CurseTheBeast.Services.Model;
using CurseTheBeast.Storage;
using Spectre.Console;
using static CurseTheBeast.Services.Model.FTBFileEntry;

namespace CurseTheBeast.Services;


public class FTBService : IDisposable
{
    static readonly IReadOnlySet<int> BlackList = new HashSet<int>()
    {
        81,     // Minecraft
        104,    // Minecraft Forge
        116,    // NeoForge
        105     // Fabric
    };

    readonly FTBApiClient _ftb;

    public FTBService()
    {
        _ftb = new FTBApiClient();
    }

    public Task<IReadOnlyList<(int Id, string Name)>> GetFeaturedModpacksAsync(CancellationToken ct = default)
    {
        return Focused.StatusAsync("获取热门整合包", async ctx =>
        {
            var result = new List<(int, string)>();
            var featuredPackIds = (await _ftb.GetFeaturedAsync(ct)).packs.ToHashSet();
            var total = featuredPackIds.Count;
            await LocalStorage.Persistent.GetOrUpdateObject("list", async cache =>
            {
                cache ??= new();
                foreach (var (id, item) in cache.Items)
                {
                    if (featuredPackIds.Remove(id))
                        result.Add((id, item.Name));
                }

                if (featuredPackIds.Count > 0)
                {
                    foreach (var id in featuredPackIds)
                    {
                        ctx.Status = Focused.Text($"获取热门整合包 {result.Count}/{total}");
                        var pack = await _ftb.GetInfoAsync(id, ct);
                        cache.Items[pack.id] = new() { Name = pack.name };
                        result.Add((pack.id, pack.name));
                    }
                }
                return cache;
            }, ModpackCache.ModpackCacheContext.Default.ModpackCache, ct);
            return (IReadOnlyList<(int Id, string Name)>)result;
        });
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> ListAsync(bool autoClear, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ = Task.Run(async () =>
        {
            var ct = cts.Token;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Spacebar)
                    {
                        cts.Cancel();
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
                }
            }
            catch (Exception)
            {

            }
        }, cts.Token);

        var idList = await Focused.StatusAsync("获取整合包列表", async ctx => await _ftb.GetListAsync(ct));
        var cache = await LocalStorage.Persistent.GetObjectAsync<ModpackCache>("list", ModpackCache.ModpackCacheContext.Default.ModpackCache);
        cache ??= new();

        return await AnsiConsole.Live(Focused.Markup("正在获取条目"))
            .AutoClear(autoClear)
            .StartAsync(async ctx =>
            {
                var ct = cts.Token;
                ctx.Refresh();

                var table = new Table();
                table.AddColumn(new TableColumn("ID"));
                table.AddColumn(new TableColumn("名称"));

                try
                {
                    foreach (var id in idList.packs)
                    {
                        if (BlackList.Contains(id))
                            continue;
                        if (cache.Items.TryGetValue(id, out var item))
                        {
                            table.AddRow(id.ToString(), item.Name);
                        }
                        else
                        {
                            var info = await _ftb.GetInfoAsync(id, ct);
                            table.AddRow(id.ToString(), info.name);
                            cache.Items[id] = new()
                            {
                                Name = info.name,
                            };
                        }

                        ctx.UpdateTarget(new Rows(table, Focused.Markup("正在获取更多条目，按空格键中止")));
                        ctx.Refresh();
                    }
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException && e.InnerException is not OperationCanceledException)
                        throw;
                    if (!ct.IsCancellationRequested)
                        throw;
                }
                finally
                {
                    if (!autoClear)
                    {
                        ctx.UpdateTarget(table);
                        ctx.Refresh();
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        cts.Cancel();
                    }
                    catch (Exception)
                    {

                    }
                }

                await LocalStorage.Persistent.SaveObjectAsync<ModpackCache>("list", cache, ModpackCache.ModpackCacheContext.Default.ModpackCache);
                return cache.Items.Where(item => !BlackList.Contains(item.Key)).Select(pair => (pair.Key, pair.Value.Name)).ToArray();
            });
    }

    public Task<IReadOnlyList<(int Id, string Name)>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        return Focused.StatusAsync("搜索中", async ctx =>
        {
            var result = await _ftb.SearchAsync(keyword, ct);
            return result.packs?.Select(p => (p.id, p.name)).ToArray() ?? Array.Empty<(int, string)>() as IReadOnlyList<(int, string)>;
        });
    }

    public Task<ModpackInfo> GetModpackInfoAsync(int modpackId, CancellationToken ct = default)
    {
        return Focused.StatusAsync("获取整合包信息", async ctx =>
        {
            return await _ftb.GetInfoAsync(modpackId, ct);
        });
    }

    public async Task<FTBModpack> GetModpackAsync(int modpackId, int versionId, CancellationToken ct = default)
    {
        return await GetModpackAsync(await GetModpackInfoAsync(modpackId, ct), versionId, ct);
    }

    public async Task<FTBModpack> GetModpackAsync(ModpackInfo info, int versionId, CancellationToken ct = default)
    {
        var version = info.versions.FirstOrDefault(v => v.id == versionId) ?? throw new Exception("Version id 不正确");

        var manifest = await LocalStorage.Persistent.GetOrSaveObject($"manifest-{info.id}-{versionId}",
            async () => await Focused.StatusAsync("获取整合包文件清单",
                async ctx => await _ftb.GetManifestAsync(info.id, versionId, ct)),
            ModpackManifest.ModpackManifestContext.Default.ModpackManifest, ct);

        var files = manifest.files.Select(f => new FTBFileEntry(f)).ToArray();
        var iconFile = info.art.FirstOrDefault(a => a.type == "square");
        // var coverFile = info.art.FirstOrDefault(a => a.type == "splash");

        await CurseforgeService.FetchModInfo(files.Where(f => f.Type.Equals("mod", StringComparison.OrdinalIgnoreCase)), ct);

        return new FTBModpack()
        {
            Id = info.id,
            Name = info.name,
            Authors = info.authors.Select(a => a.name).ToArray(),
            Summary = info.synopsis,
            ReadMe = info.description,
            Url = $"https://www.feed-the-beast.com/modpacks/" + info.id,
            Icon = iconFile == null ? null : new FileEntry(RepoType.Icon, iconFile.id.ToString())
                .WithArchiveEntryName("icon.png")
                .WithSize(iconFile.size)
                .SetUnrequired()
                .SetDownloadable("icon.png", iconFile.url),
            Version = new()
            {
                Id = manifest.id,
                Name = manifest.name,
                Type = version.type,
            },
            Runtime = new()
            {
                GameVersion = manifest.targets.First(t => t.type.Equals("game", StringComparison.OrdinalIgnoreCase)).version,
                ModLoaderType = manifest.targets.First(t => t.type.Equals("modloader", StringComparison.OrdinalIgnoreCase)).name,
                ModLoaderVersion = manifest.targets.First(t => t.type.Equals("modloader", StringComparison.OrdinalIgnoreCase)).version,
                JavaVersion = manifest.targets.FirstOrDefault(t => t.type.Equals("runtime", StringComparison.OrdinalIgnoreCase))?.version ?? "8.0.312",
                RecommendedRam = manifest.specs.recommended,
                MinimumRam = manifest.specs.minimum
            },
            Files = new()
            {
                ServerFiles = files.Where(f => f.Side.HasFlag(FileSide.Server)).ToArray(),
                ClientFilesWithoutCurseforgeMods = files.Where(f => f.Side.HasFlag(FileSide.Client)).Where(f => f.Curseforge == null).ToArray(),
                ClientFullFiles = files.Where(f => f.Side.HasFlag(FileSide.Client)).ToArray(),
                ClientCurseforgeMods = files.Where(f => f.Side.HasFlag(FileSide.Client)).Where(f => f.Curseforge != null).ToArray(),
            }
        };
    }

    public async Task DownloadModpackFilesAsync(FTBModpack pack, bool server, bool full, CancellationToken ct = default)
    {
        var files = new List<FileEntry>();
        if (server)
            files.AddRange(pack.Files.ServerFiles);
        else if (full)
            files.AddRange(pack.Files.ClientFullFiles);
        else
            files.AddRange(pack.Files.ClientFilesWithoutCurseforgeMods);

        if (pack.Icon != null)
            files.Add(pack.Icon);

        await FileDownloadService.DownloadAsync("下载整合包文件", files, ct);
        await TryRecoverUnreachableFiles(files.Where(f => f is FTBFileEntry).Select(f => f as FTBFileEntry)!, ct);

        Success.WriteLine("√ 下载完成");
    }

    public async Task TryRecoverUnreachableFiles(IEnumerable<FTBFileEntry> allFiles, CancellationToken ct = default)
    {
        var list = allFiles.Where(file => file.Unreachable && file.Curseforge != null && !string.IsNullOrWhiteSpace(file.Sha1)).ToList();
        if (list.Count == 0)
            return;

        await Focused.StatusAsync("尝试恢复失效的文件", async ctx =>
        {
            foreach (var file in list)
            {
                var rsp = await _ftb.GetModInfoAsync(file.Sha1!);
                var rspFile = rsp.versions.FirstOrDefault(f => f.sha1.Equals(file.Sha1, StringComparison.OrdinalIgnoreCase));
                if (rspFile != null)
                {
                    file.SetDownloadable(file.DisplayName!, rspFile.url);
                }
            }
        });

        await FileDownloadService.DownloadAsync("重新下载失效文件", list.Where(f => !f.Unreachable).ToArray(), ct);
    }

    public void Dispose()
    {
        _ftb.Dispose();
    }
}
