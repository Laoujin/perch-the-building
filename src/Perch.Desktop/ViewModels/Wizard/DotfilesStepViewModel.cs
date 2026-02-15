using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Scanner;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class DotfilesStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    public ObservableCollection<DotfileItemViewModel> Items { get; } = [];

    public override string Title => "Dotfiles";
    public override int StepNumber => 5;

    public DotfilesStepViewModel(WizardState state)
    {
        _state = state;
    }

    public void LoadFromScanResult()
    {
        Items.Clear();
        if (_state.ScanResult == null)
        {
            return;
        }

        var preferredGroups = ProfileDefaults.GetDotfileGroupsFor(_state.SelectedProfiles);

        foreach (var dotfile in _state.ScanResult.Dotfiles)
        {
            bool preChecked = preferredGroups.Contains(dotfile.Group);
            var item = new DotfileItemViewModel(dotfile, preChecked);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DotfileItemViewModel.IsSelected))
                {
                    UpdateState();
                }
            };
            Items.Add(item);
        }

        UpdateState();
    }

    private void UpdateState()
    {
        _state.SelectedDotfiles = Items
            .Where(i => i.IsSelected)
            .Select(i => i.Dotfile.FullPath)
            .ToImmutableHashSet();
    }
}

public sealed partial class DotfileItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected;

    public DetectedDotfile Dotfile { get; }

    public DotfileItemViewModel(DetectedDotfile dotfile, bool isSelected)
    {
        Dotfile = dotfile;
        _isSelected = isSelected;
    }
}
