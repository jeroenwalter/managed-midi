using System;
using System.Windows.Controls;

namespace WpfMidiPlayer.LogViewer
{
  /// <summary>
  ///   Interaction logic for LogViewer.xaml
  ///   Source: https://stackoverflow.com/questions/16743804/implementing-a-log-viewer-with-wpf
  /// </summary>
  public partial class LogViewer : UserControl
  {
    private bool _autoScroll = true;

    public LogViewer()
    {
      InitializeComponent();
    }

    private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
      if (!(e.Source is ScrollViewer scrollViewer))
        return;

      var tolerance = 0.001;

      if (Math.Abs(e.ExtentHeightChange) < tolerance)
      {
        // Content unchanged : user scroll event
        if (Math.Abs(scrollViewer.VerticalOffset - scrollViewer.ScrollableHeight) < tolerance)
          // Scroll bar is in bottom
          // Set autoscroll mode
          _autoScroll = true;
        else
          // Scroll bar isn't in bottom
          // Unset autoscroll mode
          _autoScroll = false;
      }

      // Content scroll event : autoscroll eventually
      if (_autoScroll && Math.Abs(e.ExtentHeightChange) > tolerance)
        // Content changed and autoscroll mode set
        // Autoscroll
        scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight);

      // User scroll event : set or unset autoscroll mode
    }
  }
}