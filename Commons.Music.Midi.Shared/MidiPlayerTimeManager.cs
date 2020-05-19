using System;
using System.Threading;
using System.Threading.Tasks;

namespace Commons.Music.Midi
{
	/// <summary>
	/// Used by MidiPlayer to manage time progress.
	/// </summary>
	public interface IMidiPlayerTimeManager
	{
		void WaitBy (int addedMilliseconds);
	}
	
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

	public class SimpleAdjustingMidiPlayerTimeManager : IMidiPlayerTimeManager
	{
		DateTime last_started = default (DateTime);
		long nominal_total_mills = 0;

		public void WaitBy (int addedMilliseconds)
		{
			if (addedMilliseconds > 0) {
				long delta = addedMilliseconds;
				if (last_started != default (DateTime)) {
					var actualTotalMills = (long) (DateTime.Now - last_started).TotalMilliseconds;
					delta -= actualTotalMills - nominal_total_mills;
				} else {
					last_started = DateTime.Now;
				}
				if (delta > 0) {
					var t = Task.Delay ((int) delta);
					t.Wait ();
				}
				nominal_total_mills += addedMilliseconds;
			}
		}
	}
}
