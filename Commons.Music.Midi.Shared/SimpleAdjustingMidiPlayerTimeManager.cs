using System;
using System.Threading.Tasks;

namespace Commons.Music.Midi
{
  public class SimpleAdjustingMidiPlayerTimeManager : IMidiPlayerTimeManager
  {
    private DateTime last_started;
    private long nominal_total_mills;

    public void WaitBy(int addedMilliseconds)
    {
      if (addedMilliseconds <= 0)
        return;

      long delta = addedMilliseconds;
      var now = DateTime.Now;
      if (last_started != default)
      {
        var actualTotalMills = (long)(now - last_started).TotalMilliseconds;
        delta -= actualTotalMills - nominal_total_mills;
      }
      else
      {
        last_started = now;
      }

      if (delta > 0)
        Task.Delay((int)delta).Wait();

      nominal_total_mills += addedMilliseconds;
    }
  }
}
