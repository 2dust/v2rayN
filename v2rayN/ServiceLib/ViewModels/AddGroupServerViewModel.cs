using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class AddGroupServerViewModel : MyReactiveObject
{
    [Reactive]
    public ProfileItem SelectedSource { get; set; }

    [Reactive]
    public IList<ProfileItem> SelectedChildren { get; set; }

    //[Reactive]
    //public 
}
