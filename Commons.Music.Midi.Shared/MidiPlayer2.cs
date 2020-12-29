using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Commons.Music.Midi
{
	/// <summary>
	/// 
	/// </summary>
  public interface IMidiPlayer
  {
    Task Start();
    Task Stop();
    Task Pause();
    Task Seek(int ticks);
  }

  /// <summary>
	/// Provides asynchronous player control
	/// </summary>
	public class MidiPlayer2 : IDisposable
	{
		public MidiPlayer2 (MidiMusic music)
			: this (music, MidiAccessManager.Empty)
		{
		}

		public MidiPlayer2 (MidiMusic music, IMidiAccess access)
			: this (music, access, new SimpleAdjustingMidiPlayerTimeManager ())
		{
		}

		public MidiPlayer2 (MidiMusic music, IMidiOutput output)
			: this (music, output, new SimpleAdjustingMidiPlayerTimeManager ())
		{
		}
		
		public MidiPlayer2 (MidiMusic music, IMidiPlayerTimeManager timeManager)
			: this (music, MidiAccessManager.Empty, timeManager)
		{
		}
		
		public MidiPlayer2 (MidiMusic music, IMidiAccess access, IMidiPlayerTimeManager timeManager)
			: this (music, access.OpenOutputAsync (access.Outputs.First ().Id).Result, timeManager)
		{
			should_dispose_output = true;
		}
		
		public MidiPlayer2 (MidiMusic music, IMidiOutput output, IMidiPlayerTimeManager timeManager)
		{
      if (timeManager == null)
				throw new ArgumentNullException (nameof(timeManager));

			this.music = music ?? throw new ArgumentNullException (nameof(music));
			this.output = output ?? throw new ArgumentNullException (nameof(output));

			messages = SmfTrackMerger.Merge (music).Tracks [0].Messages;
			player = new MidiEventLooper2 (messages, timeManager, music.DeltaTimeSpec);
			EventReceived += OnEventReceived;
      
      player.Exception += OnException;
		}

    private void OnException(object sender, Exception e)
    {
      Exception?.Invoke(this, e);
    }

    private void OnEventReceived(MidiEvent m)
    {
      switch (m.EventType)
      {
        case MidiEvent.NoteOn:
        case MidiEvent.NoteOff:
          if (channel_mask != null && channel_mask[m.Channel])
            return;
          goto default;
        case MidiEvent.SysEx1:
        case MidiEvent.SysEx2:
          {
            var buffer = new byte[m.ExtraDataLength + 1];
            buffer[0] = m.StatusByte;
            Array.Copy(m.ExtraData, m.ExtraDataOffset, buffer, 1, m.ExtraDataLength);
            output.Send(buffer, 0, m.ExtraDataLength + 1, 0);
          }
          break;
        case MidiEvent.Meta:
          // do nothing.
          break;
        default:
          {
            var size = MidiEvent.FixedDataSize(m.StatusByte);
            var buffer = new byte[3];
            buffer[0] = m.StatusByte;
            buffer[1] = m.Msb;
            buffer[2] = m.Lsb;
            output.Send(buffer, 0, size + 1, 0);
          }
          break;
      }
    }


    readonly MidiEventLooper2 player;
    readonly IMidiOutput output;
    readonly IList<MidiMessage> messages;
    readonly MidiMusic music;

		bool should_dispose_output;
		bool [] channel_mask;

		public event Action Finished {
			add => player.Finished += value;
      remove => player.Finished -= value;
    }

		public event Action PlaybackCompletedToEnd {
			add => player.PlaybackCompletedToEnd += value;
      remove => player.PlaybackCompletedToEnd -= value;
    }

		public PlayerState State => player.State;

    public double TempoChangeRatio {
			get => player.TempoRatio;
			set => player.TempoRatio = value;
		}

		public int Tempo => player.CurrentTempo;

    public int Bpm => (int) (60.0 / Tempo * 1000000.0);

    // You can break the data at your own risk but I take performance precedence.
		public byte [] TimeSignature => player.CurrentTimeSignature;

    public int PlayDeltaTime => player.PlayDeltaTime;

    public TimeSpan PositionInTime => TimeSpan.FromMilliseconds (music.GetTimePositionInMillisecondsForTick (PlayDeltaTime));

		public int GetTotalPlayTimeMilliseconds ()
		{
			return MidiMusic.GetTotalPlayTimeMilliseconds (messages, music.DeltaTimeSpec);
		}

		public event MidiEventAction EventReceived {
			add => player.EventReceived += value;
      remove => player.EventReceived -= value;
    }

    public event EventHandler<Exception> Exception;
    
		public virtual void Dispose ()
		{
			player.Stop ();
			if (should_dispose_output)
				output.Dispose ();
		}

		[Obsolete ("Its naming is misleading. It starts playing asynchronously, but won't return any results unlike typical async API. Use new Play() method instead")]
		public void PlayAsync () => Play ();

		public void Play ()
    {
      if (State != PlayerState.Playing) 
        player.Play();
    }

		[Obsolete ("Its naming is misleading. It starts playing asynchronously, but won't return any results unlike typical async API. Use new Pause() method instead")]
		public void PauseAsync () => Pause ();

		public void Pause ()
    {
      player.Pause();
    }

		public void Stop ()
		{
      player.Stop();
		}

		[Obsolete ("Its naming is misleading. It starts seeking asynchronously, but won't return any results unlike typical async API. Use new Seek() method instead")]
		public void SeekAsync (int ticks) => Seek (ticks);
		
		public void Seek (int ticks)
		{
			player.Seek (null, ticks);
		}

		public void SetChannelMask (bool [] channelMask)
		{
			if (channelMask != null && channelMask.Length != 16)
				throw new ArgumentException ("Unexpected length of channelMask array; it must be an array of 16 elements.");
			channel_mask = channelMask;
      if (channelMask == null)
        return;
			// additionally send all sound off for the muted channels.
			for (var ch = 0; ch < channelMask.Length; ch++)
				if (channelMask [ch])
					output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.AllSoundOff, 0}, 0, 3, 0);
		}
	}
}

