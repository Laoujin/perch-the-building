using System.ComponentModel;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class DeployCommand : AsyncCommand<DeployCommand.Settings>
{
    private readonly IDeployService _deployService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--config-path")]
        [Description("Path to the config repository")]
        public string? ConfigPath { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview changes without making them")]
        public bool DryRun { get; init; }

        [CommandOption("--output")]
        [Description("Output format (Pretty or Json)")]
        public OutputFormat Output { get; init; } = OutputFormat.Pretty;

        [CommandOption("--interactive")]
        [Description("Preview each module and prompt before executing")]
        public bool Interactive { get; init; }
    }

    public DeployCommand(IDeployService deployService, ISettingsProvider settingsProvider, IAnsiConsole console)
    {
        _deployService = deployService;
        _settingsProvider = settingsProvider;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string? configPath = settings.ConfigPath;

        if (string.IsNullOrWhiteSpace(configPath))
        {
            PerchSettings perchSettings = await _settingsProvider.LoadAsync(cancellationToken);
            configPath = perchSettings.ConfigRepoPath;
        }

        if (string.IsNullOrWhiteSpace(configPath))
        {
            _console.MarkupLine("[red]Error:[/] No config path specified. Use --config-path or set it in settings.");
            return 2;
        }

        if (settings.Interactive && settings.Output == OutputFormat.Json)
        {
            _console.MarkupLine("[red]Error:[/] --interactive cannot be used with --output json.");
            return 2;
        }

        if (settings.Output == OutputFormat.Json)
        {
            return await ExecuteJsonAsync(configPath, settings.DryRun, cancellationToken);
        }

        if (settings.Interactive)
        {
            return await ExecuteInteractiveAsync(configPath, settings.DryRun, cancellationToken);
        }

        return await ExecutePrettyAsync(configPath, settings.DryRun, cancellationToken);
    }

    private async Task<int> ExecutePrettyAsync(string configPath, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            _console.MarkupLine("[yellow]DRY RUN[/] — no changes will be made.");
        }

        _console.MarkupLine($"[blue]Deploying from:[/] {configPath.EscapeMarkup()}");
        _console.WriteLine();

        var table = new Table();
        table.AddColumn("Module");
        table.AddColumn("Status");
        table.AddColumn("Details");

        var moduleRows = new Dictionary<string, int>();
        var moduleActionCounts = new Dictionary<string, (int ok, int warn, int err)>();
        int exitCode = 0;

        await _console.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var progress = new SynchronousProgress<DeployResult>(result =>
                {
                    switch (result.EventType)
                    {
                        case DeployEventType.ModuleDiscovered:
                            moduleRows[result.ModuleName] = table.Rows.Count;
                            moduleActionCounts[result.ModuleName] = (0, 0, 0);
                            table.AddRow(result.ModuleName.EscapeMarkup(), "[grey]Pending[/]", "");
                            break;

                        case DeployEventType.ModuleStarted:
                            if (moduleRows.TryGetValue(result.ModuleName, out int startRow))
                            {
                                table.UpdateCell(startRow, 1, "[blue]In Progress...[/]");
                            }
                            break;

                        case DeployEventType.Action:
                            if (moduleRows.TryGetValue(result.ModuleName, out int actionRow))
                            {
                                var counts = moduleActionCounts[result.ModuleName];
                                counts = result.Level switch
                                {
                                    ResultLevel.Error => (counts.ok, counts.warn, counts.err + 1),
                                    ResultLevel.Warning => (counts.ok, counts.warn + 1, counts.err),
                                    _ => (counts.ok + 1, counts.warn, counts.err),
                                };
                                moduleActionCounts[result.ModuleName] = counts;
                                table.UpdateCell(actionRow, 2, FormatCounts(counts));
                            }
                            break;

                        case DeployEventType.ModuleCompleted:
                            if (moduleRows.TryGetValue(result.ModuleName, out int completeRow))
                            {
                                string status = result.Level == ResultLevel.Error
                                    ? "[red]Failed[/]"
                                    : "[green]Done[/]";
                                table.UpdateCell(completeRow, 1, status);
                            }
                            break;

                        case DeployEventType.ModuleSkipped:
                            table.AddRow(
                                $"[grey]{result.ModuleName.EscapeMarkup()}[/]",
                                "[grey]Skipped[/]",
                                result.Message.EscapeMarkup());
                            break;
                    }

                    ctx.Refresh();
                });

                var options = new DeployOptions { DryRun = dryRun, Progress = progress };
                exitCode = await _deployService.DeployAsync(configPath, options, cancellationToken);
            });

        _console.WriteLine();
        _console.MarkupLine(exitCode == 0
            ? "[green]Deploy complete.[/]"
            : "[red]Deploy finished with errors.[/]");

        return exitCode;
    }

    private async Task<int> ExecuteInteractiveAsync(string configPath, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            _console.MarkupLine("[yellow]DRY RUN[/] — no changes will be made.");
        }

        _console.MarkupLine($"[blue]Deploying from:[/] {configPath.EscapeMarkup()}");
        _console.WriteLine();

        bool autoAll = false;
        var options = new DeployOptions
        {
            DryRun = dryRun,
            Progress = new SynchronousProgress<DeployResult>(result =>
            {
                if (result.EventType == DeployEventType.Action)
                {
                    string status = result.Level switch
                    {
                        ResultLevel.Ok => "[green]OK[/]",
                        ResultLevel.Warning => "[yellow]WARN[/]",
                        ResultLevel.Error => "[red]FAIL[/]",
                        _ => "[grey]??[/]",
                    };
                    _console.MarkupLine($"  {status} {result.Message.EscapeMarkup()}");
                }
                else if (result.EventType == DeployEventType.ModuleSkipped)
                {
                    _console.MarkupLine($"[grey]{result.ModuleName.EscapeMarkup()}: {result.Message.EscapeMarkup()}[/]");
                }
                else if (result.EventType == DeployEventType.ModuleCompleted)
                {
                    string label = result.Level == ResultLevel.Error
                        ? "[red]Failed[/]"
                        : "[green]Done[/]";
                    _console.MarkupLine($"  {label}");
                    _console.WriteLine();
                }
            }),
            BeforeModule = (module, preview) =>
            {
                if (autoAll)
                {
                    return Task.FromResult(ModuleAction.Proceed);
                }

                _console.MarkupLine($"[bold]{module.DisplayName.EscapeMarkup()}[/]");
                if (preview.Count > 0)
                {
                    var previewTable = new Table().AddColumn("Action").AddColumn("Details");
                    foreach (DeployResult r in preview)
                    {
                        previewTable.AddRow(
                            $"[{GetColor(r.Level)}]{r.Level}[/]",
                            r.Message.EscapeMarkup());
                    }
                    _console.Write(previewTable);
                }
                else
                {
                    _console.MarkupLine("  [grey]No actions to preview.[/]");
                }

                string choice = _console.Prompt(
                    new TextPrompt<string>("  Deploy this module? [y]es / [n]o / [a]ll / [q]uit")
                        .AddChoice("y").AddChoice("n").AddChoice("a").AddChoice("q")
                        .DefaultValue("y")
                        .InvalidChoiceMessage("[red]Please enter y, n, a, or q.[/]"));

                return Task.FromResult(choice switch
                {
                    "a" => SetAutoAll(),
                    "n" => ModuleAction.Skip,
                    "q" => ModuleAction.Abort,
                    _ => ModuleAction.Proceed,
                });

                ModuleAction SetAutoAll()
                {
                    autoAll = true;
                    return ModuleAction.Proceed;
                }
            },
        };

        int exitCode = await _deployService.DeployAsync(configPath, options, cancellationToken);

        _console.MarkupLine(exitCode == 0
            ? "[green]Deploy complete.[/]"
            : "[red]Deploy finished with errors.[/]");

        return exitCode;
    }

    private async Task<int> ExecuteJsonAsync(string configPath, bool dryRun, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        var progress = new SynchronousProgress<DeployResult>(results.Add);

        var options = new DeployOptions { DryRun = dryRun, Progress = progress };
        int exitCode = await _deployService.DeployAsync(configPath, options, cancellationToken);

        var output = new
        {
            dryRun,
            exitCode,
            results = results.Select(r => new
            {
                moduleName = r.ModuleName,
                sourcePath = r.SourcePath,
                targetPath = r.TargetPath,
                level = r.Level.ToString(),
                message = r.Message,
                eventType = r.EventType.ToString(),
            }),
        };

        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        _console.WriteLine(json);
        return exitCode;
    }

    private static string FormatCounts((int ok, int warn, int err) counts)
    {
        var parts = new List<string>();
        if (counts.ok > 0) parts.Add($"[green]{counts.ok} ok[/]");
        if (counts.warn > 0) parts.Add($"[yellow]{counts.warn} warn[/]");
        if (counts.err > 0) parts.Add($"[red]{counts.err} err[/]");
        return parts.Count > 0 ? string.Join(", ", parts) : "";
    }

    private static string GetColor(ResultLevel level) => level switch
    {
        ResultLevel.Ok => "green",
        ResultLevel.Warning => "yellow",
        ResultLevel.Error => "red",
        _ => "grey",
    };

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
