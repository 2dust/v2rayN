// LiteMainWindowViewModel.cs

using System;
using System.ComponentModel;

namespace v2rayN.ViewModels
{
    public class LiteMainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Properties, methods, and other members can be added here.

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}