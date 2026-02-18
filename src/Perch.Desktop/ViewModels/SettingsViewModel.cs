using System.Reflection;
using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;
using Perch.Desktop.Views.Pages;

using Wpf.Ui;

namespace Perch.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _configRepoPath = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private bool _isDev;

    [ObservableProperty]
    private string _currentWorkBranch = string.Empty;

    [ObservableProperty]
    private string _currentWorkIssue = string.Empty;

    [ObservableProperty]
    private bool _showCurrentWork;

    public SettingsViewModel(ISettingsProvider settingsProvider, INavigationService navigationService)
    {
        _settingsProvider = settingsProvider;
        _navigationService = navigationService;

        var assembly = typeof(SettingsViewModel).Assembly;
        var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AppVersion = infoVersion?.Split('+')[0] ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        ConfigRepoPath = settings.ConfigRepoPath ?? string.Empty;
        IsDev = settings.Dev;

        if (IsDev && App.DevBranch is { } branch)
        {
            CurrentWorkBranch = branch;
            var match = Regex.Match(branch, @"^issue-(\d+)-(.+)$");
            CurrentWorkIssue = match.Success
                ? $"#{match.Groups[1].Value} -- {match.Groups[2].Value}"
                : branch;
        }

        UpdateShowCurrentWork();
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var settings = new PerchSettings
            {
                ConfigRepoPath = string.IsNullOrWhiteSpace(ConfigRepoPath) ? null : ConfigRepoPath.Trim(),
                Dev = IsDev,
            };

            await _settingsProvider.SaveAsync(settings, cancellationToken);
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void ReloadConfiguration()
    {
        _navigationService.Navigate(typeof(DashboardPage));
    }

    partial void OnIsDevChanged(bool value) => UpdateShowCurrentWork();

    private void UpdateShowCurrentWork() =>
        ShowCurrentWork = IsDev && !string.IsNullOrEmpty(CurrentWorkBranch);

    [RelayCommand]
    private void BrowseConfigRepo()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Config Repository",
            InitialDirectory = string.IsNullOrWhiteSpace(ConfigRepoPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : ConfigRepoPath,
        };

        if (dialog.ShowDialog() == true)
        {
            ConfigRepoPath = dialog.FolderName;
        }
    }

}
