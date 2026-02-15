using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Perch.Cli.Commands;

public sealed class CompletionCommand : Command<CompletionCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<shell>")]
        [Description("Shell type: powershell, bash, or zsh")]
        public string Shell { get; init; } = null!;
    }

    public CompletionCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string? script = settings.Shell.ToLowerInvariant() switch
        {
            "powershell" or "pwsh" => PowerShellScript,
            "bash" => BashScript,
            "zsh" => ZshScript,
            _ => null,
        };

        if (script == null)
        {
            _console.MarkupLine($"[red]Error:[/] Unknown shell '{settings.Shell.EscapeMarkup()}'. Supported: powershell, bash, zsh");
            return 1;
        }

        _console.WriteLine(script);
        return 0;
    }

    private const string PowerShellScript = """
        Register-ArgumentCompleter -CommandName perch -Native -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)
            $commands = @('deploy', 'status', 'apps', 'restore', 'git', 'diff', 'completion')
            $commands | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
            }
        }
        """;

    private const string BashScript = """
        _perch_completions() {
            local commands="deploy status apps restore git diff completion"
            COMPREPLY=($(compgen -W "$commands" -- "${COMP_WORDS[COMP_CWORD]}"))
        }
        complete -F _perch_completions perch
        """;

    private const string ZshScript = """
        _perch() {
            local commands=(deploy status apps restore git diff completion)
            _describe 'command' commands
        }
        compdef _perch perch
        """;
}
