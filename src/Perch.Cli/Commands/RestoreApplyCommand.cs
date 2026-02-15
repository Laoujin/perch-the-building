using System.ComponentModel;
using Perch.Core.Backup;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class RestoreApplyCommand : AsyncCommand<RestoreApplyCommand.Settings>
{
    private readonly ISnapshotProvider _snapshotProvider;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<snapshot-id>")]
        [Description("The snapshot ID to restore (e.g. 20250215-120000)")]
        public string SnapshotId { get; init; } = null!;

        [CommandOption("--file")]
        [Description("Restore only a specific file by name")]
        public string? File { get; init; }
    }

    public RestoreApplyCommand(ISnapshotProvider snapshotProvider, IAnsiConsole console)
    {
        _snapshotProvider = snapshotProvider;
        _console = console;
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var results = _snapshotProvider.RestoreSnapshot(settings.SnapshotId, settings.File, cancellationToken);

        bool hasErrors = false;

        foreach (var result in results)
        {
            string status = result.Outcome switch
            {
                RestoreOutcome.Restored => "[green]RESTORED[/]",
                RestoreOutcome.Skipped => "[yellow]SKIPPED[/]",
                RestoreOutcome.Error => "[red]ERROR[/]",
                _ => "[grey]??[/]",
            };

            string detail = result.Message != null
                ? $" {result.Message.EscapeMarkup()}"
                : $" -> {result.OriginalPath.EscapeMarkup()}";

            _console.MarkupLine($"  {status} {result.FileName.EscapeMarkup()}{detail}");

            if (result.Outcome == RestoreOutcome.Error)
            {
                hasErrors = true;
            }
        }

        return Task.FromResult(hasErrors ? 1 : 0);
    }
}
