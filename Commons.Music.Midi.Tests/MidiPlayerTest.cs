using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Commons.Music.Midi.PortMidi;
using Commons.Music.Midi.RtMidi;
using NUnit.Framework;

namespace Commons.Music.Midi.Tests
{

  public class PlayerHelper
  {
    private CancellationTokenSource _tokenSource = new CancellationTokenSource();
    private VirtualMidiPlayerTimeManager _timeManager = new VirtualMidiPlayerTimeManager();

    public PlayerHelper(IMidiAccess midiAccess)
    {
      Music = TestHelper.GetMidiMusic();
      GetMidiMusicDimensions(Music, out var maxTicks, out var maxMilliseconds);
      MaxTicks = maxTicks;
      MaxMilliseconds = maxMilliseconds;
      Player = TestHelper.GetMidiPlayer(_timeManager, Music, midiAccess);
      Player.PlaybackCompletedToEnd += () => Completed = true;
      Player.Finished += () =>
      {
        Finished = true;
        _tokenSource.Cancel();
      };
    }

    public void ProceedByAndWait(int addedMilliseconds) => _timeManager.ProceedByAndWait(addedMilliseconds, _tokenSource.Token);

    public int MaxTicks { get; }
    public int MaxMilliseconds { get; }
    public MidiMusic Music { get; }
    public MidiPlayer Player { get; }

    public bool Completed { get; private set; }

    public bool Finished { get; private set; }

    public CancellationToken Token => _tokenSource.Token;
    
    private static void GetMidiMusicDimensions(MidiMusic music, out int maxTicks, out int maxMilliseconds)
    {
      var vt = new VirtualMidiPlayerTimeManager();
      var player = TestHelper.GetMidiPlayer(vt, music);
      Assert.AreEqual(0, player.PlayDeltaTime, "PlayDeltaTime should be 0 after player creation.");

      var completed = false;
      var finished = false;
      var tokenSource = new CancellationTokenSource();
      player.PlaybackCompletedToEnd += () => completed = true;
      player.Finished += () =>
      {
        finished = true;
        tokenSource.Cancel();
      };
      player.Play();

      vt.ProceedByAndWait(1000000, tokenSource.Token);

      Assert.IsTrue(completed, "PlaybackCompletedToEnd event should've fired after waiting an insane amount of time.");
      Assert.IsTrue(finished, "Finished event should've fired after waiting an insane amount of time.");

      maxTicks = player.PlayDeltaTime;
      maxMilliseconds = player.GetTotalPlayTimeMilliseconds();
      Assert.AreEqual(4988, maxTicks, "GetMidiMusic() is expected to create a midi stream with 4988 ticks.");
      Assert.AreEqual(12989, maxMilliseconds, "GetMidiMusic() is expected to create a midi stream of 12989 milliseconds.");
    }
  }

  public class MidiPlayerTestFixtureData
  {
    public static IEnumerable MidiAccessSource
    {
      get
      {
        // use Func here, because PortMidiAccess constructor throws exception and is called even though the TestFixture is ignored.
        yield return new TestFixtureData(new Func<IMidiAccess>(() => default));
        // Uncommented because Resharper doesn't handle the output of ignored test correctly.
        //yield return new TestFixtureData(new Func<IMidiAccess>(() => new RtMidiAccess())).Ignore("rtmidi may not be runnable depending on the test runner platform");
        //yield return new TestFixtureData(new Func<IMidiAccess>(() => new PortMidiAccess())).Ignore("portmidi may not be runnable depending on the test runner platform");
      }
    }
  }

  [TestFixtureSource(typeof(MidiPlayerTestFixtureData), "MidiAccessSource")]
	public class MidiPlayerTest
  {
    private readonly Func<IMidiAccess> _midiAccessCreator;

    public MidiPlayerTest(Func<IMidiAccess> midiAccessCreator)
    {
      _midiAccessCreator = midiAccessCreator;
    }

    private PlayerHelper _helper;

    [SetUp]
		public void SetUp()
    {
      _helper = new PlayerHelper(_midiAccessCreator.Invoke());
    }

    [Test]
		public void PlaySimple ()
    {
      _helper.Player.Play ();
      _helper.ProceedByAndWait(100);
      Assert.AreEqual(PlayerState.Playing, _helper.Player.State);

      _helper.Player.Pause ();
      // First midi event occurs after 500 ms and only then the paused state is set, so wait at least that much.
      _helper.ProceedByAndWait(500);
			Assert.AreEqual(PlayerState.Paused, _helper.Player.State);

      _helper.Player.Dispose ();
      _helper.ProceedByAndWait(200000);

			Assert.AreEqual(PlayerState.Stopped, _helper.Player.State);
      Assert.IsFalse(_helper.Completed, "Completed was fired");
			Assert.IsTrue(_helper.Finished, "Finished not fired");
		}


		[Test]
		public void PlaybackCompletedToEnd ()
    {
			var qmsec = MidiMusic.GetPlayTimeMillisecondsAtTick (_helper.Music.Tracks [0].Messages, _helper.MaxTicks, 192);
      
      Assert.IsTrue (!_helper.Completed, "1 PlaybackCompletedToEnd already fired");
			Assert.IsTrue (!_helper.Finished, "2 Finished already fired");
      
      _helper.Player.Play ();
			_helper.ProceedByAndWait(1000);

			Assert.IsTrue (!_helper.Completed, "3 PlaybackCompletedToEnd already fired");
			Assert.IsTrue (!_helper.Finished, "4 Finished already fired");

      _helper.ProceedByAndWait(qmsec);
			Assert.AreEqual (_helper.MaxMilliseconds, qmsec, "qmsec");

      _helper.ProceedByAndWait(100000);

			Assert.AreEqual (_helper.MaxTicks, _helper.Player.PlayDeltaTime, "PlayDeltaTime");

      _helper.Player.Pause ();
      _helper.Player.Dispose ();

			Assert.IsTrue (_helper.Completed, "5 PlaybackCompletedToEnd not fired");
			Assert.IsTrue (_helper.Finished, "6 Finished not fired");
		}
		
		[Test]
		public void PlaybackCompletedToEndAbort ()
		{
			_helper.Player.Play ();
			_helper.ProceedByAndWait(100);
      Assert.AreEqual(PlayerState.Playing, _helper.Player.State);

      _helper.Player.Pause ();
      _helper.ProceedByAndWait(100000);

      Assert.AreEqual(PlayerState.Paused, _helper.Player.State);

      _helper.Player.Dispose (); // abort in the middle

      _helper.ProceedByAndWait(100000);

      Assert.AreEqual(PlayerState.Stopped, _helper.Player.State);

			Assert.IsFalse(_helper.Completed, "1 PlaybackCompletedToEnd unexpectedly fired");
			Assert.IsTrue (_helper.Finished, "2 Finished not fired");
		}


    [Test]
    public void GetTimePositionInMillisecondsForTick()
    {
      _helper.Player.Play();
      Assert.AreEqual(0, _helper.Player.PlayDeltaTime, "PlayDeltaTime should be 0 after calling Play.");

      _helper.ProceedByAndWait(_helper.MaxTicks / 2);
      Assert.AreNotEqual(0, _helper.Player.PlayDeltaTime, "PlayDeltaTime should not be 0 after playing some notes.");

      var oneTickAfterEnd = _helper.MaxTicks + 1;
      _helper.Player.Seek(oneTickAfterEnd);

      Assert.AreEqual(oneTickAfterEnd, _helper.Player.PlayDeltaTime, "PlayDeltaTime should be the same as set by the Seek method.");
      Assert.AreEqual(_helper.MaxMilliseconds, (int)_helper.Player.PositionInTime.TotalMilliseconds, "PositionInTime should be max after all notes are played.");

      // Seek to middle of music.
      // As playback is still blocked, this should not have any effect.
      _helper.Player.Seek(_helper.MaxTicks / 2);
      Assert.AreEqual(_helper.MaxTicks / 2, _helper.Player.PlayDeltaTime, "PlayDeltaTime should be the same as set by the Seek method.");
      Assert.AreEqual(_helper.MaxMilliseconds / 2, (int)_helper.Player.PositionInTime.TotalMilliseconds, "PositionInTime should be in the middle of the music.");
    }

    [Test]
    public void Seek()
    {
			_helper.Player.Play();
      Assert.AreEqual(0, _helper.Player.PlayDeltaTime, "PlayDeltaTime should be 0 after calling Play.");
			
      _helper.ProceedByAndWait(_helper.MaxTicks/2);
      // NB: at this point, the playerloop is blocked half way through the midi file via the timer manager.

      Assert.AreNotEqual(0, _helper.Player.PlayDeltaTime, "PlayDeltaTime should not be 0 after playing some notes.");

      var oneTickAfterEnd = _helper.MaxTicks + 1;
      _helper.Player.Seek(oneTickAfterEnd);

			Assert.AreEqual(oneTickAfterEnd, _helper.Player.PlayDeltaTime, "PlayDeltaTime should be the same as set by the Seek method.");
      Assert.AreEqual(_helper.MaxMilliseconds, (int)_helper.Player.PositionInTime.TotalMilliseconds, "PositionInTime should be max after all notes are played.");
      
      // Thread is still waiting for time to continue, so even as we Seek beyond the end of the music, the player has not finished.
      // It's still stuck in the middle of the music.
      Assert.IsFalse(_helper.Finished);
      
      // The Seek method has set the event_index back to zero, as the seeking process must still begin.
      // Also PlayDeltaTime is set to the Seek ticks argument, but as the playerloop is waiting in the WaitBy method, once it 
      // continues PlayDeltaTime will be increased with the delta time of the last note played, 
      // and THEN the seeking process will start.
      // This is not only a problem in this unit test, but can also occur in real applications.

      // Start the seeking process by unblocking the time.
      _helper.ProceedByAndWait(1000);

      Assert.IsTrue(_helper.Finished);
      // This is where the Seek bug manifests itself:
      Assert.AreEqual (oneTickAfterEnd, _helper.Player.PlayDeltaTime, "PlayDeltaTime should not be changed after all notes are played.");
      Assert.AreEqual(_helper.MaxMilliseconds, (int)_helper.Player.PositionInTime.TotalMilliseconds, "PositionInTime should not be changed after all notes are played.");

      // Seek to middle of music.
      _helper.Player.Seek(_helper.MaxTicks /2);
      Assert.AreEqual(_helper.MaxTicks /2, _helper.Player.PlayDeltaTime, "PlayDeltaTime should be the same as set by the Seek method.");
      Assert.AreEqual(_helper.MaxMilliseconds /2, (int)_helper.Player.PositionInTime.TotalMilliseconds, "PositionInTime should be in the middle of the music.");

      // As the player has stopped, the playback position as set by Seek, should no longer change.
      _helper.ProceedByAndWait(10000);
      
      Assert.AreEqual (_helper.MaxTicks / 2, _helper.Player.PlayDeltaTime, "PlayDeltaTime should not be changed after all notes are played.");
			Assert.AreEqual(_helper.MaxMilliseconds / 2, (int)_helper.Player.PositionInTime.TotalMilliseconds, "PositionInTime should not be changed after all notes are played.");
		}

    [Test]
    public async Task ExceptionEvent()
    {
      _helper.Player.EventReceived += midiEvent => { throw new Exception("some exception"); };
      Exception exception = null;
      object exceptionSender = null;
      _helper.Player.Exception += (sender, ex) =>
      {
        exceptionSender = sender;
        exception = ex;
      };

      _helper.Player.Play();
      _helper.ProceedByAndWait(1000);

      Assert.IsNotNull(exception);
      Assert.AreSame(_helper.Player, exceptionSender);
    }
    
    [Test]
    public void StopWaitsUntilPlayerLoopHasEnded()
    {
      Assert.Fail("todo");
    }
	}
}
