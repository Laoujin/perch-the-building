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
    private readonly IDotfileDetailService _detailService;

    private ImmutableArray<DotfileGroupCardModel> _allDotfiles = [];

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
    private DotfileGroupCardModel? _selectedDotfile;

    [ObservableProperty]
    private DotfileDetail? _detail;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public bool ShowCardGrid => SelectedDotfile is null;
    public bool ShowDetailView => SelectedDotfile is not null;
    public bool HasModule => Detail?.OwningModule is not null;
    public bool HasNoModule => Detail is not null && Detail.OwningModule is null;
    public ObservableCollection<DotfileGroupCardModel> Dotfiles { get; } = [];

    public DotfilesViewModel(IGalleryDetectionService detectionService, IDotfileDetailService detailService)
    {
        _detectionService = detectionService;
        _detailService = detailService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedDotfileChanged(DotfileGroupCardModel? value)
    {
        OnPropertyChanged(nameof(ShowCardGrid));
        OnPropertyChanged(nameof(ShowDetailView));
    }

    partial void OnDetailChanged(DotfileDetail? value)
    {
        OnPropertyChanged(nameof(HasModule));
        OnPropertyChanged(nameof(HasNoModule));
    }

    [RelayCommand]
    private async Task ConfigureAsync(DotfileGroupCardModel card, CancellationToken cancellationToken)
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
