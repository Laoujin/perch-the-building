using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Catalog;
using Perch.Core.Wizard;

namespace Perch.Desktop.ViewModels.Wizard;

public sealed partial class FontsStepViewModel : WizardStepViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly WizardState _state;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<FontItemViewModel> Fonts { get; } = [];

    public override string Title => "Fonts";
    public override int StepNumber => 7;

    public FontsStepViewModel(ICatalogService catalogService, WizardState state)
    {
        _catalogService = catalogService;
        _state = state;
    }

    public async Task LoadFontsAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        var catalogFonts = await _catalogService.GetAllFontsAsync(cancellationToken).ConfigureAwait(false);
        var installedFontNames = _state.ScanResult?.InstalledFonts
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        foreach (var font in catalogFonts)
        {
            bool isInstalled = installedFontNames.Any(n =>
                n.Contains(font.Name, StringComparison.OrdinalIgnoreCase));
            var item = new FontItemViewModel(font, isInstalled);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FontItemViewModel.WillInstall))
                {
                    UpdateState();
                }
            };
            Fonts.Add(item);
        }

        IsLoading = false;
    }

    private void UpdateState()
    {
        _state.FontsToInstall = Fonts
            .Where(f => f.WillInstall)
            .Select(f => f.Entry.Id)
            .ToImmutableHashSet();
    }
}

public sealed partial class FontItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _willInstall;

    public FontCatalogEntry Entry { get; }
    public bool IsInstalled { get; }

    public FontItemViewModel(FontCatalogEntry entry, bool isInstalled)
    {
        Entry = entry;
        IsInstalled = isInstalled;
        _willInstall = isInstalled;
    }
}
