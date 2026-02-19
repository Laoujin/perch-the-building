using System.Runtime.Versioning;

using NSubstitute.ExceptionExtensions;

using Perch.Core.Startup;
using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class StartupViewModelTests
{
    private IStartupService _startupService = null!;
    private StartupViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _startupService = Substitute.For<IStartupService>();
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry>());

        _vm = new StartupViewModel(_startupService);
    }

    [Test]
    public void InitialState_IsNotLoading()
    {
        Assert.That(_vm.IsLoading, Is.False);
    }

    [Test]
    public async Task RefreshAsync_PopulatesItems()
    {
        var entries = new List<StartupEntry>
        {
            MakeEntry("Discord", "discord.exe"),
            MakeEntry("Slack", "slack.exe"),
        };
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(entries);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.StartupItems, Has.Count.EqualTo(2));
            Assert.That(_vm.FilteredItems, Has.Count.EqualTo(2));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task RefreshAsync_Cancellation_NoError()
    {
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.StartupItems, Is.Empty);
    }

    [Test]
    public async Task ToggleEnabledAsync_FlipsAndCallsService()
    {
        var entry = MakeEntry("Discord", "discord.exe", isEnabled: true);
        var card = new StartupCardModel(entry);

        await _vm.ToggleEnabledCommand.ExecuteAsync(card);

        Assert.That(card.IsEnabled, Is.False);
        await _startupService.Received(1).SetEnabledAsync(entry, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAsync_RemovesFromBothCollections()
    {
        var entries = new List<StartupEntry> { MakeEntry("Discord", "discord.exe") };
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(entries);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.StartupItems, Has.Count.EqualTo(1));

        var card = _vm.StartupItems[0];
        await _vm.RemoveStartupItemCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.StartupItems, Is.Empty);
            Assert.That(_vm.FilteredItems, Is.Empty);
        });
    }

    [Test]
    public async Task AddToStartupAsync_EmptyCommand_NoOp()
    {
        await _vm.AddToStartupCommand.ExecuteAsync("   ");

        await _startupService.DidNotReceive().AddAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StartupSource>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddToStartupAsync_ValidCommand_CallsServiceAndRefreshes()
    {
        await _vm.AddToStartupCommand.ExecuteAsync(@"C:\apps\myapp.exe");

        await _startupService.Received(1).AddAsync(
            "myapp", @"C:\apps\myapp.exe", StartupSource.RegistryCurrentUser, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SearchText_FiltersItems()
    {
        var entries = new List<StartupEntry>
        {
            MakeEntry("Discord", "discord.exe"),
            MakeEntry("Slack", "slack.exe"),
        };
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(entries);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.FilteredItems, Has.Count.EqualTo(2));

        _vm.SearchText = "disc";
        Assert.That(_vm.FilteredItems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SearchText_EmptyQuery_ShowsAll()
    {
        var entries = new List<StartupEntry>
        {
            MakeEntry("Discord", "discord.exe"),
            MakeEntry("Slack", "slack.exe"),
        };
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(entries);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SearchText = "disc";
        Assert.That(_vm.FilteredItems, Has.Count.EqualTo(1));

        _vm.SearchText = "";
        Assert.That(_vm.FilteredItems, Has.Count.EqualTo(2));
    }

    private static StartupEntry MakeEntry(string name, string command, bool isEnabled = true)
        => new(name, name, command, null, StartupSource.RegistryCurrentUser, isEnabled);
}
