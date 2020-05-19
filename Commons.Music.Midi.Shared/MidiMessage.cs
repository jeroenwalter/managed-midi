using System;

namespace Commons.Music.Midi
{
  public struct MidiMessage
  {
    public MidiMessage (int deltaTime, MidiEvent evt)
    {
      DeltaTime = deltaTime;
      Event = evt;
    }

    public readonly int DeltaTime;
    public readonly MidiEvent Event;

    public override string ToString ()
    {
      return String.Format ("[dt{0}]{1}", DeltaTime, Event);
    }
  }
}