using Perch.Core.Registry;

namespace Perch.Core.Tests.Registry;

[TestFixture]
public sealed class YamlCapturedRegistryStoreTests
{
    private string _tempDir = null!;
    private string _machinesDir = null!;
    private const string TestHostname = "TEST-MACHINE";

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _machinesDir = Path.Combine(_tempDir, "machines");
        Directory.CreateDirectory(_machinesDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task Load_FileDoesNotExist_ReturnsEmptyData()
    {
        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);

        var data = await store.LoadAsync();

        Assert.That(data.Entries, Is.Empty);
    }

    [Test]
    public async Task Load_NoConfigRepoPath_ReturnsEmptyData()
    {
        var store = new YamlCapturedRegistryStore("", TestHostname);

        var data = await store.LoadAsync();

        Assert.That(data.Entries, Is.Empty);
    }

    [Test]
    public async Task SaveAndLoad_RoundTrips()
    {
        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);
        var capturedAt = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var data = new CapturedRegistryData();
        data.Entries[@"HKCU\Software\Test\Value1"] = new CapturedRegistryEntry
        {
            Value = "1", Kind = RegistryValueType.DWord, CapturedAt = capturedAt,
        };
        data.Entries[@"HKCU\Software\Test\Value2"] = new CapturedRegistryEntry
        {
            Value = "hello", Kind = RegistryValueType.String, CapturedAt = capturedAt,
        };

        await store.SaveAsync(data);
        var loaded = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Entries, Has.Count.EqualTo(2));
            Assert.That(loaded.Entries.ContainsKey(@"HKCU\Software\Test\Value1"), Is.True);
            Assert.That(loaded.Entries.ContainsKey(@"HKCU\Software\Test\Value2"), Is.True);
            Assert.That(loaded.Entries[@"HKCU\Software\Test\Value1"].Value, Is.EqualTo("1"));
            Assert.That(loaded.Entries[@"HKCU\Software\Test\Value2"].Kind, Is.EqualTo(RegistryValueType.String));
        });
    }

    [Test]
    public async Task Save_CreatesMachinesDirectoryIfMissing()
    {
        Directory.Delete(_machinesDir);
        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);
        var data = new CapturedRegistryData();
        data.Entries["key"] = new CapturedRegistryEntry
        {
            Value = "1", Kind = RegistryValueType.DWord, CapturedAt = DateTime.UtcNow,
        };

        await store.SaveAsync(data);

        Assert.That(File.Exists(Path.Combine(_machinesDir, $"{TestHostname}.yaml")), Is.True);
    }

    [Test]
    public async Task Load_CorruptFile_ReturnsEmptyData()
    {
        string filePath = Path.Combine(_machinesDir, $"{TestHostname}.yaml");
        await File.WriteAllTextAsync(filePath, "{{{{not valid yaml");
        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);

        var data = await store.LoadAsync();

        Assert.That(data.Entries, Is.Empty);
    }

    [Test]
    public async Task Save_OverwritesCapturedRegistrySection()
    {
        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);

        var first = new CapturedRegistryData();
        first.Entries["key1"] = new CapturedRegistryEntry
        {
            Value = "1", Kind = RegistryValueType.DWord, CapturedAt = DateTime.UtcNow,
        };
        await store.SaveAsync(first);

        var second = new CapturedRegistryData();
        second.Entries["key2"] = new CapturedRegistryEntry
        {
            Value = "val", Kind = RegistryValueType.String, CapturedAt = DateTime.UtcNow,
        };
        await store.SaveAsync(second);

        var loaded = await store.LoadAsync();
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Entries, Has.Count.EqualTo(1));
            Assert.That(loaded.Entries.ContainsKey("key2"), Is.True);
        });
    }

    [Test]
    public async Task Save_PreservesExistingProfileData()
    {
        string filePath = Path.Combine(_machinesDir, $"{TestHostname}.yaml");
        await File.WriteAllTextAsync(filePath, """
            include-modules:
              - git
              - vscode
            variables:
              editor: code
            """);

        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);
        var data = new CapturedRegistryData();
        data.Entries[@"HKCU\Software\Test\Value1"] = new CapturedRegistryEntry
        {
            Value = "1", Kind = RegistryValueType.DWord, CapturedAt = DateTime.UtcNow,
        };
        await store.SaveAsync(data);

        string yaml = await File.ReadAllTextAsync(filePath);
        Assert.Multiple(() =>
        {
            Assert.That(yaml, Does.Contain("include-modules"));
            Assert.That(yaml, Does.Contain("git"));
            Assert.That(yaml, Does.Contain("vscode"));
            Assert.That(yaml, Does.Contain("variables"));
            Assert.That(yaml, Does.Contain("editor: code"));
            Assert.That(yaml, Does.Contain("captured-registry"));
        });
    }

    [Test]
    public async Task Load_FileWithOnlyProfileData_ReturnsEmptyCaptured()
    {
        string filePath = Path.Combine(_machinesDir, $"{TestHostname}.yaml");
        await File.WriteAllTextAsync(filePath, """
            include-modules:
              - git
            """);

        var store = new YamlCapturedRegistryStore(_tempDir, TestHostname);
        var data = await store.LoadAsync();

        Assert.That(data.Entries, Is.Empty);
    }

    [Test]
    public async Task Save_NoConfigRepoPath_DoesNotThrow()
    {
        var store = new YamlCapturedRegistryStore("", TestHostname);
        var data = new CapturedRegistryData();
        data.Entries["key"] = new CapturedRegistryEntry
        {
            Value = "1", Kind = RegistryValueType.DWord, CapturedAt = DateTime.UtcNow,
        };

        Assert.DoesNotThrowAsync(() => store.SaveAsync(data));
    }
}
