using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Scanner;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class SystemScanStepViewModel : WizardStepViewModel
{
    private readonly WizardState _state;
    private Task? _scanTask;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _scanComplete;

    [ObservableProperty]
    private string _statusText = "Scanning...";

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

    public SystemScanStepViewModel(WizardState state)
    {
        _state = state;
    }

    public void BeginScan(ISystemScanner scanner, CancellationToken cancellationToken = default)
    {
        if (_scanTask != null)
        {
            return;
        }

        IsScanning = true;
        ScanComplete = false;

        var progress = new Progress<string>(message =>
        {
            StatusText = message;
            ScanLog.Add(message);
        });

        _scanTask = RunScanCoreAsync(scanner, progress, cancellationToken);
    }

    public async Task WaitForScanAsync()
    {
        if (_scanTask != null)
        {
            await _scanTask.ConfigureAwait(false);
        }
    }

    private async Task RunScanCoreAsync(ISystemScanner scanner, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var result = await scanner.ScanAsync(progress, cancellationToken).ConfigureAwait(false);
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
