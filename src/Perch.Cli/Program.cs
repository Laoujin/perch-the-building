using Microsoft.Extensions.DependencyInjection;
using Perch.Cli.Commands;
using Perch.Cli.Infrastructure;
using Perch.Core;
using Spectre.Console;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddPerchCore();
services.AddSingleton(AnsiConsole.Console);

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.AddCommand<DeployCommand>("deploy")
        .WithDescription("Deploy managed configs by creating symlinks");
    config.AddCommand<StatusCommand>("status")
        .WithDescription("Check for drift between managed configs and deployed symlinks");
    config.AddCommand<AppsCommand>("apps")
        .WithDescription("Show installed apps and their config module status");
    config.AddBranch("git", git =>
    {
        git.AddCommand<GitSetupCommand>("setup")
            .WithDescription("Register git clean filters defined in module manifests");
    });
    config.AddBranch("diff", diff =>
    {
        diff.AddCommand<DiffStartCommand>("start")
            .WithDescription("Capture a filesystem snapshot for change detection");
        diff.AddCommand<DiffStopCommand>("stop")
            .WithDescription("Compare current state against the captured snapshot");
    });
    config.AddBranch("restore", restore =>
    {
        restore.AddCommand<RestoreListCommand>("list")
            .WithDescription("List available pre-deploy snapshots");
        restore.AddCommand<RestoreApplyCommand>("apply")
            .WithDescription("Restore files from a pre-deploy snapshot");
    });
    config.AddCommand<CompletionCommand>("completion")
        .WithDescription("Output shell tab-completion script");
});

return app.Run(args);
