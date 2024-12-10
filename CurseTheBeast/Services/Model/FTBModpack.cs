using CurseTheBeast.Storage;

namespace CurseTheBeast.Services.Model;


public class FTBModpack
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string[] Authors { get; init; } = null!;
    public string Summary { get; init; } = null!;
    public string ReadMe { get; init; } = null!;
    public string Url { get; init; } = null!;
    public FileEntry? Icon { get; init; }

    public VersionInfo Version { get; init; } = null!;
    public RuntimeInfo Runtime { get; init; } = null!;
    public FilesInfo Files { get; init; } = null!;


    public class RuntimeInfo
    {
        public string GameVersion { get; init; } = null!;
        public string ModLoaderType { get; init; } = null!;
        public string ModLoaderVersion { get; init; } = null!;
        public string JavaVersion { get; init; } = null!;
        public int MinimumRam { get; init; }
        public int RecommendedRam { get; init; }
    }

    public class VersionInfo
    {
        public int Id { get; init; }
        public string Name { get; init; } = null!;
        public string Type { get; init; } = null!;
    }

    public class FilesInfo
    {
        public FTBFileEntry[] ServerFiles { get; init; } = null!;
        public FTBFileEntry[] ClientFullFiles { get; init; } = null!;
        public FTBFileEntry[] ClientFilesWithoutCurseforgeMods { get; init; } = null!;
        public FTBFileEntry[] ClientCurseforgeMods { get; init; } = null!;
    }
}
