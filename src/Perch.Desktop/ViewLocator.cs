using Avalonia.Controls;
using Avalonia.Controls.Templates;

using Perch.Desktop.ViewModels;

namespace Perch.Desktop;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data == null)
        {
            return null;
        }

        string name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
