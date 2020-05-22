using System;
using System.Threading;
using System.Threading.Tasks;

namespace Commons.Music.Midi.Tests
{
  public sealed class VirtualMidiPlayerTimeManager : IMidiPlayerTimeManager, IDisposable
  {
    ManualResetEventSlim wait_handle = new ManualResetEventSlim(false);
    long total_waited_milliseconds;
    long total_proceeded_milliseconds;
    bool should_terminate;
    bool disposed;


    /// <summary>
    /// Progresses the time with addedMilliseconds, then blocks until the token is cancelled
    /// or until timeOutMilliseconds have passed, whichever comes first.
    /// </summary>
    /// <param name="addedMilliseconds"></param>
    /// <param name="token"></param>
    /// <param name="timeOutMilliseconds"></param>
    public void ProceedByAndWait(int addedMilliseconds, CancellationToken token = default, int timeOutMilliseconds = 100)
    {
      try
      {
        ProceedBy(addedMilliseconds);

        if (token == default)
          Task.Delay(timeOutMilliseconds).Wait();
        else
          Task.Delay(timeOutMilliseconds, token).Wait();
      }
      catch (Exception)
      {
        // ignored
      }
    }

    public void Dispose()
    {
      if (disposed)
        return;

      Abort();
      
      // don't Dispose wait_handle, as this may generate an exception in the WaitBy method.
      // Instead let its finalizer do the dirty work, it's acceptable for wait handles and especially here in the unit test code we don't really care about leaks.
      
      disposed = true;
    }

    private void Abort()
    {
      should_terminate = true;
      wait_handle.Set();
    }

    // Don't expose this method to the unit tests.
    void IMidiPlayerTimeManager.WaitBy(int addedMilliseconds)
    {
      while (!should_terminate && total_waited_milliseconds + addedMilliseconds > total_proceeded_milliseconds)
      {
        wait_handle.Wait();
        wait_handle.Reset();
      }
      total_waited_milliseconds += addedMilliseconds;
    }

    private void ProceedBy(int addedMilliseconds)
    {
      if (addedMilliseconds < 0)
        throw new ArgumentOutOfRangeException(nameof(addedMilliseconds),
          "Argument must be non-negative integer");
      total_proceeded_milliseconds += addedMilliseconds;
      wait_handle.Set();
    }
  }
}