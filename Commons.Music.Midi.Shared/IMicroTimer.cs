using System;

namespace Commons.Music.Midi
{
  public interface IMicroTimer
  {
    long Interval { get; set; }
    long IgnoreEventIfLateBy { get; set; }
    bool Enabled { get; set; }
    event EventHandler<MicroTimerEventArgs> MicroTimerElapsed;
    void Start();
    void Stop();
    void StopAndWait();
    bool StopAndWait(int timeoutInMilliSec);
    void Abort();
  }
}