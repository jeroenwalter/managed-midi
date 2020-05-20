using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WpfMidiPlayer.Properties;

namespace WpfMidiPlayer.Mvvm
{
  public class ObservableObject : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
      Application.Current.Dispatcher.BeginInvoke((Action) (() =>
      {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }));
    }
  }
}