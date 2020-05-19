using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Commons.Music.Midi.Tests
{
	[TestFixture]
	public class SMFTest
	{
		[Test]
		public void GetBpm ()
		{
			Assert.AreEqual (120, MidiMetaType.GetBpm (new byte[] {7, 0xA1, 0x20}, 0), "120");
			Assert.AreEqual (140, Math.Round (MidiMetaType.GetBpm (new byte[] {6, 0x8A, 0xB1}, 0)), "140");
		}

		[Test]
		public void GetTempo ()
		{
			Assert.AreEqual (500000, MidiMetaType.GetTempo (new byte[] {7, 0xA1, 0x20}, 0), "500000");
		}
		
		[Test]
		public void GetFixedSize ()
		{
			Assert.AreEqual (2, MidiEvent.FixedDataSize (0x90), "NoteOn");
			Assert.AreEqual (1, MidiEvent.FixedDataSize (0xC0), "ProgramChange");
			Assert.AreEqual (1, MidiEvent.FixedDataSize (0xD0), "CAf");
			Assert.AreEqual (2, MidiEvent.FixedDataSize (0xA0), "PAf");
			Assert.AreEqual (0, MidiEvent.FixedDataSize (0xF0), "SysEx");
			Assert.AreEqual (2, MidiEvent.FixedDataSize (0xF2), "SongPositionPointer");
			Assert.AreEqual (1, MidiEvent.FixedDataSize (0xF3), "SongSelect");
			Assert.AreEqual (0, MidiEvent.FixedDataSize (0xF8), "MidiClock");
			Assert.AreEqual (0, MidiEvent.FixedDataSize (0xFF), "META");
		}

		[Test]
		public void MidiEventConvert ()
		{
			var bytes1 = new byte [] {0xF8};
			var events1 = MidiEvent.Convert (bytes1, 0, bytes1.Length);
			Assert.AreEqual (1, events1.Count (), "bytes1 count");

			var bytes2 = new byte [] {0xFE};
			var events2 = MidiEvent.Convert (bytes2, 0, bytes2.Length);
			Assert.AreEqual (1, events2.Count (), "bytes2 count");
		}

		[Test]
		public void MidiMusicGetPlayTimeMillisecondsAtTick ()
		{
			var music = TestHelper.GetMidiMusic ();
			Assert.AreEqual (0, music.GetTimePositionInMillisecondsForTick (0), "tick 0");
			Assert.AreEqual (125, music.GetTimePositionInMillisecondsForTick (48), "tick 48");
			Assert.AreEqual (500, music.GetTimePositionInMillisecondsForTick (192), "tick 192");
		}
		
		[Test]
		public void SmfReaderRead ()
		{
			foreach (var name in GetType ().Assembly.GetManifestResourceNames ()) {
				using (var stream = GetType ().Assembly.GetManifestResourceStream (name)) {
					try {
						new SmfReader ().Read (stream);
					}
					catch {
						Assert.Warn ($"Failed at {name}");
						throw;
					}
				}
			}
		}

		[Test]
		public void Convert ()
		{
			int [] bytes = {0xF0, 0x0A, 0x41, 0x10, 0x42, 0x12, 0x40, 0, 0x7F, 0, 0x41, 0xF7}; // am too lazy to add cast to byte...
			var msgs = MidiEvent.Convert (bytes.Select (i => (byte) i).ToArray (), 0, bytes.Length);
			Assert.AreEqual (1, msgs.Count (), "message length");
			Assert.AreEqual (bytes.Length, msgs.First().ExtraDataLength);
		}
	}
}
