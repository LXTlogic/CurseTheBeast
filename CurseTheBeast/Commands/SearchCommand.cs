﻿using CurseTheBeast.Commands.Options;
using CurseTheBeast.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CurseTheBeast.Commands;


[Description("搜索整合包")]
public class SearchCommand : AsyncCommand<SearchCommand.Options>
{
    public class Options : HttpOptions
    {
        [Description("关键词")]
        [CommandArgument(0, "<Keyword>")]
        public string Keyword { get; set; } = null!;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Options options)
    {
        HttpConfigService.SetupHttp(options);
        using var ftb = new FTBService();
        var results = await ftb.SearchAsync(options.Keyword);

        var table = new Table();
        table.AddColumn(new TableColumn("Id"));
        table.AddColumn(new TableColumn("Name"));
        foreach (var item in results.OrderByDescending(item => item.Update))
        {
            table.AddRow(
                new Text(item.Id.ToString()), 
                new Text(item.Name.ToString()));
        }
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        return 0;
    }
}
