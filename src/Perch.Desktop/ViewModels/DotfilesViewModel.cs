using System.Collections.Immutable;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class DotfilesViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IAppDetailService _detailService;
    private readonly IPendingChangesService _pendingChanges;

    private ImmutableArray<AppCardModel> _allDotfiles = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private AppCardModel? _selectedApp;

    [ObservableProperty]
    private AppDetail? _detail;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public bool ShowCardGrid => SelectedApp is null;
    public bool ShowDetailView => SelectedApp is not null;
    public bool HasModule => Detail?.OwningModule is not null;
    public bool HasNoModule => Detail is not null && Detail.OwningModule is null;
    public bool HasFileStatuses => Detail is not null && !Detail.FileStatuses.IsDefaultOrEmpty;
    public bool HasAlternatives => Detail is not null && !Detail.Alternatives.IsDefaultOrEmpty;
    public ObservableCollection<AppCardModel> Dotfiles { get; } = [];

    public DotfilesViewModel(
        IGalleryDetectionService detectionService,
        IAppDetailService detailService,
        IPendingChangesService pendingChanges)
    {
        _detectionService = detectionService;
        _detailService = detailService;
        _pendingChanges = pendingChanges;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedAppChanged(AppCardModel? value)
    {
        OnPropertyChanged(nameof(ShowCardGrid));
        OnPropertyChanged(nameof(ShowDetailView));
    }

    partial void OnDetailChanged(AppDetail? value)
    {
        OnPropertyChanged(nameof(HasModule));
        OnPropertyChanged(nameof(HasNoModule));
        OnPropertyChanged(nameof(HasFileStatuses));
        OnPropertyChanged(nameof(HasAlternatives));
    }

    [RelayCommand]
    private async Task ConfigureAppAsync(AppCardModel card, CancellationToken cancellationToken)
    {
        SelectedApp = card;
        Detail = null;
        IsLoadingDetail = true;

        try
        {
            Detail = await _detailService.LoadDetailAsync(card, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsLoadingDetail = false;
        }
    }

    [RelayCommand]
    private void ToggleDotfile(AppCardModel app)
    {
        if (_pendingChanges.Contains(app.Id, PendingChangeKind.LinkDotfile))
            _pendingChanges.Remove(app.Id, PendingChangeKind.LinkDotfile);
        else if (!app.IsManaged)
            _pendingChanges.Add(new LinkDotfileChange(app));
    }

    [RelayCommand]
    private void BackToGrid()
    {
        SelectedApp = null;
        Detail = null;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _allDotfiles = await _detectionService.DetectDotfilesAsync(cancellationToken);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load dotfiles: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Dotfiles.Clear();
        foreach (var df in _allDotfiles)
        {
            if (df.MatchesSearch(SearchText))
                Dotfiles.Add(df);
        }

        LinkedCount = _allDotfiles.Count(d => d.Status == CardStatus.Linked);
        TotalCount = _allDotfiles.Length;
    }
}
