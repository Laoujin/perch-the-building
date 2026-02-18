using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Perch.Desktop.Models;

public partial class FontCardModel : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string? FamilyName { get; }
    public string? Description { get; }
    public string? PreviewText { get; }
    public string? FullPath { get; }
    public FontCardSource Source { get; }
    public ImmutableArray<string> Tags { get; }

    [ObservableProperty]
    private CardStatus _status;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isBackedUp;

    [ObservableProperty]
    private string _sampleText = "The quick brown fox jumps over the lazy dog";

    public string? FileName => FullPath is not null ? Path.GetFileName(FullPath) : null;

    private string? BackupPath => FullPath is not null ? FullPath + ".backup" : null;

    public FontCardModel(
        string id,
        string name,
        string? familyName,
        string? description,
        string? previewText,
        string? fullPath,
        FontCardSource source,
        ImmutableArray<string> tags,
        CardStatus status)
    {
        Id = id;
        Name = name;
        FamilyName = familyName;
        Description = description;
        PreviewText = previewText;
        FullPath = fullPath;
        Source = source;
        Tags = tags;
        Status = status;
        IsBackedUp = BackupPath is not null && File.Exists(BackupPath);
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void Preview()
    {
        if (FullPath is null)
            return;

        Process.Start(new ProcessStartInfo(FullPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenLocation()
    {
        if (FullPath is null)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{FullPath}\"",
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private void ToggleBackup()
    {
        if (FullPath is null || BackupPath is null)
            return;

        if (IsBackedUp)
        {
            if (File.Exists(BackupPath) && !File.Exists(FullPath))
            {
                File.Move(BackupPath, FullPath);
                IsBackedUp = false;
            }
        }
        else
        {
            if (File.Exists(FullPath))
            {
                File.Move(FullPath, BackupPath);
                IsBackedUp = true;
            }
        }
    }
}
