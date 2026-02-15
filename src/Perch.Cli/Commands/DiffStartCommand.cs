using System.ComponentModel;
using Perch.Core.Diff;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class DiffStartCommand : AsyncCommand<DiffStartCommand.Settings>
{
    private readonly IDiffSnapshotService _diffService;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Directory path to snapshot")]
        public string Path { get; init; } = null!;
    }

    public DiffStartCommand(IDiffSnapshotService diffService, IAnsiConsole console)
    {
        _diffService = diffService;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            await _diffService.CaptureAsync(settings.Path, cancellationToken);
            _console.MarkupLine($"[green]Snapshot captured for:[/] {settings.Path.EscapeMarkup()}");
            _console.MarkupLine("[grey]Make your changes, then run 'perch diff stop' to see what changed.[/]");
            return 0;
        }
        catch (DirectoryNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
