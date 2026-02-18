using Perch.Desktop.Models;

namespace Perch.Desktop.Services;

public enum PendingChangeKind
{
    LinkApp,
    UnlinkApp,
    ApplyTweak,
    RevertTweak,
    LinkDotfile,
    OnboardFont,
    ToggleStartup,
}

public abstract record PendingChange(string Id, string DisplayName, string Description, PendingChangeKind Kind)
{
    public bool IsAdditive => Kind is PendingChangeKind.LinkApp or PendingChangeKind.ApplyTweak
        or PendingChangeKind.LinkDotfile or PendingChangeKind.OnboardFont
        || (this is ToggleStartupChange { Enable: true });
}

public sealed record LinkAppChange(AppCardModel App)
    : PendingChange(App.Id, App.DisplayLabel, "Link app config", PendingChangeKind.LinkApp);

public sealed record UnlinkAppChange(AppCardModel App)
    : PendingChange(App.Id, App.DisplayLabel, "Unlink app config", PendingChangeKind.UnlinkApp);

public sealed record ApplyTweakChange(TweakCardModel Tweak)
    : PendingChange(Tweak.Id, Tweak.Name, Tweak.Description ?? "Apply registry tweak", PendingChangeKind.ApplyTweak);

public sealed record RevertTweakChange(TweakCardModel Tweak)
    : PendingChange(Tweak.Id, Tweak.Name, Tweak.Description ?? "Revert registry tweak", PendingChangeKind.RevertTweak);

public sealed record LinkDotfileChange(DotfileGroupCardModel Dotfile)
    : PendingChange(Dotfile.Id, Dotfile.DisplayLabel, "Link dotfile group", PendingChangeKind.LinkDotfile);

public sealed record OnboardFontChange(FontCardModel Font)
    : PendingChange(Font.Id, Font.Name, "Onboard font to config", PendingChangeKind.OnboardFont);

public sealed record ToggleStartupChange(StartupCardModel Startup, bool Enable)
    : PendingChange(Startup.Name, Startup.Name, Enable ? "Enable startup item" : "Disable startup item", PendingChangeKind.ToggleStartup);
