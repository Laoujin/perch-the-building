using System.Collections.Immutable;
using System.Runtime.Versioning;

using Perch.Core.Catalog;
using Perch.Core.Modules;
using Perch.Core.Startup;
using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.Tests;

[TestFixture]
[Platform("Win")]
[SupportedOSPlatform("windows")]
public sealed class PendingChangeTests
{
    private static AppCardModel MakeApp(string id = "test-app", string name = "Test App") =>
        new(new CatalogEntry(id, name, null, "Dev", [], null, null, null, null, null, null),
            CardTier.Other, CardStatus.Unmanaged);

    private static TweakCardModel MakeTweak(string id = "tweak-id", string name = "My Tweak", string? description = null) =>
        new(new TweakCatalogEntry(id, name, "System", [], description, false, [], []), CardStatus.Unmanaged);

    private static FontCardModel MakeFont(string id = "font-id", string name = "Font Name") =>
        new(id, name, null, null, null, null, FontCardSource.Gallery, [], CardStatus.Unmanaged);

    private static StartupCardModel MakeStartup(string name = "TestApp") =>
        new(new StartupEntry("id", name, "cmd", null, StartupSource.RegistryCurrentUser, true));

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class IsAdditiveTests
    {
        [Test]
        public void IsAdditive_LinkAppChange_ReturnsTrue()
        {
            var change = new LinkAppChange(MakeApp());
            Assert.That(change.IsAdditive, Is.True);
        }

        [Test]
        public void IsAdditive_UnlinkAppChange_ReturnsFalse()
        {
            var change = new UnlinkAppChange(MakeApp());
            Assert.That(change.IsAdditive, Is.False);
        }

        [Test]
        public void IsAdditive_ApplyTweakChange_ReturnsTrue()
        {
            var change = new ApplyTweakChange(MakeTweak());
            Assert.That(change.IsAdditive, Is.True);
        }

        [Test]
        public void IsAdditive_RevertTweakChange_ReturnsFalse()
        {
            var change = new RevertTweakChange(MakeTweak());
            Assert.That(change.IsAdditive, Is.False);
        }

        [Test]
        public void IsAdditive_RevertTweakToCapturedChange_ReturnsFalse()
        {
            var change = new RevertTweakToCapturedChange(MakeTweak());
            Assert.That(change.IsAdditive, Is.False);
        }

        [Test]
        public void IsAdditive_LinkDotfileChange_ReturnsTrue()
        {
            var change = new LinkDotfileChange(MakeApp());
            Assert.That(change.IsAdditive, Is.True);
        }

        [Test]
        public void IsAdditive_OnboardFontChange_ReturnsTrue()
        {
            var change = new OnboardFontChange(MakeFont());
            Assert.That(change.IsAdditive, Is.True);
        }

        [Test]
        public void IsAdditive_ToggleStartupChange_Enable_ReturnsTrue()
        {
            var change = new ToggleStartupChange(MakeStartup(), Enable: true);
            Assert.That(change.IsAdditive, Is.True);
        }

        [Test]
        public void IsAdditive_ToggleStartupChange_Disable_ReturnsFalse()
        {
            var change = new ToggleStartupChange(MakeStartup(), Enable: false);
            Assert.That(change.IsAdditive, Is.False);
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class KindTests
    {
        [Test]
        public void Kind_LinkAppChange_IsLinkApp()
        {
            Assert.That(new LinkAppChange(MakeApp()).Kind, Is.EqualTo(PendingChangeKind.LinkApp));
        }

        [Test]
        public void Kind_UnlinkAppChange_IsUnlinkApp()
        {
            Assert.That(new UnlinkAppChange(MakeApp()).Kind, Is.EqualTo(PendingChangeKind.UnlinkApp));
        }

        [Test]
        public void Kind_ApplyTweakChange_IsApplyTweak()
        {
            Assert.That(new ApplyTweakChange(MakeTweak()).Kind, Is.EqualTo(PendingChangeKind.ApplyTweak));
        }

        [Test]
        public void Kind_RevertTweakChange_IsRevertTweak()
        {
            Assert.That(new RevertTweakChange(MakeTweak()).Kind, Is.EqualTo(PendingChangeKind.RevertTweak));
        }

        [Test]
        public void Kind_RevertTweakToCapturedChange_IsRevertTweakToCaptured()
        {
            Assert.That(new RevertTweakToCapturedChange(MakeTweak()).Kind, Is.EqualTo(PendingChangeKind.RevertTweakToCaptured));
        }

        [Test]
        public void Kind_LinkDotfileChange_IsLinkDotfile()
        {
            Assert.That(new LinkDotfileChange(MakeApp()).Kind, Is.EqualTo(PendingChangeKind.LinkDotfile));
        }

        [Test]
        public void Kind_OnboardFontChange_IsOnboardFont()
        {
            Assert.That(new OnboardFontChange(MakeFont()).Kind, Is.EqualTo(PendingChangeKind.OnboardFont));
        }

        [Test]
        public void Kind_ToggleStartupChange_IsToggleStartup()
        {
            Assert.That(new ToggleStartupChange(MakeStartup(), Enable: true).Kind, Is.EqualTo(PendingChangeKind.ToggleStartup));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class DescriptionFallbackTests
    {
        [Test]
        public void Description_ApplyTweakChange_NullDescription_UsesDefault()
        {
            var change = new ApplyTweakChange(MakeTweak(description: null));
            Assert.That(change.Description, Is.EqualTo("Apply registry tweak"));
        }

        [Test]
        public void Description_ApplyTweakChange_WithDescription_UsesProvided()
        {
            var change = new ApplyTweakChange(MakeTweak(description: "Custom desc"));
            Assert.That(change.Description, Is.EqualTo("Custom desc"));
        }

        [Test]
        public void Description_RevertTweakChange_NullDescription_UsesDefault()
        {
            var change = new RevertTweakChange(MakeTweak(description: null));
            Assert.That(change.Description, Is.EqualTo("Revert registry tweak"));
        }

        [Test]
        public void Description_RevertTweakChange_WithDescription_UsesProvided()
        {
            var change = new RevertTweakChange(MakeTweak(description: "Custom desc"));
            Assert.That(change.Description, Is.EqualTo("Custom desc"));
        }

        [Test]
        public void Description_RevertTweakToCapturedChange_NullDescription_UsesDefault()
        {
            var change = new RevertTweakToCapturedChange(MakeTweak(description: null));
            Assert.That(change.Description, Is.EqualTo("Revert tweak to captured state"));
        }

        [Test]
        public void Description_RevertTweakToCapturedChange_WithDescription_UsesProvided()
        {
            var change = new RevertTweakToCapturedChange(MakeTweak(description: "Custom desc"));
            Assert.That(change.Description, Is.EqualTo("Custom desc"));
        }
    }

    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public sealed class PropertyMappingTests
    {
        [Test]
        public void Properties_LinkAppChange_MapsFromApp()
        {
            var app = MakeApp("my-app", "My App");
            var change = new LinkAppChange(app);

            Assert.Multiple(() =>
            {
                Assert.That(change.Id, Is.EqualTo("my-app"));
                Assert.That(change.DisplayName, Is.EqualTo("My App"));
                Assert.That(change.Description, Is.EqualTo("Link app config"));
            });
        }

        [Test]
        public void Properties_UnlinkAppChange_MapsFromApp()
        {
            var app = MakeApp("my-app", "My App");
            var change = new UnlinkAppChange(app);

            Assert.Multiple(() =>
            {
                Assert.That(change.Id, Is.EqualTo("my-app"));
                Assert.That(change.DisplayName, Is.EqualTo("My App"));
                Assert.That(change.Description, Is.EqualTo("Unlink app config"));
            });
        }

        [Test]
        public void Properties_ApplyTweakChange_MapsFromTweak()
        {
            var tweak = MakeTweak("tweak-1", "Dark Mode");
            var change = new ApplyTweakChange(tweak);

            Assert.Multiple(() =>
            {
                Assert.That(change.Id, Is.EqualTo("tweak-1"));
                Assert.That(change.DisplayName, Is.EqualTo("Dark Mode"));
            });
        }

        [Test]
        public void Properties_LinkDotfileChange_MapsFromDotfile()
        {
            var dotfile = MakeApp("git-config", "Git Config");
            var change = new LinkDotfileChange(dotfile);

            Assert.Multiple(() =>
            {
                Assert.That(change.Id, Is.EqualTo("git-config"));
                Assert.That(change.DisplayName, Is.EqualTo("Git Config"));
                Assert.That(change.Description, Is.EqualTo("Link dotfile group"));
            });
        }

        [Test]
        public void Properties_OnboardFontChange_MapsFromFont()
        {
            var font = MakeFont("fira", "Fira Code");
            var change = new OnboardFontChange(font);

            Assert.Multiple(() =>
            {
                Assert.That(change.Id, Is.EqualTo("fira"));
                Assert.That(change.DisplayName, Is.EqualTo("Fira Code"));
                Assert.That(change.Description, Is.EqualTo("Onboard font to config"));
            });
        }

        [Test]
        public void Properties_ToggleStartupChange_Enable_MapsFromStartup()
        {
            var startup = MakeStartup("MyApp");
            var change = new ToggleStartupChange(startup, Enable: true);

            Assert.Multiple(() =>
            {
                Assert.That(change.Id, Is.EqualTo("MyApp"));
                Assert.That(change.DisplayName, Is.EqualTo("MyApp"));
                Assert.That(change.Description, Is.EqualTo("Enable startup item"));
            });
        }

        [Test]
        public void Properties_ToggleStartupChange_Disable_HasDisableDescription()
        {
            var change = new ToggleStartupChange(MakeStartup(), Enable: false);
            Assert.That(change.Description, Is.EqualTo("Disable startup item"));
        }
    }
}
