#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Deploy;
using Perch.Core.Modules;
using Perch.Core.Startup;
using Perch.Core.Symlinks;
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
    private IAppLinkService _appLinkService = null!;
    private IAppDetailService _detailService = null!;
    private IStartupService _startupService = null!;
    private AppsViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _appLinkService = Substitute.For<IAppLinkService>();
        _detailService = Substitute.For<IAppDetailService>();
        _startupService = Substitute.For<IStartupService>();

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<AppCardModel>.Empty);

        _vm = new AppsViewModel(_detectionService, _appLinkService, _detailService, _startupService);
    }

    [Test]
    public void InitialState_ShowsCategories()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowAppCategories, Is.True);
            Assert.That(_vm.ShowAppDetail, Is.False);
            Assert.That(_vm.ShowAppConfigDetail, Is.False);
            Assert.That(_vm.IsLoading, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task RefreshAsync_PopulatesCategories()
    {
        var apps = ImmutableArray.Create(
            MakeCard("vscode", "Development/IDEs"),
            MakeCard("rider", "Development/IDEs"),
            MakeCard("vlc", "Media/Players"));

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(apps);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.AppCategories, Has.Count.EqualTo(2));
            Assert.That(_vm.IsLoading, Is.False);
            Assert.That(_vm.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task RefreshAsync_OnFailure_SetsErrorMessage()
    {
        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("catalog unavailable"));

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ErrorMessage, Does.Contain("catalog unavailable"));
            Assert.That(_vm.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task RefreshAsync_ClearsErrorOnRetry()
    {
        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("fail"));

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.ErrorMessage, Is.Not.Null);

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<AppCardModel>.Empty);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task SelectCategory_NavigatesToDetail()
    {
        var apps = ImmutableArray.Create(MakeCard("vscode", "Development/IDEs"));
        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(apps);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Development");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowAppCategories, Is.False);
            Assert.That(_vm.ShowAppDetail, Is.True);
            Assert.That(_vm.SelectedAppCategory, Is.EqualTo("Development"));
        });
    }

    [Test]
    public async Task BackToCategories_ResetsNavigation()
    {
        var apps = ImmutableArray.Create(MakeCard("vscode", "Development/IDEs"));
        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(apps);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Development");
        _vm.BackToCategoriesCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowAppCategories, Is.True);
            Assert.That(_vm.SelectedAppCategory, Is.Null);
        });
    }

    [Test]
    public async Task ConfigureApp_LoadsDetail()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry>());

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowAppConfigDetail, Is.True);
            Assert.That(_vm.AppDetail, Is.EqualTo(detail));
            Assert.That(_vm.IsLoadingAppDetail, Is.False);
        });
    }

    [Test]
    public void BackToCategoryDetail_ResetsAppSelection()
    {
        _vm.BackToCategoryDetailCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.SelectedApp, Is.Null);
            Assert.That(_vm.AppDetail, Is.Null);
        });
    }

    [Test]
    public async Task SearchText_FiltersCategories()
    {
        var apps = ImmutableArray.Create(
            MakeCard("vscode", "Development/IDEs"),
            MakeCard("vlc", "Media/Players"));

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(apps);

        await _vm.RefreshCommand.ExecuteAsync(null);
        Assert.That(_vm.AppCategories, Has.Count.EqualTo(2));

        _vm.SearchText = "vscode";
        Assert.That(_vm.AppCategories, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LinkAppAsync_WithWarnings_StillSetsLinked()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        _appLinkService.LinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult>
            {
                new("vscode", "s", "t", ResultLevel.Ok, "ok"),
                new("vscode", "s2", "t2", ResultLevel.Warning, "warning"),
            });

        await _vm.LinkAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task SelectCategory_SortsAppsByStatusThenName()
    {
        var apps = ImmutableArray.Create(
            MakeCard("zapp", "Dev/IDEs", CardStatus.Detected),
            MakeCard("aapp", "Dev/IDEs", CardStatus.Linked),
            MakeCard("mapp", "Dev/IDEs", CardStatus.Broken));

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(apps);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Dev");

        var orderedApps = _vm.FilteredCategoryApps[0].Apps;
        Assert.Multiple(() =>
        {
            Assert.That(orderedApps[0].Name, Is.EqualTo("aapp"));
            Assert.That(orderedApps[1].Name, Is.EqualTo("mapp"));
            Assert.That(orderedApps[2].Name, Is.EqualTo("zapp"));
        });
    }

    [Test]
    public async Task LinkAppAsync_OnSuccess_SetsStatusToLinked()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        _appLinkService.LinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Ok, "ok") });

        await _vm.LinkAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task LinkAppAsync_OnError_DoesNotChangeStatus()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        _appLinkService.LinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Error, "fail") });

        await _vm.LinkAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task UnlinkAppAsync_OnSuccess_SetsStatusToDetected()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Linked);
        _appLinkService.UnlinkAppAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Ok, "ok") });

        await _vm.UnlinkAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Detected));
    }

    [Test]
    public async Task FixAppAsync_OnSuccess_SetsStatusToLinked()
    {
        var card = MakeCard("vscode", "Development/IDEs", CardStatus.Broken);
        _appLinkService.FixAppLinksAsync(card.CatalogEntry, Arg.Any<CancellationToken>())
            .Returns(new List<DeployResult> { new("vscode", "s", "t", ResultLevel.Ok, "ok") });

        await _vm.FixAppCommand.ExecuteAsync(card);

        Assert.That(card.Status, Is.EqualTo(CardStatus.Linked));
    }

    [Test]
    public async Task ToggleAppStartupAsync_ExistingEntry_RemovesIt()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        var startupEntry = new StartupEntry("vscode", "vscode", "vscode.exe", null, StartupSource.RegistryCurrentUser, true);
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry> { startupEntry });

        await _vm.ConfigureAppCommand.ExecuteAsync(card);
        Assert.That(_vm.IsInStartup, Is.True);

        await _vm.ToggleAppStartupCommand.ExecuteAsync(null);

        Assert.That(_vm.IsInStartup, Is.False);
        await _startupService.Received(1).RemoveAsync(startupEntry, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ToggleAppStartupAsync_NoEntry_AddsIt()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry>());

        await _vm.ConfigureAppCommand.ExecuteAsync(card);
        Assert.That(_vm.IsInStartup, Is.False);

        await _vm.ToggleAppStartupCommand.ExecuteAsync(null);

        Assert.That(_vm.IsInStartup, Is.True);
        await _startupService.Received(1).AddAsync(
            "vscode", "vscode", StartupSource.RegistryCurrentUser, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConfigureApp_ChecksStartupStatus()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);

        var startupEntry = new StartupEntry("vscode", "vscode", "vscode.exe", null, StartupSource.RegistryCurrentUser, true);
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry> { startupEntry });

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.That(_vm.IsInStartup, Is.True);
    }

    [Test]
    public async Task SelectCategory_PopulatesFilteredCategoryApps()
    {
        var apps = ImmutableArray.Create(
            MakeCard("vscode", "Development/IDEs"),
            MakeCard("rider", "Development/IDEs"),
            MakeCard("vlc", "Media/Players"));

        _detectionService.DetectAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(apps);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Development");

        Assert.That(_vm.FilteredCategoryApps, Has.Count.EqualTo(1));
        Assert.That(_vm.FilteredCategoryApps[0].Apps, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task HasModule_WhenDetailHasOwningModule_ReturnsTrue()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        var module = new AppModule("vscode", "VS Code", true, "/modules/vscode", [], []);
        var detail = new AppDetail(card, module, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry>());

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.That(_vm.HasModule, Is.True);
    }

    [Test]
    public async Task HasNoModule_WhenDetailHasNullModule_ReturnsTrue()
    {
        var card = MakeCard("vscode", "Development/IDEs");
        var detail = new AppDetail(card, null, null, null, null, []);
        _detailService.LoadDetailAsync(card, Arg.Any<CancellationToken>())
            .Returns(detail);
        _startupService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StartupEntry>());

        await _vm.ConfigureAppCommand.ExecuteAsync(card);

        Assert.That(_vm.HasNoModule, Is.True);
    }

    private static AppCardModel MakeCard(string name, string category, CardStatus status = CardStatus.Detected)
    {
        var entry = new CatalogEntry(name, name, name, category, [], null, null, null, null, null, null);
        return new AppCardModel(entry, CardTier.YourApps, status);
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
    private SystemTweaksViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _detectionService = Substitute.For<IGalleryDetectionService>();
        _settingsProvider = Substitute.For<ISettingsProvider>();

        _settingsProvider.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { Profiles = ["Developer"] });

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<TweakCardModel>.Empty);
        _detectionService.DetectFontsAsync(Arg.Any<CancellationToken>())
            .Returns(new FontDetectionResult(
                ImmutableArray<FontCardModel>.Empty,
                ImmutableArray<FontCardModel>.Empty));

        _vm = new SystemTweaksViewModel(_detectionService, _settingsProvider);
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
        _vm.SelectCategoryCommand.Execute("Registry");

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowDetail, Is.True);
            Assert.That(_vm.ShowCategories, Is.False);
            Assert.That(_vm.SelectedCategory, Is.EqualTo("Registry"));
        });

        await Task.CompletedTask;
    }

    [Test]
    public void BackToCategories_ResetsNavigation()
    {
        _vm.SelectCategoryCommand.Execute("Registry");
        _vm.BackToCategoriesCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.ShowCategories, Is.True);
            Assert.That(_vm.SelectedCategory, Is.Null);
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
            .Returns(tweaks);
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
    public async Task RefreshAsync_BuildsCategoriesFromTweaks()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Registry"),
            MakeTweak("tweak2", "Registry"),
            MakeTweak("tweak3", "Explorer"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(tweaks);

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(_vm.Categories, Has.Count.EqualTo(2));
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
    public async Task SelectCategory_FiltersTweaksForCategory()
    {
        var tweaks = ImmutableArray.Create(
            MakeTweak("tweak1", "Registry"),
            MakeTweak("tweak2", "Explorer"));

        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(tweaks);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Registry");

        Assert.That(_vm.FilteredTweaks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectCategory_Fonts_LeavesFilteredTweaksEmpty()
    {
        var tweaks = ImmutableArray.Create(MakeTweak("tweak1", "Registry"));
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(tweaks);

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
        var tweaks = ImmutableArray.Create(MakeTweak("tweak1", "Registry"), MakeTweak("tweak2", "Explorer"));
        _detectionService.DetectTweaksAsync(Arg.Any<IReadOnlySet<UserProfile>>(), Arg.Any<CancellationToken>())
            .Returns(tweaks);

        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectCategoryCommand.Execute("Registry");
        Assert.That(_vm.FilteredTweaks, Has.Count.EqualTo(1));

        // ApplyFilter() is empty — SearchText changes don't filter tweaks (known bug)
        _vm.SearchText = "nonexistent";
        Assert.That(_vm.FilteredTweaks, Has.Count.EqualTo(1));
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
