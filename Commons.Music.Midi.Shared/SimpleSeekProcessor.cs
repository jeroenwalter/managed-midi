namespace Commons.Music.Midi
{
  class SimpleSeekProcessor : ISeekProcessor
  {
    public SimpleSeekProcessor (int ticks)
    {
      this.seek_to = ticks;
    }

    private int seek_to, current;
    public SeekFilterResult FilterMessage (MidiMessage message)
    {
      current += message.DeltaTime;
      if (current >= seek_to)
        return SeekFilterResult.PassAndTerminate;
      switch (message.Event.EventType) {
        case MidiEvent.NoteOn:
        case MidiEvent.NoteOff:
          return SeekFilterResult.Block;
      }
      return SeekFilterResult.Pass;
    }
  }
}