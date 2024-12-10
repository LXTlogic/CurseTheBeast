using CurseTheBeast.Storage;
using CurseTheBeast.Services.Model;
using CurseTheBeast.Utils;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace CurseTheBeast.Packs;


public class ServerModpack
{
    private readonly FTBModpack _pack;
    private readonly IReadOnlyCollection<FileEntry> _loaderFiles;
    private readonly bool _preinstalled;
    readonly string _name;

    public ServerModpack(FTBModpack pack, IReadOnlyCollection<FileEntry> loaderFiles, bool preinstalled)
    {
        _pack = pack;
        _loaderFiles = loaderFiles;
        _preinstalled = preinstalled;
        _name = $"{pack.Name} v{pack.Version.Name} Server{(preinstalled ? " Preinstalled" : "")}";
    }

    public async Task PackServerAsync(Stream stream, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true, UTF8);
        await writeAssetsAsync(archive, ct);
        await writeManifestAsync(archive, ct);
        await setCommentAsync(archive, ct);
        await writeReadmeMdAsync(archive, ct);
        await writeIconAsync(archive, ct);
        await writeLoaderFilesAsync(archive, ct);
    }

    async Task writeManifestAsync(ZipArchive archive, CancellationToken ct)
    {
        // 瞎编的清单
        await archive.WriteJsonFileAsync("server-manifest.json", new JsonObject
        {
            ["name"] = _pack.Name,
            ["version"] = _pack.Version.Name,
            ["gameVersion"] = _pack.Runtime.GameVersion,
            ["modLoaderType"] = _pack.Runtime.ModLoaderType,
            ["modLoaderVersion"] = _pack.Runtime.ModLoaderVersion,
            ["javaVersion"] = _pack.Runtime.JavaVersion,
            ["recommendedRam"] = _pack.Runtime.RecommendedRam,
            ["minimumRam"] = _pack.Runtime.MinimumRam,
        }, ct);

        var unreachableFiles = new JsonArray(_pack.Files.ServerFiles.Where(f => f.Unreachable).Select(f => new JsonObject()
        {
            ["path"] = f.ArchiveEntryName,
            ["urls"] = new JsonArray(f.Urls.Select(url => JsonValue.Create(url)).ToArray()),
            ["curseforge"] = f.Curseforge == null ? null : new JsonObject()
            {
                ["projectId"] = f.Curseforge.ProjectId,
                ["fileId"] = f.Curseforge.FileId,
                ["page"] = $"https://www.curseforge.com/projects/{f.Curseforge.ProjectId}",
            },
        }).ToArray());
        if (unreachableFiles.Count > 0)
            await archive.WriteJsonFileAsync($"unreachable-files.json", unreachableFiles, ct);
    }

    async Task writeAssetsAsync(ZipArchive archive, CancellationToken ct)
    {
        foreach (var file in _pack.Files.ServerFiles)
        {
            if (!file.Unreachable)
            {
                await archive.WriteFileAsync(_name, file, ct);
            }
        }
    }

    Task setCommentAsync(ZipArchive archive, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_name);
        if (_pack.Summary != null)
        {
            sb.AppendLine();
            sb.AppendLine(_pack.Summary);
        }
        sb.AppendLine();
        sb.Append(_pack.Url);
        archive.Comment = sb.ToString();
        return Task.CompletedTask;
    }

    async Task writeReadmeMdAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_pack.ReadMe == null)
            return;

        await archive.WriteTextFileAsync("README.md", _pack.ReadMe, ct);
    }

    async Task writeIconAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_pack.Icon == null || _pack.Icon.Unreachable)
            return;

        await archive.WriteFileAsync(null, _pack.Icon, ct);
    }

    async Task writeLoaderFilesAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_loaderFiles == null)
            return;

        foreach (var file in _loaderFiles)
            await archive.WriteFileAsync(_name, file, ct);

        if (Environment.OSVersion.Platform == PlatformID.Win32NT && _preinstalled)
            await archive.WriteTextFileAsync("双击“run.bat”文件即可启动服务端", "", ct);
    }
}
