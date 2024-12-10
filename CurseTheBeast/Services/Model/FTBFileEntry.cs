using CurseTheBeast.Api.FTB.Model;
using CurseTheBeast.Storage;

namespace CurseTheBeast.Services.Model;


public class FTBFileEntry : FileEntry
{
    public long Id { get; }
    public FileSide Side { get; }
    public CurseforgeInfo? Curseforge { get; private set; }
    public string Type { get; }
    public long CFMurmur { get; }

    public FTBFileEntry(ModpackManifest.File file)
        : base(RepoType.Asset, getAssetCachePath(file.id))
    {
        Id = file.id;
        CFMurmur = file.hashes.cfMurmur;
        Type = file.type;

        WithSha1(file.hashes.sha1);
        WithSize(file.size);
        WithArchiveEntryName(file.path, file.name);

        if (ArchiveEntryName!.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) 
            && ArchiveEntryName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
            ArchiveEntryName = ArchiveEntryName.Remove(ArchiveEntryName.Length - 9);

        SetDownloadable(file.name, [file.url, ..file.mirrors]);
        // 有些mod删库跑路，跳过下载交给用户处理
        SetUnrequired();

        Side = file switch
        {
            { serveronly: true } => FileSide.Server,
            { clientonly: true } => FileSide.Client,
            _ => FileSide.Both,
        };
    }

    public FTBFileEntry WithCurseforgeInfo(long projectId, long fileId)
    {
        Curseforge = new CurseforgeInfo()
        {
            ProjectId = projectId,
            FileId = fileId
        };
        return this;
    }

    static string[] getAssetCachePath(long id)
    {
        return [((byte)(id & 0xFF)).ToString("x2"), id.ToString("x2")];
    }

    public class CurseforgeInfo
    {
        public long ProjectId { get; init; }
        public long FileId { get; init; }
    }

    [Flags]
    public enum FileSide
    {
        Client = 1,
        Server = 2,
        Both = Client | Server
    }
}
