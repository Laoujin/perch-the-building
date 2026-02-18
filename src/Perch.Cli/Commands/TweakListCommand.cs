using System.Collections.Immutable;
using System.ComponentModel;

using Perch.Core.Catalog;
using Perch.Core.Tweaks;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class TweakListCommand : AsyncCommand<TweakListCommand.Settings>
{
    private readonly ICatalogService _catalog;
    private readonly ITweakService _tweakService;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--output")]
        [Description("Output format (Pretty or Json)")]
        public OutputFormat Output { get; init; } = OutputFormat.Pretty;

        [CommandOption("--profile")]
        [Description("Filter by profile (e.g. developer, power-user, gamer)")]
        public string? Profile { get; init; }
    }

    public TweakListCommand(ICatalogService catalog, ITweakService tweakService, IAnsiConsole console)
    {
        _catalog = catalog;
        _tweakService = tweakService;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var tweaks = await _catalog.GetAllTweaksAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.Profile))
        {
            tweaks = tweaks.Where(t =>
                t.Profiles.IsDefaultOrEmpty ||
                t.Profiles.Any(p => p.Equals(settings.Profile, StringComparison.OrdinalIgnoreCase)))
                .ToImmutableArray();
        }

        if (tweaks.Length == 0)
        {
            _console.MarkupLine("[grey]No tweaks found.[/]");
            return 0;
        }

        if (settings.Output == OutputFormat.Json)
        {
            return ExecuteJson(tweaks);
        }

        return ExecutePretty(tweaks);
    }

    private int ExecutePretty(ImmutableArray<TweakCatalogEntry> tweaks)
    {
        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Category");
        table.AddColumn("Status");
        table.AddColumn("Registry");

        foreach (var tweak in tweaks)
        {
            var detection = _tweakService.Detect(tweak);

            string statusDisplay = detection.Status switch
            {
                TweakStatus.Applied => "[green]Applied[/]",
                TweakStatus.Partial => "[yellow]Partial[/]",
                TweakStatus.NotApplied => "[grey]Not Applied[/]",
                _ => "[grey]Unknown[/]",
            };

            table.AddRow(
                tweak.Name.EscapeMarkup(),
                tweak.Category.EscapeMarkup(),
                statusDisplay,
                tweak.Registry.Length.ToString());
        }

        _console.Write(table);
        return 0;
    }

    private int ExecuteJson(ImmutableArray<TweakCatalogEntry> tweaks)
    {
        var output = tweaks.Select(t =>
        {
            var detection = _tweakService.Detect(t);
            return new
            {
                id = t.Id,
                name = t.Name,
                category = t.Category,
                status = detection.Status.ToString(),
                registryEntries = t.Registry.Length,
            };
        });

        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        _console.WriteLine(json);
        return 0;
    }
}
