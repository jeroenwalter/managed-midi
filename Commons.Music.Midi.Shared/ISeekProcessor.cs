namespace Commons.Music.Midi
{
  interface ISeekProcessor
  {
    SeekFilterResult FilterMessage (MidiMessage message);
    int SeekTo { get; }
  }
}