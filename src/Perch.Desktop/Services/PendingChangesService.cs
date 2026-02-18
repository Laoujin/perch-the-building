using System.Collections.ObjectModel;
using System.Collections.Specialized;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Perch.Desktop.Services;

public sealed partial class PendingChangesService : ObservableObject, IPendingChangesService
{
    private readonly ObservableCollection<PendingChange> _changes = [];
    private readonly ReadOnlyObservableCollection<PendingChange> _readOnlyChanges;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _hasChanges;

    public ReadOnlyObservableCollection<PendingChange> Changes => _readOnlyChanges;

    public PendingChangesService()
    {
        _readOnlyChanges = new(_changes);
        _changes.CollectionChanged += OnCollectionChanged;
    }

    public void Add(PendingChange change)
    {
        for (int i = _changes.Count - 1; i >= 0; i--)
        {
            var existing = _changes[i];
            if (existing.Id == change.Id && existing.Kind == change.Kind)
            {
                _changes.RemoveAt(i);
                break;
            }
        }

        _changes.Add(change);
    }

    public void Remove(string id, PendingChangeKind kind)
    {
        for (int i = _changes.Count - 1; i >= 0; i--)
        {
            if (_changes[i].Id == id && _changes[i].Kind == kind)
            {
                _changes.RemoveAt(i);
                return;
            }
        }
    }

    public bool Contains(string id, PendingChangeKind kind)
    {
        for (int i = 0; i < _changes.Count; i++)
        {
            if (_changes[i].Id == id && _changes[i].Kind == kind)
                return true;
        }

        return false;
    }

    public void Clear() => _changes.Clear();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Count = _changes.Count;
        HasChanges = _changes.Count > 0;
    }
}
