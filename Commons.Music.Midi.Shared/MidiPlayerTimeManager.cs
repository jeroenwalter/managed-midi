using System;
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
