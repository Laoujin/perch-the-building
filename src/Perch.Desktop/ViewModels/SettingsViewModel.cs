using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Core.Config;

using Wpf.Ui.Appearance;

namespace Perch.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsProvider _settingsProvider;

    [ObservableProperty]
    private string _configRepoPath = string.Empty;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    public SettingsViewModel(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;

        var assembly = typeof(SettingsViewModel).Assembly;
        var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AppVersion = infoVersion?.Split('+')[0] ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.LoadAsync(cancellationToken);
        ConfigRepoPath = settings.ConfigRepoPath ?? string.Empty;

        IsDarkTheme = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
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

    partial void OnIsDarkThemeChanged(bool value)
    {
        var theme = value ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme);
    }
}
