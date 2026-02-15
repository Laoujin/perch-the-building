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
});

return app.Run(args);
