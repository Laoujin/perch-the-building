using System.Collections.Immutable;
using System.Runtime.Versioning;
using System.Windows;

using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Scanner;
using Perch.Core.Startup;
using Perch.Core.Tweaks;
using Perch.Desktop;
using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class AppsViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private IAppDetailService _detailService = null!;
    private ISettingsProvider _settingsProvider = null!;
    private IPendingChangesService _pendingChanges = null!;
    private AppsViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _detailService = Substitute.For<IAppDetailService>();
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _pendingChanges = Substitute.For<IPendingChangesService>();

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Developer"] });

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(
                ImmutableArray<AppCardModel>.Empty,
                ImmutableArray<AppCardModel>.Empty,
                ImmutableArray<AppCardModel>.Empty));

        _vm = new AppsViewModel(_detectionService, _detailService, _settingsProvider, _pendingChanges);
    }

    [Test]
    public void InitialState_IsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.IsLoading, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
            Assert.That(_vm.Categories, Is.Empty);
        });
    }

    [Test]
    public async Task RefreshAsync_PopulatesCategories()
    {
        var yourApp = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        var suggested = MakeCard("rider", "Development/IDEs", CardStatus.Detected);
        var other = MakeCard("vlc", "Media/Players", CardStatus.Detected, CardTier.Other);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(
                ImmutableArray.Create(yourApp),
                ImmutableArray.Create(suggested),
                ImmutableArray.Create(other)));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.Categories, Has.Count.EqualTo(2));
            Assert.That(_vm.Categories[0].BroadCategory, Is.EqualTo("Development"));
            Assert.That(_vm.Categories[0].ItemCount, Is.EqualTo(2));
            Assert.That(_vm.Categories[1].BroadCategory, Is.EqualTo("Media"));
            Assert.That(_vm.Categories[1].ItemCount, Is.EqualTo(1));
            Assert.That(_vm.LinkedCount, Is.EqualTo(1));
            Assert.That(_vm.DetectedCount, Is.EqualTo(2));
            Assert.That(_vm.IsLoading, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task RefreshAsync_OnFailure_SetsErrorMessage()
    {
        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("catalog unavailable"));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Does.Contain("catalog unavailable"));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public void ToggleApp_Detected_AddsLinkChange()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Detected);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<LinkAppChange>(c => c.App == card));
    }

    [Test]
    public void ToggleApp_Linked_AddsUnlinkChange()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<UnlinkAppChange>(c => c.App == card));
    }

    [Test]
    public void ToggleApp_Broken_AddsUnlinkChange()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Drifted);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<UnlinkAppChange>(c => c.App == card));
    }

    [Test]
    public void ToggleApp_Unmanaged_AddsLinkChange()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Unmanaged);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<LinkAppChange>(c => c.App == card));
    }

    [Test]
    public void ToggleApp_WithExistingLinkPending_RemovesIt()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Detected);
        _pendingChanges.Contains(card.Id, PendingChangeKind.LinkApp).Returns(true);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Remove(card.Id, PendingChangeKind.LinkApp);
        _pendingChanges.DidNotReceive().Add(Arg.Any<PendingChange>());
    }

    [Test]
    public void ToggleApp_WithExistingUnlinkPending_RemovesIt()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        _pendingChanges.Contains(card.Id, PendingChangeKind.UnlinkApp).Returns(true);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Remove(card.Id, PendingChangeKind.UnlinkApp);
        _pendingChanges.DidNotReceive().Add(Arg.Any<PendingChange>());
    }

    [Test]
    public async Task SearchText_FiltersCategories()
    {
        var yourApp = MakeCard("vscode", "Development/IDEs");
        var suggested = MakeCard("rider", "Development/IDEs");
        var other = MakeCard("vlc", "Media/Players", CardStatus.Detected, CardTier.Other);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(
                ImmutableArray.Create(yourApp),
                ImmutableArray.Create(suggested),
                ImmutableArray.Create(other)));

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.Categories, Has.Count.EqualTo(2));

        _vm.SearchText = "vscode";
        Assert.That(_vm.Categories, Has.Count.EqualTo(1));
        Assert.That(_vm.Categories[0].BroadCategory, Is.EqualTo("Development"));
        Assert.That(_vm.Categories[0].ItemCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SubGroups_SortsByTierThenStatusThenName()
    {
        var apps = ImmutableArray.Create(
            MakeCard("zapp", "Dev/IDEs", CardStatus.Detected, CardTier.Other),
            MakeCard("aapp", "Dev/IDEs", CardStatus.Synced, CardTier.YourApps),
            MakeCard("mapp", "Dev/IDEs", CardStatus.Drifted, CardTier.YourApps),
            MakeCard("sapp", "Dev/IDEs", CardStatus.Detected, CardTier.Suggested));

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(
                apps.Where(a => a.Tier == CardTier.YourApps).ToImmutableArray(),
                apps.Where(a => a.Tier == CardTier.Suggested).ToImmutableArray(),
                apps.Where(a => a.Tier == CardTier.Other).ToImmutableArray()));

        await _vm.RefreshCommand.ExecuteAsync(null);

        var subGroups = _vm.Categories.Single(c => c.BroadCategory == "Dev").SubGroups;
        Assert.That(subGroups, Has.Count.EqualTo(1));

        var group = subGroups[0];
        Assert.Multiple(() =>
        {
            Assert.That(group.SubCategory, Is.EqualTo("IDEs"));
            Assert.That(group.Apps[0].Name, Is.EqualTo("mapp"));  // YourApps + Broken (status 0)
            Assert.That(group.Apps[1].Name, Is.EqualTo("aapp"));  // YourApps + Linked (status 2)
            Assert.That(group.Apps[2].Name, Is.EqualTo("sapp"));  // Suggested
            Assert.That(group.Apps[3].Name, Is.EqualTo("zapp"));  // Other
        });
    }

    [Test]
    public async Task DependencyGraph_ChildHiddenFromTopLevel()
    {
        var parent = MakeCard("dotnet-sdk", "Development/Runtimes", CardStatus.Synced);
        var child = MakeCardWithRequires("vscode", "Development/IDEs", ["dotnet-sdk"], CardStatus.Detected);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(parent, child), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        var subGroups = _vm.Categories.Single(c => c.BroadCategory == "Development").SubGroups;
        var runtimes = subGroups.Single(g => g.SubCategory == "Runtimes");

        Assert.Multiple(() =>
        {
            Assert.That(runtimes.Apps, Has.Count.EqualTo(1));
            Assert.That(runtimes.Apps[0].Name, Is.EqualTo("dotnet-sdk"));
            Assert.That(runtimes.Apps[0].HasDependents, Is.True);
            Assert.That(runtimes.Apps[0].DependentApps, Has.Length.EqualTo(1));
            Assert.That(runtimes.Apps[0].DependentApps[0].Name, Is.EqualTo("vscode"));
        });
    }

    [Test]
    public async Task DependencyGraph_CircularRequires_BothStayTopLevel()
    {
        var a = MakeCardWithRequires("app-a", "Dev/Tools", ["app-b"]);
        var b = MakeCardWithRequires("app-b", "Dev/Tools", ["app-a"]);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(a, b), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        var subGroups = _vm.Categories.Single(c => c.BroadCategory == "Dev").SubGroups;
        Assert.Multiple(() =>
        {
            Assert.That(subGroups, Has.Count.EqualTo(1));
            Assert.That(subGroups[0].Apps, Has.Count.EqualTo(2));
            Assert.That(a.HasDependents, Is.False);
            Assert.That(b.HasDependents, Is.False);
        });
    }

    [Test]
    public async Task Search_ParentShowsIfDependentMatches()
    {
        var parent = MakeCard("dotnet-sdk", "Development/Runtimes", CardStatus.Synced);
        var child = MakeCardWithRequires("my-child-tool", "Development/Tools", ["dotnet-sdk"]);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(parent, child), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.SearchText = "my-child";
        Assert.That(_vm.Categories, Has.Count.EqualTo(1));
        var subGroups = _vm.Categories[0].SubGroups;
        Assert.That(subGroups, Has.Count.EqualTo(1));
        Assert.That(subGroups[0].Apps[0].Name, Is.EqualTo("dotnet-sdk"));
    }

    [Test]
    public void TagClick_SetsSearchText()
    {
        _vm.TagClickCommand.Execute("editor");
        Assert.That(_vm.SearchText, Is.EqualTo("editor"));
    }

    private static AppCardModel MakeCard(string name, string category, CardStatus status = CardStatus.Detected, CardTier tier = CardTier.YourApps)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null);
        return new AppCardModel(entry, tier, status);
    }

    private static AppCardModel MakeCardWithRequires(string name, string category, string[] requires, CardStatus status = CardStatus.Detected, CardTier tier = CardTier.YourApps)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null,
            Requires: [.. requires]);
        return new AppCardModel(entry, tier, status);
    }

    private static AppCardModel MakeCardWithSuggests(string name, string category, string[] suggests, CardStatus status = CardStatus.Detected, CardTier tier = CardTier.YourApps)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null,
            Suggests: [.. suggests]);
        return new AppCardModel(entry, tier, status);
    }

    [Test]
    public void ShowGrid_AlwaysTrue()
    {
        Assert.That(_vm.ShowGrid, Is.True);
    }

    [Test]
    public void ShowDetail_AlwaysFalse()
    {
        Assert.That(_vm.ShowDetail, Is.False);
    }

    [Test]
    public async Task ToggleExpand_SetsIsExpandedAndLoadsDetail()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ToggleExpandCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(card.IsExpanded, Is.True);
            Assert.That(card.Detail, Is.EqualTo(detail));
            Assert.That(card.IsLoadingDetail, Is.False);
        });
    }

    [Test]
    public async Task ToggleExpand_Collapse_SetsIsExpandedFalse()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ToggleExpandCommand.ExecuteAsync(card);
        Assert.That(card.IsExpanded, Is.True);

        await _vm.ToggleExpandCommand.ExecuteAsync(card);
        Assert.That(card.IsExpanded, Is.False);
    }

    [Test]
    public async Task ToggleExpand_CachedDetail_SkipsReload()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ToggleExpandCommand.ExecuteAsync(card);
        await _vm.ToggleExpandCommand.ExecuteAsync(card);
        await _vm.ToggleExpandCommand.ExecuteAsync(card);

        await _detailService.Received(1).LoadDetailAsync(card, Arg.Any<CancellationToken>());
        Assert.That(card.IsExpanded, Is.True);
    }

    [Test]
    public async Task ToggleExpand_LoadFailure_CollapsesCard()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await _vm.ToggleExpandCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(card.IsExpanded, Is.False);
            Assert.That(card.Detail, Is.Null);
        });
    }

    [Test]
    public async Task NavigateToApp_KnownApp_SetsSearchToDisplayLabel()
    {
        var app = MakeCard("vscode", "Development/IDEs", CardStatus.Synced);
        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(app), [], []));
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.NavigateToAppCommand.Execute("vscode");

        Assert.That(_vm.SearchText, Is.EqualTo(app.DisplayLabel));
    }

    [Test]
    public void NavigateToApp_UnknownApp_SetsSearchToId()
    {
        _vm.NavigateToAppCommand.Execute("unknown-app");

        Assert.That(_vm.SearchText, Is.EqualTo("unknown-app"));
    }

    [Test]
    public async Task RefreshAsync_Cancellation_NoError()
    {
        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Is.Null);
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public void ToggleCategoryExpand_TogglesIsExpanded()
    {
        var category = new AppCategoryCardModel("Dev", "Dev", 5, 2);
        Assert.That(category.IsExpanded, Is.False);

        _vm.ToggleCategoryExpandCommand.Execute(category);
        Assert.That(category.IsExpanded, Is.True);

        _vm.ToggleCategoryExpandCommand.Execute(category);
        Assert.That(category.IsExpanded, Is.False);
    }

    [Test]
    public async Task ComputeTopPicks_SetsIsHot_WhenStarsDouble()
    {
        var app1 = MakeCardWithAlternatives("editor1", "Dev/Editors", ["editor2", "editor3"]);
        app1.GitHubStars = 10000;
        var app2 = MakeCardWithAlternatives("editor2", "Dev/Editors", ["editor1", "editor3"]);
        app2.GitHubStars = 4000;
        var app3 = MakeCardWithAlternatives("editor3", "Dev/Editors", ["editor1", "editor2"]);
        app3.GitHubStars = 3000;

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult([], [], ImmutableArray.Create(app1, app2, app3)));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(app1.IsHot, Is.True);
    }

    [Test]
    public async Task ComputeTopPicks_NoHot_WhenManagedAppsInGroup()
    {
        var app1 = MakeCardWithAlternatives("editor1", "Dev/Editors", ["editor2"], CardStatus.Synced);
        app1.GitHubStars = 10000;
        var app2 = MakeCardWithAlternatives("editor2", "Dev/Editors", ["editor1"]);
        app2.GitHubStars = 2000;

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(app1), [], ImmutableArray.Create(app2)));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(app1.IsHot, Is.False);
    }

    [Test]
    public async Task ComputeTopPicks_NoHot_WhenStarsNotDoubled()
    {
        var app1 = MakeCardWithAlternatives("editor1", "Dev/Editors", ["editor2"]);
        app1.GitHubStars = 5000;
        var app2 = MakeCardWithAlternatives("editor2", "Dev/Editors", ["editor1"]);
        app2.GitHubStars = 4000;

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult([], [], ImmutableArray.Create(app1, app2)));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(app1.IsHot, Is.False);
            Assert.That(app2.IsHot, Is.False);
        });
    }

    [Test]
    public async Task SubCategoryOrder_UsesDefinedOrder()
    {
        var ide = MakeCard("rider", "Development/IDEs", CardStatus.Detected);
        var cli = MakeCard("dotnet-cli", "Development/CLI Tools", CardStatus.Detected);
        var editor = MakeCard("vim", "Development/Editors", CardStatus.Detected);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(cli, editor, ide), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        var devCategory = _vm.Categories.Single(c => c.BroadCategory == "Development");
        var subNames = devCategory.SubGroups.Select(g => g.SubCategory).ToList();
        Assert.That(subNames, Is.EqualTo(new[] { "IDEs", "Editors", "CLI Tools" }));
    }

    [Test]
    public async Task ProfilePriority_CategoriesWithProfileMatchFirst()
    {
        var devApp = MakeCardWithProfiles("vscode", "Development/IDEs", ["developer"], CardStatus.Detected);
        var gameApp = MakeCard("steam", "Gaming/Stores", CardStatus.Detected);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(gameApp, devApp), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Categories[0].BroadCategory, Is.EqualTo("Development"));
    }

    private static AppCardModel MakeCardWithAlternatives(string name, string category, string[] alternatives, CardStatus status = CardStatus.Unmanaged, CardTier tier = CardTier.Other)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null,
            Alternatives: [.. alternatives]);
        return new AppCardModel(entry, tier, status);
    }

    private static AppCardModel MakeCardWithProfiles(string name, string category, string[] profiles, CardStatus status = CardStatus.Detected, CardTier tier = CardTier.YourApps)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null,
            Profiles: [.. profiles]);
        return new AppCardModel(entry, tier, status);
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class DotfilesViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private IAppDetailService _detailService = null!;
    private IPendingChangesService _pendingChanges = null!;
    private DotfilesViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _detailService = Substitute.For<IAppDetailService>();
        _pendingChanges = Substitute.For<IPendingChangesService>();

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<AppCardModel>.Empty);

        _vm = new DotfilesViewModel(_detectionService, _detailService, _pendingChanges);
    }

    [Test]
    public void InitialState_ShowsCardGrid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowGrid, Is.True);
            Assert.That(_vm.ShowDetail, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task RefreshAsync_PopulatesDotfiles()
    {
        var dotfiles = ImmutableArray.Create(
            MakeDotfileCard("bashrc", CardStatus.Synced),
            MakeDotfileCard("gitconfig", CardStatus.Detected));

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(dotfiles);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.Dotfiles, Has.Count.EqualTo(2));
            Assert.That(_vm.LinkedCount, Is.EqualTo(1));
            Assert.That(_vm.TotalCount, Is.EqualTo(2));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task RefreshAsync_OnFailure_SetsErrorMessage()
    {
        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("catalog down"));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Does.Contain("catalog down"));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task BackToGrid_ResetsSelection()
    {
        var card = MakeDotfileCard("bashrc", CardStatus.Synced);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);
        Assert.That(_vm.ShowDetail, Is.True);

        _vm.BackToGridCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowGrid, Is.True);
            Assert.That(_vm.SelectedApp, Is.Null);
            Assert.That(_vm.Detail, Is.Null);
        });
    }

    [Test]
    public async Task SearchText_FiltersDotfiles()
    {
        var dotfiles = ImmutableArray.Create(
            MakeDotfileCard("bashrc", CardStatus.Synced),
            MakeDotfileCard("gitconfig", CardStatus.Detected));

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(dotfiles);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.Dotfiles, Has.Count.EqualTo(2));

        _vm.SearchText = "bash";
        Assert.That(_vm.Dotfiles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ConfigureAsync_SetsSelectedAppAndLoadsDetail()
    {
        var card = MakeDotfileCard("bashrc", CardStatus.Synced);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedApp, Is.EqualTo(card));
            Assert.That(_vm.Detail, Is.EqualTo(detail));
            Assert.That(_vm.ShowDetail, Is.True);
        });
    }

    [Test]
    public async Task ConfigureAsync_SetsIsLoadingDetail()
    {
        var card = MakeDotfileCard("bashrc", CardStatus.Synced);
        var isLoadingDuringLoad = false;
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isLoadingDuringLoad = _vm.IsLoadingDetail;
                return new AppDetail(card, null, null, null, null, []);
            });

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(isLoadingDuringLoad, Is.True);
            Assert.That(_vm.IsLoadingDetail, Is.False);
        });
    }

    [Test]
    public async Task LinkedCount_OnlyCountsLinkedStatus()
    {
        var dotfiles = ImmutableArray.Create(
            MakeDotfileCard("bashrc", CardStatus.Synced),
            MakeDotfileCard("gitconfig", CardStatus.Detected),
            MakeDotfileCard("vimrc", CardStatus.Synced));

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(dotfiles);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.LinkedCount, Is.EqualTo(2));
            Assert.That(_vm.TotalCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task HasModule_WhenDetailHasOwningModule_ReturnsTrue()
    {
        var card = MakeDotfileCard("bashrc", CardStatus.Synced);
        var module = new AppModule("bash", "Bash", true, "/modules/bash", [], []);
        var detail = new AppDetail(card, module, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.That(_vm.HasModule, Is.True);
    }

    [Test]
    public async Task HasNoModule_WhenDetailHasNullModule_ReturnsTrue()
    {
        var card = MakeDotfileCard("bashrc", CardStatus.Synced);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.That(_vm.HasNoModule, Is.True);
    }

    private static AppCardModel MakeDotfileCard(string name, CardStatus status)
    {
        var entry = new CatalogEntry(name, name, name, "Shell", [], null, null, null, null, null, null, CatalogKind.Dotfile);
        return new AppCardModel(entry, CardTier.Other, status);
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class SystemTweaksViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private ISettingsProvider _settingsProvider = null!;
    private IStartupService _startupService = null!;
    private ITweakService _tweakService = null!;
    private ICertificateScanner _certificateScanner = null!;
    private IPendingChangesService _pendingChanges = null!;
    private SystemTweaksViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _startupService = Substitute.For<IStartupService>();
        _tweakService = Substitute.For<ITweakService>();
        _certificateScanner = Substitute.For<ICertificateScanner>();
        _pendingChanges = Substitute.For<IPendingChangesService>();

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Developer"] });

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(ImmutableArray<TweakCardModel>.Empty, ImmutableArray<TweakDetectionError>.Empty));
        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(new FontDetectionResult(
                ImmutableArray<FontCardModel>.Empty,
                ImmutableArray<FontCardModel>.Empty));
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<StartupEntry>());
        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<DetectedCertificate>.Empty);

        _vm = new SystemTweaksViewModel(_detectionService, _settingsProvider, _startupService, _tweakService, _certificateScanner, _pendingChanges);
    }

    [Test]
    public void InitialState_ShowsCategories()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowGrid, Is.True);
            Assert.That(_vm.ShowDetail, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task RefreshAsync_UsesProfilesFromSettings()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Gamer", "Casual"] });

        await _vm.RefreshCommand.ExecuteAsync(null);

        await _detectionService.Received(1).DetectTweaksAsync(
            Arg.Is<IReadOnlySet<UserProfile>>(p =>
                p.Contains(UserProfile.Gamer) && p.Contains(UserProfile.Casual) && p.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefreshAsync_FallsBackToDefaultProfiles_WhenNoneStored()
    {
        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings());

        await _vm.RefreshCommand.ExecuteAsync(null);

        await _detectionService.Received(1).DetectTweaksAsync(
            Arg.Is<IReadOnlySet<UserProfile>>(p =>
                p.Contains(UserProfile.Developer) && p.Contains(UserProfile.PowerUser) && p.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefreshAsync_OnFailure_SetsErrorMessage()
    {
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("network error"));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Does.Contain("network error"));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task SelectCategory_NavigatesToDetail()
    {
        _vm.SelectCategoryCommand.Execute("System Tweaks");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowDetail, Is.True);
            Assert.That(_vm.ShowGrid, Is.False);
            Assert.That(_vm.SelectedCategory, Is.EqualTo("System Tweaks"));
        });

        await Task.CompletedTask;
    }

    [Test]
    public void BackToCategories_ResetsNavigation()
    {
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.BackToCategoriesCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowGrid, Is.True);
            Assert.That(_vm.SelectedCategory, Is.Null);
        });
    }

    [Test]
    public async Task SubGroups_ReturnsTweaksGroupedBySubCategory()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Explorer/Files"),
            MakeTweak("tweak2", "Explorer/Files"),
            MakeTweak("tweak3", "Explorer/Context Menu"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SetProfileFilterCommand.Execute("All");

        var subGroups = _vm.SubCategories.Single(c => c.Category == "Explorer").SubGroups;
        Assert.Multiple(() =>
        {
            Assert.That(subGroups, Has.Count.EqualTo(2));
            Assert.That(subGroups[0].SubCategory, Is.EqualTo("Context Menu"));
            Assert.That(subGroups[1].SubCategory, Is.EqualTo("Files"));
            Assert.That(subGroups[1].Tweaks, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public async Task RefreshAsync_PopulatesTweaksAndFonts()
    {
        var tweaks = ImmutableArray.Create(MakeTweak("tweak1", "Registry"), MakeTweak("tweak2", "Explorer"));
        var fonts = new FontDetectionResult(
            ImmutableArray.Create(MakeFont("Arial")),
            ImmutableArray.Create(MakeFont("FiraCode")));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));
        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.Tweaks, Has.Count.EqualTo(2));
            Assert.That(_vm.InstalledFonts, Has.Count.EqualTo(1));
            Assert.That(_vm.NerdFonts, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RefreshAsync_BuildsSingleSystemTweaksCategory()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Registry"),
            MakeTweak("tweak2", "Registry"),
            MakeTweak("tweak3", "Explorer"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Categories.Any(c => c.Category == "System Tweaks"), Is.True);
        Assert.That(_vm.Categories.First(c => c.Category == "System Tweaks").ItemCount, Is.EqualTo(3));
    }

    [Test]
    public async Task RefreshAsync_FontsCategoryAddedWhenFontsExist()
    {
        var fonts = new FontDetectionResult(
            ImmutableArray.Create(MakeFont("Arial")),
            ImmutableArray<FontCardModel>.Empty);

        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Categories.Any(c => c.Category == "Fonts"), Is.True);
    }

    [Test]
    public async Task RebuildSubCategories_GroupsByBroadCategory()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Explorer/Files"),
            MakeTweak("tweak2", "Explorer/Context Menu"),
            MakeTweak("tweak3", "Privacy/Telemetry"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SetProfileFilterCommand.Execute("All");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SubCategories, Has.Count.EqualTo(2));
            Assert.That(_vm.SubCategories[0].Category, Is.EqualTo("Explorer"));
            Assert.That(_vm.SubCategories[0].ItemCount, Is.EqualTo(2));
            Assert.That(_vm.SubCategories[1].Category, Is.EqualTo("Privacy"));
            Assert.That(_vm.SubCategories[1].ItemCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SelectCategory_SystemTweaks_BuildsSubCategories()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Explorer/Files"),
            MakeTweak("tweak2", "Explorer/Context Menu"),
            MakeTweak("tweak3", "Privacy/Telemetry"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SetProfileFilterCommand.Execute("All");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowSubCategories, Is.True);
            Assert.That(_vm.SubCategories, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task SelectCategory_Fonts_ShowsSubCategoriesFalse()
    {
        var tweaks = ImmutableArray.Create(MakeTweak("tweak1", "Explorer/Files"));
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Fonts");

        Assert.That(_vm.ShowSubCategories, Is.False);
    }

    [Test]
    public async Task FontSearchText_FiltersInstalledFontGroups()
    {
        var fonts = new FontDetectionResult(
            ImmutableArray.Create(MakeFont("Arial"), MakeFont("Consolas")),
            ImmutableArray<FontCardModel>.Empty);

        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.FilteredInstalledFontGroups, Has.Count.EqualTo(2));

        _vm.FontSearchText = "Arial";
        Assert.That(_vm.FilteredInstalledFontGroups, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task FontSearchText_FiltersNerdFonts()
    {
        var fonts = new FontDetectionResult(
            ImmutableArray<FontCardModel>.Empty,
            ImmutableArray.Create(MakeFont("FiraCode"), MakeFont("JetBrains")));

        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.FilteredNerdFonts, Has.Count.EqualTo(2));

        _vm.FontSearchText = "Fira";
        Assert.That(_vm.FilteredNerdFonts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SearchText_FiltersSubCategories()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Explorer/Files"),
            MakeTweak("tweak2", "Explorer/Files"),
            MakeTweak("tweak3", "Privacy/Telemetry"));
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SetProfileFilterCommand.Execute("All");
        Assert.That(_vm.SubCategories, Has.Count.EqualTo(2));

        _vm.SearchText = "tweak3";
        Assert.That(_vm.SubCategories, Has.Count.EqualTo(1));
        Assert.That(_vm.SubCategories[0].Category, Is.EqualTo("Privacy"));
    }

    [Test]
    public async Task RefreshAsync_SetsIsSuggestedOnMatchingTweaks()
    {
        // TweakCatalogEntry: Id, Name, Category, Tags, Description, Reversible, Profiles, Registry
        var entry1 = new TweakCatalogEntry("tweak1", "tweak1", "Explorer", [], null, true, ["developer"], []);
        var entry2 = new TweakCatalogEntry("tweak2", "tweak2", "Explorer", [], null, true, ["gamer"], []);
        var tweaks = ImmutableArray.Create(
            new TweakCardModel(entry1, CardStatus.Detected),
            new TweakCardModel(entry2, CardStatus.Detected));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.Tweaks[0].IsSuggested, Is.True);
            Assert.That(_vm.Tweaks[1].IsSuggested, Is.False);
        });
    }

    [Test]
    public async Task SetProfileFilter_FiltersSubCategories()
    {
        var entry1 = new TweakCatalogEntry("tweak1", "tweak1", "Explorer/Files", [], null, true, ["developer"], []);
        var entry2 = new TweakCatalogEntry("tweak2", "tweak2", "Explorer/Context Menu", [], null, true, ["developer"], []);
        var entry3 = new TweakCatalogEntry("tweak3", "tweak3", "Explorer/Context Menu", [], null, true, ["developer"], []);
        var entry4 = new TweakCatalogEntry("tweak4", "tweak4", "Privacy/Telemetry", [], null, true, ["gamer"], []);
        var entry5 = new TweakCatalogEntry("tweak5", "tweak5", "Privacy/Tracking", [], null, true, ["gamer"], []);
        var entry6 = new TweakCatalogEntry("tweak6", "tweak6", "Privacy/Tracking", [], null, true, ["gamer"], []);
        var tweaks = ImmutableArray.Create(
            new TweakCardModel(entry1, CardStatus.Detected),
            new TweakCardModel(entry2, CardStatus.Detected),
            new TweakCardModel(entry3, CardStatus.Detected),
            new TweakCardModel(entry4, CardStatus.Detected),
            new TweakCardModel(entry5, CardStatus.Detected),
            new TweakCardModel(entry6, CardStatus.Detected));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");

        // Default filter is "Suggested" — only developer tweaks visible
        Assert.That(_vm.SubCategories.Count(c => c.Category == "Explorer"), Is.EqualTo(1));

        _vm.SetProfileFilterCommand.Execute("All");
        Assert.That(_vm.SubCategories, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ProfileFilter_FiltersSubGroupsCorrectly()
    {
        var entry1 = new TweakCatalogEntry("tweak1", "tweak1", "Explorer/Files", [], null, true, ["developer"], []);
        var entry2 = new TweakCatalogEntry("tweak2", "tweak2", "Privacy/Telemetry", [], null, true, ["gamer"], []);
        var tweaks = ImmutableArray.Create(
            new TweakCardModel(entry1, CardStatus.Detected),
            new TweakCardModel(entry2, CardStatus.Detected));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SetProfileFilterCommand.Execute("developer");

        Assert.That(_vm.SubCategories, Has.Count.EqualTo(1));
        Assert.That(_vm.SubCategories[0].Category, Is.EqualTo("Explorer"));

        var subGroups = _vm.SubCategories[0].SubGroups;
        Assert.That(subGroups, Has.Count.EqualTo(1));
        Assert.That(subGroups[0].Tweaks, Has.Length.EqualTo(1));
    }

    [Test]
    public void ApplyTweak_QueuesPendingChange()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Unmanaged);

        _vm.ApplyTweakCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<ApplyTweakChange>(c => c.Tweak == card));
        _pendingChanges.Received(1).Remove("t1", PendingChangeKind.RevertTweak);
    }

    [Test]
    public void RevertTweak_QueuesPendingChange()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord, 1));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        _vm.RevertTweakCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<RevertTweakChange>(c => c.Tweak == card));
        _pendingChanges.Received(1).Remove("t1", PendingChangeKind.ApplyTweak);
    }

    [Test]
    public void TweakCardModel_IsAllApplied_WhenAllEntriesApplied()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Detected) { AppliedCount = 1 };

        Assert.That(card.IsAllApplied, Is.True);
    }

    [Test]
    public void TweakCardModel_IsAllApplied_FalseWhenPartial()
    {
        var reg = ImmutableArray.Create(
            new RegistryEntryDefinition("HKCU\\Test", "Val1", 0, RegistryValueType.DWord),
            new RegistryEntryDefinition("HKCU\\Test", "Val2", 1, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Drifted) { AppliedCount = 1 };

        Assert.That(card.IsAllApplied, Is.False);
    }

    [Test]
    public void TweakCardModel_RestartRequired_TrueWhenTagged()
    {
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", ["restart"], null, true, [], []);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        Assert.That(card.RestartRequired, Is.True);
    }

    [Test]
    public void TweakCardModel_RestartRequired_FalseWhenNoTag()
    {
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", ["privacy"], null, true, [], []);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        Assert.That(card.RestartRequired, Is.False);
    }

    [Test]
    public void TweakCardModel_RegistryKeyCountText_Singular()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        Assert.That(card.RegistryKeyCountText, Is.EqualTo("1 registry key"));
    }

    [Test]
    public void TweakCardModel_RegistryKeyCountText_Plural()
    {
        var reg = ImmutableArray.Create(
            new RegistryEntryDefinition("HKCU\\Test", "Val1", 0, RegistryValueType.DWord),
            new RegistryEntryDefinition("HKCU\\Test", "Val2", 1, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        Assert.That(card.RegistryKeyCountText, Is.EqualTo("2 registry keys"));
    }

    [Test]
    public void OpenCertificateManager_CommandExists()
    {
        Assert.That(_vm.OpenCertificateManagerCommand, Is.Not.Null);
    }

    [Test]
    public async Task RemoveCertificate_CallsScannerRemoveAndRemovesFromCollections()
    {
        var cert = MakeCertificate("AABB", CertificateStoreName.Personal);
        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(cert));

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.CertificateItems, Has.Count.EqualTo(1));

        _vm.SelectCategoryCommand.Execute("Certificates");
        var card = _vm.CertificateItems[0];
        await _vm.RemoveCertificateCommand.ExecuteAsync(card);

        await _certificateScanner.Received(1).RemoveAsync(cert, Arg.Any<CancellationToken>());
        Assert.That(_vm.CertificateItems, Is.Empty);
    }

    [Test]
    public async Task RemoveCertificate_RemovesFromFilteredGroups()
    {
        var cert1 = MakeCertificate("AABB", CertificateStoreName.Personal);
        var cert2 = MakeCertificate("CCDD", CertificateStoreName.Personal);
        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(cert1, cert2));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Certificates");
        Assert.That(_vm.FilteredCertificateGroups, Has.Count.EqualTo(1));
        Assert.That(_vm.FilteredCertificateGroups[0].Certificates, Has.Count.EqualTo(2));

        var card = _vm.CertificateItems[0];
        await _vm.RemoveCertificateCommand.ExecuteAsync(card);

        Assert.That(_vm.FilteredCertificateGroups[0].Certificates, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RemoveCertificate_RemovesGroupWhenLastCertDeleted()
    {
        var cert = MakeCertificate("AABB", CertificateStoreName.Personal);
        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(cert));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Certificates");
        Assert.That(_vm.FilteredCertificateGroups, Has.Count.EqualTo(1));

        await _vm.RemoveCertificateCommand.ExecuteAsync(_vm.CertificateItems[0]);

        Assert.That(_vm.FilteredCertificateGroups, Is.Empty);
    }

    [Test]
    public async Task RefreshAsync_WithDetectionErrors_SetsErrorMessage()
    {
        var errors = ImmutableArray.Create(
            new TweakDetectionError("Bad Tweak", "t1", "HKCU\\Test", null, "Access denied"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(ImmutableArray<TweakCardModel>.Empty, errors));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Does.Contain("Bad Tweak"));
            Assert.That(_vm.ErrorMessage, Does.Contain("Access denied"));
            Assert.That(_vm.ErrorMessage, Does.Contain("HKCU\\Test"));
        });
    }

    [Test]
    public async Task RefreshAsync_WithDetectionErrors_IncludesSourceFile()
    {
        var errors = ImmutableArray.Create(
            new TweakDetectionError("Tweak", "t1", null, @"C:\test.yaml", "parse error"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(ImmutableArray<TweakCardModel>.Empty, errors));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.ErrorMessage, Does.Contain(@"C:\test.yaml"));
    }

    [Test]
    public void ToggleStartupEnabled_AddsPendingChange()
    {
        var entry = new StartupEntry("TestApp", "Test App", "test.exe", null, StartupSource.RegistryCurrentUser, true);
        var card = new StartupCardModel(entry);

        _vm.ToggleStartupEnabledCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<ToggleStartupChange>(c => c.Startup == card && !c.Enable));
    }

    [Test]
    public void RevertTweakToCaptured_QueuesPendingChange()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        _vm.RevertTweakToCapturedCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<RevertTweakToCapturedChange>(c => c.Tweak == card));
        _pendingChanges.Received(1).Remove("t1", PendingChangeKind.ApplyTweak);
        _pendingChanges.Received(1).Remove("t1", PendingChangeKind.RevertTweak);
    }

    [Test]
    public async Task StartupSearchText_FiltersStartupItems()
    {
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new StartupEntry("Chrome", "Chrome", "chrome.exe", null, StartupSource.RegistryCurrentUser, true),
                new StartupEntry("Spotify", "Spotify", "spotify.exe", null, StartupSource.StartupFolderUser, true),
            });

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Startup");
        Assert.That(_vm.FilteredStartupItems, Has.Count.EqualTo(2));

        _vm.StartupSearchText = "Chrome";
        Assert.That(_vm.FilteredStartupItems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CertificateExpiryFilter_FiltersGroups()
    {
        var validCert = new DetectedCertificate("AA", "CN=Valid", "CN=Issuer", null,
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1), false, CertificateStoreName.Personal);
        var expiredCert = new DetectedCertificate("BB", "CN=Expired", "CN=Issuer", null,
            DateTime.Now.AddYears(-2), DateTime.Now.AddDays(-1), false, CertificateStoreName.Personal);

        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(validCert, expiredCert));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Certificates");
        Assert.That(_vm.FilteredCertificateGroups[0].Certificates, Has.Count.EqualTo(2));

        _vm.SetCertificateExpiryFilterCommand.Execute("Valid");
        Assert.That(_vm.FilteredCertificateGroups[0].Certificates, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CertificateSearchText_FiltersGroups()
    {
        var cert1 = MakeCertificate("AABB", CertificateStoreName.Personal);
        var cert2 = new DetectedCertificate("CCDD", "CN=Other", "CN=Issuer", null,
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1), false, CertificateStoreName.Personal);

        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(cert1, cert2));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Certificates");

        _vm.CertificateSearchText = "Other";
        Assert.That(_vm.FilteredCertificateGroups[0].Certificates, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TrackAllInstalledFonts_TogglesSelection()
    {
        var fonts = new FontDetectionResult(
            ImmutableArray.Create(MakeFont("Arial"), MakeFont("Consolas")),
            ImmutableArray<FontCardModel>.Empty);

        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.FilteredInstalledFontGroups.All(g => g.IsSelected), Is.False);

        _vm.TrackAllInstalledFontsCommand.Execute(null);
        Assert.That(_vm.FilteredInstalledFontGroups.All(g => g.IsSelected), Is.True);

        _vm.TrackAllInstalledFontsCommand.Execute(null);
        Assert.That(_vm.FilteredInstalledFontGroups.All(g => g.IsSelected), Is.False);
    }

    [Test]
    public async Task TrackAllNewStartupItems_TracksUntracked()
    {
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new StartupEntry("Chrome", "Chrome", "chrome.exe", null, StartupSource.RegistryCurrentUser, true),
            });

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Startup");
        Assert.That(_vm.FilteredStartupItems[0].IsTracked, Is.False);

        _vm.TrackAllNewStartupItemsCommand.Execute(null);
        Assert.That(_vm.FilteredStartupItems[0].IsTracked, Is.True);
    }

    [Test]
    public async Task OnFontPropertyChanged_IsSelected_AddsPendingChange()
    {
        var fonts = new FontDetectionResult(
            ImmutableArray.Create(MakeFont("Arial")),
            ImmutableArray<FontCardModel>.Empty);

        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.InstalledFonts[0].IsSelected = true;
        _pendingChanges.Received(1).Add(Arg.Is<OnboardFontChange>(c => c.Font.Name == "Arial"));

        _vm.InstalledFonts[0].IsSelected = false;
        _pendingChanges.Received(1).Remove("Arial", PendingChangeKind.OnboardFont);
    }

    [Test]
    public async Task RefreshAsync_BuildsStartupCategory()
    {
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { new StartupEntry("Chrome", "Chrome", "chrome.exe", null, StartupSource.RegistryCurrentUser, true) });

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Categories.Any(c => c.Category == "Startup"), Is.True);
    }

    [Test]
    public async Task RefreshAsync_BuildsCertificatesCategory()
    {
        var cert = MakeCertificate("AABB", CertificateStoreName.Personal);
        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(cert));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Categories.Any(c => c.Category == "Certificates"), Is.True);
    }

    [Test]
    public async Task RefreshAsync_BuildsProfileFilters()
    {
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, ["developer", "gamer"], []);
        var tweaks = ImmutableArray.Create(new TweakCardModel(entry, CardStatus.Detected));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.AvailableProfileFilters, Does.Contain("All"));
        Assert.That(_vm.AvailableProfileFilters, Does.Contain("Suggested"));
        Assert.That(_vm.AvailableProfileFilters, Does.Contain("developer"));
    }

    [Test]
    public async Task Dispose_UnsubscribesFontChanges()
    {
        var fonts = new FontDetectionResult(
            ImmutableArray.Create(MakeFont("Arial")),
            ImmutableArray<FontCardModel>.Empty);

        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(fonts);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.Dispose();

        _vm.InstalledFonts[0].IsSelected = true;
        _pendingChanges.DidNotReceive().Add(Arg.Any<OnboardFontChange>());
    }

    [Test]
    public async Task RemoveStartupItemAsync_RemovesFromCollections()
    {
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { new StartupEntry("Chrome", "Chrome", "chrome.exe", null, StartupSource.RegistryCurrentUser, true) });

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Startup");
        var card = _vm.StartupItems[0];

        await _vm.RemoveStartupItemCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.StartupItems, Is.Empty);
            Assert.That(_vm.FilteredStartupItems, Is.Empty);
        });
        await _startupService.Received(1).RemoveAsync(card.Entry);
    }

    [Test]
    public async Task SelectCategory_Certificates_SetsExpiryFilter()
    {
        var cert = MakeCertificate("AABB", CertificateStoreName.Personal);
        _certificateScanner.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(cert));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Certificates");

        Assert.That(_vm.ActiveCertificateExpiryFilter, Is.EqualTo("All"));
    }

    private static TweakCardModel MakeTweak(string name, string category)
    {
        var entry = new TweakCatalogEntry(name, name, category, [], null, true, [], []);
        return new TweakCardModel(entry, CardStatus.Detected);
    }

    private static FontCardModel MakeFont(string name)
    {
        return new FontCardModel(name, name, name, null, null, null, FontCardSource.Detected, [], CardStatus.Detected);
    }

    private static DetectedCertificate MakeCertificate(string thumbprint, CertificateStoreName store)
    {
        return new DetectedCertificate(
            thumbprint,
            $"CN=Test-{thumbprint}",
            "CN=TestIssuer",
            null,
            DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(1),
            false,
            store);
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class GalleryViewModelBaseTests
{
    [Test]
    public async Task LoadProfilesAsync_ParsesStoredProfiles()
    {
        var settings = Substitute.For<ISettingsProvider>();
        settings.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Gamer", "Casual"] });

        var profiles = await TestableGalleryViewModel.TestLoadProfilesAsync(settings);

        Assert.Multiple(() =>
        {
            Assert.That(profiles, Has.Count.EqualTo(2));
            Assert.That(profiles, Does.Contain(UserProfile.Gamer));
            Assert.That(profiles, Does.Contain(UserProfile.Casual));
        });
    }

    [Test]
    public async Task LoadProfilesAsync_FallsBackToDefaults_WhenNoProfiles()
    {
        var settings = Substitute.For<ISettingsProvider>();
        settings.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings());

        var profiles = await TestableGalleryViewModel.TestLoadProfilesAsync(settings);

        Assert.Multiple(() =>
        {
            Assert.That(profiles, Has.Count.EqualTo(2));
            Assert.That(profiles, Does.Contain(UserProfile.Developer));
            Assert.That(profiles, Does.Contain(UserProfile.PowerUser));
        });
    }

    [Test]
    public async Task LoadProfilesAsync_IgnoresInvalidProfileNames()
    {
        var settings = Substitute.For<ISettingsProvider>();
        settings.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Developer", "InvalidProfile"] });

        var profiles = await TestableGalleryViewModel.TestLoadProfilesAsync(settings);

        Assert.Multiple(() =>
        {
            Assert.That(profiles, Has.Count.EqualTo(1));
            Assert.That(profiles, Does.Contain(UserProfile.Developer));
        });
    }

    private sealed class TestableGalleryViewModel : GalleryViewModelBase
    {
        public override bool ShowGrid => true;
        public override bool ShowDetail => false;

        public static Task<HashSet<UserProfile>> TestLoadProfilesAsync(
            ISettingsProvider settingsProvider, CancellationToken cancellationToken = default)
            => LoadProfilesAsync(settingsProvider, cancellationToken);
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class BulkObservableCollectionTests
{
    [Test]
    public void ReplaceAll_ReplacesExistingItems()
    {
        var collection = new BulkObservableCollection<string> { "a", "b", "c" };

        collection.ReplaceAll(["x", "y"]);

        Assert.That(collection, Is.EqualTo(new[] { "x", "y" }));
    }

    [Test]
    public void ReplaceAll_WithEmpty_ClearsCollection()
    {
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };

        collection.ReplaceAll([]);

        Assert.That(collection, Is.Empty);
    }

    [Test]
    public void ReplaceAll_FiresSingleResetNotification()
    {
        var collection = new BulkObservableCollection<string> { "a", "b" };
        var notifications = new List<System.Collections.Specialized.NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, e) => notifications.Add(e);

        collection.ReplaceAll(["x", "y", "z"]);

        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications[0].Action,
            Is.EqualTo(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
    }

    [Test]
    public void ReplaceAll_UpdatesCountProperty()
    {
        var collection = new BulkObservableCollection<int> { 1, 2 };
        var propertyNames = new List<string>();
        ((System.ComponentModel.INotifyPropertyChanged)collection).PropertyChanged +=
            (_, e) => propertyNames.Add(e.PropertyName!);

        collection.ReplaceAll([10, 20, 30]);

        Assert.Multiple(() =>
        {
            Assert.That(collection.Count, Is.EqualTo(3));
            Assert.That(propertyNames, Does.Contain("Count"));
            Assert.That(propertyNames, Does.Contain("Item[]"));
        });
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class CountToVisibilityConverterTests
{
    private readonly Perch.Desktop.Converters.CountToVisibilityConverter _converter = new();

    [Test]
    public void Convert_PositiveInt_ReturnsVisible() =>
        Assert.That(_converter.Convert(5, typeof(object), null!, null!), Is.EqualTo(Visibility.Visible));

    [Test]
    public void Convert_Zero_ReturnsCollapsed() =>
        Assert.That(_converter.Convert(0, typeof(object), null!, null!), Is.EqualTo(Visibility.Collapsed));

    [Test]
    public void Convert_NegativeInt_ReturnsCollapsed() =>
        Assert.That(_converter.Convert(-1, typeof(object), null!, null!), Is.EqualTo(Visibility.Collapsed));

    [Test]
    public void Convert_NonInt_ReturnsCollapsed() =>
        Assert.That(_converter.Convert("hello", typeof(object), null!, null!), Is.EqualTo(Visibility.Collapsed));
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class NullToVisibilityConverterTests
{
    private readonly Perch.Desktop.Converters.NullToVisibilityConverter _converter = new();

    [Test]
    public void Convert_NonNullObject_ReturnsVisible() =>
        Assert.That(_converter.Convert(new object(), typeof(object), null!, null!), Is.EqualTo(Visibility.Visible));

    [Test]
    public void Convert_Null_ReturnsCollapsed() =>
        Assert.That(_converter.Convert(null, typeof(object), null!, null!), Is.EqualTo(Visibility.Collapsed));

    [Test]
    public void Convert_NonEmptyString_ReturnsVisible() =>
        Assert.That(_converter.Convert("hello", typeof(object), null!, null!), Is.EqualTo(Visibility.Visible));

    [Test]
    public void Convert_EmptyString_ReturnsCollapsed() =>
        Assert.That(_converter.Convert("", typeof(object), null!, null!), Is.EqualTo(Visibility.Collapsed));
}

[TestFixture]
public sealed class AppCardModelTests
{
    private static AppCardModel MakeCard(CardStatus status)
    {
        var entry = new CatalogEntry("test", "Test", "Test", "Dev", [], null, null, null, null, null, null);
        return new AppCardModel(entry, CardTier.YourApps, status);
    }

    [TestCase(CardStatus.Unmanaged, "Add to Perch")]
    [TestCase(CardStatus.Detected, "Add to Perch")]
    [TestCase(CardStatus.PendingAdd, "Remove from Perch")]
    [TestCase(CardStatus.PendingRemove, "Add to Perch")]
    [TestCase(CardStatus.Synced, "Remove from Perch")]
    [TestCase(CardStatus.Drifted, "Remove from Perch")]
    public void ActionButtonText_ReturnsCorrectText(CardStatus status, string expected)
    {
        var card = MakeCard(status);
        Assert.That(card.ActionButtonText, Is.EqualTo(expected));
    }

    [TestCase(CardStatus.Unmanaged, false)]
    [TestCase(CardStatus.Detected, false)]
    [TestCase(CardStatus.PendingAdd, true)]
    [TestCase(CardStatus.PendingRemove, false)]
    [TestCase(CardStatus.Synced, true)]
    [TestCase(CardStatus.Drifted, true)]
    public void IsManaged_ReturnsCorrectValue(CardStatus status, bool expected)
    {
        var card = MakeCard(status);
        Assert.That(card.IsManaged, Is.EqualTo(expected));
    }

    [TestCase(CardStatus.Unmanaged, true)]
    [TestCase(CardStatus.Detected, true)]
    [TestCase(CardStatus.PendingAdd, false)]
    [TestCase(CardStatus.PendingRemove, true)]
    [TestCase(CardStatus.Synced, false)]
    [TestCase(CardStatus.Drifted, false)]
    public void IsActionAdd_ReturnsCorrectValue(CardStatus status, bool expected)
    {
        var card = MakeCard(status);
        Assert.That(card.IsActionAdd, Is.EqualTo(expected));
    }
}
