namespace ServiceLib.Base;

public interface ICloseable
{
    public event EventHandler? RequestClose;
}
