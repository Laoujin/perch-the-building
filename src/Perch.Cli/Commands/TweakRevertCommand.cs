using System.ComponentModel;

using Perch.Core.Catalog;
using Perch.Core.Deploy;
using Perch.Core.Tweaks;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class TweakRevertCommand : AsyncCommand<TweakRevertCommand.Settings>
{
    private readonly ICatalogService _catalog;
    private readonly ITweakService _tweakService;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("The tweak ID to revert")]
        public string Id { get; init; } = "";

        [CommandOption("--dry-run")]
        [Description("Show what would be changed without writing")]
        public bool DryRun { get; init; }
    }

    public TweakRevertCommand(ICatalogService catalog, ITweakService tweakService, IAnsiConsole console)
    {
        _catalog = catalog;
        _tweakService = tweakService;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var tweak = await _catalog.GetTweakAsync(settings.Id, cancellationToken);
        if (tweak is null)
        {
            _console.MarkupLine($"[red]Error:[/] Tweak '{settings.Id.EscapeMarkup()}' not found.");
            return 1;
        }

        if (!tweak.Reversible)
        {
            _console.MarkupLine($"[yellow]Warning:[/] Tweak '{tweak.Name.EscapeMarkup()}' is not marked as reversible.");
        }

        if (settings.DryRun)
        {
            _console.MarkupLine($"[yellow]Dry run:[/] {tweak.Name.EscapeMarkup()}");
        }

        var result = _tweakService.Revert(tweak, settings.DryRun);

        foreach (var entry in result.Entries)
        {
            string icon = entry.Level switch
            {
                ResultLevel.Ok => "[green]OK[/]",
                ResultLevel.Warning => "[yellow]WARN[/]",
                ResultLevel.Error => "[red]ERR[/]",
                _ => "[grey]?[/]",
            };
            _console.MarkupLine($"  {icon} {entry.Message.EscapeMarkup()}");
        }

        return result.Level == ResultLevel.Error ? 1 : 0;
    }
}
