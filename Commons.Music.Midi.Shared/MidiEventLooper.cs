using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Commons.Music.Midi
{
  internal sealed class MidiEventLooper : IDisposable
  {
    public MidiEventLooper (IList<MidiMessage> messages, IMidiPlayerTimeManager timeManager, int deltaTimeSpec)
    {
      if (deltaTimeSpec < 0)
        throw new NotSupportedException ("SMPTe-based delta time is not implemented in this player.");
			
      delta_time_spec = deltaTimeSpec;
      time_manager = timeManager ?? throw new ArgumentNullException(nameof(timeManager)); ;

      this.messages = messages ?? throw new ArgumentNullException (nameof(messages));
    }

    public event MidiEventAction EventReceived;
    public event Action Starting;
    public event Action Finished;
    public event Action PlaybackCompletedToEnd;
    public event EventHandler<Exception> Exception;

    public PlayerState State { get; private set; } = PlayerState.Stopped;
    public int CurrentTempo { get; private set; } = MidiMetaType.DefaultTempo;
    public int PlayDeltaTime { get; private set; }
    public double TempoRatio { get; set; } = 1.0;

    private readonly IMidiPlayerTimeManager time_manager;
    private readonly IList<MidiMessage> messages;
    private readonly int delta_time_spec;
    
    private readonly ManualResetEventSlim pause_handle = new ManualResetEventSlim (false);
    
    private bool do_pause; 
    private bool do_stop;
    private bool do_mute;
    private int event_idx;
    
    internal byte [] current_time_signature = new byte [4];
    
    private Task sync_player_task;
    private ISeekProcessor seek_processor;
    
    public void Dispose ()
    {
      if (State != PlayerState.Stopped)
        Stop ();
      // NB: Stop should wait here until the PlayLoop has ended!
      // Otherwise the midi output may be disposed while processing midi events.
    }

    public void Play ()
    {
      if (State == PlayerState.Stopped)
        StartLoop();

      pause_handle.Set ();
      State = PlayerState.Playing;
    }

    public void Pause ()
    {
      do_pause = true;
    }

    public void Stop()
    {
      if (State == PlayerState.Stopped)
        return;

      do_stop = true;
      pause_handle.Set();

      var stopped = sync_player_task.Wait(1000);
      if (stopped)
        return;

      Debug.WriteLine("PlayLoop did not stop on time.");
    }
    
    public void Seek(ISeekProcessor seekProcessor, int ticks)
    {
      seek_processor = seekProcessor ?? new SimpleSeekProcessor(ticks);
      event_idx = 0;
      PlayDeltaTime = ticks;
      do_mute = true;
    }

    private void Mute()
    {
      for (int i = 0; i < 16; i++)
        OnEvent(new MidiEvent((byte)(MidiEvent.CC + i), MidiCC.AllSoundOff, 0, null, 0, 0));
    }

    private void ResetControllersOnAllChannels()
    {
      for (var i = 0; i < 16; i++)
        OnEvent(new MidiEvent((byte) (MidiEvent.CC + i), MidiCC.ResetAllControllers, 0, null, 0, 0));
    }

    private void StartLoop()
    {
      if (sync_player_task?.Status == TaskStatus.Running)
        return;

      event_idx = 0;
      PlayDeltaTime = 0;

      sync_player_task = Task.Run(() =>
      {
        // As this task is not awaited, exceptions will not be handled, not even by the unhandled exception handler.
        // TaskScheduler.UnobservedTaskException event will be called instead.
        // This may cause the program to exit unexpectedly.
        // Therefore catch exceptions here.
        // Not only that, but the PlayerLoop emits EventReceived events and these may throw exceptions.
        // We'll want to handle those as well.

        try
        {
          PlayerLoop();
        }
        catch (Exception e)
        {
          Debug.WriteLine(e);

          // DO NOT emit EventReceived or other events, e.g via Mute().
          // Only the Exception event is allowed.

          Exception?.Invoke(this, e); // let's hope this one doesn't throw.
        }
      });
    }

    private void PlayerLoop ()
    {
      ResetControllersOnAllChannels();
      Mute();

      Starting?.Invoke ();
      
      while (!do_stop && event_idx != messages.Count) 
      {
        if (do_pause) 
        {
          Mute();
          State = PlayerState.Paused;

          pause_handle.Reset();
          pause_handle.Wait();
          do_pause = false;
          
          continue;
        }

        if (do_mute)
        {
          Mute();
          do_mute = false;
        }

        ProcessMessage (messages [event_idx++]);
      }

      do_stop = false;
      Mute();
      State = PlayerState.Stopped;

      if (event_idx == messages.Count)
        PlaybackCompletedToEnd?.Invoke ();

      Finished?.Invoke ();
    }

    private int GetContextDeltaTimeInMilliseconds (int deltaTime) => (int) (CurrentTempo / 1000.0 * deltaTime / delta_time_spec / TempoRatio);

    private void ProcessMessage (MidiMessage m)
    {
      if (seek_processor != null) {
        var result = seek_processor.FilterMessage (m);
        switch (result) {
          case SeekFilterResult.PassAndTerminate:
          case SeekFilterResult.BlockAndTerminate:
            seek_processor = null;
            break;
        }

        switch (result) {
          case SeekFilterResult.Block:
          case SeekFilterResult.BlockAndTerminate:
            return; // ignore this event
        }
      }
      else if (m.DeltaTime != 0) {
        var ms = GetContextDeltaTimeInMilliseconds (m.DeltaTime);
        time_manager.WaitBy (ms);
        PlayDeltaTime += m.DeltaTime;
      }
			
      if (m.Event.StatusByte == MidiEvent.Reset) {
        if (m.Event.Msb == MidiMetaType.Tempo)
          CurrentTempo = MidiMetaType.GetTempo (m.Event.ExtraData, m.Event.ExtraDataOffset);
        else if (m.Event.Msb == MidiMetaType.TimeSignature && m.Event.ExtraDataLength == 4)
          Array.Copy (m.Event.ExtraData, current_time_signature, 4);
      }

      OnEvent (m.Event);
    }

    private void OnEvent (MidiEvent m)
    {
      EventReceived?.Invoke (m);
    }
  }
}