using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Catalog;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class WindowsTweaksStepViewModel : WizardStepViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All Tweaks";

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All Tweaks"];

    public override string Title => "Windows Tweaks";
    public override int StepNumber => 9;

    public WindowsTweaksStepViewModel(ICatalogService catalogService, WizardState state)
    {
        _catalogService = catalogService;
        _state = state;
    }

    public async Task LoadTweaksAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        var tweaks = await _catalogService.GetAllTweaksAsync(cancellationToken).ConfigureAwait(false);
        var activeProfiles = ProfileDefaults.GetTweakProfilesFor(_state.SelectedProfiles);

        foreach (var tweak in tweaks.OrderBy(t => t.Priority))
        {
            bool preChecked = tweak.Profiles.Any(p => activeProfiles.Contains(p));
            var item = new TweakItemViewModel(tweak, preChecked);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TweakItemViewModel.IsSelected))
                {
                    UpdateState();
                }
            };
            Tweaks.Add(item);

            if (!Categories.Contains(tweak.Category))
            {
                Categories.Add(tweak.Category);
            }
        }

        UpdateState();
        IsLoading = false;
    }

    private void UpdateState()
    {
        _state.TweaksToApply = Tweaks
            .Where(t => t.IsSelected)
            .Select(t => t.Entry.Id)
            .ToImmutableHashSet();
    }
}

public sealed partial class TweakItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected;

    public TweakCatalogEntry Entry { get; }

    public TweakItemViewModel(TweakCatalogEntry entry, bool isSelected)
    {
        Entry = entry;
        _isSelected = isSelected;
    }
}
