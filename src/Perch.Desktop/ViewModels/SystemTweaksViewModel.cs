using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core;
using Perch.Core.Config;
using Perch.Core.Modules;
using Perch.Core.Registry;

namespace Perch.Desktop.ViewModels;

public sealed partial class SystemTweaksViewModel : ViewModelBase
{
    private readonly IModuleDiscoveryService _discoveryService;
    private readonly IRegistryProvider _registryProvider;
    private readonly IPlatformDetector _platformDetector;
    private readonly ISettingsProvider _settingsProvider;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasConfigRepo = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _appliedCount;

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<TweakModuleViewModel> AllModules { get; } = [];
    public ObservableCollection<TweakModuleViewModel> FilteredModules { get; } = [];

    public SystemTweaksViewModel(
        IModuleDiscoveryService discoveryService,
        IRegistryProvider registryProvider,
        IPlatformDetector platformDetector,
        ISettingsProvider settingsProvider)
    {
        _discoveryService = discoveryService;
        _registryProvider = registryProvider;
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

            foreach (var module in result.Modules
                .Where(m => m.Registry.Length > 0)
                .OrderBy(m => m.DisplayName))
            {
                var entries = new List<RegistryEntryViewModel>();
                foreach (var reg in module.Registry)
                {
                    object? current = null;
                    try
                    {
                        current = _registryProvider.GetValue(reg.Key, reg.Name);
                    }
                    catch
                    {
                        // Registry read may fail on non-Windows
                    }

                    var isApplied = current is not null && Equals(current, reg.Value);
                    entries.Add(new RegistryEntryViewModel(reg.Key, reg.Name, reg.Value, reg.Kind, current, isApplied));
                }

                AllModules.Add(new TweakModuleViewModel(module.Name, module.DisplayName, entries));
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
        AppliedCount = 0;
        TotalCount = 0;

        foreach (var module in AllModules)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !module.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !module.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredModules.Add(module);
            TotalCount += module.Entries.Count;
            AppliedCount += module.Entries.Count(e => e.IsApplied);
        }
    }
}

public sealed class TweakModuleViewModel
{
    public string Name { get; }
    public string DisplayName { get; }
    public IReadOnlyList<RegistryEntryViewModel> Entries { get; }

    public string StatusSummary
    {
        get
        {
            var applied = Entries.Count(e => e.IsApplied);
            return applied == Entries.Count ? "All applied" : $"{applied}/{Entries.Count} applied";
        }
    }

    public bool IsFullyApplied => Entries.All(e => e.IsApplied);

    public TweakModuleViewModel(string name, string displayName, IReadOnlyList<RegistryEntryViewModel> entries)
    {
        Name = name;
        DisplayName = displayName;
        Entries = entries;
    }
}

public sealed class RegistryEntryViewModel
{
    public string Key { get; }
    public string ValueName { get; }
    public object DesiredValue { get; }
    public RegistryValueType Kind { get; }
    public object? CurrentValue { get; }
    public bool IsApplied { get; }

    public string StatusDisplay => IsApplied ? "Applied" : CurrentValue is null ? "Not set" : "Different";

    public string DesiredDisplay => $"{DesiredValue} ({Kind})";
    public string CurrentDisplay => CurrentValue?.ToString() ?? "(not set)";

    public RegistryEntryViewModel(string key, string valueName, object desiredValue, RegistryValueType kind, object? currentValue, bool isApplied)
    {
        Key = key;
        ValueName = valueName;
        DesiredValue = desiredValue;
        Kind = kind;
        CurrentValue = currentValue;
        IsApplied = isApplied;
    }
}
