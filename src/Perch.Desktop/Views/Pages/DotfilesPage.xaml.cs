using System.Windows.Controls;

using Perch.Desktop.ViewModels;

namespace Perch.Desktop.Views.Pages;

public partial class DotfilesPage : Page
{
    public DotfilesViewModel ViewModel { get; }

    public DotfilesPage(DotfilesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
