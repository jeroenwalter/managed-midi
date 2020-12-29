using System;
using System.Threading;

namespace Commons.Music.Midi
{
  /// <summary>
  ///   MicroTimer class
  /// </summary>
  /// <remarks>
  /// This timer uses Thread.SpinWait().
  /// This causes 100% cpu load on the cpu the this timer thread is running on.
  /// </remarks>
  /// <see cref="https://www.codeproject.com/Articles/98346/Microsecond-and-Millisecond-NET-Timer" />
  public class MicroTimer : IMicroTimer
  {
    private long _ignoreEventIfLateBy = long.MaxValue;
    private bool _stopTimer = true;

    private Thread _threadTimer;
    private long _timerIntervalInMicroSec;

    public MicroTimer()
    {
    }

    public MicroTimer(long timerIntervalInMicroseconds)
    {
      Interval = timerIntervalInMicroseconds;
    }

    public long Interval
    {
      get => Interlocked.Read(ref _timerIntervalInMicroSec);
      set => Interlocked.Exchange(ref _timerIntervalInMicroSec, value);
    }

    public long IgnoreEventIfLateBy
    {
      get => Interlocked.Read(ref _ignoreEventIfLateBy);
      set => Interlocked.Exchange(ref _ignoreEventIfLateBy, value <= 0 ? long.MaxValue : value);
    }

    public bool Enabled
    {
      get => _threadTimer != null && _threadTimer.IsAlive;
      set
      {
        if (value)
          Start();
        else
          Stop();
      }
    }

    public event EventHandler<MicroTimerEventArgs> MicroTimerElapsed;

    public void Start()
    {
      if (Enabled || Interval <= 0)
        return;

      _stopTimer = false;

      _threadTimer = new Thread(() => NotificationTimer(ref _timerIntervalInMicroSec, ref _ignoreEventIfLateBy, ref _stopTimer))
      {
        Priority = ThreadPriority.Highest,
        Name = "MicroTimer",
        IsBackground = true
      };
      _threadTimer.Start();
    }

    public void Stop()
    {
      _stopTimer = true;
    }

    public void StopAndWait()
    {
      StopAndWait(Timeout.Infinite);
    }

    public bool StopAndWait(int timeoutInMilliSec)
    {
      _stopTimer = true;

      if (!Enabled ||
          _threadTimer.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
        return true;

      return _threadTimer.Join(timeoutInMilliSec);
    }

    public void Abort()
    {
      _stopTimer = true;

      if (Enabled)
        _threadTimer.Abort();
    }

    private void NotificationTimer(ref long timerIntervalInMicroSec,
      ref long ignoreEventIfLateBy,
      ref bool stopTimer)
    {
      var timerCount = 0;
      long nextNotification = 0;

      var microStopwatch = new MicroStopwatch();
      microStopwatch.Start();

      while (!stopTimer)
      {
        var callbackFunctionExecutionTime =
          microStopwatch.ElapsedMicroseconds - nextNotification;

        var timerIntervalInMicroSecCurrent = Interlocked.Read(ref timerIntervalInMicroSec);
        var ignoreEventIfLateByCurrent = Interlocked.Read(ref ignoreEventIfLateBy);

        nextNotification += timerIntervalInMicroSecCurrent;
        timerCount++;
        long elapsedMicroseconds;

        while ((elapsedMicroseconds = microStopwatch.ElapsedMicroseconds)
               < nextNotification)
          Thread.SpinWait(10);
        
        var timerLateBy = elapsedMicroseconds - nextNotification;

        if (timerLateBy >= ignoreEventIfLateByCurrent)
          continue;

        var microTimerEventArgs = new MicroTimerEventArgs(timerCount,
          elapsedMicroseconds,
          timerLateBy,
          callbackFunctionExecutionTime);

        MicroTimerElapsed?.Invoke(this, microTimerEventArgs);
      }

      microStopwatch.Stop();
    }
  }
}