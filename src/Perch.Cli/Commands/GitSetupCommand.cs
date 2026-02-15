using System.ComponentModel;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Git;
using Perch.Core.Modules;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class GitSetupCommand : AsyncCommand<GitSetupCommand.Settings>
{
    private readonly ICleanFilterService _cleanFilterService;
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--config-path")]
        [Description("Path to the config repository")]
        public string? ConfigPath { get; init; }
    }

    public GitSetupCommand(ICleanFilterService cleanFilterService, IModuleDiscoveryService discoveryService, ISettingsProvider settingsProvider, IAnsiConsole console)
    {
        _cleanFilterService = cleanFilterService;
        _discoveryService = discoveryService;
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

        DiscoveryResult discovery = await _discoveryService.DiscoverAsync(configPath, cancellationToken);

        foreach (string error in discovery.Errors)
        {
            _console.MarkupLine($"[red]Error:[/] {error.EscapeMarkup()}");
        }

        var results = await _cleanFilterService.SetupAsync(configPath, discovery.Modules, cancellationToken);

        if (results.Length == 0)
        {
            _console.MarkupLine("[grey]No clean filters defined in any module.[/]");
            return discovery.Errors.Length > 0 ? 1 : 0;
        }

        bool hasErrors = discovery.Errors.Length > 0;
        foreach (CleanFilterResult result in results)
        {
            string prefix = result.Level switch
            {
                ResultLevel.Ok => "[green]OK[/]",
                ResultLevel.Error => "[red]FAIL[/]",
                _ => "[grey]??[/]",
            };

            _console.MarkupLine($"  {prefix} [{(result.Level == ResultLevel.Ok ? "green" : "red")}]{result.ModuleName.EscapeMarkup()}[/]: {result.Message.EscapeMarkup()}");

            if (result.Level == ResultLevel.Error)
            {
                hasErrors = true;
            }
        }

        return hasErrors ? 1 : 0;
    }
}
