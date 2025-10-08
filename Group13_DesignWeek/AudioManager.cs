// AudioManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace Group13_DesignWeek
{
    /// <summary>
    /// Lightweight audio helper for loops and one-shots.
    /// Uses System.Media.SoundPlayer... keep WAVs PCM/uncompressed for fastest load.
    /// Call Audio.Init(baseFolder) once at startup.
    /// Then call the small verbs from your gameplay code, eg:
    ///   Audio.PlayDoorOpen();
    ///   Audio.PlayFlavorSfx(room, playerPos);
    /// </summary>
    static class Audio
    {
        // central registry of loaded players keyed by a short name
        private static readonly Dictionary<string, SoundPlayer> _players = new(StringComparer.OrdinalIgnoreCase);

        // optional currently looping bg key
        private static string _currentLoopKey = null;

        // where the wavs live on disk
        private static string _baseFolder = AppDomain.CurrentDomain.BaseDirectory;

        // quick map of friendly keys to filenames you told me you have
        // drop your WAVs in the folder you pass to Init(...)
        private static readonly Dictionary<string, string> _fileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // global SFX
            { "button",      "Button.wav" },        // clicks... plate press... simple UI
            { "sweep",       "Sweep.wav" },         // creature "h"
            { "key",         "Key.wav" },           // key found
            { "scrape",      "Knife Scrape.wav" },  // gritty metal... locked door... push heavy
            { "leaves",      "Leaves.wav" },        // creature "Y"
            { "door",        "Metal Door.wav" },    // doors... lockers... heavy metal
            { "portal",      "Portal.wav" },        // glyph '0'

            // bg examples... optional... add your own
            // { "bg_level1", "Bg_L1.wav" },
            // { "bg_level2", "Bg_L2.wav" },
        };

        /// <summary>Point the audio system at your WAV folder and prewarm the common sounds.</summary>
        public static void Init(string baseFolder)
        {
            if (!string.IsNullOrWhiteSpace(baseFolder))
                _baseFolder = baseFolder;

            // pre-load frequent SFX so first play has no hiccup
            Preload("button");
            Preload("door");
            Preload("scrape");
            Preload("key");
            Preload("sweep");
            Preload("leaves");
            Preload("portal");
        }

        /// <summary>Start a looping bg track by key... stops any existing loop.</summary>
        public static void PlayBgLoop(string key)
        {
            if (string.Equals(_currentLoopKey, key, StringComparison.OrdinalIgnoreCase))
                return;

            StopBg();

            if (TryGetPlayer(key, out var sp))
            {
                _currentLoopKey = key;
                try { sp.PlayLooping(); } catch { /* ignore */ }
            }
        }

        /// <summary>Stop current looping bg if any.</summary>
        public static void StopBg()
        {
            if (_currentLoopKey == null) return;
            if (TryGetPlayer(_currentLoopKey, out var sp))
            {
                try { sp.Stop(); } catch { }
            }
            _currentLoopKey = null;
        }

        /// <summary>Play a short one-shot by friendly key.</summary>
        public static void PlayOnce(string key)
        {
            if (TryGetPlayer(key, out var sp))
            {
                try { sp.Play(); } catch { }
            }
        }

        // -------------------------
        // Convenience helpers you can call from your game code
        // -------------------------

        public static void PlayPlatePress() => PlayOnce("button");
        public static void PlayPushHeavy() => PlayOnce("scrape");
        public static void PlayDoorOpen() => PlayOnce("door");
        public static void PlayDoorLocked() => PlayOnce("scrape");   // gritty feedback
        public static void PlayLockerOpen() => PlayOnce("door");
        public static void PlayFoundKey() => PlayOnce("key");

        /// <summary>
        /// Flavor glyph SFX... call when the player interacts with a flavor tile.
        /// Maps your glyphs to the provided sounds:
        ///   'h' ... Sweep.wav
        ///   '[' or ']' ... Metal Door.wav  ... big metal maw vibe
        ///   'Y' ... Leaves.wav
        ///   '0' ... Portal.wav
        ///   default ... Button.wav as a soft click
        /// </summary>
        public static void PlayFlavorAt(Room room, (int, int) pos)
        {
            if (room == null) return;

            if (room.FlavorGlyphs.TryGetValue(pos, out var g))
            {
                switch (g)
                {
                    case 'h': PlayOnce("sweep"); break;
                    case '[': case ']': PlayOnce("door"); break;
                    case 'Y': PlayOnce("leaves"); break;
                    case '0': PlayOnce("portal"); break;
                    default: PlayOnce("button"); break;
                }
            }
            else
            {
                PlayOnce("button");
            }
        }

        /// <summary>
        /// Lever ambience idea... optionally pulse neighbors when a lever is pulled.
        /// Pass the lever position as the tuple you already have... we use Item1/Item2, not .x/.y.
        /// </summary>
        public static void PlayLeverThemeSfx(Room room, (int, int) leverPos)
        {
            if (room == null) return;

            // current tile glyph
            if (room.FlavorGlyphs.TryGetValue(leverPos, out var centerGlyph))
                PlayFlavorGlyph(centerGlyph);

            // check the 4-neighbors for extra flavor... safe bounds with Item1/Item2
            var dirs = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
            foreach (var d in dirs)
            {
                var p = (leverPos.Item1 + d.dx, leverPos.Item2 + d.dy);
                if (p.Item1 < 0 || p.Item2 < 0 || p.Item1 >= room.Width || p.Item2 >= room.Height)
                    continue;

                if (room.FlavorGlyphs.TryGetValue(p, out var g))
                    PlayFlavorGlyph(g);
            }

            static void PlayFlavorGlyph(char g)
            {
                switch (g)
                {
                    case 'h': PlayOnce("sweep"); break;
                    case '[': case ']': PlayOnce("door"); break;
                    case 'Y': PlayOnce("leaves"); break;
                    case '0': PlayOnce("portal"); break;
                    default: PlayOnce("button"); break;
                }
            }
        }

        // -------------------------
        // Internals
        // -------------------------

        private static void Preload(string key) => TryGetPlayer(key, out _);

        private static bool TryGetPlayer(string key, out SoundPlayer sp)
        {
            sp = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (_players.TryGetValue(key, out sp)) return true;

            if (!_fileMap.TryGetValue(key, out var fileName)) return false;

            var full = Path.Combine(_baseFolder, fileName);
            if (!File.Exists(full)) return false;

            try
            {
                sp = new SoundPlayer(full);
                sp.Load(); // synchronous load so first play is instant
                _players[key] = sp;
                return true;
            }
            catch
            {
                sp = null;
                return false;
            }
        }
    }
}
