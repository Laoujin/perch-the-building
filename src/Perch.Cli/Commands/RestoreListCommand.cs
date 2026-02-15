using Perch.Core.Backup;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class RestoreListCommand : Command<RestoreListCommand.Settings>
{
    private readonly ISnapshotProvider _snapshotProvider;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings;

    public RestoreListCommand(ISnapshotProvider snapshotProvider, IAnsiConsole console)
    {
        _snapshotProvider = snapshotProvider;
        _console = console;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var snapshots = _snapshotProvider.ListSnapshots();

        if (snapshots.Count == 0)
        {
            _console.MarkupLine("[grey]No snapshots found.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Timestamp");
        table.AddColumn("Files");

        foreach (var snapshot in snapshots)
        {
            table.AddRow(
                snapshot.Id.EscapeMarkup(),
                snapshot.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                snapshot.Files.Length > 0 ? snapshot.Files.Length.ToString() : "[grey]unknown[/]");
        }

        _console.Write(table);
        return 0;
    }
}
