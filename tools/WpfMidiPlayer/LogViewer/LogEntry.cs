using System;
using WpfMidiPlayer.Mvvm;

namespace WpfMidiPlayer.LogViewer
{
  public class LogEntry : ObservableObject
  {
    public DateTime DateTime { get; set; }

    public int Index { get; set; }

    public string Message { get; set; }
  }
}