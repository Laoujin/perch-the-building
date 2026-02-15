using Perch.Core.Modules;

namespace Perch.Core.Tests.Modules;

[TestFixture]
public sealed class EnvironmentExpanderTests
{
    [Test]
    public void Expand_WindowsPercentSyntax_ExpandsVariable()
    {
        Environment.SetEnvironmentVariable("PERCH_TEST_VAR", "resolved");
        try
        {
            var result = EnvironmentExpander.Expand("%PERCH_TEST_VAR%\\subfolder");

            Assert.That(result, Is.EqualTo("resolved\\subfolder"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_VAR", null);
        }
    }

    [Test]
    public void Expand_UnixDollarSyntax_ExpandsVariable()
    {
        Environment.SetEnvironmentVariable("PERCH_TEST_VAR", "resolved");
        try
        {
            var result = EnvironmentExpander.Expand("$PERCH_TEST_VAR/subfolder");

            Assert.That(result, Is.EqualTo("resolved/subfolder"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_VAR", null);
        }
    }

    [Test]
    public void Expand_MultipleVariables_ExpandsAll()
    {
        Environment.SetEnvironmentVariable("PERCH_A", "first");
        Environment.SetEnvironmentVariable("PERCH_B", "second");
        try
        {
            var result = EnvironmentExpander.Expand("%PERCH_A%\\%PERCH_B%\\file");

            Assert.That(result, Is.EqualTo("first\\second\\file"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_A", null);
            Environment.SetEnvironmentVariable("PERCH_B", null);
        }
    }

    [Test]
    public void Expand_UnknownVariable_LeavesUnchanged()
    {
        var result = EnvironmentExpander.Expand("%PERCH_NONEXISTENT_12345%\\file");

        Assert.That(result, Is.EqualTo("%PERCH_NONEXISTENT_12345%\\file"));
    }

    [Test]
    public void Expand_NoVariables_ReturnsOriginal()
    {
        var result = EnvironmentExpander.Expand("C:\\plain\\path\\file.txt");

        Assert.That(result, Is.EqualTo("C:\\plain\\path\\file.txt"));
    }

    [Test]
    public void Expand_UnknownDollarVariable_LeavesUnchanged()
    {
        var result = EnvironmentExpander.Expand("$PERCH_NONEXISTENT_12345/file");

        Assert.That(result, Is.EqualTo("$PERCH_NONEXISTENT_12345/file"));
    }

    [Test]
    public void Expand_CustomVariable_WindowsSyntax_Resolves()
    {
        var variables = new Dictionary<string, string> { ["editor"] = "code" };

        var result = EnvironmentExpander.Expand("%editor%/config", variables);

        Assert.That(result, Is.EqualTo("code/config"));
    }

    [Test]
    public void Expand_CustomVariable_UnixSyntax_Resolves()
    {
        var variables = new Dictionary<string, string> { ["editor"] = "code" };

        var result = EnvironmentExpander.Expand("$editor/config", variables);

        Assert.That(result, Is.EqualTo("code/config"));
    }

    [Test]
    public void Expand_CustomVariable_OverridesEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("PERCH_TEST_OVERRIDE", "env-value");
        try
        {
            var variables = new Dictionary<string, string> { ["PERCH_TEST_OVERRIDE"] = "custom-value" };

            var result = EnvironmentExpander.Expand("%PERCH_TEST_OVERRIDE%/file", variables);

            Assert.That(result, Is.EqualTo("custom-value/file"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_OVERRIDE", null);
        }
    }

    [Test]
    public void Expand_CustomVariable_FallsBackToEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("PERCH_TEST_FALLBACK", "env-value");
        try
        {
            var variables = new Dictionary<string, string> { ["other"] = "value" };

            var result = EnvironmentExpander.Expand("%PERCH_TEST_FALLBACK%/file", variables);

            Assert.That(result, Is.EqualTo("env-value/file"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_FALLBACK", null);
        }
    }

    [Test]
    public void Expand_CustomVariable_MixedWithEnvVars()
    {
        Environment.SetEnvironmentVariable("PERCH_TEST_ENV", "from-env");
        try
        {
            var variables = new Dictionary<string, string> { ["custom"] = "from-profile" };

            var result = EnvironmentExpander.Expand("%PERCH_TEST_ENV%/%custom%/file", variables);

            Assert.That(result, Is.EqualTo("from-env/from-profile/file"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_ENV", null);
        }
    }

    [Test]
    public void Expand_NullVariables_BehavesLikeNoVariables()
    {
        Environment.SetEnvironmentVariable("PERCH_TEST_NULL", "resolved");
        try
        {
            var result = EnvironmentExpander.Expand("%PERCH_TEST_NULL%/file", null);

            Assert.That(result, Is.EqualTo("resolved/file"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERCH_TEST_NULL", null);
        }
    }
}
