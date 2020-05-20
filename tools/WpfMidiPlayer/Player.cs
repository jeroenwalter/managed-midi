using System;
using System.IO;
using System.Linq;
using Commons.Music.Midi;

namespace WpfMidiPlayer
{
  public class Player : IPlayer
  {
    private readonly ILogger _logger;
    private bool _isDisposed;
    private MidiMusic _midiMusic;
    private IMidiOutput _midiOutput;
    private MidiPlayer _midiPlayer;

    private double _playbackPositionPercentage;
    private readonly TimeSpan _playbackPositionUpdatedEventInterval = TimeSpan.FromMilliseconds(100.0);
    private DateTime _playbackPositionUpdatedEventLastCheck = DateTime.MinValue;
    
    private double _tempoChangeRatio = 1.0;
    private int _totalTicks;

    public event EventHandler Finished;
    

    public Player(IMidiOutput midiOutput, ILogger logger)
    {
      _midiOutput = midiOutput ?? throw new ArgumentNullException(nameof(midiOutput));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      
      _logger.Info("Player initialized");
      _logger.Info("Using synthesizer '{0}'", _midiOutput.Details.Name);
    }

    public event EventHandler<double> PlaybackPositionUpdated;

    public PlayerState State => _midiPlayer?.State ?? PlayerState.Stopped;

    public double TempoChangeRatio
    {
      get => _tempoChangeRatio;
      set
      {
        _tempoChangeRatio = Math.Max(0.05, value);
        if (_midiPlayer != null)
          _midiPlayer.TempoChangeRatio = _tempoChangeRatio;
      }
    }


    public int TotalPlayTimeMilliseconds { get; private set; }

    public double PlaybackPositionPercentage
    {
      get => _playbackPositionPercentage;
      private set
      {
        _playbackPositionPercentage = value;

        var now = DateTime.Now;
        if (now.Subtract(_playbackPositionUpdatedEventLastCheck) < _playbackPositionUpdatedEventInterval)
          return;

        _playbackPositionUpdatedEventLastCheck = now;
        PlaybackPositionUpdated?.Invoke(this, _playbackPositionPercentage);
      }
    }


    public void Start(Stream midiStream)
    {
      Stop();

      if (!LoadMidiFile(midiStream))
        return;

      _midiPlayer = new MidiPlayer(_midiMusic, _midiOutput)
      {
        TempoChangeRatio = _tempoChangeRatio
      };

      _midiPlayer.EventReceived += OnEventReceived;
      _midiPlayer.Finished += OnFinished;
      _midiPlayer.Play();
    }

    private void OnFinished()
    {
      Finished?.Invoke(this, new EventArgs());
    }

    public void Stop()
    {
      if (_midiPlayer == null) 
        return;

      _midiPlayer.EventReceived -= OnEventReceived;
      _midiPlayer.Finished -= OnFinished;
      _midiPlayer.Dispose();
      _midiPlayer = null;
    }

    public void Pause()
    {
      if (_midiPlayer == null)
        return;

      switch (_midiPlayer.State)
      {
        case PlayerState.Playing:
          _midiPlayer.Pause();
          break;
        case PlayerState.Paused:
          _midiPlayer.Play();
          break;
        case PlayerState.Stopped:
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    public void Seek(double playbackPositionPercentage)
    {
      _logger.Info("Seeking to {0} %", playbackPositionPercentage);
      _midiPlayer?.Seek((int)(_totalTicks * Math.Max(0.0, Math.Min(1.0, playbackPositionPercentage / 100.0))));
    }

    public void Dispose()
    {
      ThrowIfDisposed();

      _isDisposed = true;

      Dispose(true);
      GC.SuppressFinalize(this);
    }

    private bool LoadMidiFile(Stream midiStream)
    {
      try
      {
        var parser = new SmfReader();
        parser.Read(midiStream);
        _midiMusic = parser.Music;

        _totalTicks = _midiMusic.GetTotalTicks();
        TotalPlayTimeMilliseconds = _midiMusic.GetTotalPlayTimeMilliseconds();
        PlaybackPositionPercentage = 0;

        _logger.Info("Midi stream loaded, total play time: {0}", TimeSpan.FromMilliseconds(TotalPlayTimeMilliseconds).ToString(@"hh\:mm\:ss"));
        return true;
      }
      catch (Exception e)
      {
        _logger.Error("Exception while loading midi stream.");
        _logger.Error(e.ToString());

        _midiMusic = null;
        TotalPlayTimeMilliseconds = 0;
        PlaybackPositionPercentage = 0;
        return false;
      }
    }

    private void OnEventReceived(MidiEvent m)
    {
      if (_midiPlayer == null)
        return;

      PlaybackPositionPercentage = _totalTicks > 0.0 ?  100.0 * _midiPlayer.PlayDeltaTime / _totalTicks : 0.0;
    }


    private void Dispose(bool disposing)
    {
      _midiOutput?.Dispose();
      _midiOutput = null;

      if (!disposing) return;

      _midiPlayer?.Dispose();
      _midiPlayer = null;
    }

    private void ThrowIfDisposed()
    {
      if (_isDisposed)
        throw new ObjectDisposedException(GetType().FullName);
    }

    ~Player()
    {
      Dispose(false);
    }
  }
}