using System.Collections.Immutable;
using System.Runtime.Versioning;

using Perch.Core.Catalog;
using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class LanguagesViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private IAppDetailService _detailService = null!;
    private IPendingChangesService _pendingChanges = null!;
    private LanguagesViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _detailService = Substitute.For<IAppDetailService>();
        _pendingChanges = Substitute.For<IPendingChangesService>();

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<AppCardModel>.Empty);

        _vm = new LanguagesViewModel(_detectionService, _detailService, _pendingChanges);
    }

    [Test]
    public async Task EcosystemSort_DriftedFirst_ThenDetected_ThenSynced_ThenUnmanaged()
    {
        var unmanaged = MakeRuntime("go", "Go", CardStatus.Unmanaged);
        var synced = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced);
        var detected = MakeRuntime("node", "Node.js", CardStatus.Detected);
        var drifted = MakeRuntime("python", "Python", CardStatus.Drifted);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([unmanaged, synced, detected, drifted]);

        await _vm.RefreshCommand.ExecuteAsync(null);

        var names = _vm.Ecosystems.Select(e => e.Name).ToList();
        Assert.That(names, Is.EqualTo(new[] { "Python", "Node.js", ".NET", "Go" }));
    }

    [Test]
    public async Task EcosystemSort_AlphabeticalWithinSameTier()
    {
        var b = MakeRuntime("ruby", "Ruby", CardStatus.Unmanaged);
        var a = MakeRuntime("go", "Go", CardStatus.Unmanaged);
        var c = MakeRuntime("zig", "Zig", CardStatus.Unmanaged);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([b, a, c]);

        await _vm.RefreshCommand.ExecuteAsync(null);

        var names = _vm.Ecosystems.Select(e => e.Name).ToList();
        Assert.That(names, Is.EqualTo(new[] { "Go", "Ruby", "Zig" }));
    }

    [Test]
    public async Task SubCategoryBadgeCounts_ReflectItemStatuses()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Drifted,
            requires: ["dotnet"]);
        var ide2 = MakeApp("vs", "Visual Studio", "Languages/IDEs", CardStatus.Synced,
            requires: ["dotnet"]);
        var cli = MakeApp("dotnet-ef", "EF CLI", "Languages/CLI Tools", CardStatus.Detected,
            requires: ["dotnet"]);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide, ide2, cli]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var ideGroup = _vm.SubCategories.First(g => g.SubCategory == "IDEs");
        Assert.Multiple(() =>
        {
            Assert.That(ideGroup.DriftedCount, Is.EqualTo(1));
            Assert.That(ideGroup.SyncedCount, Is.EqualTo(1));
            Assert.That(ideGroup.DetectedCount, Is.EqualTo(0));
        });

        var cliGroup = _vm.SubCategories.First(g => g.SubCategory == "CLI Tools");
        Assert.Multiple(() =>
        {
            Assert.That(cliGroup.DetectedCount, Is.EqualTo(1));
            Assert.That(cliGroup.SyncedCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ConfigFilesSubCategory_CollectsDotfileEntries()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["nuget-config"]);
        var dotfile = MakeApp("nuget-config", "nuget.config", "Languages/Configuration Files",
            CardStatus.Detected, kind: CatalogKind.Dotfile);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, dotfile]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var configGroup = _vm.SubCategories.FirstOrDefault(g => g.SubCategory == "Configuration Files");
        Assert.That(configGroup, Is.Not.Null);
        Assert.That(configGroup!.Apps, Has.Count.EqualTo(1));
        Assert.That(configGroup.Apps[0].Id, Is.EqualTo("nuget-config"));
    }

    [Test]
    public async Task ConfigFilesSubCategory_IsLastInOrder()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider", "nuget-config"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Synced);
        var dotfile = MakeApp("nuget-config", "nuget.config", "Languages/Configuration Files",
            CardStatus.Detected, kind: CatalogKind.Dotfile);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide, dotfile]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var lastGroup = _vm.SubCategories.Last();
        Assert.That(lastGroup.SubCategory, Is.EqualTo("Configuration Files"));
    }

    [Test]
    public async Task DotfileEntries_ExcludedFromRegularSubCategories()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["nuget-config"]);
        var dotfile = MakeApp("nuget-config", "nuget.config", "Languages/Runtimes",
            CardStatus.Detected, kind: CatalogKind.Dotfile);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, dotfile]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var runtimeGroup = _vm.SubCategories.FirstOrDefault(g => g.SubCategory == "Runtimes");
        if (runtimeGroup is not null)
        {
            Assert.That(runtimeGroup.Apps.Any(a => a.Id == "nuget-config"), Is.False);
        }

        var configGroup = _vm.SubCategories.FirstOrDefault(g => g.SubCategory == "Configuration Files");
        Assert.That(configGroup, Is.Not.Null);
    }

    [Test]
    public void HasDetailPage_True_WhenEntryHasConfig()
    {
        var config = new CatalogConfigDefinition(
            ImmutableArray.Create(new CatalogConfigLink("src", ImmutableDictionary<Core.Platform, string>.Empty)));
        var entry = new CatalogEntry("test", "Test", null, "Dev", [], null, null, null, null, config, null);
        var card = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
        Assert.That(card.HasDetailPage, Is.True);
    }

    [Test]
    public void HasDetailPage_True_WhenEntryHasAlternatives()
    {
        var entry = new CatalogEntry("test", "Test", null, "Dev", [], null, null, null, null, null, null,
            Alternatives: ["alt1"]);
        var card = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
        Assert.That(card.HasDetailPage, Is.True);
    }

    [Test]
    public void HasDetailPage_True_WhenEntryHasTweaks()
    {
        var tweak = new AppOwnedTweak("t1", "Tweak", null, []);
        var entry = new CatalogEntry("test", "Test", null, "Dev", [], null, null, null, null, null, null,
            Tweaks: [tweak]);
        var card = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
        Assert.That(card.HasDetailPage, Is.True);
    }

    [Test]
    public void HasDetailPage_True_WhenEntryHasExtensions()
    {
        var ext = new CatalogExtensions(["ext1"], []);
        var entry = new CatalogEntry("test", "Test", null, "Dev", [], null, null, null, null, null, ext);
        var card = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
        Assert.That(card.HasDetailPage, Is.True);
    }

    [Test]
    public void HasDetailPage_True_WhenEntryHasInstall()
    {
        var install = new InstallDefinition("some.tool", null);
        var entry = new CatalogEntry("test", "Test", null, "Dev", [], null, null, null, install, null, null,
            Kind: CatalogKind.CliTool);
        var card = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
        Assert.That(card.HasDetailPage, Is.True);
    }

    [Test]
    public void HasDetailPage_False_WhenEntryHasNothingExpandable()
    {
        var entry = new CatalogEntry("test", "Test", null, "Dev", [], null, null, null, null, null, null,
            Kind: CatalogKind.CliTool);
        var card = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
        Assert.That(card.HasDetailPage, Is.False);
    }

    [Test]
    public async Task CollapsibleState_ExpandedWhenDriftedItems()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Drifted);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var ideGroup = _vm.SubCategories.First(g => g.SubCategory == "IDEs");
        Assert.That(ideGroup.IsExpanded, Is.True);
    }

    [Test]
    public async Task CollapsibleState_CollapsedWhenAllSyncedAndMoreThanFive()
    {
        var suggests = Enumerable.Range(1, 6).Select(i => $"cli-{i}").ToImmutableArray();
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: suggests);

        var cliTools = suggests.Select(id =>
            MakeApp(id, $"Tool {id}", "Languages/CLI Tools", CardStatus.Synced)).ToList();

        var allApps = new List<AppCardModel> { runtime };
        allApps.AddRange(cliTools);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(allApps.ToImmutableArray());

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var cliGroup = _vm.SubCategories.First(g => g.SubCategory == "CLI Tools");
        Assert.That(cliGroup.IsExpanded, Is.False);
    }

    [Test]
    public async Task CollapsibleState_ExpandedWhenFiveOrFewerItems()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["cli-1", "cli-2"]);
        var cli1 = MakeApp("cli-1", "Tool 1", "Languages/CLI Tools", CardStatus.Synced);
        var cli2 = MakeApp("cli-2", "Tool 2", "Languages/CLI Tools", CardStatus.Synced);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, cli1, cli2]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        var cliGroup = _vm.SubCategories.First(g => g.SubCategory == "CLI Tools");
        Assert.That(cliGroup.IsExpanded, Is.True);
    }

    private static AppCardModel MakeRuntime(
        string id, string name, CardStatus status,
        ImmutableArray<string> suggests = default)
    {
        var entry = new CatalogEntry(id, name, null, "Languages/Runtimes", [], null, null, null, null, null, null,
            Kind: CatalogKind.Runtime,
            Suggests: suggests);
        return new AppCardModel(entry, CardTier.Other, status);
    }

    [Test]
    public async Task SelectItemAsync_LoadsDetailAndSetsProperties()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Synced);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide]);

        var detail = new AppDetail(ide, null, null, null, null, []);
        _detailService.LoadDetailAsync(ide, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);

        await _vm.SelectItemCommand.ExecuteAsync(ide);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedItem, Is.EqualTo(ide));
            Assert.That(_vm.ItemDetail, Is.EqualTo(detail));
            Assert.That(_vm.ShowItemDetail, Is.True);
            Assert.That(_vm.ShowEcosystemDetail, Is.False);
            Assert.That(_vm.IsLoadingDetail, Is.False);
        });
    }

    [Test]
    public async Task ToggleApp_UnmanagedApp_AddsLinkChange()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Unmanaged);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide]);
        _pendingChanges.Contains(Arg.Any<string>(), Arg.Any<PendingChangeKind>()).Returns(false);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.ToggleAppCommand.Execute(ide);

        _pendingChanges.Received(1).Add(Arg.Is<LinkAppChange>(c => c.App == ide));
    }

    [Test]
    public async Task ToggleApp_ManagedApp_AddsUnlinkChange()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Synced);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide]);
        _pendingChanges.Contains(Arg.Any<string>(), Arg.Any<PendingChangeKind>()).Returns(false);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.ToggleAppCommand.Execute(ide);

        _pendingChanges.Received(1).Add(Arg.Is<UnlinkAppChange>(c => c.App == ide));
    }

    [Test]
    public async Task ToggleApp_PendingLink_RemovesIt()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Unmanaged);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide]);
        _pendingChanges.Contains("rider", PendingChangeKind.LinkApp).Returns(true);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.ToggleAppCommand.Execute(ide);

        _pendingChanges.Received(1).Remove("rider", PendingChangeKind.LinkApp);
    }

    [Test]
    public async Task BackToGrid_ClearsEverything()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced);
        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);
        Assert.That(_vm.ShowDetail, Is.True);

        _vm.BackToGridCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedEcosystem, Is.Null);
            Assert.That(_vm.SelectedItem, Is.Null);
            Assert.That(_vm.ItemDetail, Is.Null);
            Assert.That(_vm.ShowGrid, Is.True);
        });
    }

    [Test]
    public async Task BackToEcosystem_ClearsSelectedItem()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced,
            suggests: ["rider"]);
        var ide = MakeApp("rider", "Rider", "Languages/IDEs", CardStatus.Synced);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, ide]);
        _detailService.LoadDetailAsync(ide, Arg.Any<CancellationToken>())
            .Returns(new AppDetail(ide, null, null, null, null, []));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectEcosystemCommand.Execute(_vm.Ecosystems[0]);
        await _vm.SelectItemCommand.ExecuteAsync(ide);
        Assert.That(_vm.ShowItemDetail, Is.True);

        _vm.BackToEcosystemCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedItem, Is.Null);
            Assert.That(_vm.ItemDetail, Is.Null);
            Assert.That(_vm.ShowEcosystemDetail, Is.True);
            Assert.That(_vm.ShowItemDetail, Is.False);
        });
    }

    [Test]
    public async Task RefreshAsync_OnFailure_SetsErrorMessage()
    {
        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns<ImmutableArray<AppCardModel>>(_ => throw new InvalidOperationException("network error"));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Does.Contain("network error"));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task RefreshAsync_CallsInvalidateCache()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _detectionService.Received(1).InvalidateCache();
    }

    [Test]
    public async Task SearchText_FiltersEcosystems()
    {
        var dotnet = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced);
        var node = MakeRuntime("node", "Node.js", CardStatus.Detected);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([dotnet, node]);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.Ecosystems, Has.Count.EqualTo(2));

        _vm.SearchText = "Node";
        Assert.That(_vm.Ecosystems, Has.Count.EqualTo(1));
        Assert.That(_vm.Ecosystems[0].Name, Is.EqualTo("Node.js"));
    }

    [Test]
    public async Task RefreshAsync_UpdatesSummaryCounts()
    {
        var synced = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced);
        var detected = MakeRuntime("node", "Node.js", CardStatus.Detected);
        var drifted = MakeRuntime("python", "Python", CardStatus.Drifted);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([synced, detected, drifted]);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SyncedCount, Is.EqualTo(1));
            Assert.That(_vm.DetectedCount, Is.EqualTo(1));
            Assert.That(_vm.DriftedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EcosystemWithDependents_IncludesDependentItems()
    {
        var runtime = MakeRuntime("dotnet", ".NET SDK", CardStatus.Synced);
        var dep = MakeApp("dotnet-ef", "EF CLI", "Languages/CLI Tools", CardStatus.Detected,
            requires: ["dotnet"]);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns([runtime, dep]);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Ecosystems[0].Items, Has.Length.EqualTo(2));
    }

    private static AppCardModel MakeApp(
        string id, string name, string category, CardStatus status,
        CatalogKind kind = CatalogKind.App,
        ImmutableArray<string> requires = default,
        ImmutableArray<string> suggests = default)
    {
        var entry = new CatalogEntry(id, name, null, category, [], null, null, null, null, null, null,
            Kind: kind,
            Requires: requires,
            Suggests: suggests);
        return new AppCardModel(entry, CardTier.Other, status);
    }
}
