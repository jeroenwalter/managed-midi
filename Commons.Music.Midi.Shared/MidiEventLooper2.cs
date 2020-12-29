using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Commons.Music.Midi
{
  internal sealed class MidiEventLooper2 : IDisposable
  {
    private readonly int _deltaTimeSpec;
    private readonly IList<MidiMessage> _messages;

    private readonly ManualResetEventSlim _pauseHandle = new ManualResetEventSlim(false);
    private readonly AutoResetEvent _timerEvent = new AutoResetEvent(false);

    private readonly IMidiPlayerTimeManager _timeManager;
    private readonly MicroTimer _timer;

    internal byte[] CurrentTimeSignature = new byte[4];
    
    private bool _doPause;
    private bool _doStop;
    private int _eventIdx;
    private ISeekProcessor _seekProcessor;

    private Task _syncPlayerTask;

    public MidiEventLooper2(IList<MidiMessage> messages, IMidiPlayerTimeManager timeManager, int deltaTimeSpec)
    {
      if (deltaTimeSpec < 0)
        throw new NotSupportedException("SMPTe-based delta time is not implemented in this player.");

      _deltaTimeSpec = deltaTimeSpec;
      _timeManager = timeManager ?? throw new ArgumentNullException(nameof(timeManager));

      _messages = messages ?? throw new ArgumentNullException(nameof(messages));


      _timer = new MicroTimer(1000);
      _timer.MicroTimerElapsed += TimerOnMicroTimerElapsed;
    }

    private void TimerOnMicroTimerElapsed(object sender, MicroTimerEventArgs timerEventArgs)
    {
      _timerEvent.Set();
    }

    public PlayerState State { get; private set; } = PlayerState.Stopped;
    public int CurrentTempo { get; private set; } = MidiMetaType.DefaultTempo;
    public int PlayDeltaTime { get; private set; }
    public double TempoRatio { get; set; } = 1.0;

    public void Dispose()
    {
      if (State != PlayerState.Stopped)
        Stop();
      // NB: Stop should wait here until the PlayLoop has ended!
      // Otherwise the midi output may be disposed while processing midi events.
    }

    public event MidiEventAction EventReceived;
    public event Action Starting;
    public event Action Finished;
    public event Action PlaybackCompletedToEnd;
    public event EventHandler<Exception> Exception;

    public void Play()
    {
      if (State == PlayerState.Stopped)
        StartLoop();

      _pauseHandle.Set();
      State = PlayerState.Playing;
    }

    public void Pause()
    {
      if (State == PlayerState.Playing)
        _doPause = true;
    }

    public void Stop()
    {
      if (State == PlayerState.Stopped)
        return;

      _doStop = true;
      _pauseHandle.Set();

      var stopped = _syncPlayerTask.Wait(1000);
      if (stopped)
        return;

      Debug.WriteLine("PlayLoop did not stop on time.");
    }

    public void Seek(ISeekProcessor seekProcessor, int ticks)
    {
      _seekProcessor = seekProcessor ?? new SimpleSeekProcessor(ticks);
    }

    private MidiMessage SeekToTicks()
    {
      if (_seekProcessor == null)
        throw new InvalidOperationException("SeekToTicks called while _seekProcessor == null");

      _eventIdx = 0;
      PlayDeltaTime = _seekProcessor.SeekTo;

      Mute();
      var midiMessage = _messages[_eventIdx++];
      var seekProcessor = _seekProcessor;
      while (seekProcessor != null && _eventIdx != _messages.Count)
      {
        var result = seekProcessor.FilterMessage(midiMessage);
        switch (result)
        {
          case SeekFilterResult.PassAndTerminate:
          case SeekFilterResult.BlockAndTerminate:
            _seekProcessor = null;
            return midiMessage;

          case SeekFilterResult.Block:
            midiMessage = _messages[_eventIdx++];
            continue; // ignore this event
        }

        ProcessMidiMessage(midiMessage);
        midiMessage = _messages[_eventIdx++];
      }
      
      return midiMessage;
    }

    private void ProcessMidiMessage(MidiMessage midiMessage)
    {
      if (midiMessage.Event.StatusByte == MidiEvent.Reset)
      {
        switch (midiMessage.Event.Msb)
        {
          case MidiMetaType.Tempo:
            CurrentTempo = MidiMetaType.GetTempo(midiMessage.Event.ExtraData, midiMessage.Event.ExtraDataOffset);
            break;
          case MidiMetaType.TimeSignature when midiMessage.Event.ExtraDataLength == 4:
            Array.Copy(midiMessage.Event.ExtraData, CurrentTimeSignature, 4);
            break;
        }
      }

      OnEvent(midiMessage.Event);
    }

    private void Mute()
    {
      for (var i = 0; i < 16; i++)
        OnEvent(new MidiEvent((byte)(MidiEvent.CC + i), MidiCC.AllSoundOff, 0, null, 0, 0));
    }

    private void ResetControllersOnAllChannels()
    {
      for (var i = 0; i < 16; i++)
        OnEvent(new MidiEvent((byte)(MidiEvent.CC + i), MidiCC.ResetAllControllers, 0, null, 0, 0));
    }

    private void StartLoop()
    {
      if (_syncPlayerTask?.Status == TaskStatus.Running)
        return;

      _eventIdx = 0;
      PlayDeltaTime = 0;

      _syncPlayerTask = Task.Run(() =>
      {
        // As this task is not awaited, exceptions will not be handled, not even by the unhandled exception handler.
        // TaskScheduler.UnobservedTaskException event will be called instead.
        // This may cause the program to exit unexpectedly.
        // Therefore catch exceptions here.
        // Not only that, but the PlayerLoop emits EventReceived events and these may throw exceptions.
        // We'll want to handle those as well.

        try
        {
          _timer.Start();
          PlayerLoop();
          _timer.StopAndWait(100);
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

    private void PlayerLoop()
    {
      ResetControllersOnAllChannels();
      Mute();

      Starting?.Invoke();
      var midiMessage = _messages[_eventIdx++];
      var msToWait = 0;

      while (!_doStop && _eventIdx != _messages.Count)
      {
        _timerEvent.WaitOne();
        
        if (_doPause)
        {
          Mute();
          State = PlayerState.Paused;

          _pauseHandle.Reset();
          _pauseHandle.Wait();
          _doPause = false;

          continue;
        }

        if (_seekProcessor != null)
        {
          midiMessage = SeekToTicks();
          msToWait = 0;
        }
        else
        {
          if (TempoRatio <= 0.0)
            continue;

          if (msToWait > 0)
            msToWait--;
        }
        
        while (msToWait == 0 && _eventIdx != _messages.Count)
        {
          if (_seekProcessor != null)
            midiMessage = SeekToTicks();
          else
          {
            PlayDeltaTime += midiMessage.DeltaTime;
            ProcessMidiMessage(midiMessage);
            midiMessage = _messages[_eventIdx++];
          }
          
          msToWait = GetContextDeltaTimeInMilliseconds(midiMessage.DeltaTime);
        }
      }

      _doStop = false;
      Mute();
      State = PlayerState.Stopped;

      if (_eventIdx == _messages.Count)
        PlaybackCompletedToEnd?.Invoke();

      Finished?.Invoke();
    }

    private int GetContextDeltaTimeInMilliseconds(int deltaTime)
    {
      if (TempoRatio <= 0.0)
        return int.MaxValue;

      return (int)(CurrentTempo / 1000.0 * deltaTime / _deltaTimeSpec / TempoRatio);
    }

    

    private void OnEvent(MidiEvent m)
    {
      EventReceived?.Invoke(m);
    }
  }
}