#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Startup;
using Perch.Core.Symlinks;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;
using Perch.Desktop.ViewModels;

using Wpf.Ui;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class AppsViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private IAppLinkService _appLinkService = null!;
    private IAppDetailService _detailService = null!;
    private ISettingsProvider _settingsProvider = null!;
    private ISnackbarService _snackbarService = null!;
    private AppsViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _appLinkService = Substitute.For<IAppLinkService>();
        _detailService = Substitute.For<IAppDetailService>();
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _snackbarService = Substitute.For<ISnackbarService>();

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Developer"] });

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(
                ImmutableArray<AppCardModel>.Empty,
                ImmutableArray<AppCardModel>.Empty,
                ImmutableArray<AppCardModel>.Empty));

        _vm = new AppsViewModel(_detectionService, _appLinkService, _detailService, _settingsProvider, _snackbarService);
    }

    [Test]
    public void InitialState_IsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.IsLoading, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
            Assert.That(_vm.YourApps, Is.Empty);
            Assert.That(_vm.SuggestedApps, Is.Empty);
            Assert.That(_vm.BrowseCategories, Is.Empty);
        });
    }

    [Test]
    public async Task RefreshAsync_PopulatesTiers()
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
            Assert.That(_vm.YourApps, Has.Count.EqualTo(1));
            Assert.That(_vm.SuggestedApps, Has.Count.EqualTo(1));
            Assert.That(_vm.BrowseCategories, Has.Count.EqualTo(1));
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
    public async Task ToggleApp_Detected_Links()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Detected);
        _appLinkService.LinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Ok, "ok") });

        await _vm.ToggleAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task ToggleApp_Linked_Unlinks()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);
        _appLinkService.UnlinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Ok, "ok") });

        await _vm.ToggleAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task ToggleApp_Broken_Fixes()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Broken);
        _appLinkService.FixAppLinksAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Ok, "ok") });

        await _vm.ToggleAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task ToggleApp_NotInstalled_NoOp()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.NotInstalled);

        await _vm.ToggleAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.NotInstalled));
        await _appLinkService.DidNotReceive().LinkAppAsync(Arg.Any<CatalogEntry>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ToggleApp_OnError_DoesNotChangeStatus()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Detected);
        _appLinkService.LinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Error, "fail") });

        await _vm.ToggleAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task SearchText_FiltersAllTiers()
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
        Assert.That(_vm.YourApps, Has.Count.EqualTo(1));
        Assert.That(_vm.SuggestedApps, Has.Count.EqualTo(1));
        Assert.That(_vm.BrowseCategories, Has.Count.EqualTo(1));

        _vm.SearchText = "vscode";
        Assert.That(_vm.YourApps, Has.Count.EqualTo(1));
        Assert.That(_vm.SuggestedApps, Is.Empty);
        Assert.That(_vm.BrowseCategories, Is.Empty);
    }

    [Test]
    public async Task YourApps_SortedByStatusThenName()
    {
        var apps = ImmutableArray.Create(
            MakeCard("zapp", "Dev/IDEs", CardStatus.Detected),
            MakeCard("aapp", "Dev/IDEs", CardStatus.Linked),
            MakeCard("mapp", "Dev/IDEs", CardStatus.Broken));

        _detectionService.DetectAppsAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new GalleryDetectionResult(apps, [], []));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.YourApps[0].Name, Is.EqualTo("mapp"));
            Assert.That(_vm.YourApps[1].Name, Is.EqualTo("zapp"));
            Assert.That(_vm.YourApps[2].Name, Is.EqualTo("aapp"));
        });
    }

    private static AppCardModel MakeCard(string name, string category, CardStatus status = CardStatus.Detected, CardTier tier = CardTier.YourApps)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null);
        return new AppCardModel(entry, tier, status);
    }
}

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class DotfilesViewModelTests
{
    private IGalleryDetectionService _detectionService = null!;
    private IDotfileDetailService _detailService = null!;
    private DotfilesViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _detailService = Substitute.For<IDotfileDetailService>();

        _detectionService.DetectDotfilesAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<DotfileGroupCardModel>.Empty);

        _vm = new DotfilesViewModel(_detectionService, _detailService);
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
    private SystemTweaksViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _settingsProvider = Substitute.For<ISettingsProvider>();
        _startupService = Substitute.For<IStartupService>();
        _tweakService = Substitute.For<ITweakService>();

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

        _vm = new SystemTweaksViewModel(_detectionService, _settingsProvider, _startupService, _tweakService);
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
    public void BackToSubCategories_ResetsSubCategory()
    {
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SelectSubCategoryCommand.Execute("Registry");
        _vm.BackToSubCategoriesCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowSubCategories, Is.True);
            Assert.That(_vm.ShowTweakCards, Is.False);
            Assert.That(_vm.SelectedSubCategory, Is.Null);
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
    public async Task SelectSubCategory_FiltersTweaksForSubCategory()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Registry"),
            MakeTweak("tweak2", "Registry"),
            MakeTweak("tweak3", "Registry"),
            MakeTweak("tweak4", "Explorer"),
            MakeTweak("tweak5", "Explorer"),
            MakeTweak("tweak6", "Explorer"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SelectSubCategoryCommand.Execute("Registry");

        Assert.That(_vm.FilteredTweaks, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task SelectCategory_SystemTweaks_BuildsSubCategories()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Registry"),
            MakeTweak("tweak2", "Registry"),
            MakeTweak("tweak3", "Registry"),
            MakeTweak("tweak4", "Explorer"),
            MakeTweak("tweak5", "Explorer"),
            MakeTweak("tweak6", "Explorer"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowSubCategories, Is.True);
            Assert.That(_vm.SubCategories, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task SelectCategory_Fonts_LeavesFilteredTweaksEmpty()
    {
        var tweaks = ImmutableArray.Create(MakeTweak("tweak1", "Registry"));
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Fonts");

        Assert.That(_vm.FilteredTweaks, Is.Empty);
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
    public async Task SearchText_DoesNotFilterTweaks()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Registry"),
            MakeTweak("tweak2", "Registry"),
            MakeTweak("tweak3", "Registry"),
            MakeTweak("tweak4", "Explorer"),
            MakeTweak("tweak5", "Explorer"),
            MakeTweak("tweak6", "Explorer"));
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SelectSubCategoryCommand.Execute("Registry");
        Assert.That(_vm.FilteredTweaks, Has.Count.EqualTo(3));

        _vm.SearchText = "nonexistent";
        Assert.That(_vm.FilteredTweaks, Has.Count.EqualTo(3));
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
        var entry1 = new TweakCatalogEntry("tweak1", "tweak1", "Explorer", [], null, true, ["developer"], []);
        var entry2 = new TweakCatalogEntry("tweak2", "tweak2", "Explorer", [], null, true, ["developer"], []);
        var entry3 = new TweakCatalogEntry("tweak3", "tweak3", "Explorer", [], null, true, ["developer"], []);
        var entry4 = new TweakCatalogEntry("tweak4", "tweak4", "Privacy", [], null, true, ["gamer"], []);
        var entry5 = new TweakCatalogEntry("tweak5", "tweak5", "Privacy", [], null, true, ["gamer"], []);
        var entry6 = new TweakCatalogEntry("tweak6", "tweak6", "Privacy", [], null, true, ["gamer"], []);
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
    public async Task SmallSubCategories_MergedIntoOther()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Explorer"),
            MakeTweak("tweak2", "Explorer"),
            MakeTweak("tweak3", "Explorer"),
            MakeTweak("tweak4", "Privacy"),
            MakeTweak("tweak5", "Tiny"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new TweakDetectionPageResult(tweaks, ImmutableArray<TweakDetectionError>.Empty));

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("System Tweaks");
        _vm.SetProfileFilterCommand.Execute("All");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SubCategories.Any(c => c.Category == "Explorer"), Is.True);
            Assert.That(_vm.SubCategories.Any(c => c.Category == "Other"), Is.True);
            Assert.That(_vm.SubCategories.Any(c => c.Category == "Tiny"), Is.False);
            Assert.That(_vm.SubCategories.Any(c => c.Category == "Privacy"), Is.False);
        });
    }

    [Test]
    public void ApplyTweak_CallsServiceAndRefreshesCard()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.NotInstalled);

        var appliedDetection = new TweakDetectionResult(
            TweakStatus.Applied,
            ImmutableArray.Create(new RegistryEntryStatus(reg[0], 0, true)));
        _tweakService.Apply(entry).Returns(new TweakOperationResult(ResultLevel.Ok, []));
        _tweakService.Detect(entry).Returns(appliedDetection);

        _vm.ApplyTweakCommand.Execute(card);

        _tweakService.Received(1).Apply(entry);
        Assert.That(card.Status, Is.EqualTo(CardStatus.Detected));
        Assert.That(card.AppliedCount, Is.EqualTo(1));
    }

    [Test]
    public void RevertTweak_CallsServiceAndRefreshesCard()
    {
        var reg = ImmutableArray.Create(new RegistryEntryDefinition("HKCU\\Test", "Val", 0, RegistryValueType.DWord, 1));
        var entry = new TweakCatalogEntry("t1", "Tweak", "Explorer", [], null, true, [], reg);
        var card = new TweakCardModel(entry, CardStatus.Detected);

        var revertedDetection = new TweakDetectionResult(
            TweakStatus.NotApplied,
            ImmutableArray.Create(new RegistryEntryStatus(reg[0], 1, false)));
        _tweakService.Revert(entry).Returns(new TweakOperationResult(ResultLevel.Ok, []));
        _tweakService.Detect(entry).Returns(revertedDetection);

        _vm.RevertTweakCommand.Execute(card);

        _tweakService.Received(1).Revert(entry);
        Assert.That(card.Status, Is.EqualTo(CardStatus.NotInstalled));
        Assert.That(card.AppliedCount, Is.EqualTo(0));
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
