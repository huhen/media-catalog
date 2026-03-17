using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaCatalog.ViewModels;

public partial class UsernameInputViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _userName = "";
}
