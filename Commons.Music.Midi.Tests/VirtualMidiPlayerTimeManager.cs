using System;
using System.Threading;

namespace Commons.Music.Midi.Tests
{
  public sealed class VirtualMidiPlayerTimeManager : IMidiPlayerTimeManager, IDisposable
  {
    ManualResetEvent wait_handle = new ManualResetEvent (false);
    long total_waited_milliseconds, total_proceeded_milliseconds;
    bool should_terminate, disposed;

    public void Dispose ()
    {
      Abort ();
    }

    public void Abort ()
    {
      if (disposed)
        return;
      should_terminate = true;
      wait_handle.Set ();
      wait_handle.Dispose ();
      disposed = true;
    }
		
    public void WaitBy (int addedMilliseconds)
    {
      while (!should_terminate && total_waited_milliseconds + addedMilliseconds > total_proceeded_milliseconds) {
        wait_handle.WaitOne ();
        wait_handle.Reset();
      }
      total_waited_milliseconds += addedMilliseconds;
    }

    private void ProceedBy (int addedMilliseconds)
    {
      if (addedMilliseconds < 0)
        throw new ArgumentOutOfRangeException (nameof(addedMilliseconds),
          "Argument must be non-negative integer");
      total_proceeded_milliseconds += addedMilliseconds;
      wait_handle.Set ();
    }

    public bool ProceedByAndWait(int addedMilliseconds, int timeOutMilliseconds = 100)
    {
      ProceedBy(addedMilliseconds);
      return SpinWait.SpinUntil(() => !wait_handle.WaitOne(0), timeOutMilliseconds);
    }
  }
}