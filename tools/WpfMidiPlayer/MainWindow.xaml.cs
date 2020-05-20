using System.Windows;

namespace WpfMidiPlayer
{
  /// <summary>
  ///   Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
      Closed += (sender, args) =>
      {
        var vm = DataContext as MainWindowViewModel;
        vm?.Dispose();
      };
    }
  }
}