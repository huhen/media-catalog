using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using MediaCatalog.Services;

namespace MediaCatalog.ViewModels;

public class AuthMethodSelectionViewModel
{
    public IEnumerable<AuthMethodItemViewModel> Methods { get; }

    public AuthMethodSelectionViewModel(IEnumerable<YandexAuthMethod> methods)
    {
        Methods = methods.Select(m => new AuthMethodItemViewModel(m)).ToList();
    }
}

public class AuthMethodItemViewModel
{
    public YandexAuthMethod Method { get; }
    public string DisplayName => Method.GetDisplayName();
    public StreamGeometry Icon => Method.GetIcon();

    public AuthMethodItemViewModel(YandexAuthMethod method)
    {
        Method = method;
    }
}
