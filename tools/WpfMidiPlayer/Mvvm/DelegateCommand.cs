using System;
using System.Windows.Input;
using WpfMidiPlayer.Properties;

namespace WpfMidiPlayer.Mvvm
{
  public class DelegateCommand : ICommand
  {
    private readonly Func<bool> _canExecute;
    private readonly Action _execute;

    public DelegateCommand([NotNull] Action execute)
      : this(execute, null)
    {
    }

    public DelegateCommand([NotNull] Action execute, Func<bool> canExecute)
    {
      _execute = execute ?? throw new ArgumentNullException(nameof(execute));
      _canExecute = canExecute ?? (() => true);
    }

    public bool CanExecute(object parameter)
    {
      return _canExecute.Invoke();
    }

    public void Execute(object parameter)
    {
      _execute.Invoke();
    }

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
      CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
  }
}