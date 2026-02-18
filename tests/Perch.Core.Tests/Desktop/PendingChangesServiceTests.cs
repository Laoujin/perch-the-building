#if DESKTOP_TESTS
using System.Collections.Immutable;
using System.Runtime.Versioning;

using Perch.Core.Catalog;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class PendingChangesServiceTests
{
    private PendingChangesService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new PendingChangesService();
    }

    [Test]
    public void InitialState_IsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_sut.Count, Is.Zero);
            Assert.That(_sut.HasChanges, Is.False);
            Assert.That(_sut.Changes, Is.Empty);
        });
    }

    [Test]
    public void Add_SingleChange_IncrementsCount()
    {
        var app = CreateAppCard("app1");
        _sut.Add(new LinkAppChange(app));

        Assert.Multiple(() =>
        {
            Assert.That(_sut.Count, Is.EqualTo(1));
            Assert.That(_sut.HasChanges, Is.True);
        });
    }

    [Test]
    public void Add_DuplicateIdAndKind_ReplacesExisting()
    {
        var app = CreateAppCard("app1");
        _sut.Add(new LinkAppChange(app));
        _sut.Add(new LinkAppChange(app));

        Assert.That(_sut.Count, Is.EqualTo(1));
    }

    [Test]
    public void Add_SameIdDifferentKind_KeepsBoth()
    {
        var app = CreateAppCard("app1");
        _sut.Add(new LinkAppChange(app));
        _sut.Add(new UnlinkAppChange(app));

        Assert.That(_sut.Count, Is.EqualTo(2));
    }

    [Test]
    public void Remove_ExistingChange_DecrementsCount()
    {
        var app = CreateAppCard("app1");
        _sut.Add(new LinkAppChange(app));
        _sut.Remove("app1", PendingChangeKind.LinkApp);

        Assert.Multiple(() =>
        {
            Assert.That(_sut.Count, Is.Zero);
            Assert.That(_sut.HasChanges, Is.False);
        });
    }

    [Test]
    public void Remove_NonExistentId_DoesNothing()
    {
        var app = CreateAppCard("app1");
        _sut.Add(new LinkAppChange(app));
        _sut.Remove("app2", PendingChangeKind.LinkApp);

        Assert.That(_sut.Count, Is.EqualTo(1));
    }

    [Test]
    public void Remove_WrongKind_DoesNothing()
    {
        var app = CreateAppCard("app1");
        _sut.Add(new LinkAppChange(app));
        _sut.Remove("app1", PendingChangeKind.UnlinkApp);

        Assert.That(_sut.Count, Is.EqualTo(1));
    }

    [Test]
    public void Clear_RemovesAllChanges()
    {
        var app1 = CreateAppCard("app1");
        var app2 = CreateAppCard("app2");
        _sut.Add(new LinkAppChange(app1));
        _sut.Add(new UnlinkAppChange(app2));

        _sut.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_sut.Count, Is.Zero);
            Assert.That(_sut.HasChanges, Is.False);
            Assert.That(_sut.Changes, Is.Empty);
        });
    }

    [Test]
    public void Add_RaisesPropertyChanged_ForCount()
    {
        var raised = new List<string>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        _sut.Add(new LinkAppChange(CreateAppCard("app1")));

        Assert.That(raised, Does.Contain(nameof(PendingChangesService.Count)));
        Assert.That(raised, Does.Contain(nameof(PendingChangesService.HasChanges)));
    }

    [Test]
    public void Changes_IsReadOnly()
    {
        Assert.That(_sut.Changes, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyObservableCollection<PendingChange>>());
    }

    [Test]
    public void Add_MultipleTypes_TracksAll()
    {
        var app = CreateAppCard("app1");
        var tweak = CreateTweakCard("tweak1");
        var dotfile = CreateDotfileCard("df1");

        _sut.Add(new LinkAppChange(app));
        _sut.Add(new ApplyTweakChange(tweak));
        _sut.Add(new LinkDotfileChange(dotfile));

        Assert.That(_sut.Count, Is.EqualTo(3));
    }

    private static AppCardModel CreateAppCard(string id)
    {
        var entry = new CatalogEntry(id, id, null, "test", [], null, null, null, null, null, null);
        return new AppCardModel(entry, CardTier.YourApps, CardStatus.Detected);
    }

    private static TweakCardModel CreateTweakCard(string id)
    {
        var entry = new TweakCatalogEntry(id, id, "test", [], null, true, [], []);
        return new TweakCardModel(entry, CardStatus.NotInstalled);
    }

    private static AppCardModel CreateDotfileCard(string id)
    {
        var entry = new CatalogEntry(id, id, null, "test", [], null, null, null, null, null, null, CatalogKind.Dotfile);
        return new AppCardModel(entry, CardTier.Other, CardStatus.Detected);
    }
}
#endif
