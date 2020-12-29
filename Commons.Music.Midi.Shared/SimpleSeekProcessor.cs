namespace Commons.Music.Midi
{
  class SimpleSeekProcessor : ISeekProcessor
  {
    public SimpleSeekProcessor (int ticks)
    {
      SeekTo = ticks;
    }

    public int SeekTo { get; }

    private int _current;

    public SeekFilterResult FilterMessage (MidiMessage message)
    {
      _current += message.DeltaTime;
      if (_current >= SeekTo)
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