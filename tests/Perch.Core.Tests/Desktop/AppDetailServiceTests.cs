#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Symlinks;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class AppDetailServiceTests
{
    private IModuleDiscoveryService _moduleDiscovery = null!;
    private ICatalogService _catalog = null!;
    private ISettingsProvider _settings = null!;
    private IPlatformDetector _platformDetector = null!;
    private ISymlinkProvider _symlinkProvider = null!;
    private AppDetailService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _moduleDiscovery = Substitute.For<IModuleDiscoveryService>();
        _catalog = Substitute.For<ICatalogService>();
        _settings = Substitute.For<ISettingsProvider>();
        _platformDetector = Substitute.For<IPlatformDetector>();
        _symlinkProvider = Substitute.For<ISymlinkProvider>();
        _settings.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = @"C:\config" });
        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<CatalogEntry>.Empty);
        _platformDetector.CurrentPlatform.Returns(Platform.Windows);

        _service = new AppDetailService(_moduleDiscovery, _catalog, _settings, _platformDetector, _symlinkProvider);
    }

    [Test]
    public async Task LoadDetailAsync_NoConfigRepo_ReturnsEmptyDetail()
    {
        _settings.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new PerchSettings { ConfigRepoPath = null });

        var card = MakeCard("vscode");
        var detail = await _service.LoadDetailAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(detail.Card, Is.EqualTo(card));
            Assert.That(detail.OwningModule, Is.Null);
            Assert.That(detail.Manifest, Is.Null);
            Assert.That(detail.ManifestYaml, Is.Null);
            Assert.That(detail.ManifestPath, Is.Null);
            Assert.That(detail.Alternatives, Is.Empty);
        });
    }

    [Test]
    public async Task LoadDetailAsync_NoModules_ReturnsNullModule()
    {
        _moduleDiscovery.DiscoverAsync(@"C:\config", Arg.Any<CancellationToken>())
            .Returns(new DiscoveryResult([], []));

        var card = MakeCard("vscode");
        var detail = await _service.LoadDetailAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(detail.OwningModule, Is.Null);
            Assert.That(detail.Manifest, Is.Null);
            Assert.That(detail.ManifestYaml, Is.Null);
        });
    }

    [Test]
    public async Task LoadDetailAsync_ReturnsAlternativesFromSameCategory()
    {
        _moduleDiscovery.DiscoverAsync(@"C:\config", Arg.Any<CancellationToken>())
            .Returns(new DiscoveryResult([], []));

        var alt = MakeCatalogEntry("sublime-text", "Editors");
        var other = MakeCatalogEntry("firefox", "Browsers");
        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(alt, other));

        var card = MakeCard("vscode", category: "Editors");
        var detail = await _service.LoadDetailAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(detail.Alternatives, Has.Length.EqualTo(1));
            Assert.That(detail.Alternatives[0].Id, Is.EqualTo("sublime-text"));
        });
    }

    [Test]
    public async Task LoadDetailAsync_ExcludesSelfFromAlternatives()
    {
        _moduleDiscovery.DiscoverAsync(@"C:\config", Arg.Any<CancellationToken>())
            .Returns(new DiscoveryResult([], []));

        var self = MakeCatalogEntry("vscode", "Editors");
        var alt = MakeCatalogEntry("sublime-text", "Editors");
        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(self, alt));

        var card = MakeCard("vscode", category: "Editors");
        var detail = await _service.LoadDetailAsync(card);

        Assert.Multiple(() =>
        {
            Assert.That(detail.Alternatives, Has.Length.EqualTo(1));
            Assert.That(detail.Alternatives[0].Id, Is.EqualTo("sublime-text"));
        });
    }

    [Test]
    public async Task LoadDetailAsync_NoAlternativesInCategory_ReturnsEmpty()
    {
        _moduleDiscovery.DiscoverAsync(@"C:\config", Arg.Any<CancellationToken>())
            .Returns(new DiscoveryResult([], []));

        _catalog.GetAllAppsAsync(Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(MakeCatalogEntry("firefox", "Browsers")));

        var card = MakeCard("vscode", category: "Editors");
        var detail = await _service.LoadDetailAsync(card);

        Assert.That(detail.Alternatives, Is.Empty);
    }

    private static AppCardModel MakeCard(string id, string category = "Editors") =>
        new(MakeCatalogEntry(id, category), CardTier.YourApps, CardStatus.Detected);

    private static CatalogEntry MakeCatalogEntry(string id, string category) =>
        new(id, id, null, category, [], null, null, null, null, null, null);
}
#endif
