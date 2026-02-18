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
    config.AddBranch("filter", filter =>
    {
        filter.AddCommand<FilterCleanCommand>("clean")
            .WithDescription("Apply clean filter rules for a module (git clean filter protocol)");
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
    config.AddBranch("registry", registry =>
    {
        registry.AddCommand<RegistryCaptureCommand>("capture")
            .WithDescription("Capture current registry values for a module");
    });
    config.AddBranch("tweak", tweak =>
    {
        tweak.AddCommand<TweakListCommand>("list")
            .WithDescription("List gallery tweaks with their current status");
        tweak.AddCommand<TweakApplyCommand>("apply")
            .WithDescription("Apply a gallery tweak by ID");
        tweak.AddCommand<TweakRevertCommand>("revert")
            .WithDescription("Revert a previously applied tweak");
    });
    config.AddCommand<CompletionCommand>("completion")
        .WithDescription("Generate shell completion script");
});

return app.Run(args);
