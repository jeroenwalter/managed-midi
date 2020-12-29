namespace Commons.Music.Midi
{
  public struct TimeSignature
  {
    /// <summary>
    /// The beats-per-measure(upper number) of the time signature.
    /// </summary>
    public byte Numerator;

    /// <summary>
    /// The type of beat (lower number) of the time signature.
    /// </summary>
    public byte Denominator;
    
    /// <summary>
    /// The number of "MIDI clocks" between metronome clicks.There are 24 MIDI clocks in one quarter note.
    /// </summary>
    public byte Clocks;

    /// <summary>
    /// The number of notated 32nds in 24 MIDI clocks. The default value is 8.
    /// </summary>
    public byte Notated32nds;
  }
}