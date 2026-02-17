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

    public ObservableCollection<DotfileCardModel> Dotfiles { get; } = [];

    public DotfilesViewModel(IGalleryDetectionService detectionService)
    {
        _detectionService = detectionService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

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
