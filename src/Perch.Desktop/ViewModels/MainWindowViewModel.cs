using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Desktop.Services;

using Wpf.Ui;

namespace Perch.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IPendingChangesService _pendingChanges;

    [ObservableProperty]
    private string _applicationTitle = "Perch";

    [ObservableProperty]
    private int _pendingChangeCount;

    [ObservableProperty]
    private bool _hasPendingChanges;

    public MainWindowViewModel(INavigationService navigationService, IPendingChangesService pendingChanges)
    {
        _navigationService = navigationService;
        _pendingChanges = pendingChanges;
        _pendingChanges.PropertyChanged += OnPendingChangesPropertyChanged;
    }

    private void OnPendingChangesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPendingChangesService.Count))
            PendingChangeCount = _pendingChanges.Count;
        else if (e.PropertyName == nameof(IPendingChangesService.HasChanges))
            HasPendingChanges = _pendingChanges.HasChanges;
    }
}
