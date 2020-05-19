using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Commons.Music.Midi.Tests
{
	[TestFixture]
	public class MidiPlayerTest
	{
		[Test]
		public void PlaySimple ()
		{
			var vt = new VirtualMidiPlayerTimeManager();
			var player = TestHelper.GetMidiPlayer (vt);
			player.Play ();
			vt.ProceedByAndWait(200000);
			player.Pause ();
			player.Dispose ();
		}

		[Ignore ("rtmidi may not be runnable depending on the test runner platform")]
		[Test]
		public void PlayRtMidi ()
		{
			var vt = new VirtualMidiPlayerTimeManager();
			var player = TestHelper.GetMidiPlayer (vt, new RtMidi.RtMidiAccess ());
			player.Play ();
			vt.ProceedByAndWait(200000);
			player.Pause ();
			player.Dispose ();
		}

		[Ignore ("portmidi may not be runnable depending on the test runner platform")]
		[Test]
		public void PlayPortMidi ()
		{
			var vt = new VirtualMidiPlayerTimeManager();
			var player = TestHelper.GetMidiPlayer (vt, new PortMidi.PortMidiAccess ());
			player.Play ();
			vt.ProceedByAndWait(200000);
			player.Pause ();
			player.Dispose ();
		}

		[Test]
		public void PlaybackCompletedToEnd ()
		{
			var vt = new VirtualMidiPlayerTimeManager ();
			var music = TestHelper.GetMidiMusic ();
			var qmsec = MidiMusic.GetPlayTimeMillisecondsAtTick (music.Tracks [0].Messages, 4998, 192);
			var player = TestHelper.GetMidiPlayer (vt, music);
			bool completed = false, finished = false;
			
			player.PlaybackCompletedToEnd += () => completed = true;
			player.Finished += () => finished = true;
			Assert.IsTrue (!completed, "1 PlaybackCompletedToEnd already fired");
			Assert.IsTrue (!finished, "2 Finished already fired");
			player.Play ();
			vt.ProceedByAndWait(100);
			Assert.IsTrue (!completed, "3 PlaybackCompletedToEnd already fired");
			Assert.IsTrue (!finished, "4 Finished already fired");
			vt.ProceedByAndWait(qmsec);
			Assert.AreEqual (12989, qmsec, "qmsec");
			// FIXME: this is ugly
			while (player.PlayDeltaTime < 4988)
				Task.Delay (100);
			Assert.AreEqual (4988, player.PlayDeltaTime, "PlayDeltaTime");
			player.Pause ();
			player.Dispose ();
			Assert.IsTrue (completed, "5 PlaybackCompletedToEnd not fired");
			Assert.IsTrue (finished, "6 Finished not fired");
		}
		
		[Test]
		public void PlaybackCompletedToEndAbort ()
		{
			var vt = new VirtualMidiPlayerTimeManager ();
			var player = TestHelper.GetMidiPlayer (vt);
			bool completed = false, finished = false;
			player.PlaybackCompletedToEnd += () => completed = true;
			player.Finished += () => finished = true;
			player.Play ();
			vt.ProceedByAndWait(100);
			
			player.Pause ();
			player.Dispose (); // abort in the middle
			Assert.IsFalse( completed, "1 PlaybackCompletedToEnd unexpectedly fired");
			Assert.IsTrue (finished, "2 Finished not fired");
		}

    
    [Test]
    public void GetTimePositionInMillisecondsForTick()
    {
      var vt = new VirtualMidiPlayerTimeManager();
      GetDefaultMidiMusicDimensions(vt, out var maxTicks, out var maxMilliseconds);

      //////////////////////////////////////
      var player = TestHelper.GetMidiPlayer(vt);
			player.Play();
			// This one's tricky, only works if player thread hasn't started yet.
      Assert.AreEqual(0, player.PlayDeltaTime, "PlayDeltaTime should be 0 after calling Play.");
			
      vt.ProceedByAndWait(maxTicks/2);
      Assert.AreNotEqual(0, player.PlayDeltaTime, "PlayDeltaTime should not be 0 after playing some notes.");

      var oneTickAfterEnd = maxTicks + 1;
			player.Seek(oneTickAfterEnd);

			Assert.AreEqual(oneTickAfterEnd, player.PlayDeltaTime, "PlayDeltaTime should be the same as set by the Seek method.");
      Assert.AreEqual(maxMilliseconds, (int)player.PositionInTime.TotalMilliseconds, "PositionInTime should be max after all notes are played.");
      
			// Let's wait and see if the player thread does something else...
			vt.ProceedByAndWait(1000);
      
      Assert.AreEqual (oneTickAfterEnd, player.PlayDeltaTime, "PlayDeltaTime should not be changed after all notes are played.");
      Assert.AreEqual(maxMilliseconds, (int)player.PositionInTime.TotalMilliseconds, "PositionInTime should not be changed after all notes are played.");
			
			// Seek to middle of music.
			// As playback has stopped, this should not have any effect.
      player.Seek(maxTicks/2);
      Assert.AreEqual(maxTicks/2, player.PlayDeltaTime, "PlayDeltaTime should be the same as set by the Seek method.");
      Assert.AreEqual(maxMilliseconds/2, (int)player.PositionInTime.TotalMilliseconds, "PositionInTime should be in the middle of the music.");

			// As the player has stopped, this should have no effect.
      vt.ProceedByAndWait(10000);
      
      Assert.AreEqual (maxTicks / 2, player.PlayDeltaTime, "PlayDeltaTime should not be changed after all notes are played.");
			Assert.AreEqual(maxMilliseconds / 2, (int)player.PositionInTime.TotalMilliseconds, "PositionInTime should not be changed after all notes are played.");
		}

    private static void GetDefaultMidiMusicDimensions(VirtualMidiPlayerTimeManager vt, out int maxTicks, out int maxMilliseconds)
    {
      var playerMaxTicks = TestHelper.GetMidiPlayer(vt);
      Assert.AreEqual(0, playerMaxTicks.PlayDeltaTime, "PlayDeltaTime should be 0 after player creation.");

      var completedMaxTicks = false;
      var finishedMaxTicks = false;
      playerMaxTicks.PlaybackCompletedToEnd += () => completedMaxTicks = true;
      playerMaxTicks.Finished += () => finishedMaxTicks = true;
      playerMaxTicks.Play();
      // Max nr ticks of music returned by GetMidiMusic()= 4988 and last about 12 seconds.
      // 
      vt.ProceedByAndWait(1000000);
      Assert.IsTrue(completedMaxTicks,
        "PlaybackCompletedToEnd event should've fired after waiting an insane amount of time.");
      Assert.IsTrue(finishedMaxTicks, "Finished event should've fired after waiting an insane amount of time.");
      maxTicks = playerMaxTicks.PlayDeltaTime;
      maxMilliseconds = playerMaxTicks.GetTotalPlayTimeMilliseconds();
      Assert.AreEqual(4988, maxTicks, "GetMidiMusic() is expected to create a midi stream with 4988 ticks.");
    }
	}
}
