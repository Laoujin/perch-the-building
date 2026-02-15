using Perch.Core.Diff;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class DiffStopCommand : AsyncCommand
{
    private readonly IDiffSnapshotService _diffService;
    private readonly IAnsiConsole _console;

    public DiffStopCommand(IDiffSnapshotService diffService, IAnsiConsole console)
    {
        _diffService = diffService;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (!_diffService.HasActiveSnapshot())
        {
            _console.MarkupLine("[red]Error:[/] No active snapshot. Run 'perch diff start <path>' first.");
            return 1;
        }

        DiffResult result = await _diffService.CompareAsync(cancellationToken);

        if (result.Changes.Length == 0)
        {
            _console.MarkupLine("[grey]No changes detected.[/]");
            return 0;
        }

        _console.MarkupLine($"[blue]Changes in:[/] {result.RootPath.EscapeMarkup()}");
        _console.WriteLine();

        var table = new Table();
        table.AddColumn("Change");
        table.AddColumn("File");

        foreach (DiffChange change in result.Changes)
        {
            string label = change.Type switch
            {
                DiffChangeType.Added => "[green]Added[/]",
                DiffChangeType.Modified => "[yellow]Modified[/]",
                DiffChangeType.Deleted => "[red]Deleted[/]",
                _ => "[grey]Unknown[/]",
            };

            table.AddRow(label, change.RelativePath.EscapeMarkup());
        }

        _console.Write(table);
        return 0;
    }
}
