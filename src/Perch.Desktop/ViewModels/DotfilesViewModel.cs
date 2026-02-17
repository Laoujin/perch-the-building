using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Perch.Desktop.Models;
using Perch.Desktop.Services;

namespace Perch.Desktop.ViewModels;

public sealed partial class DotfilesViewModel : ViewModelBase
{
    private readonly IGalleryDetectionService _detectionService;
    private readonly IDotfileDetailService _detailService;

    private ImmutableArray<DotfileCardModel> _allDotfiles = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private DotfileCardModel? _selectedDotfile;

    [ObservableProperty]
    private DotfileDetail? _detail;

    [ObservableProperty]
    private bool _isLoadingDetail;

    [ObservableProperty]
    private bool _showRawEditor;

    public bool ShowCardGrid => SelectedDotfile is null;
    public bool ShowDetailView => SelectedDotfile is not null;
    public bool HasModule => Detail?.OwningModule is not null;
    public bool HasNoModule => Detail is not null && Detail.OwningModule is null;
    public bool ShowStructuredView => HasModule && !ShowRawEditor;
    public bool ShowEditorView => HasModule && ShowRawEditor;

    public ObservableCollection<DotfileCardModel> Dotfiles { get; } = [];

    public DotfilesViewModel(IGalleryDetectionService detectionService, IDotfileDetailService detailService)
    {
        _detectionService = detectionService;
        _detailService = detailService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedDotfileChanged(DotfileCardModel? value)
    {
        OnPropertyChanged(nameof(ShowCardGrid));
        OnPropertyChanged(nameof(ShowDetailView));
    }

    partial void OnDetailChanged(DotfileDetail? value)
    {
        OnPropertyChanged(nameof(HasModule));
        OnPropertyChanged(nameof(HasNoModule));
        OnPropertyChanged(nameof(ShowStructuredView));
        OnPropertyChanged(nameof(ShowEditorView));
    }

    partial void OnShowRawEditorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStructuredView));
        OnPropertyChanged(nameof(ShowEditorView));
    }

    [RelayCommand]
    private async Task ConfigureAsync(DotfileCardModel card, CancellationToken cancellationToken)
    {
        SelectedDotfile = card;
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
    private void BackToGrid()
    {
        SelectedDotfile = null;
        Detail = null;
        ShowRawEditor = false;
    }

    [RelayCommand]
    private void ToggleEditor() => ShowRawEditor = !ShowRawEditor;

    [RelayCommand]
    private void AddDroppedFiles(string[] files)
    {
        Trace.TraceInformation("{0} file(s) queued for linking", files.Length);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            _allDotfiles = await _detectionService.DetectDotfilesAsync(cancellationToken);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            return;
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
        UpdateSelectedCount();
    }

    public void UpdateSelectedCount()
    {
        SelectedCount = _allDotfiles.Count(d => d.IsSelected);
    }

    public void ClearSelection()
    {
        foreach (var df in _allDotfiles)
            df.IsSelected = false;
        SelectedCount = 0;
    }
}
