namespace ServiceLib.Base;

public interface IWindowDialog
{
    public Task<bool> ShowDialogAsync<TViewModel>(TViewModel vm)
        where TViewModel : class;
}
