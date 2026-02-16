using System.Collections.Immutable;
using Perch.Core;
using Perch.Core.Deploy;
using Perch.Core.Git;
using Perch.Core.Modules;
using Perch.Core.Packages;

namespace Perch.Core.Tests.Git;

[TestFixture]
public sealed class CleanFilterServiceTests
{
    private IProcessRunner _processRunner = null!;
    private CleanFilterService _service = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessRunResult(0, "", ""));
        _service = new CleanFilterService(_processRunner);
        _tempDir = Path.Combine(Path.GetTempPath(), $"perch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private AppModule CreateModuleWithFilter(string name, string scriptName, params string[] files)
    {
        string modulePath = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(modulePath);
        string scriptPath = Path.Combine(modulePath, scriptName);
        File.WriteAllText(scriptPath, "#!/bin/bash");
        var filter = new CleanFilterDefinition($"{name}-clean", scriptName, files.ToImmutableArray());
        return new AppModule(name, name, true, modulePath, ImmutableArray<Platform>.Empty,
            ImmutableArray.Create(new LinkEntry("dummy", "dummy", LinkType.Symlink)), CleanFilter: filter);
    }

    [Test]
    public async Task SetupAsync_RegistersFilterInGitConfig()
    {
        var module = CreateModuleWithFilter("obsidian", "clean.sh", "data.json");
        var modules = ImmutableArray.Create(module);

        await _service.SetupAsync(_tempDir, modules);

        await _processRunner.Received(1).RunAsync("git",
            Arg.Is<string>(s => s.Contains("config --local filter.obsidian-clean.clean")),
            _tempDir, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetupAsync_AddsGitattributesEntry()
    {
        var module = CreateModuleWithFilter("obsidian", "clean.sh", "data.json", "workspace.json");
        var modules = ImmutableArray.Create(module);

        await _service.SetupAsync(_tempDir, modules);

        string gitattributes = await File.ReadAllTextAsync(Path.Combine(_tempDir, ".gitattributes"));
        Assert.Multiple(() =>
        {
            Assert.That(gitattributes, Does.Contain("obsidian/data.json filter=obsidian-clean"));
            Assert.That(gitattributes, Does.Contain("obsidian/workspace.json filter=obsidian-clean"));
        });
    }

    [Test]
    public async Task SetupAsync_AlreadyRegistered_SkipsWithOk()
    {
        var module = CreateModuleWithFilter("obsidian", "clean.sh", "data.json");
        var modules = ImmutableArray.Create(module);

        string gitattributesPath = Path.Combine(_tempDir, ".gitattributes");
        await File.WriteAllTextAsync(gitattributesPath, "obsidian/data.json filter=obsidian-clean\n");

        var results = await _service.SetupAsync(_tempDir, modules);

        Assert.That(results, Has.Length.EqualTo(1));
        Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Ok));
        string content = await File.ReadAllTextAsync(gitattributesPath);
        int count = content.Split("obsidian/data.json").Length - 1;
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task SetupAsync_ScriptNotFound_ReportsError()
    {
        string modulePath = Path.Combine(_tempDir, "mymod");
        Directory.CreateDirectory(modulePath);
        var filter = new CleanFilterDefinition("mymod-clean", "nonexistent.sh", ImmutableArray.Create("data.json"));
        var module = new AppModule("mymod", "mymod", true, modulePath, ImmutableArray<Platform>.Empty,
            ImmutableArray.Create(new LinkEntry("dummy", "dummy", LinkType.Symlink)), CleanFilter: filter);
        var modules = ImmutableArray.Create(module);

        var results = await _service.SetupAsync(_tempDir, modules);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Error));
            Assert.That(results[0].Message, Does.Contain("not found"));
        });
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetupAsync_MultipleModules_RegistersAll()
    {
        var module1 = CreateModuleWithFilter("obsidian", "clean.sh", "data.json");
        var module2 = CreateModuleWithFilter("vscode", "clean-vscode.sh", "state.json");
        var modules = ImmutableArray.Create(module1, module2);

        var results = await _service.SetupAsync(_tempDir, modules);

        Assert.That(results, Has.Length.EqualTo(2));
        Assert.That(results.All(r => r.Level == ResultLevel.Ok), Is.True);
    }

    [Test]
    public async Task SetupAsync_NoCleanFilter_Skips()
    {
        var module = new AppModule("plain", "plain", true, Path.Combine(_tempDir, "plain"), ImmutableArray<Platform>.Empty,
            ImmutableArray.Create(new LinkEntry("dummy", "dummy", LinkType.Symlink)));
        var modules = ImmutableArray.Create(module);

        var results = await _service.SetupAsync(_tempDir, modules);

        Assert.That(results, Is.Empty);
        await _processRunner.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetupAsync_RulesBasedFilter_RegistersPerchCommand()
    {
        string modulePath = Path.Combine(_tempDir, "npp");
        Directory.CreateDirectory(modulePath);
        var rules = ImmutableArray.Create(new FilterRule("strip-xml-elements", ImmutableArray.Create("FindHistory")));
        var filter = new CleanFilterDefinition("npp-clean", null, ImmutableArray.Create("config.xml"), rules);
        var module = new AppModule("npp", "npp", true, modulePath, ImmutableArray<Platform>.Empty,
            ImmutableArray.Create(new LinkEntry("dummy", "dummy", LinkType.Symlink)), CleanFilter: filter);
        var modules = ImmutableArray.Create(module);

        var results = await _service.SetupAsync(_tempDir, modules);

        Assert.That(results, Has.Length.EqualTo(1));
        Assert.That(results[0].Level, Is.EqualTo(ResultLevel.Ok));
        await _processRunner.Received(1).RunAsync("git",
            Arg.Is<string>(s => s.Contains("perch filter clean npp")),
            _tempDir, Arg.Any<CancellationToken>());
    }
}
