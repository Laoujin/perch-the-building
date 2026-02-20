using System.Collections.Immutable;

using NSubstitute;

using Perch.Core;
using Perch.Core.Catalog;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Packages;

[TestFixture]
public sealed class InstallResolverTests
{
    private ICatalogService _catalogService = null!;
    private InstallResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _catalogService = Substitute.For<ICatalogService>();
        _resolver = new InstallResolver(_catalogService);
    }

    private static CatalogEntry CreateApp(string id, string? winget = null, string? choco = null) =>
        new(id, id, null, "Test", ImmutableArray<string>.Empty, null, null, null,
            new InstallDefinition(winget, choco), null, null);

    private static InstallManifest CreateManifest(
        ImmutableArray<string> apps,
        ImmutableDictionary<string, MachineInstallOverrides>? machines = null) =>
        new(apps, machines ?? ImmutableDictionary<string, MachineInstallOverrides>.Empty);

    [Test]
    public async Task ResolveAsync_AppsWithWinget_ResolvesToPackages()
    {
        _catalogService.GetAppAsync("git", Arg.Any<CancellationToken>())
            .Returns(CreateApp("git", winget: "Git.Git"));
        _catalogService.GetAppAsync("vscode", Arg.Any<CancellationToken>())
            .Returns(CreateApp("vscode", winget: "Microsoft.VisualStudioCode"));

        var manifest = CreateManifest(ImmutableArray.Create("git", "vscode"));

        var resolution = await _resolver.ResolveAsync(manifest, "PC", Platform.Windows);

        Assert.That(resolution.Packages, Has.Length.EqualTo(2));
        Assert.That(resolution.Errors, Is.Empty);
    }

    [Test]
    public async Task ResolveAsync_MachineAdd_IncludesExtraApp()
    {
        _catalogService.GetAppAsync("git", Arg.Any<CancellationToken>())
            .Returns(CreateApp("git", winget: "Git.Git"));
        _catalogService.GetAppAsync("heidisql", Arg.Any<CancellationToken>())
            .Returns(CreateApp("heidisql", winget: "HeidiSQL.HeidiSQL"));

        var machines = ImmutableDictionary.CreateBuilder<string, MachineInstallOverrides>(StringComparer.OrdinalIgnoreCase);
        machines["HOME-PC"] = new MachineInstallOverrides(ImmutableArray.Create("heidisql"), ImmutableArray<string>.Empty);
        var manifest = CreateManifest(ImmutableArray.Create("git"), machines.ToImmutable());

        var resolution = await _resolver.ResolveAsync(manifest, "HOME-PC", Platform.Windows);

        Assert.That(resolution.Packages, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task ResolveAsync_MachineExclude_RemovesApp()
    {
        _catalogService.GetAppAsync("git", Arg.Any<CancellationToken>())
            .Returns(CreateApp("git", winget: "Git.Git"));

        var machines = ImmutableDictionary.CreateBuilder<string, MachineInstallOverrides>(StringComparer.OrdinalIgnoreCase);
        machines["WORK-PC"] = new MachineInstallOverrides(ImmutableArray<string>.Empty, ImmutableArray.Create("docker"));
        var manifest = CreateManifest(ImmutableArray.Create("git", "docker"), machines.ToImmutable());

        var resolution = await _resolver.ResolveAsync(manifest, "WORK-PC", Platform.Windows);

        Assert.That(resolution.Packages, Has.Length.EqualTo(1));
        Assert.That(resolution.Packages[0].Name, Is.EqualTo("Git.Git"));
    }

    [Test]
    public async Task ResolveAsync_MissingGalleryEntry_ReportsError()
    {
        _catalogService.GetAppAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((CatalogEntry?)null);

        var manifest = CreateManifest(ImmutableArray.Create("nonexistent"));

        var resolution = await _resolver.ResolveAsync(manifest, "PC", Platform.Windows);

        Assert.That(resolution.Packages, Is.Empty);
        Assert.That(resolution.Errors, Has.Length.EqualTo(1));
        Assert.That(resolution.Errors[0], Does.Contain("nonexistent"));
    }

    [Test]
    public async Task ResolveAsync_FallsBackToChoco_WhenNoWinget()
    {
        _catalogService.GetAppAsync("oldapp", Arg.Any<CancellationToken>())
            .Returns(CreateApp("oldapp", choco: "oldapp-choco"));

        var manifest = CreateManifest(ImmutableArray.Create("oldapp"));

        var resolution = await _resolver.ResolveAsync(manifest, "PC", Platform.Windows);

        Assert.That(resolution.Packages, Has.Length.EqualTo(1));
        Assert.That(resolution.Packages[0].Manager, Is.EqualTo(PackageManager.Chocolatey));
    }

    [Test]
    public async Task ResolveAsync_CatalogException_ReportsError()
    {
        _catalogService.GetAppAsync("broken", Arg.Any<CancellationToken>())
            .Returns<CatalogEntry?>(_ => throw new InvalidOperationException("network error"));

        var manifest = CreateManifest(ImmutableArray.Create("broken"));

        var resolution = await _resolver.ResolveAsync(manifest, "PC", Platform.Windows);

        Assert.That(resolution.Packages, Is.Empty);
        Assert.That(resolution.Errors, Has.Length.EqualTo(1));
        Assert.That(resolution.Errors[0], Does.Contain("network error"));
    }

    [Test]
    public async Task ResolveAsync_NoInstallMetadata_ReportsError()
    {
        var noInstall = new CatalogEntry("app", "app", null, "Test", ImmutableArray<string>.Empty,
            null, null, null, null, null, null);
        _catalogService.GetAppAsync("app", Arg.Any<CancellationToken>())
            .Returns(noInstall);

        var manifest = CreateManifest(ImmutableArray.Create("app"));

        var resolution = await _resolver.ResolveAsync(manifest, "PC", Platform.Windows);

        Assert.That(resolution.Errors, Has.Length.EqualTo(1));
        Assert.That(resolution.Errors[0], Does.Contain("no install metadata"));
    }

    [Test]
    public async Task ResolveAsync_NonWindows_ReturnsNoPackage()
    {
        _catalogService.GetAppAsync("git", Arg.Any<CancellationToken>())
            .Returns(CreateApp("git", winget: "Git.Git"));

        var manifest = CreateManifest(ImmutableArray.Create("git"));

        var resolution = await _resolver.ResolveAsync(manifest, "PC", Platform.Linux);

        Assert.That(resolution.Packages, Is.Empty);
    }

    [Test]
    public async Task ResolveFontsAsync_ResolvesWingetPackage()
    {
        _catalogService.GetAppAsync("cascadia-code", Arg.Any<CancellationToken>())
            .Returns(CreateApp("cascadia-code", winget: "Microsoft.CascadiaCode"));

        var resolution = await _resolver.ResolveFontsAsync(
            ImmutableArray.Create("cascadia-code"), Platform.Windows);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Packages, Has.Length.EqualTo(1));
            Assert.That(resolution.Packages[0].Name, Is.EqualTo("Microsoft.CascadiaCode"));
            Assert.That(resolution.Packages[0].Manager, Is.EqualTo(PackageManager.Winget));
            Assert.That(resolution.Errors, Is.Empty);
        });
    }

    [Test]
    public async Task ResolveFontsAsync_MissingEntry_ReportsError()
    {
        _catalogService.GetAppAsync("nonexistent-font", Arg.Any<CancellationToken>())
            .Returns((CatalogEntry?)null);

        var resolution = await _resolver.ResolveFontsAsync(
            ImmutableArray.Create("nonexistent-font"), Platform.Windows);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Packages, Is.Empty);
            Assert.That(resolution.Errors, Has.Length.EqualTo(1));
            Assert.That(resolution.Errors[0], Does.Contain("nonexistent-font"));
        });
    }

    [Test]
    public async Task ResolveFontsAsync_CatalogException_ReportsError()
    {
        _catalogService.GetAppAsync("broken-font", Arg.Any<CancellationToken>())
            .Returns<CatalogEntry?>(_ => throw new InvalidOperationException("timeout"));

        var resolution = await _resolver.ResolveFontsAsync(
            ImmutableArray.Create("broken-font"), Platform.Windows);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Packages, Is.Empty);
            Assert.That(resolution.Errors, Has.Length.EqualTo(1));
            Assert.That(resolution.Errors[0], Does.Contain("timeout"));
        });
    }

    [Test]
    public async Task ResolveFontsAsync_FallsBackToChoco()
    {
        _catalogService.GetAppAsync("fira-code", Arg.Any<CancellationToken>())
            .Returns(CreateApp("fira-code", choco: "nerd-fonts-FiraCode"));

        var resolution = await _resolver.ResolveFontsAsync(
            ImmutableArray.Create("fira-code"), Platform.Windows);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Packages, Has.Length.EqualTo(1));
            Assert.That(resolution.Packages[0].Manager, Is.EqualTo(PackageManager.Chocolatey));
        });
    }

    [Test]
    public async Task ResolveFontsAsync_MultipleFonts_ResolvesAll()
    {
        _catalogService.GetAppAsync("font-a", Arg.Any<CancellationToken>())
            .Returns(CreateApp("font-a", winget: "FontA.WinGet"));
        _catalogService.GetAppAsync("font-b", Arg.Any<CancellationToken>())
            .Returns(CreateApp("font-b", choco: "font-b-choco"));

        var resolution = await _resolver.ResolveFontsAsync(
            ImmutableArray.Create("font-a", "font-b"), Platform.Windows);

        Assert.That(resolution.Packages, Has.Length.EqualTo(2));
    }
}
