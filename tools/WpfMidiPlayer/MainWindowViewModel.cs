using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Commons.Music.Midi;
using Microsoft.Win32;
using WpfMidiPlayer.LogViewer;
using WpfMidiPlayer.Mvvm;
using WpfMidiPlayer.Properties;

namespace WpfMidiPlayer
{
  public class MainWindowViewModel : ObservableObject, IDisposable
  {
    private const int MaxNrOfLogEntries = 1000;

    private readonly ILogger _logger;
    private IPlayer _player;

    private DelegateCommand _browseCommand;

    private string _fileName = string.Empty;
    private int _logIndex;

    private DelegateCommand _pauseCommand;
    private DelegateCommand _playCommand;
    private DelegateCommand _stopCommand;
    
    private IMidiPortDetails _selectedOutput;

    public MainWindowViewModel()
    {
      _logger = new LoggerWithActionCallback(AddToLog);
      _logger.Trace("Application started.");
      
      _selectedOutput = MidiAccessManager.Default.Outputs.FirstOrDefault(o => o.Name == Settings.Default.MidiOutput) 
                        ?? MidiAccessManager.Default.Outputs.FirstOrDefault();

      TempoChangeRatio = 1.0;
      FileName = Settings.Default.FileName;
    }

    public bool IsPlaying => _player != null;

    private double _tempoChangeRatio;
    public double TempoChangeRatio
    {
      get => _tempoChangeRatio;
      set
      {
        _tempoChangeRatio = value;
        
        if (_player != null)
          _player.TempoChangeRatio = _tempoChangeRatio;

        _logger.Trace("Tempo changed to {0}.", _tempoChangeRatio);

        RaisePropertyChanged();
      }
    }

    public IEnumerable<IMidiPortDetails> Outputs => MidiAccessManager.Default.Outputs;

    public string SelectedOutputName 
    {
      get => _selectedOutput?.Name;

      set
      {
        SelectedOutput = Outputs.FirstOrDefault(o => o.Name == value);
        RaisePropertyChanged();
      }
    }

    public IMidiPortDetails SelectedOutput
    {
      get => _selectedOutput;
      set
      {
        StopPlayer();

        _selectedOutput = value;

        if (_selectedOutput != null)
        {
          Settings.Default.MidiOutput = _selectedOutput.Name;
          Settings.Default.Save();
        }

        RaisePropertyChanged();

        StartPlayer();
      }
    }

    public ObservableCollection<LogEntry> LogEntries { get; set; } = new ObservableCollection<LogEntry>();

    public string FileName
    {
      get => _fileName;
      set
      {
        _fileName = value;

        Settings.Default.FileName = _fileName;
        Settings.Default.Save();

        _logger.Trace("FileName changed to {0}.", _fileName);

        RaisePropertyChanged();
        PlayCommand.RaiseCanExecuteChanged();
      }
    }

    public DelegateCommand BrowseCommand => _browseCommand ??= new DelegateCommand(() =>
    {
      var openFileDialog = new OpenFileDialog
      {
        Filter = "Midi files (*.mid)|*.mid"
      };

      if (openFileDialog.ShowDialog() != true)
        return;

      FileName = openFileDialog.FileName;
      StartPlay();
    });

    private void StartPlay()
    {
      StopPlayer();

      if (string.IsNullOrWhiteSpace(FileName))
      {
        _logger.Warn("Unable to start playing, filename is empty.");
        return;
      }

      if (SelectedOutput == null)
      {
        _logger.Warn("No output device selected.");
        return;
      }

      try
      {
        var midiOutput = MidiAccessManager.Default.OpenOutputAsync(SelectedOutput.Id).Result;

        _player = new Player(midiOutput, _logger);
        _player.PlaybackPositionUpdated += OnPlaybackPositionUpdated;
        _player.Finished += OnFinished;
        
        TempoChangeRatio = 1.0;

        _logger.Info("Starting the player for file '{0}'.", FileName);
        var midiStream = File.OpenRead(FileName);
        _player.Start(midiStream);
      }
      catch (Exception e)
      {
        _logger.Error("Exception while starting the player for file '{0}'.", FileName);
        _logger.Error(e.ToString());
      }
    }

    private void OnFinished(object? sender, EventArgs e)
    {
      RaisePropertyChanged(nameof(State));
    }

    public PlayerState State => _player?.State ?? PlayerState.Stopped;

    public DelegateCommand PlayCommand => _playCommand ??= new DelegateCommand(StartPlayer, () => !string.IsNullOrEmpty(FileName));
    public DelegateCommand PauseCommand => _pauseCommand ??= new DelegateCommand(PausePlayer, () => State != PlayerState.Stopped);
    public DelegateCommand StopCommand => _stopCommand ??= new DelegateCommand(StopPlayer, () => State != PlayerState.Stopped);

    private void StartPlayer()
    {
      StartPlay();
      
      Task.Delay(100).Wait();

      RaisePropertyChanged(nameof(State));
      RaisePropertyChanged(nameof(PlaybackPosition));
      RaisePropertyChanged(nameof(PauseButtonText));
      PauseCommand.RaiseCanExecuteChanged();
      StopCommand.RaiseCanExecuteChanged();
    }

    private void PausePlayer()
    {
      _player?.Pause();

      Task.Delay(100).Wait();

      RaisePropertyChanged(nameof(PauseButtonText));
      RaisePropertyChanged(nameof(State));
    }

    private void StopPlayer()
    {
      if (_player != null)
      {
        _player.Stop();
        
        Task.Delay(100).Wait();
        
        _player.PlaybackPositionUpdated -= OnPlaybackPositionUpdated;
        _player.Finished -= OnFinished;
        _player.Dispose();
        _player = null;
      }

      PlayCommand.RaiseCanExecuteChanged();
      PauseCommand.RaiseCanExecuteChanged();
      RaisePropertyChanged(nameof(PauseButtonText));
      RaisePropertyChanged(nameof(State));
    }

    public double PlaybackPosition
    {
      get => _player?.PlaybackPositionPercentage ?? 0.0;
      set
      {
        _player?.Seek(value);
        RaisePropertyChanged();
      }
    }

    public string PauseButtonText => State == PlayerState.Playing ? "Pause" : "Resume";

    public void Dispose()
    {
      StopPlayer();

      GC.SuppressFinalize(this);
    }


    private void OnPlaybackPositionUpdated(object sender, double playbackPositionPercentage)
    {
      RaisePropertyChanged(nameof(PlaybackPosition));
    }


    private void AddToLog(string message)
    {
      Application.Current.Dispatcher.BeginInvoke((Action)(() =>
     {
       LogEntries.Add(new LogEntry { Message = message, DateTime = DateTime.Now, Index = _logIndex++ });
       while (LogEntries.Count > MaxNrOfLogEntries)
         LogEntries.RemoveAt(0);
     }));
    }

  }
}