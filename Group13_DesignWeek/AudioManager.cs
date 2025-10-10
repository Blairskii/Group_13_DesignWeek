// Audio.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace Group13_DesignWeek
{
    static class Audio
    {
        // registry of loaded players
        private static readonly Dictionary<string, SoundPlayer> _players = new(StringComparer.OrdinalIgnoreCase);

        // background loop player
        private static SoundPlayer _ambientLoop = null;

        // base folder for all WAV files
        private static string _baseFolder = AppDomain.CurrentDomain.BaseDirectory;

        // friendly names to filenames
        private static readonly Dictionary<string, string> _fileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "button",      "Button.wav" },
            { "sweep",       "Sweep.wav" },
            { "key",         "Key.wav" },
            { "scrape",      "Knife Scrape.wav" },
            { "leaves",      "Leaves.wav" },
            { "door",        "Metal Door.wav" },
            { "portal",      "Portal.wav" },
            { "locker",      "Locker.wav" },
            { "plate",       "Plate.wav" },
            { "sleepy",      "Sleepy.wav" },
            { "window",      "Window.wav" },
            { "green",       "Green.wav" },
            { "doughnaut",   "Doughnaut.wav" },
            { "lever",       "Lever.wav" },
            { "bg",          "BG.wav" }  // background music loop
        };

        // -------------------------------------
        // Initialization
        // -------------------------------------
        public static void Init(string baseFolder)
        {
            if (!string.IsNullOrWhiteSpace(baseFolder))
                _baseFolder = baseFolder;

            Preload("button");
            Preload("door");
            Preload("scrape");
            Preload("key");
            Preload("leaves");
            Preload("portal");
        }

        // -------------------------------------
        // General one-shots
        // -------------------------------------
        public static void PlayOnce(string key)
        {
            if (TryGetPlayer(key, out var sp))
            {
                try { sp.Play(); } catch { }
            }
        }

        // Common game SFX convenience
        public static void PlayPlatePress() => PlayOnce("plate");
        public static void PlayDoorOpen() => PlayOnce("door");
        public static void PlayDoorLocked() => PlayOnce("door");
        public static void PlayLockerOpen() => PlayOnce("locker");
        public static void PlayFoundKey() => PlayOnce("key");

        // -------------------------------------
        // Background loop
        // -------------------------------------
        public static void PlayBackgroundLoop(string fileName = "BG.wav")
        {
            try
            {
                string full = Path.Combine(_baseFolder, fileName);
                if (File.Exists(full))
                {
                    _ambientLoop = new SoundPlayer(full);
                    _ambientLoop.Load();
                    _ambientLoop.PlayLooping();
                }
            }
            catch
            {
                // ignore errors if file missing
            }
        }

        public static void StopBackgroundLoop()
        {
            try
            {
                _ambientLoop?.Stop();
            }
            catch { }
        }

        // -------------------------------------
        // Flavor SFX by glyph legend
        // -------------------------------------
        public static void PlayFlavorAt(Room room, (int, int) pos)
        {
            if (room == null) return;

            if (room.FlavorGlyphs.TryGetValue(pos, out var g))
            {
                switch (g)
                {
                    case 'C': PlayOnce("locker"); break;           // Concealment cube
                    case 'h': PlayOnce("sleepy"); break;           // Sleeper
                    case '[':
                    case ']': PlayOnce("window"); break;           // Metal maw
                    case 'Y': PlayOnce("green"); break;            // Green friend
                    case '0': PlayOnce("portal"); break;           // Portal
                    case 'O':
                    case 'F': PlayOnce("doughnaut"); break;        // Doughnaut
                    default: PlayOnce("button"); break;
                }
            }
            else
            {
                PlayOnce("button");
            }
        }

        // -------------------------------------
        // Internals
        // -------------------------------------
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
                sp.Load(); // synchronous load
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
