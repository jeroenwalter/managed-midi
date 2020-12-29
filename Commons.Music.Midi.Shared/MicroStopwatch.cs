using System;
using System.Diagnostics;

namespace Commons.Music.Midi
{
  /// <summary>
  ///   MicroStopwatch class
  /// </summary>
  /// <see cref="https://www.codeproject.com/Articles/98346/Microsecond-and-Millisecond-NET-Timer" />
  public class MicroStopwatch : Stopwatch
  {
    private readonly double _microSecPerTick = 1000000D / Frequency;

    public MicroStopwatch()
    {
      if (!IsHighResolution)
        throw new Exception("On this system the high-resolution " +
                            "performance counter is not available");
    }

    public long ElapsedMicroseconds => (long)(ElapsedTicks * _microSecPerTick);
  }
}