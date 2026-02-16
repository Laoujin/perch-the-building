using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Symlinks;

namespace Perch.Desktop.ViewModels;

public sealed partial class DotfilesViewModel : ViewModelBase
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly ISymlinkProvider _symlinkProvider;
    private readonly IPlatformDetector _platformDetector;
    private readonly ISettingsProvider _settingsProvider;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasConfigRepo = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<ModuleItemViewModel> AllModules { get; } = [];
    public ObservableCollection<ModuleItemViewModel> FilteredModules { get; } = [];

    public DotfilesViewModel(
        IModuleDiscoveryService discoveryService,
        ISymlinkProvider symlinkProvider,
        IPlatformDetector platformDetector,
        ISettingsProvider settingsProvider)
    {
        _discoveryService = discoveryService;
        _symlinkProvider = symlinkProvider;
        _platformDetector = platformDetector;
        _settingsProvider = settingsProvider;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ConfigRepoPath))
        {
            HasConfigRepo = false;
            return;
        }

        HasConfigRepo = true;
        IsLoading = true;
        AllModules.Clear();

        try
        {
            var result = await _discoveryService.DiscoverAsync(settings.ConfigRepoPath, cancellationToken);
            var platform = _platformDetector.CurrentPlatform;

            foreach (var module in result.Modules.OrderBy(m => m.DisplayName))
            {
                var links = new List<LinkItemViewModel>();
                foreach (var link in module.Links)
                {
                    var target = link.GetTargetForPlatform(platform);
                    if (target is null) continue;

                    var sourcePath = Path.Combine(module.ModulePath, link.Source);
                    var isLinked = _symlinkProvider.IsSymlink(target);
                    var currentTarget = isLinked ? _symlinkProvider.GetSymlinkTarget(target) : null;
                    var pointsToSource = currentTarget is not null &&
                        string.Equals(Path.GetFullPath(currentTarget), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase);

                    var status = isLinked && pointsToSource ? LinkStatus.Linked
                        : isLinked ? LinkStatus.Drift
                        : File.Exists(target) || Directory.Exists(target) ? LinkStatus.Conflict
                        : LinkStatus.NotLinked;

                    links.Add(new LinkItemViewModel(link.Source, target, link.LinkType, status));
                }

                var moduleVm = new ModuleItemViewModel(
                    module.Name,
                    module.DisplayName,
                    module.Enabled,
                    module.ModulePath,
                    links);

                AllModules.Add(moduleVm);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ApplyFilter();
        IsLoading = false;
    }

    private void ApplyFilter()
    {
        FilteredModules.Clear();
        LinkedCount = 0;
        TotalCount = 0;

        foreach (var module in AllModules)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !module.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !module.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredModules.Add(module);
            TotalCount += module.Links.Count;
            LinkedCount += module.Links.Count(l => l.Status == LinkStatus.Linked);
        }
    }
}

public sealed class ModuleItemViewModel
{
    public string Name { get; }
    public string DisplayName { get; }
    public bool Enabled { get; }
    public string ModulePath { get; }
    public IReadOnlyList<LinkItemViewModel> Links { get; }

    public string StatusSummary
    {
        get
        {
            var linked = Links.Count(l => l.Status == LinkStatus.Linked);
            return linked == Links.Count ? "All linked" : $"{linked}/{Links.Count} linked";
        }
    }

    public bool IsFullyLinked => Links.All(l => l.Status == LinkStatus.Linked);

    public ModuleItemViewModel(string name, string displayName, bool enabled, string modulePath, IReadOnlyList<LinkItemViewModel> links)
    {
        Name = name;
        DisplayName = displayName;
        Enabled = enabled;
        ModulePath = modulePath;
        Links = links;
    }
}

public sealed class LinkItemViewModel
{
    public string Source { get; }
    public string Target { get; }
    public LinkType LinkType { get; }
    public LinkStatus Status { get; }

    public string StatusDisplay => Status switch
    {
        LinkStatus.Linked => "Linked",
        LinkStatus.Drift => "Drift",
        LinkStatus.Conflict => "File exists",
        LinkStatus.NotLinked => "Not linked",
        _ => "Unknown",
    };

    public LinkItemViewModel(string source, string target, LinkType linkType, LinkStatus status)
    {
        Source = source;
        Target = target;
        LinkType = linkType;
        Status = status;
    }
}

public enum LinkStatus
{
    Linked,
    Drift,
    Conflict,
    NotLinked,
}
