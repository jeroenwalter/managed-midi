using System;
using System.IO;
using Commons.Music.Midi;

namespace WpfMidiPlayer
{
  public interface IPlayer : IDisposable
  {
    PlayerState State { get; }

    /// <summary>
    ///   Changes the tempo of the song being played.
    ///   0.5 is half the tempo, 1.0 is normal tempo, 2.0 is twice the tempo.
    /// </summary>
    double TempoChangeRatio { get; set; }

    int TotalPlayTimeMilliseconds { get; }
    double PlaybackPositionPercentage { get; }

    event EventHandler<double> PlaybackPositionUpdated;
    event EventHandler Finished;

    /// <summary>
    ///   Loads the midi stream and starts playback.
    /// </summary>
    /// <param name="midiStream">Stream containing a SMF midi file</param>
    /// <see cref="https://www.midi.org/specifications-old/item/standard-midi-files-smf" />
    void Start(Stream midiStream);

    void Stop();

    /// <summary>
    ///   Pauses/Resumes playback.
    /// </summary>
    void Pause();

    /// <summary>
    /// </summary>
    /// <param name="playbackPositionPercentage">Percentage</param>
    void Seek(double playbackPositionPercentage);
  }
}