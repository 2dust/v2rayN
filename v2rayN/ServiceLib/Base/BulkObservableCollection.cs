namespace ServiceLib.Base;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public BulkObservableCollection()
    {
    }

    public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }
    public BulkObservableCollection(List<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnPropertyChanged(e);
        }
    }

    public void AddRange(IEnumerable<T>? collection)
    {
        if (collection == null)
        {
            return;
        }

        _suppressNotification = true;
        try
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public bool Replace(T oldItem, T newItem)
    {
        var index = Items.IndexOf(oldItem);
        if (index < 0)
        {
            return false;
        }

        Items[index] = newItem;

        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Replace,
            newItem,
            oldItem,
            index));

        return true;
    }

    public void ReplaceRange(IEnumerable<T>? collection)
    {
        if (collection == null)
        {
            return;
        }

        _suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in collection)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
