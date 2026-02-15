using System.ComponentModel;
using Perch.Core.Config;
using Perch.Core.Deploy;
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

        if (settings.Output == OutputFormat.Json)
        {
            return await ExecuteJsonAsync(configPath, settings.DryRun, cancellationToken);
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

        int exitCode = 0;

        await _console.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var progress = new SynchronousProgress<DeployResult>(result =>
                {
                    string status = result.Level switch
                    {
                        ResultLevel.Ok => "[green]OK[/]",
                        ResultLevel.Warning => "[yellow]WARN[/]",
                        ResultLevel.Error => "[red]FAIL[/]",
                        _ => "[grey]??[/]",
                    };

                    table.AddRow(
                        $"[{GetColor(result.Level)}]{result.ModuleName.EscapeMarkup()}[/]",
                        status,
                        result.Message.EscapeMarkup());
                    ctx.Refresh();
                });

                exitCode = await _deployService.DeployAsync(configPath, dryRun, progress, cancellationToken);
            });

        _console.WriteLine();
        if (exitCode == 0)
        {
            _console.MarkupLine("[green]Deploy complete.[/]");
        }
        else
        {
            _console.MarkupLine("[red]Deploy finished with errors.[/]");
        }

        return exitCode;
    }

    private async Task<int> ExecuteJsonAsync(string configPath, bool dryRun, CancellationToken cancellationToken)
    {
        var results = new List<DeployResult>();
        var progress = new SynchronousProgress<DeployResult>(results.Add);

        int exitCode = await _deployService.DeployAsync(configPath, dryRun, progress, cancellationToken);

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
            }),
        };

        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        _console.WriteLine(json);
        return exitCode;
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
