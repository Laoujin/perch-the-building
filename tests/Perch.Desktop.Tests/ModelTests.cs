using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;

using Perch.Core.Catalog;
using Perch.Core.Modules;
using Perch.Core.Registry;
using Perch.Core.Scanner;
using Perch.Core.Startup;
using Perch.Core.Deploy;
using Perch.Core.Status;
using Perch.Core.Tweaks;
using Perch.Desktop.Models;
using Perch.Desktop.ViewModels;
using Perch.Desktop.ViewModels.Wizard;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class ModelTests
{
    private static CatalogEntry MakeEntry(
        string id = "test-app",
        string name = "Test App",
        string? displayName = null,
        string category = "Development",
        string? description = null,
        CatalogKind kind = CatalogKind.App,
        CatalogLinks? links = null,
        InstallDefinition? install = null,
        CatalogConfigDefinition? config = null,
        CatalogExtensions? extensions = null,
        ImmutableArray<AppOwnedTweak> tweaks = default,
        ImmutableArray<string> requires = default,
        ImmutableArray<string> suggests = default,
        ImmutableArray<string> alternatives = default,
        ImmutableArray<string> os = default,
        string? license = null,
        ImmutableArray<string> tags = default) =>
        new(id, name, displayName, category, tags.IsDefault ? [] : tags, description, null, links, install, config, extensions,
            kind, tweaks, default, os, false, license, alternatives, suggests, requires);

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class FontCardModelTests
    {
        [Test]
        public void Constructor_SetsAllProperties()
        {
            var model = new FontCardModel("fira", "Fira Code", "Fira Code", "A mono font", "preview", @"C:\Fonts\FiraCode.ttf", FontCardSource.Gallery, ["mono", "code"], CardStatus.Synced);

            Assert.Multiple(() =>
            {
                Assert.That(model.Id, Is.EqualTo("fira"));
                Assert.That(model.Name, Is.EqualTo("Fira Code"));
                Assert.That(model.FamilyName, Is.EqualTo("Fira Code"));
                Assert.That(model.Description, Is.EqualTo("A mono font"));
                Assert.That(model.PreviewText, Is.EqualTo("preview"));
                Assert.That(model.FullPath, Is.EqualTo(@"C:\Fonts\FiraCode.ttf"));
                Assert.That(model.Source, Is.EqualTo(FontCardSource.Gallery));
                Assert.That(model.Tags, Is.EqualTo(new[] { "mono", "code" }));
                Assert.That(model.Status, Is.EqualTo(CardStatus.Synced));
            });
        }

        [Test]
        public void FileName_WithPath_ReturnsFileName()
        {
            var model = new FontCardModel("fira", "Fira", null, null, null, @"C:\Fonts\FiraCode.ttf", FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.FileName, Is.EqualTo("FiraCode.ttf"));
        }

        [Test]
        public void FileName_NullPath_ReturnsNull()
        {
            var model = new FontCardModel("fira", "Fira", null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.FileName, Is.Null);
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new FontCardModel("fira", "Fira Code", null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesName_ReturnsTrue()
        {
            var model = new FontCardModel("fira", "Fira Code", null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("fira"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesDescription_ReturnsTrue()
        {
            var model = new FontCardModel("fira", "Fira Code", null, "A monospaced font", null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("monospaced"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new FontCardModel("fira", "Fira Code", null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("xyz"), Is.False);
        }

        [Test]
        public void ToggleExpandCommand_TogglesIsExpanded()
        {
            var model = new FontCardModel("fira", "Fira", null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.IsExpanded, Is.False);
            model.ToggleExpandCommand.Execute(null);
            Assert.That(model.IsExpanded, Is.True);
            model.ToggleExpandCommand.Execute(null);
            Assert.That(model.IsExpanded, Is.False);
        }

        [Test]
        public void SampleText_HasDefault()
        {
            var model = new FontCardModel("fira", "Fira", null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);
            Assert.That(model.SampleText, Is.EqualTo("The quick brown fox jumps over the lazy dog"));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class AppCardModelTests
    {
        [Test]
        public void Constructor_SetsAllProperties()
        {
            var entry = MakeEntry(description: "desc", links: new("https://example.com", "https://docs.com", "https://github.com"), license: "MIT");
            var model = new AppCardModel(entry, CardTier.YourApps, CardStatus.Synced, "https://logo.png");

            Assert.Multiple(() =>
            {
                Assert.That(model.Id, Is.EqualTo("test-app"));
                Assert.That(model.Name, Is.EqualTo("Test App"));
                Assert.That(model.Category, Is.EqualTo("Development"));
                Assert.That(model.Description, Is.EqualTo("desc"));
                Assert.That(model.Tier, Is.EqualTo(CardTier.YourApps));
                Assert.That(model.Status, Is.EqualTo(CardStatus.Synced));
                Assert.That(model.Website, Is.EqualTo("https://example.com"));
                Assert.That(model.GitHub, Is.EqualTo("https://github.com"));
                Assert.That(model.Docs, Is.EqualTo("https://docs.com"));
                Assert.That(model.License, Is.EqualTo("MIT"));
                Assert.That(model.LogoUrl, Is.EqualTo("https://logo.png"));
            });
        }

        [Test]
        public void GitHubStarsFormatted_Null_ReturnsNull()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            model.GitHubStars = null;
            Assert.That(model.GitHubStarsFormatted, Is.Null);
        }

        [Test]
        public void GitHubStarsFormatted_Zero_ReturnsNull()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged) { GitHubStars = 0 };
            Assert.That(model.GitHubStarsFormatted, Is.Null);
        }

        [Test]
        public void GitHubStarsFormatted_Under1000_ReturnsExact()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged) { GitHubStars = 500 };
            Assert.That(model.GitHubStarsFormatted, Is.EqualTo("500"));
        }

        [Test]
        public void GitHubStarsFormatted_Over1000_ReturnsK()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged) { GitHubStars = 2000 };
            Assert.That(model.GitHubStarsFormatted, Is.EqualTo("2k"));
        }

        [Test]
        public void DisplayLabel_NoDisplayName_ReturnsName()
        {
            var model = new AppCardModel(MakeEntry(name: "my-tool"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.DisplayLabel, Is.EqualTo("my-tool"));
        }

        [Test]
        public void DisplayLabel_WithDisplayName_ReturnsDisplayName()
        {
            var model = new AppCardModel(MakeEntry(displayName: "My Tool"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.DisplayLabel, Is.EqualTo("My Tool"));
        }

        [Test]
        public void BroadCategory_SplitsOnSlash()
        {
            var model = new AppCardModel(MakeEntry(category: "Development/IDEs"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.BroadCategory, Is.EqualTo("Development"));
        }

        [Test]
        public void SubCategory_SplitsOnSlash()
        {
            var model = new AppCardModel(MakeEntry(category: "Development/IDEs"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.SubCategory, Is.EqualTo("IDEs"));
        }

        [Test]
        public void SubCategory_NoSlash_ReturnsCategory()
        {
            var model = new AppCardModel(MakeEntry(category: "System"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.SubCategory, Is.EqualTo("System"));
        }

        [TestCase(CardStatus.Synced, true)]
        [TestCase(CardStatus.Drifted, true)]
        [TestCase(CardStatus.PendingAdd, true)]
        [TestCase(CardStatus.Unmanaged, false)]
        [TestCase(CardStatus.Detected, false)]
        [TestCase(CardStatus.PendingRemove, false)]
        public void IsManaged_CorrectForStatus(CardStatus status, bool expected)
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, status);
            Assert.That(model.IsManaged, Is.EqualTo(expected));
        }

        [TestCase(CardStatus.Unmanaged, true)]
        [TestCase(CardStatus.Detected, true)]
        [TestCase(CardStatus.PendingRemove, true)]
        [TestCase(CardStatus.Synced, false)]
        public void IsActionAdd_CorrectForStatus(CardStatus status, bool expected)
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, status);
            Assert.That(model.IsActionAdd, Is.EqualTo(expected));
        }

        [Test]
        public void ActionButtonText_Add()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.ActionButtonText, Is.EqualTo("Add to Perch"));
        }

        [Test]
        public void ActionButtonText_Remove()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Synced);
            Assert.That(model.ActionButtonText, Is.EqualTo("Remove from Perch"));
        }

        [Test]
        public void IsSuggested_True()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Suggested, CardStatus.Unmanaged);
            Assert.That(model.IsSuggested, Is.True);
        }

        [Test]
        public void IsSuggested_False()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.IsSuggested, Is.False);
        }

        [TestCase(CatalogKind.CliTool, "cli-tool")]
        [TestCase(CatalogKind.Runtime, "runtime")]
        [TestCase(CatalogKind.Dotfile, "dotfile")]
        [TestCase(CatalogKind.App, null)]
        public void KindBadge_CorrectForKind(CatalogKind kind, string? expected)
        {
            var model = new AppCardModel(MakeEntry(kind: kind), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.KindBadge, Is.EqualTo(expected));
        }

        [Test]
        public void HasDetailPage_WithTweaks_True()
        {
            var tweak = new AppOwnedTweak("t1", "Tweak", null, []);
            var model = new AppCardModel(MakeEntry(tweaks: [tweak]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasDetailPage, Is.True);
        }

        [Test]
        public void HasDetailPage_WithInstall_True()
        {
            var model = new AppCardModel(MakeEntry(install: new("winget-id", null)), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasDetailPage, Is.True);
        }

        [Test]
        public void HasDetailPage_Bare_False()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasDetailPage, Is.False);
        }

        [Test]
        public void HasInstall_WithWinget_True()
        {
            var model = new AppCardModel(MakeEntry(install: new("winget-id", null)), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasInstall, Is.True);
        }

        [Test]
        public void HasInstall_NoInstall_False()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasInstall, Is.False);
        }

        [Test]
        public void HasDependents_WithDeps_True()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            var dep = new AppCardModel(MakeEntry(id: "dep"), CardTier.Other, CardStatus.Unmanaged);
            model.DependentApps = [dep];
            Assert.That(model.HasDependents, Is.True);
            Assert.That(model.DependentAppCount, Is.EqualTo(1));
        }

        [Test]
        public void HasDependents_NoDeps_False()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasDependents, Is.False);
            Assert.That(model.DependentAppCount, Is.EqualTo(0));
        }

        [Test]
        public void OnStatusChanged_RaisesPropertyNotifications()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            var changedProps = new List<string>();
            model.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

            model.Status = CardStatus.Synced;

            Assert.That(changedProps, Has.Member("IsManaged"));
            Assert.That(changedProps, Has.Member("IsActionAdd"));
            Assert.That(changedProps, Has.Member("ActionButtonText"));
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesName_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(name: "Visual Studio Code"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("visual"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesDisplayName_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(displayName: "VS Code"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("vs code"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesDescription_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(description: "a code editor"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("editor"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesCategory_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(category: "Development/IDEs"), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("ides"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesTag_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(tags: ["editor", "ide"]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("ide"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesDependentApp_ReturnsTrue()
        {
            var model = new AppCardModel(MakeEntry(name: "Parent"), CardTier.Other, CardStatus.Unmanaged);
            var dep = new AppCardModel(MakeEntry(id: "dep", name: "Child Dep"), CardTier.Other, CardStatus.Unmanaged);
            model.DependentApps = [dep];
            Assert.That(model.MatchesSearch("child"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("zzzzz"), Is.False);
        }

        [Test]
        public void HasExtensions_WithBundled_True()
        {
            var model = new AppCardModel(MakeEntry(extensions: new(["ext1"], [])), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasExtensions, Is.True);
        }

        [Test]
        public void HasExtensions_Empty_False()
        {
            var model = new AppCardModel(MakeEntry(), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasExtensions, Is.False);
        }

        [Test]
        public void HasOs_WithOs_True()
        {
            var model = new AppCardModel(MakeEntry(os: ["windows"]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasOs, Is.True);
        }

        [Test]
        public void HasRequires_WithRequires_True()
        {
            var model = new AppCardModel(MakeEntry(requires: ["nodejs"]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasRequires, Is.True);
        }

        [Test]
        public void HasAlternatives_WithAlternatives_True()
        {
            var model = new AppCardModel(MakeEntry(alternatives: ["alt-app"]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasAlternatives, Is.True);
        }

        [Test]
        public void HasSuggests_WithSuggests_True()
        {
            var model = new AppCardModel(MakeEntry(suggests: ["other-app"]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasSuggests, Is.True);
        }

        [Test]
        public void HasTweaks_WithTweaks_True()
        {
            var tweak = new AppOwnedTweak("t1", "Tweak", null, []);
            var model = new AppCardModel(MakeEntry(tweaks: [tweak]), CardTier.Other, CardStatus.Unmanaged);
            Assert.That(model.HasTweaks, Is.True);
        }

        [Test]
        public void OnDetailChanged_RaisesHasFileStatuses()
        {
            var entry = MakeEntry();
            var model = new AppCardModel(entry, CardTier.Other, CardStatus.Unmanaged);
            var detail = new AppDetail(model, null, null, null, null, []);
            model.Detail = detail;

            var changedProps = new List<string>();
            model.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

            model.Detail = null;

            Assert.That(changedProps, Has.Member("HasFileStatuses"));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class EcosystemCardModelTests
    {
        [Test]
        public void Constructor_SetsAllProperties()
        {
            var model = new EcosystemCardModel("eco1", "Node.js Ecosystem", "desc", "https://logo.png", "https://web.com", "https://docs.com", "https://gh.com", "MIT");

            Assert.Multiple(() =>
            {
                Assert.That(model.Id, Is.EqualTo("eco1"));
                Assert.That(model.Name, Is.EqualTo("Node.js Ecosystem"));
                Assert.That(model.Description, Is.EqualTo("desc"));
                Assert.That(model.LogoUrl, Is.EqualTo("https://logo.png"));
                Assert.That(model.Website, Is.EqualTo("https://web.com"));
                Assert.That(model.Docs, Is.EqualTo("https://docs.com"));
                Assert.That(model.GitHub, Is.EqualTo("https://gh.com"));
                Assert.That(model.License, Is.EqualTo("MIT"));
            });
        }

        [Test]
        public void GitHubStarsFormatted_Null_ReturnsNull()
        {
            var model = new EcosystemCardModel("eco1", "Eco", null, null, null, null, null, null);
            Assert.That(model.GitHubStarsFormatted, Is.Null);
        }

        [Test]
        public void GitHubStarsFormatted_Zero_ReturnsNull()
        {
            var model = new EcosystemCardModel("eco1", "Eco", null, null, null, null, null, null) { GitHubStars = 0 };
            Assert.That(model.GitHubStarsFormatted, Is.Null);
        }

        [Test]
        public void GitHubStarsFormatted_Under1000_ReturnsExact()
        {
            var model = new EcosystemCardModel("eco1", "Eco", null, null, null, null, null, null) { GitHubStars = 42 };
            Assert.That(model.GitHubStarsFormatted, Is.EqualTo("42"));
        }

        [Test]
        public void GitHubStarsFormatted_Over1000_ReturnsK()
        {
            var model = new EcosystemCardModel("eco1", "Eco", null, null, null, null, null, null) { GitHubStars = 3000 };
            Assert.That(model.GitHubStarsFormatted, Is.EqualTo("3k"));
        }

        [Test]
        public void UpdateCounts_CalculatesFromItems()
        {
            var model = new EcosystemCardModel("eco1", "Eco", null, null, null, null, null, null);
            model.Items =
            [
                new AppCardModel(MakeEntry(id: "a1"), CardTier.Other, CardStatus.Synced),
                new AppCardModel(MakeEntry(id: "a2"), CardTier.Other, CardStatus.Synced),
                new AppCardModel(MakeEntry(id: "a3"), CardTier.Other, CardStatus.Drifted),
                new AppCardModel(MakeEntry(id: "a4"), CardTier.Other, CardStatus.Detected),
            ];

            model.UpdateCounts();

            Assert.Multiple(() =>
            {
                Assert.That(model.SyncedCount, Is.EqualTo(2));
                Assert.That(model.DriftedCount, Is.EqualTo(1));
                Assert.That(model.DetectedCount, Is.EqualTo(1));
                Assert.That(model.HasBadges, Is.True);
            });
        }

        [Test]
        public void HasBadges_NoCounts_False()
        {
            var model = new EcosystemCardModel("eco1", "Eco", null, null, null, null, null, null);
            Assert.That(model.HasBadges, Is.False);
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new EcosystemCardModel("eco1", "Node.js", null, null, null, null, null, null);
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesName_ReturnsTrue()
        {
            var model = new EcosystemCardModel("eco1", "Node.js", null, null, null, null, null, null);
            Assert.That(model.MatchesSearch("node"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesDescription_ReturnsTrue()
        {
            var model = new EcosystemCardModel("eco1", "Node.js", "JavaScript runtime", null, null, null, null, null);
            Assert.That(model.MatchesSearch("javascript"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesChildItem_ReturnsTrue()
        {
            var model = new EcosystemCardModel("eco1", "Node.js", null, null, null, null, null, null);
            model.Items = [new AppCardModel(MakeEntry(id: "npm", name: "npm"), CardTier.Other, CardStatus.Unmanaged)];
            Assert.That(model.MatchesSearch("npm"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new EcosystemCardModel("eco1", "Node.js", null, null, null, null, null, null);
            Assert.That(model.MatchesSearch("xyz"), Is.False);
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class StartupCardModelTests
    {
        private static StartupEntry MakeStartupEntry(
            StartupSource source = StartupSource.RegistryCurrentUser,
            bool isEnabled = true) =>
            new("id", "TestApp", @"C:\App\test.exe", null, source, isEnabled);

        [Test]
        public void Constructor_SetsProperties()
        {
            var entry = MakeStartupEntry();
            var model = new StartupCardModel(entry);

            Assert.Multiple(() =>
            {
                Assert.That(model.Entry, Is.SameAs(entry));
                Assert.That(model.Name, Is.EqualTo("TestApp"));
                Assert.That(model.Command, Is.EqualTo(@"C:\App\test.exe"));
                Assert.That(model.IsEnabled, Is.True);
            });
        }

        [TestCase(StartupSource.RegistryCurrentUser, "Registry (User)")]
        [TestCase(StartupSource.RegistryLocalMachine, "Registry (Machine)")]
        [TestCase(StartupSource.StartupFolderUser, "Startup Folder (User)")]
        [TestCase(StartupSource.StartupFolderAllUsers, "Startup Folder (All Users)")]
        public void SourceLabel_CorrectForSource(StartupSource source, string expected)
        {
            var model = new StartupCardModel(MakeStartupEntry(source: source));
            Assert.That(model.SourceLabel, Is.EqualTo(expected));
        }

        [TestCase(StartupSource.RegistryCurrentUser, true)]
        [TestCase(StartupSource.RegistryLocalMachine, true)]
        [TestCase(StartupSource.StartupFolderUser, false)]
        [TestCase(StartupSource.StartupFolderAllUsers, false)]
        public void IsRegistrySource_CorrectForSource(StartupSource source, bool expected)
        {
            var model = new StartupCardModel(MakeStartupEntry(source: source));
            Assert.That(model.IsRegistrySource, Is.EqualTo(expected));
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new StartupCardModel(MakeStartupEntry());
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesName_ReturnsTrue()
        {
            var model = new StartupCardModel(MakeStartupEntry());
            Assert.That(model.MatchesSearch("testapp"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesCommand_ReturnsTrue()
        {
            var model = new StartupCardModel(MakeStartupEntry());
            Assert.That(model.MatchesSearch("test.exe"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesSourceLabel_ReturnsTrue()
        {
            var model = new StartupCardModel(MakeStartupEntry());
            Assert.That(model.MatchesSearch("Registry"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new StartupCardModel(MakeStartupEntry());
            Assert.That(model.MatchesSearch("xyz"), Is.False);
        }

        [Test]
        public void Constructor_DisabledEntry_SetsIsEnabledFalse()
        {
            var model = new StartupCardModel(MakeStartupEntry(isEnabled: false));
            Assert.That(model.IsEnabled, Is.False);
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class FontFamilyGroupModelTests
    {
        private static FontCardModel MakeFont(string id = "f1", string name = "Font", string? previewText = null) =>
            new(id, name, name, null, previewText, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);

        [Test]
        public void Constructor_SetsProperties()
        {
            var fonts = new[] { MakeFont() };
            var model = new FontFamilyGroupModel("Fira Code", fonts);

            Assert.Multiple(() =>
            {
                Assert.That(model.FamilyName, Is.EqualTo("Fira Code"));
                Assert.That(model.Fonts, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void SpecimenPhrase_UsesPreviewText()
        {
            var model = new FontFamilyGroupModel("Test", [MakeFont(previewText: "Custom preview")]);
            Assert.That(model.SpecimenPhrase, Is.EqualTo("Custom preview"));
        }

        [Test]
        public void SpecimenPhrase_FallbackWhenNoPreview()
        {
            var model = new FontFamilyGroupModel("Test", [MakeFont()]);
            Assert.That(model.SpecimenPhrase, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void IsSelected_PropagatesDown()
        {
            var f1 = MakeFont("f1");
            var f2 = MakeFont("f2");
            var model = new FontFamilyGroupModel("Test", [f1, f2]);

            model.IsSelected = true;

            Assert.Multiple(() =>
            {
                Assert.That(f1.IsSelected, Is.True);
                Assert.That(f2.IsSelected, Is.True);
            });
        }

        [Test]
        public void IsSelected_SyncsFromChildren_AllSelected()
        {
            var f1 = MakeFont("f1");
            var f2 = MakeFont("f2");
            var model = new FontFamilyGroupModel("Test", [f1, f2]);

            f1.IsSelected = true;
            f2.IsSelected = true;

            Assert.That(model.IsSelected, Is.True);
        }

        [Test]
        public void IsSelected_SyncsFromChildren_NotAllSelected()
        {
            var f1 = MakeFont("f1");
            var f2 = MakeFont("f2");
            var model = new FontFamilyGroupModel("Test", [f1, f2]);

            f1.IsSelected = true;

            Assert.That(model.IsSelected, Is.False);
        }

        [Test]
        public void CollectionChanged_NewItemGetsTracked()
        {
            var f1 = MakeFont("f1");
            var model = new FontFamilyGroupModel("Test", [f1]);

            var f2 = MakeFont("f2");
            model.Fonts.Add(f2);

            f1.IsSelected = true;
            f2.IsSelected = true;

            Assert.That(model.IsSelected, Is.True);
        }

        [Test]
        public void CollectionChanged_RemovedItemUntracked()
        {
            var f1 = MakeFont("f1");
            var f2 = MakeFont("f2");
            var model = new FontFamilyGroupModel("Test", [f1, f2]);

            model.Fonts.Remove(f2);
            f1.IsSelected = true;

            Assert.That(model.IsSelected, Is.True);
        }

        [Test]
        public void Dispose_UnsubscribesEvents()
        {
            var f1 = MakeFont("f1");
            var model = new FontFamilyGroupModel("Test", [f1]);

            model.Dispose();
            f1.IsSelected = true;

            Assert.That(model.IsSelected, Is.False);
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new FontFamilyGroupModel("Fira Code", [MakeFont()]);
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesFamilyName_ReturnsTrue()
        {
            var model = new FontFamilyGroupModel("Fira Code", [MakeFont()]);
            Assert.That(model.MatchesSearch("fira"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesChildFont_ReturnsTrue()
        {
            var model = new FontFamilyGroupModel("MyFamily", [MakeFont(name: "SpecialName")]);
            Assert.That(model.MatchesSearch("special"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new FontFamilyGroupModel("Fira Code", [MakeFont()]);
            Assert.That(model.MatchesSearch("xyz"), Is.False);
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class CertificateCardModelTests
    {
        private static DetectedCertificate MakeCert(
            string subject = "CN=Test",
            string issuer = "CN=CA",
            DateTime? notAfter = null,
            string? friendlyName = null) =>
            new("ABCD1234", subject, issuer, friendlyName, DateTime.Now.AddYears(-1), notAfter ?? DateTime.Now.AddYears(1), false, CertificateStoreName.Personal);

        [Test]
        public void Constructor_SetsProperties()
        {
            var cert = MakeCert();
            var model = new CertificateCardModel(cert);

            Assert.Multiple(() =>
            {
                Assert.That(model.Certificate, Is.SameAs(cert));
                Assert.That(model.SubjectDisplayName, Is.EqualTo("Test"));
                Assert.That(model.IssuerDisplayName, Is.EqualTo("CA"));
                Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.Valid));
            });
        }

        [Test]
        public void ExpiryStatus_Expired()
        {
            var model = new CertificateCardModel(MakeCert(notAfter: DateTime.Now.AddDays(-1)));
            Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.Expired));
        }

        [Test]
        public void ExpiryStatus_ExpiringSoon()
        {
            var model = new CertificateCardModel(MakeCert(notAfter: DateTime.Now.AddDays(15)));
            Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.ExpiringSoon));
        }

        [Test]
        public void ExpiryStatus_Valid()
        {
            var model = new CertificateCardModel(MakeCert(notAfter: DateTime.Now.AddYears(1)));
            Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.Valid));
        }

        [Test]
        public void ExtractCn_WithCnPrefix()
        {
            Assert.That(CertificateCardModel.ExtractCn("CN=MyServer,OU=IT"), Is.EqualTo("MyServer"));
        }

        [Test]
        public void ExtractCn_WithCnNoComma()
        {
            Assert.That(CertificateCardModel.ExtractCn("CN=MyServer"), Is.EqualTo("MyServer"));
        }

        [Test]
        public void ExtractCn_NoCn_ReturnsOriginal()
        {
            Assert.That(CertificateCardModel.ExtractCn("O=MyOrg"), Is.EqualTo("O=MyOrg"));
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new CertificateCardModel(MakeCert());
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesThumbprint_ReturnsTrue()
        {
            var model = new CertificateCardModel(MakeCert());
            Assert.That(model.MatchesSearch("ABCD"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesSubject_ReturnsTrue()
        {
            var model = new CertificateCardModel(MakeCert(subject: "CN=SpecialSubj"));
            Assert.That(model.MatchesSearch("SpecialSubj"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesFriendlyName_ReturnsTrue()
        {
            var model = new CertificateCardModel(MakeCert(friendlyName: "My Cert"));
            Assert.That(model.MatchesSearch("My Cert"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new CertificateCardModel(MakeCert());
            Assert.That(model.MatchesSearch("xyz"), Is.False);
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class CertificateStoreGroupModelTests
    {
        [TestCase(CertificateStoreName.Personal, "Personal")]
        [TestCase(CertificateStoreName.TrustedRoot, "Trusted Root CAs")]
        [TestCase(CertificateStoreName.Intermediate, "Intermediate CAs")]
        [TestCase(CertificateStoreName.TrustedPeople, "Trusted People")]
        public void DisplayName_CorrectForStore(CertificateStoreName store, string expected)
        {
            var model = new CertificateStoreGroupModel(store, []);
            Assert.That(model.DisplayName, Is.EqualTo(expected));
        }

        [Test]
        public void Constructor_SetsProperties()
        {
            var cert = new CertificateCardModel(
                new DetectedCertificate("thumb", "CN=Subj", "CN=Issuer", null, DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1), false, CertificateStoreName.Personal));
            var model = new CertificateStoreGroupModel(CertificateStoreName.Personal, [cert]);

            Assert.Multiple(() =>
            {
                Assert.That(model.Store, Is.EqualTo(CertificateStoreName.Personal));
                Assert.That(model.Certificates, Has.Count.EqualTo(1));
                Assert.That(model.IsExpanded, Is.True);
            });
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new CertificateStoreGroupModel(CertificateStoreName.Personal, []);
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesDisplayName_ReturnsTrue()
        {
            var model = new CertificateStoreGroupModel(CertificateStoreName.Personal, []);
            Assert.That(model.MatchesSearch("personal"), Is.True);
        }

        [Test]
        public void MatchesSearch_MatchesChildCert_ReturnsTrue()
        {
            var cert = new CertificateCardModel(
                new DetectedCertificate("UNIQUE123", "CN=Subj", "CN=Issuer", null, DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1), false, CertificateStoreName.Personal));
            var model = new CertificateStoreGroupModel(CertificateStoreName.Personal, [cert]);
            Assert.That(model.MatchesSearch("UNIQUE"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new CertificateStoreGroupModel(CertificateStoreName.Personal, []);
            Assert.That(model.MatchesSearch("xyz"), Is.False);
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class AppCategoryCardModelTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            var model = new AppCategoryCardModel("Development", "Development", 10, 5, 2, 1);

            Assert.Multiple(() =>
            {
                Assert.That(model.BroadCategory, Is.EqualTo("Development"));
                Assert.That(model.DisplayName, Is.EqualTo("Development"));
                Assert.That(model.ItemCount, Is.EqualTo(10));
                Assert.That(model.SelectedCount, Is.EqualTo(5));
                Assert.That(model.DetectedCount, Is.EqualTo(2));
                Assert.That(model.SuggestedCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void Constructor_NullSubGroups_DefaultsToEmpty()
        {
            var model = new AppCategoryCardModel("Dev", "Dev", 0, 0);
            Assert.That(model.SubGroups, Is.Empty);
            Assert.That(model.Apps, Is.Empty);
        }

        [TestCase("Development")]
        [TestCase("Browsers")]
        [TestCase("System")]
        [TestCase("Communication")]
        [TestCase("Media")]
        [TestCase("Gaming")]
        [TestCase("Utilities")]
        [TestCase("Unknown")]
        public void GetIcon_DoesNotThrow(string category)
        {
            var model = new AppCategoryCardModel(category, category, 0, 0);
            Assert.That(model.IconSymbol, Is.Not.EqualTo(default(Wpf.Ui.Controls.SymbolRegular)));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class TweakCategoryCardModelTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            var model = new TweakCategoryCardModel("Startup", "Startup", "desc", 5, 2);

            Assert.Multiple(() =>
            {
                Assert.That(model.Category, Is.EqualTo("Startup"));
                Assert.That(model.DisplayName, Is.EqualTo("Startup"));
                Assert.That(model.Description, Is.EqualTo("desc"));
                Assert.That(model.ItemCount, Is.EqualTo(5));
                Assert.That(model.SelectedCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void Constructor_NullSubGroups_DefaultsToEmpty()
        {
            var model = new TweakCategoryCardModel("Cat", "Cat", null, 0, 0);
            Assert.That(model.SubGroups, Is.Empty);
        }

        [TestCase("Startup")]
        [TestCase("Explorer")]
        [TestCase("Privacy")]
        [TestCase("Fonts")]
        [TestCase("Taskbar")]
        [TestCase("Performance")]
        [TestCase("Input")]
        [TestCase("Appearance")]
        [TestCase("Certificates")]
        [TestCase("Unknown")]
        public void GetIcon_DoesNotThrow(string category)
        {
            var model = new TweakCategoryCardModel(category, category, null, 0, 0);
            Assert.That(model.IconSymbol, Is.Not.EqualTo(default(Wpf.Ui.Controls.SymbolRegular)));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class StatusItemViewModelTests
    {
        [Test]
        public void Constructor_SetsPropertiesFromResult()
        {
            var result = new StatusResult("my-module", @"C:\source", @"C:\target", DriftLevel.Drift, "Files differ", StatusCategory.Link);
            var vm = new StatusItemViewModel(result);

            Assert.Multiple(() =>
            {
                Assert.That(vm.ModuleName, Is.EqualTo("my-module"));
                Assert.That(vm.SourcePath, Is.EqualTo(@"C:\source"));
                Assert.That(vm.TargetPath, Is.EqualTo(@"C:\target"));
                Assert.That(vm.Level, Is.EqualTo(DriftLevel.Drift));
                Assert.That(vm.Message, Is.EqualTo("Files differ"));
                Assert.That(vm.Category, Is.EqualTo(StatusCategory.Link));
            });
        }

        [TestCase(DriftLevel.Missing, "Missing")]
        [TestCase(DriftLevel.Drift, "Drift")]
        [TestCase(DriftLevel.Error, "Error")]
        [TestCase(DriftLevel.Ok, "OK")]
        public void LevelDisplay_CorrectForLevel(DriftLevel level, string expected)
        {
            var result = new StatusResult("m", "", "", level, "");
            var vm = new StatusItemViewModel(result);
            Assert.That(vm.LevelDisplay, Is.EqualTo(expected));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class DeployResultItemViewModelTests
    {
        [Test]
        public void Constructor_SetsPropertiesFromResult()
        {
            var result = new DeployResult("git", @"C:\config\git", @"C:\Users\test\.gitconfig", ResultLevel.Ok, "Symlink created");
            var vm = new DeployResultItemViewModel(result);

            Assert.Multiple(() =>
            {
                Assert.That(vm.ModuleName, Is.EqualTo("git"));
                Assert.That(vm.SourcePath, Is.EqualTo(@"C:\config\git"));
                Assert.That(vm.TargetPath, Is.EqualTo(@"C:\Users\test\.gitconfig"));
                Assert.That(vm.Level, Is.EqualTo(ResultLevel.Ok));
                Assert.That(vm.Message, Is.EqualTo("Symlink created"));
            });
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class TweakCardModelTests
    {
        private static TweakCatalogEntry MakeTweak(
            string id = "dark-mode",
            string name = "Dark Mode",
            string category = "Appearance/Theme",
            string? description = null,
            bool reversible = true,
            ImmutableArray<string> tags = default,
            ImmutableArray<string> profiles = default,
            ImmutableArray<RegistryEntryDefinition> registry = default) =>
            new(id, name, category, tags.IsDefault ? [] : tags, description, reversible,
                profiles.IsDefault ? [] : profiles, registry.IsDefault ? [] : registry);

        [Test]
        public void Constructor_SetsAllProperties()
        {
            var reg = ImmutableArray.Create(new RegistryEntryDefinition(@"HKCU\Software\Test", "DarkMode", 1, RegistryValueType.DWord));
            var entry = MakeTweak(description: "Enable dark theme", tags: ["restart"], profiles: ["developer"], registry: reg);

            var model = new TweakCardModel(entry, CardStatus.Synced);

            Assert.Multiple(() =>
            {
                Assert.That(model.Id, Is.EqualTo("dark-mode"));
                Assert.That(model.Name, Is.EqualTo("Dark Mode"));
                Assert.That(model.Category, Is.EqualTo("Appearance/Theme"));
                Assert.That(model.Description, Is.EqualTo("Enable dark theme"));
                Assert.That(model.Reversible, Is.True);
                Assert.That(model.Status, Is.EqualTo(CardStatus.Synced));
                Assert.That(model.Tags, Has.Length.EqualTo(1));
                Assert.That(model.Profiles, Has.Length.EqualTo(1));
                Assert.That(model.Registry, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void BroadCategory_ReturnsFirstSegment()
        {
            var model = new TweakCardModel(MakeTweak(category: "Appearance/Theme"), CardStatus.Unmanaged);
            Assert.That(model.BroadCategory, Is.EqualTo("Appearance"));
        }

        [Test]
        public void SubCategory_ReturnsAfterSlash()
        {
            var model = new TweakCardModel(MakeTweak(category: "Appearance/Theme"), CardStatus.Unmanaged);
            Assert.That(model.SubCategory, Is.EqualTo("Theme"));
        }

        [Test]
        public void SubCategory_NoSlash_ReturnsCategory()
        {
            var model = new TweakCardModel(MakeTweak(category: "Privacy"), CardStatus.Unmanaged);
            Assert.That(model.SubCategory, Is.EqualTo("Privacy"));
        }

        [Test]
        public void TotalCount_ReturnsRegistryLength()
        {
            var reg = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord),
                new RegistryEntryDefinition(@"HKCU\B", "Y", 0, RegistryValueType.DWord));
            var model = new TweakCardModel(MakeTweak(registry: reg), CardStatus.Unmanaged);
            Assert.That(model.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public void RestartRequired_TagPresent_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(tags: ["restart"]), CardStatus.Unmanaged);
            Assert.That(model.RestartRequired, Is.True);
        }

        [Test]
        public void RestartRequired_NoTag_ReturnsFalse()
        {
            var model = new TweakCardModel(MakeTweak(tags: ["performance"]), CardStatus.Unmanaged);
            Assert.That(model.RestartRequired, Is.False);
        }

        [Test]
        public void RegistryKeyCountText_SingleKey()
        {
            var reg = ImmutableArray.Create(new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord));
            var model = new TweakCardModel(MakeTweak(registry: reg), CardStatus.Unmanaged);
            Assert.That(model.RegistryKeyCountText, Is.EqualTo("1 registry key"));
        }

        [Test]
        public void RegistryKeyCountText_MultipleKeys()
        {
            var reg = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord),
                new RegistryEntryDefinition(@"HKCU\B", "Y", 0, RegistryValueType.DWord));
            var model = new TweakCardModel(MakeTweak(registry: reg), CardStatus.Unmanaged);
            Assert.That(model.RegistryKeyCountText, Is.EqualTo("2 registry keys"));
        }

        [Test]
        public void IsAllApplied_AllApplied_ReturnsTrue()
        {
            var reg = ImmutableArray.Create(new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord));
            var model = new TweakCardModel(MakeTweak(registry: reg), CardStatus.Synced) { AppliedCount = 1 };
            Assert.That(model.IsAllApplied, Is.True);
        }

        [Test]
        public void IsAllApplied_NotAllApplied_ReturnsFalse()
        {
            var reg = ImmutableArray.Create(
                new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord),
                new RegistryEntryDefinition(@"HKCU\B", "Y", 0, RegistryValueType.DWord));
            var model = new TweakCardModel(MakeTweak(registry: reg), CardStatus.Synced) { AppliedCount = 1 };
            Assert.That(model.IsAllApplied, Is.False);
        }

        [Test]
        public void IsAllApplied_ZeroTotal_ReturnsFalse()
        {
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged) { AppliedCount = 0 };
            Assert.That(model.IsAllApplied, Is.False);
        }

        [Test]
        public void HasCapturedValues_WithCapture_ReturnsTrue()
        {
            var def = new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord);
            var entries = ImmutableArray.Create(new RegistryEntryStatus(def, 0, "captured", false));
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged) { DetectedEntries = entries };
            Assert.That(model.HasCapturedValues, Is.True);
        }

        [Test]
        public void HasCapturedValues_NoCaptured_ReturnsFalse()
        {
            var def = new RegistryEntryDefinition(@"HKCU\A", "X", 1, RegistryValueType.DWord);
            var entries = ImmutableArray.Create(new RegistryEntryStatus(def, 0, null, false));
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged) { DetectedEntries = entries };
            Assert.That(model.HasCapturedValues, Is.False);
        }

        [Test]
        public void HasCapturedValues_DefaultEntries_ReturnsFalse()
        {
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged);
            Assert.That(model.HasCapturedValues, Is.False);
        }

        [Test]
        public void MatchesProfile_EmptyProfiles_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged);
            Assert.That(model.MatchesProfile([UserProfile.Developer]), Is.True);
        }

        [Test]
        public void MatchesProfile_MatchingProfile_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(profiles: ["developer"]), CardStatus.Unmanaged);
            Assert.That(model.MatchesProfile([UserProfile.Developer]), Is.True);
        }

        [Test]
        public void MatchesProfile_NonMatchingProfile_ReturnsFalse()
        {
            var model = new TweakCardModel(MakeTweak(profiles: ["gamer"]), CardStatus.Unmanaged);
            Assert.That(model.MatchesProfile([UserProfile.Developer]), Is.False);
        }

        [Test]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch(""), Is.True);
        }

        [Test]
        public void MatchesSearch_ByName_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(name: "Dark Mode"), CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("dark"), Is.True);
        }

        [Test]
        public void MatchesSearch_ByDescription_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(description: "Enable dark theme"), CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("theme"), Is.True);
        }

        [Test]
        public void MatchesSearch_ByCategory_ReturnsTrue()
        {
            var model = new TweakCardModel(MakeTweak(category: "Appearance/Theme"), CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("appear"), Is.True);
        }

        [Test]
        public void MatchesSearch_NoMatch_ReturnsFalse()
        {
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged);
            Assert.That(model.MatchesSearch("zzzzz"), Is.False);
        }

        [Test]
        public void ObservableProperties_RaisePropertyChanged()
        {
            var model = new TweakCardModel(MakeTweak(), CardStatus.Unmanaged);
            var changed = new List<string>();
            model.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

            model.Status = CardStatus.Synced;
            model.IsSelected = true;
            model.IsExpanded = true;
            model.AppliedCount = 5;
            model.IsSuggested = true;

            Assert.That(changed, Is.SupersetOf(new[] { "Status", "IsSelected", "IsExpanded", "AppliedCount", "IsSuggested" }));
        }
    }
}
