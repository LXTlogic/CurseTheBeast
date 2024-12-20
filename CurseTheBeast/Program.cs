global using static CurseTheBeast.GlobalStyle;
global using static CurseTheBeast.Utils.MyEncoding;
using CurseTheBeast;
using CurseTheBeast.Commands;
using CurseTheBeast.Storage;
using CurseTheBeast.Utils;
using Spectre.Console;
using Spectre.Console.Cli;


Console.BackgroundColor = ConsoleColor.Black;
Console.Title = $"{AppInfo.Name} v{AppInfo.Version} - {AppInfo.Author}";
LocalStorage.PruneUnusedTemp();
LocalStorage.Persistent.CleanLegacyAssetCache();

var app = new CommandApp();
app.Configure(config =>
{
    config.Settings.ApplicationName = AppInfo.Name;
    config.Settings.ApplicationVersion = AppInfo.Version;
    config.Settings.CaseSensitivity = CaseSensitivity.None;
    config.Settings.ShowOptionDefaultValues = false;
    config.Settings.PropagateExceptions = false;
    config.Settings.StrictParsing = true;
    config.Settings.ValidateExamples = true;
    config.AddCommand<DownloadCommand>("download");
    config.AddCommand<InspectCommand>("inspect");
    config.AddCommand<FeaturedCommand>("featured");
    config.AddCommand<SearchCommand>("search");
    config.AddCommand<ListCommand>("list");
    // config.AddCommand<DiffCommand>("diff");
    config.SetExceptionHandler(ErrorUtils.Handler);
});
app.SetDefaultCommand<DefaultCommand>();

var ret = await app.RunAsync(args);
LocalStorage.PruneUnusedTemp();

if (NativeUtils.IsRunningByDoubleClick.Value)
{
    AnsiConsole.WriteLine();
    if (ret == 0)
        Focused.Write("按任意键退出...");
    else
        Error.Write("发生了错误，按任意键退出...");
    Console.ReadKey();
}
return ret;
