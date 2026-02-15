using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Scanner;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class SystemScanStepViewModel : WizardStepViewModel
{
    private readonly ISystemScanner _scanner;
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _scanComplete;

    [ObservableProperty]
    private string _statusText = "Ready to scan...";

    [ObservableProperty]
    private int _appCount;

    [ObservableProperty]
    private int _dotfileCount;

    [ObservableProperty]
    private int _fontCount;

    [ObservableProperty]
    private int _extensionCount;

    public ObservableCollection<string> ScanLog { get; } = [];

    public override string Title => "System Scan";
    public override int StepNumber => 3;
    public override bool CanSkip => true;

    public SystemScanStepViewModel(ISystemScanner scanner, WizardState state)
    {
        _scanner = scanner;
        _state = state;
    }

    public async Task RunScanAsync(CancellationToken cancellationToken = default)
    {
        IsScanning = true;
        ScanComplete = false;
        ScanLog.Clear();

        var progress = new Progress<string>(message =>
        {
            StatusText = message;
            ScanLog.Add(message);
        });

        var result = await _scanner.ScanAsync(progress, cancellationToken).ConfigureAwait(false);
        _state.ScanResult = result;

        AppCount = result.InstalledPackages.Length;
        DotfileCount = result.Dotfiles.Length;
        FontCount = result.InstalledFonts.Length;
        ExtensionCount = result.VsCodeExtensions.Length;

        StatusText = $"Found {AppCount} apps, {DotfileCount} dotfiles, {FontCount} fonts, {ExtensionCount} extensions";
        IsScanning = false;
        ScanComplete = true;
    }
}
