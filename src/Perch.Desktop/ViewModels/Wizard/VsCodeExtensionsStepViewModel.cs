using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Scanner;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class VsCodeExtensionsStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _vsCodeDetected;

    public ObservableCollection<ExtensionItemViewModel> InstalledExtensions { get; } = [];

    public override string Title => "VS Code Extensions";
    public override int StepNumber => 8;

    public VsCodeExtensionsStepViewModel(WizardState state)
    {
        _state = state;
    }

    public void LoadFromScanResult()
    {
        InstalledExtensions.Clear();
        if (_state.ScanResult == null)
        {
            return;
        }

        VsCodeDetected = _state.ScanResult.VsCodeDetected;
        if (!VsCodeDetected)
        {
            return;
        }

        foreach (var ext in _state.ScanResult.VsCodeExtensions)
        {
            var item = new ExtensionItemViewModel(ext, isSelected: true);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExtensionItemViewModel.IsSelected))
                {
                    UpdateState();
                }
            };
            InstalledExtensions.Add(item);
        }

        UpdateState();
    }

    private void UpdateState()
    {
        _state.ExtensionsToSync = InstalledExtensions
            .Where(e => e.IsSelected)
            .Select(e => e.Extension.Id)
            .ToImmutableHashSet();
    }
}

public sealed partial class ExtensionItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected;

    public DetectedVsCodeExtension Extension { get; }

    public ExtensionItemViewModel(DetectedVsCodeExtension extension, bool isSelected)
    {
        Extension = extension;
        _isSelected = isSelected;
    }
}
