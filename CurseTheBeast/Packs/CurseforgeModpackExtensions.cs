using CurseTheBeast.Services.Model;
using CurseTheBeast.Utils;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

namespace CurseTheBeast.Packs;


public static class CurseforgeModpackExtensions
{
    public static async Task PackCurseforgeAsync(this FTBModpack pack, Stream stream, bool full, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true, UTF8);
        await archive.writeAssetsAsync(pack, full, ct);
        await archive.writeManifestAsync(pack, full, ct);
        await archive.writeDescriptionAsync(pack, ct);
        await archive.setCommentAsync(pack, full, ct);
        await archive.writeReadmeMdAsync(pack, ct);
        await archive.writeIconAsync(pack, ct);
    }

    static async Task writeManifestAsync(this ZipArchive archive, FTBModpack pack, bool full, CancellationToken ct)
    {
        var manifest = new JsonObject
        {
            ["minecraft"] = new JsonObject
            {
                ["version"] = pack.Runtime.GameVersion,
                ["modLoaders"] = new JsonArray()
                {
                    // NativeAot下匿名类型数组的Json序列化有BUG，必须手动构造json对象
                    new JsonObject()
                    {
                        ["id"] = $"{pack.Runtime.ModLoaderType}-{pack.Runtime.ModLoaderVersion}",
                        ["primary"] = true
                    }
                }
            },
            ["manifestType"] = "minecraftModpack",
            ["manifestVersion"] = 1,
            ["name"] = pack.Name,
            ["version"] = pack.Version.Name,
            ["author"] = string.Join("; ", pack.Authors),
            ["files"] = new JsonArray((full ? Array.Empty<FTBFileEntry>() : pack.Files.ClientCurseforgeMods).Select(f => new JsonObject()
            {
                ["projectID"] = f.Curseforge!.ProjectId,
                ["fileID"] = f.Curseforge!.FileId,
                ["required"] = true,
            }).ToArray()),
            ["overrides"] = "overrides",
        };

        await archive.WriteJsonFileAsync("manifest.json", manifest, ct);

        var unreachableFiles = new JsonArray(pack.Files.ClientFullFiles.Where(f => f.Unreachable).Select(f => new JsonObject()
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
            await archive.WriteJsonFileAsync("overrides/unreachable-files.json", unreachableFiles, ct);
    }

    static async Task writeAssetsAsync(this ZipArchive archive, FTBModpack pack, bool full, CancellationToken ct)
    {
        foreach (var file in (full ? pack.Files.ClientFullFiles : pack.Files.ClientFilesWithoutCurseforgeMods))
        {
            if(!file.Unreachable)
                await archive.WriteFileAsync("overrides", file, ct);
        }
    }

    static async Task writeDescriptionAsync(this ZipArchive archive, FTBModpack pack, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var file in pack.Files.ClientCurseforgeMods)
            sb.AppendLine($"<li><a href=\"https://www.curseforge.com/projects/{file.Curseforge!.ProjectId}\">{HtmlEncoder.Default.Encode(file.DisplayName!)}</a></li>");
        sb.AppendLine("</ul>");

        await archive.WriteTextFileAsync("modlist.html", sb.ToString(), ct);
    }

    static Task setCommentAsync(this ZipArchive archive, FTBModpack pack, bool full, CancellationToken ct)
    {
        var comment = new StringBuilder();

        comment.AppendLine($"{pack.Name} v{pack.Version.Name}{(full ? " Full" : "")}");
        if (pack.Summary != null)
        {
            comment.AppendLine();
            comment.AppendLine(pack.Summary);
        }
        comment.AppendLine();
        comment.Append(pack.Url);

        archive.Comment = comment.ToString();
        return Task.CompletedTask;
    }

    static async Task writeReadmeMdAsync(this ZipArchive archive, FTBModpack pack, CancellationToken ct)
    {
        if (pack.ReadMe == null)
            return;

        await archive.WriteTextFileAsync("overrides/README.md", pack.ReadMe, ct);
    }

    static async Task writeIconAsync(this ZipArchive archive, FTBModpack pack, CancellationToken ct)
    {
        if (pack.Icon == null || pack.Icon.Unreachable)
            return;

        await archive.WriteFileAsync("overrides", pack.Icon, ct);
    }
}
