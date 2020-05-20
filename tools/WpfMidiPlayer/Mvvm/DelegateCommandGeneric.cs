using System;
using System.Windows.Input;
using WpfMidiPlayer.Properties;

namespace WpfMidiPlayer.Mvvm
{
  public class DelegateCommand<T> : ICommand
  {
    private readonly Func<T, bool> _canExecute;
    private readonly Action<T> _execute;

    public DelegateCommand([NotNull] Action<T> execute)
      : this(execute, null)
    {
    }

    public DelegateCommand([NotNull] Action<T> execute, Func<T, bool> canExecute)
    {
      _execute = execute ?? throw new ArgumentNullException(nameof(execute));
      _canExecute = canExecute ?? (t => true);
    }

    public bool CanExecute(object parameter)
    {
      if (parameter is T instance)
        return _canExecute.Invoke(instance);

      return _canExecute.Invoke(default);
    }

    public void Execute(object parameter)
    {
      if (parameter is T instance)
        _execute.Invoke(instance);

      _execute.Invoke(default);
    }

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
      CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
  }
}