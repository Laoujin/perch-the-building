using System.Collections.ObjectModel;

namespace Perch.Desktop.Models;

public sealed class AppCategoryGroup
{
    public string SubCategory { get; }
    public ObservableCollection<AppCardModel> Apps { get; }

    public AppCategoryGroup(string subCategory, ObservableCollection<AppCardModel> apps)
    {
        SubCategory = subCategory;
        Apps = apps;
    }
}
