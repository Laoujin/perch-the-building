using Perch.Core;

namespace Perch.Core.Tests;

[TestFixture]
public sealed class PathDisplayTests
{
    [Test]
    public void TruncateMiddle_ShortPath_ReturnsUnchanged()
    {
        var path = @"C:\Users\config\.gitconfig";

        var result = PathDisplay.TruncateMiddle(path, 75);

        Assert.That(result, Is.EqualTo(path));
    }

    [Test]
    public void TruncateMiddle_LongPath_InsertsEllipsisInMiddle()
    {
        var path = @"C:\Users\wouter\Dropbox\Personal\Programming\UnixCode\dotfiles\perch-config\modules\git\.gitconfig";

        var result = PathDisplay.TruncateMiddle(path, 60);

        Assert.That(result, Does.Contain("..."));
        Assert.That(result, Does.StartWith(@"C:\"));
        Assert.That(result, Does.EndWith(".gitconfig"));
        Assert.That(result.Length, Is.LessThanOrEqualTo(60));
    }

    [Test]
    public void TruncateMiddle_PreservesTrailingSegments()
    {
        var path = @"C:\Users\wouter\Dropbox\Personal\Programming\UnixCode\dotfiles\perch-config\modules\git\.gitconfig";

        var result = PathDisplay.TruncateMiddle(path, 60);

        // Should keep end segments (more useful context) over start segments
        Assert.That(result, Does.Contain("git"));
    }

    [Test]
    public void TruncateMiddle_TwoSegmentPath_UsesFallbackTruncation()
    {
        var longName = new string('a', 80);
        var path = $@"C:\{longName}";

        var result = PathDisplay.TruncateMiddle(path, 40);

        Assert.That(result, Does.Contain("..."));
        Assert.That(result.Length, Is.LessThanOrEqualTo(40));
    }

    [Test]
    public void TruncateMiddle_ForwardSlashes_HandledCorrectly()
    {
        var path = "/home/wouter/dropbox/personal/programming/unixcode/dotfiles/perch-config/modules/git/.gitconfig";

        var result = PathDisplay.TruncateMiddle(path, 50);

        Assert.That(result, Does.Contain("..."));
        Assert.That(result, Does.Contain("/"));
        Assert.That(result, Does.Not.Contain(@"\"));
    }

    [Test]
    public void TruncateMiddle_VeryTightBudget_StillProducesValidResult()
    {
        var path = @"C:\Users\wouter\very\deep\nested\path\file.txt";

        var result = PathDisplay.TruncateMiddle(path, 20);

        Assert.That(result, Does.Contain("..."));
        Assert.That(result, Does.StartWith(@"C:\"));
        Assert.That(result, Does.EndWith("file.txt"));
    }

    [Test]
    public void TruncateMiddle_ExactlyAtLimit_ReturnsUnchanged()
    {
        var path = @"C:\short\path.txt";

        var result = PathDisplay.TruncateMiddle(path, path.Length);

        Assert.That(result, Is.EqualTo(path));
    }
}
