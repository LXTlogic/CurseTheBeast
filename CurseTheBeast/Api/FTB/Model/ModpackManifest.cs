using System.Text.Json.Serialization;

namespace CurseTheBeast.Api.FTB.Model;


public partial class ModpackManifest : FTBRsp
{
    public File[] files { get; init; } = null!;
    public Specs specs { get; init; } = null!;
    public Target[] targets { get; init; } = null!;
    public long installs { get; init; }
    public long plays { get; init; }
    public long refreshed { get; init; }
    /// <summary>
    /// url
    /// </summary>
    public string changelog { get; init; } = null!;
    public int parent { get; init; }
    public string notification { get; init; } = null!;
    /*
    public object[] links { get; init; } = null!;
    */
    public int id { get; init; }
    /// <summary>
    /// 1.0.0
    /// </summary>
    public string name { get; init; } = null!;
    // public string type { get; init; } = null!;
    public long updated { get; init; }
    [JsonPropertyName("private")]
    public bool private_ { get; init; }


    public class File
    {
        /// <summary>
        /// ./config/Advancedperipherals/
        /// </summary>
        public string path { get; init; } = null!;
        public string url { get; init; } = null!;
        public string[] mirrors { get; init; } = null!;
        public int size { get; init; }
        /*
        public object[] tags { get; init; } = null!;
        */
        public bool clientonly { get; init; }
        public bool serveronly { get; init; }
        public bool optional { get; init; }
        /// <summary>
        /// peripherals.toml
        /// </summary>
        public string name { get; init; } = null!;
        /// <summary>
        /// config
        /// </summary>
        public string type { get; init; } = null!;
        public long updated { get; init; }
        public FileHash hashes { get; init; } = null!;


        public class FileHash
        {
            public string sha1 { get; init; } = null!;
            public long cfMurmur { get; init; }
        }
    }


    public class Specs
    {
        public int id { get; init; }
        /// <summary>
        /// ram in MB
        /// </summary>
        public int minimum { get; init; }
        /// <summary>
        /// ram in MB
        /// </summary>
        public int recommended { get; init; }
    }


    public class Target
    {
        /// <summary>
        /// 43.2.6, 1.19.2, 17.0.2+8
        /// </summary>
        public string version { get; init; } = null!;
        public int id { get; init; }
        /// <summary>
        /// forge, minecraft, java
        /// </summary>
        public string name { get; init; } = null!;
        /// <summary>
        /// modloader, game, runtime
        /// </summary>
        public string type { get; init; } = null!;
        public long updated { get; init; }
    }

    [JsonSerializable(typeof(ModpackManifest))]
    public partial class ModpackManifestContext : JsonSerializerContext
    {

    }
}
