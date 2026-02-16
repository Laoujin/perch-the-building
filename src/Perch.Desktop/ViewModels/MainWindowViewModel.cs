using CommunityToolkit.Mvvm.ComponentModel;

using Wpf.Ui;

namespace Perch.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _applicationTitle = "Perch";

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }
}
