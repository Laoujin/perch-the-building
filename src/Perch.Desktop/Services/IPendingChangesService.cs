using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Perch.Desktop.Services;

public interface IPendingChangesService : INotifyPropertyChanged
{
    ReadOnlyObservableCollection<PendingChange> Changes { get; }
    int Count { get; }
    bool HasChanges { get; }
    void Add(PendingChange change);
    void Remove(string id, PendingChangeKind kind);
    bool Contains(string id, PendingChangeKind kind);
    void Clear();
}
