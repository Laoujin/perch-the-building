using System.Collections.ObjectModel;

namespace Perch.Desktop.Models;

public sealed class AppCategoryGroup
{
    public string Category { get; }
    public ObservableCollection<AppCardModel> Apps { get; }

    public AppCategoryGroup(string category, ObservableCollection<AppCardModel> apps)
    {
        Category = category;
        Apps = apps;
    }
}
