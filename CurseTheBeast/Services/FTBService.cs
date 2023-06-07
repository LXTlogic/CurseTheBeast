﻿using CurseTheBeast.Api.FTB;
using CurseTheBeast.Api.FTB.Model;
using CurseTheBeast.Services.Model;
using CurseTheBeast.Storage;
using Spectre.Console;
using static CurseTheBeast.Services.Model.FTBFileEntry;

namespace CurseTheBeast.Services;


public class FTBService : IDisposable
{
    readonly FTBApiClient _ftb;

    public FTBService()
    {
        _ftb = new FTBApiClient();
    }

    public Task<IReadOnlyList<(int Id, string Name, int update)>> GetFeaturedModpacksAsync(CancellationToken ct = default)
    {
        return Focused.StatusAsync("获取热门整合包", async ctx =>
            {
                var result = new List<(int, string, int)>();
                var featuredPackIds = (await _ftb.GetFeaturedAsync(ct)).packs.ToHashSet();
                var total = featuredPackIds.Count;
                await LocalStorage.Persistent.GetOrUpdateObject("list", async cache =>
                {
                    cache ??= new();
                    foreach (var (id, item) in cache.Items)
                    {
                        if (featuredPackIds.Remove(id))
                            result.Add((id, item.Name, item.Update));
                    }

                    if (featuredPackIds.Count > 0)
                    {
                        foreach (var id in featuredPackIds)
                        {
                            ctx.Status = Focused.Text($"获取热门整合包 {result.Count}/{total}");
                            var pack = await _ftb.GetInfoAsync(id, ct);
                            cache.Items[pack.id] = new() { Name = pack.name, Update = pack.updated };
                            result.Add((pack.id, pack.name, pack.updated));
                        }
                    }
                    return cache;
                }, ModpackCache.ModpackCacheContext.Default.ModpackCache, ct);
                return (IReadOnlyList<(int Id, string Name, int Update)>)result;
            });
    }

    public Task<IReadOnlyList<(int Id, string Name, int Update)>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        return Focused.StatusAsync("搜索中", async ctx =>
            {
                var result = await _ftb.SearchAsync(keyword, ct);
                return result.packs?.Select(p => (p.id, p.name, p.updated)).ToArray() ?? Array.Empty<(int, string, int)>() as IReadOnlyList<(int, string, int)> ;
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
            null, ct);

        var files = manifest.files.Select(f => new FTBFileEntry(f)).ToArray();
        var iconFile = info.art.FirstOrDefault(a => a.type == "square");
        // var coverFile = info.art.FirstOrDefault(a => a.type == "splash");

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
                ClientFilesWithoutCurseforge = files.Where(f => f.Curseforge == null).Where(f => f.Side.HasFlag(FileSide.Client)).ToArray(),
                ClientFullFiles = files.Where(f => f.Side.HasFlag(FileSide.Client)).ToArray(),
                ClientCurseforgeFiles = files.Where(f => f.Curseforge != null).Where(f => f.Side.HasFlag(FileSide.Client)).ToArray(),
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
            files.AddRange(pack.Files.ClientFilesWithoutCurseforge);

        if (pack.Icon != null)
            files.Add(pack.Icon);

        await FileDownloadService.DownloadAsync("下载整合包文件", files, ct);
        Success.WriteLine("√ 下载完成");
    }

    public void Dispose()
    {
        _ftb.Dispose();
    }
}