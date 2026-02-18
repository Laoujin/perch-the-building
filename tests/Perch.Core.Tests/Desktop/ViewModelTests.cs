#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Scanner;
using Perch.Core.Startup;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

namespace Perch.Core.Tests.Desktop;

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
        var yourApp = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);
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
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<UnlinkAppChange>(c => c.App == card));
    }

    [Test]
    public void ToggleApp_Broken_AddsUnlinkChange()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Broken);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Add(Arg.Is<UnlinkAppChange>(c => c.App == card));
    }

    [Test]
    public void ToggleApp_NotInstalled_NoOp()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.NotInstalled);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.DidNotReceive().Add(Arg.Any<PendingChange>());
    }

    [Test]
    public void ToggleApp_Detected_RemovesPreviousUnlink()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Detected);

        _vm.ToggleAppCommand.Execute(card);

        _pendingChanges.Received(1).Remove(card.Id, PendingChangeKind.UnlinkApp);
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
    public async Task GetCategorySubGroups_SortsByTierThenStatusThenName()
    {
        var apps = ImmutableArray.Create(
            MakeCard("zapp", "Dev/IDEs", CardStatus.Detected, CardTier.Other),
            MakeCard("aapp", "Dev/IDEs", CardStatus.Linked, CardTier.YourApps),
            MakeCard("mapp", "Dev/IDEs", CardStatus.Broken, CardTier.YourApps),
            MakeCard("sapp", "Dev/IDEs", CardStatus.Detected, CardTier.Suggested));

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(
                apps.Where(a => a.Tier == CardTier.YourApps).ToImmutableArray(),
                apps.Where(a => a.Tier == CardTier.Suggested).ToImmutableArray(),
                apps.Where(a => a.Tier == CardTier.Other).ToImmutableArray()));

        await _vm.RefreshCommand.ExecuteAsync(null);

        var subGroups = _vm.GetCategorySubGroups("Dev").ToList();
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
        var parent = MakeCard("dotnet-sdk", "Development/Runtimes", CardStatus.Linked);
        var child = MakeCardWithRequires("vscode", "Development/IDEs", ["dotnet-sdk"], CardStatus.Detected);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(parent, child), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        var subGroups = _vm.GetCategorySubGroups("Development").ToList();
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

        var subGroups = _vm.GetCategorySubGroups("Dev").ToList();
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
        var parent = MakeCard("dotnet-sdk", "Development/Runtimes", CardStatus.Linked);
        var child = MakeCardWithRequires("my-child-tool", "Development/Tools", ["dotnet-sdk"]);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(parent, child), [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.SearchText = "my-child";
        Assert.That(_vm.Categories, Has.Count.EqualTo(1));
        var subGroups = _vm.GetCategorySubGroups("Development").ToList();
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
    public void InitialState_ShowsCardGrid_HidesDetailView()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowCardGrid, Is.True);
            Assert.That(_vm.ShowDetailView, Is.False);
            Assert.That(_vm.SelectedApp, Is.Null);
            Assert.That(_vm.Detail, Is.Null);
            Assert.That(_vm.HasEcosystem, Is.False);
        });
    }

    [Test]
    public async Task ConfigureAppAsync_SetsSelectedAppAndLoadsDetail()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(card), [], []));
        await _vm.RefreshCommand.ExecuteAsync(null);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedApp, Is.EqualTo(card));
            Assert.That(_vm.Detail, Is.EqualTo(detail));
            Assert.That(_vm.ShowDetailView, Is.True);
            Assert.That(_vm.ShowCardGrid, Is.False);
            Assert.That(_vm.IsLoadingDetail, Is.False);
        });
    }

    [Test]
    public async Task BackToGrid_ResetsSelection()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(card), [], []));
        await _vm.RefreshCommand.ExecuteAsync(null);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);
        Assert.That(_vm.ShowDetailView, Is.True);

        _vm.BackToGridCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowCardGrid, Is.True);
            Assert.That(_vm.SelectedApp, Is.Null);
            Assert.That(_vm.Detail, Is.Null);
            Assert.That(_vm.HasEcosystem, Is.False);
        });
    }

    [Test]
    public async Task ConfigureAppAsync_BuildsEcosystemFromDependentsAndSuggests()
    {
        var child = MakeCardWithRequires("eslint", "Development/CLI Tools", ["nodejs"]);
        var suggestedTool = MakeCard("yarn", "Development/CLI Tools");
        var parent = MakeCardWithSuggests("nodejs", "Development/Languages", ["yarn"]);

        var detail = new AppDetail(parent, null, null, null, null, []);
        _detailService.LoadDetailAsync(parent, Arg.Any<CancellationToken>())
            .Returns(detail);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(parent, child, suggestedTool), [], []));
        await _vm.RefreshCommand.ExecuteAsync(null);

        await _vm.ConfigureAppCommand.ExecuteAsync(parent);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.HasEcosystem, Is.True);
            var allEcoApps = _vm.EcosystemGroups.SelectMany(g => g.Apps).ToList();
            Assert.That(allEcoApps, Has.Count.EqualTo(2));
            Assert.That(allEcoApps.Any(a => a.Id == "eslint"), Is.True);
            Assert.That(allEcoApps.Any(a => a.Id == "yarn"), Is.True);
        });
    }

    [Test]
    public async Task ConfigureAppAsync_EcosystemExcludesSelfAndDeduplicates()
    {
        var child = MakeCardWithRequires("eslint", "Development/CLI Tools", ["nodejs"]);
        var parent = MakeCardWithSuggests("nodejs", "Development/Languages", ["eslint", "nodejs"]);

        var detail = new AppDetail(parent, null, null, null, null, []);
        _detailService.LoadDetailAsync(parent, Arg.Any<CancellationToken>())
            .Returns(detail);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(parent, child), [], []));
        await _vm.RefreshCommand.ExecuteAsync(null);

        await _vm.ConfigureAppCommand.ExecuteAsync(parent);

        var allEcoApps = _vm.EcosystemGroups.SelectMany(g => g.Apps).ToList();
        Assert.That(allEcoApps, Has.Count.EqualTo(1));
        Assert.That(allEcoApps[0].Id, Is.EqualTo("eslint"));
    }

    [Test]
    public async Task ConfigureAppAsync_NoSuggests_NoDependents_NoEcosystem()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(ImmutableArray.Create(card), [], []));
        await _vm.RefreshCommand.ExecuteAsync(null);

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.That(_vm.HasEcosystem, Is.False);
        Assert.That(_vm.EcosystemGroups, Is.Empty);
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class DotfilesViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private IDotfileDetailService _detailService = null!;
    private IPendingChangesService _pendingChanges = null!;
    private DotfilesViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _detailService = Substitute.For<IDotfileDetailService>();
        _pendingChanges = Substitute.For<IPendingChangesService>();

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<DotfileGroupCardModel>.Empty);

        _vm = new DotfilesViewModel(_detectionService, _detailService, _pendingChanges);
    }

    [Test]
    public void InitialState_ShowsCardGrid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowCardGrid, Is.True);
            Assert.That(_vm.ShowDetailView, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task RefreshAsync_PopulatesDotfiles()
    {
        var dotfiles = ImmutableArray.Create(
            MakeDotfileGroup("bashrc", CardStatus.Linked),
            MakeDotfileGroup("gitconfig", CardStatus.Detected));

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
        var group = MakeDotfileGroup("bashrc", CardStatus.Linked);
        var detail = new DotfileDetail(group, null, null, null, null, []);
        _detailService.LoadDetailAsync(group, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureCommand.ExecuteAsync(group);
        Assert.That(_vm.ShowDetailView, Is.True);

        _vm.BackToGridCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowCardGrid, Is.True);
            Assert.That(_vm.SelectedDotfile, Is.Null);
            Assert.That(_vm.Detail, Is.Null);
        });
    }

    [Test]
    public async Task SearchText_FiltersDotfiles()
    {
        var dotfiles = ImmutableArray.Create(
            MakeDotfileGroup("bashrc", CardStatus.Linked),
            MakeDotfileGroup("gitconfig", CardStatus.Detected));

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(dotfiles);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.Dotfiles, Has.Count.EqualTo(2));

        _vm.SearchText = "bash";
        Assert.That(_vm.Dotfiles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ConfigureAsync_SetsSelectedDotfileAndLoadsDetail()
    {
        var group = MakeDotfileGroup("bashrc", CardStatus.Linked);
        var detail = new DotfileDetail(group, null, null, null, null, []);
        _detailService.LoadDetailAsync(group, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureCommand.ExecuteAsync(group);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedDotfile, Is.EqualTo(group));
            Assert.That(_vm.Detail, Is.EqualTo(detail));
            Assert.That(_vm.ShowDetailView, Is.True);
        });
    }

    [Test]
    public async Task ConfigureAsync_SetsIsLoadingDetail()
    {
        var group = MakeDotfileGroup("bashrc", CardStatus.Linked);
        var isLoadingDuringLoad = false;
        _detailService.LoadDetailAsync(group, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isLoadingDuringLoad = _vm.IsLoadingDetail;
                return new DotfileDetail(group, null, null, null, null, []);
            });

        await _vm.ConfigureCommand.ExecuteAsync(group);

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
            MakeDotfileGroup("bashrc", CardStatus.Linked),
            MakeDotfileGroup("gitconfig", CardStatus.Detected),
            MakeDotfileGroup("vimrc", CardStatus.Linked));

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
        var group = MakeDotfileGroup("bashrc", CardStatus.Linked);
        var module = new AppModule("bash", "Bash", true, "/modules/bash", [], []);
        var detail = new DotfileDetail(group, module, null, null, null, []);
        _detailService.LoadDetailAsync(group, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureCommand.ExecuteAsync(group);

        Assert.That(_vm.HasModule, Is.True);
    }

    [Test]
    public async Task HasNoModule_WhenDetailHasNullModule_ReturnsTrue()
    {
        var group = MakeDotfileGroup("bashrc", CardStatus.Linked);
        var detail = new DotfileDetail(group, null, null, null, null, []);
        _detailService.LoadDetailAsync(group, Arg.Any<CancellationToken>())
            .Returns(detail);

        await _vm.ConfigureCommand.ExecuteAsync(group);

        Assert.That(_vm.HasNoModule, Is.True);
    }

    private static DotfileGroupCardModel MakeDotfileGroup(string name, CardStatus status)
    {
        var entry = new CatalogEntry(name, name, name, "Shell", [], null, null, null, null, null, null, CatalogKind.Dotfile);
        return new DotfileGroupCardModel(entry, [], status);
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
            Assert.That(_vm.ShowCategories, Is.True);
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
            Assert.That(_vm.ShowCategories, Is.False);
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
            Assert.That(_vm.ShowCategories, Is.True);
            Assert.That(_vm.SelectedCategory, Is.Null);
        });
    }

    [Test]
    public async Task GetCategorySubGroups_ReturnsTweaksGroupedBySubCategory()
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

        var subGroups = _vm.GetCategorySubGroups("Explorer").ToList();
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

        var subGroups = _vm.GetCategorySubGroups("Explorer").ToList();
        Assert.That(subGroups, Has.Count.EqualTo(1));
        Assert.That(subGroups[0].Tweaks, Has.Length.EqualTo(1));
    }

    [Test]
    public void ApplyTweak_QueuesPendingChange()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.NotInstalled);

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
        var card = new TweakCardModel(entry, CardStatus.Drift) { AppliedCount = 1 };

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

    private static TweakCardModel MakeTweak(string name, string category)
    {
        var entry = new TweakCatalogEntry(name, name, category, [], null, true, [], []);
        return new TweakCardModel(entry, CardStatus.Detected);
    }

    private static FontCardModel MakeFont(string name)
    {
        return new FontCardModel(name, name, name, null, null, null, FontCardSource.Detected, [], CardStatus.Detected);
    }
}
#endif
