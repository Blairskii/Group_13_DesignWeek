// AudioManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace Group13_DesignWeek
{
    static class Audio
    {
        private static readonly Dictionary<string, SoundPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
        private static string _baseFolder = AppDomain.CurrentDomain.BaseDirectory;

        // Maps legend names or interaction categories to specific WAV files
        private static readonly Dictionary<string, string> _fileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Frisbee", "Plate.wav" },               // pressure plates
            { "Concealment tube", "Locker.wav" },     // concealment cube
            { "Sleeper", "Sleepy.wav" },              // h
            { "Metal Maw", "Window.wav" },            // [ or ]
            { "Green friend", "Green.wav" },          // Y
            { "Doughnout", "Doughnaut.wav" },         // 0 (portal)
            { "Bone stick", "Lever.wav" },            // L
            { "Secret container", "Locker.wav" },     // T
            { "Metal mouth", "Door.wav" },            // D
            { "Found key", "Key.wav" }                // key pickup
        };

        public static void Init(string baseFolder)
        {
            if (!string.IsNullOrWhiteSpace(baseFolder))
                _baseFolder = baseFolder;

            foreach (var key in _fileMap.Keys)
                Preload(key);
        }

        // ==================================
        // Main playback methods
        // ==================================
        public static void PlayOnce(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (TryGetPlayer(key, out var sp))
            {
                try { sp.Play(); } catch { }
            }
        }

        public static void PlayLoop(string key)
        {
            if (TryGetPlayer(key, out var sp))
            {
                try { sp.PlayLooping(); } catch { }
            }
        }

        public static void StopAll()
        {
            foreach (var sp in _players.Values)
            {
                try { sp.Stop(); } catch { }
            }
        }

        // ==================================
        // Game-specific helpers
        // ==================================
        public static void PlayPlatePress() => PlayOnce("Frisbee");
        public static void PlayDoorOpen() => PlayOnce("Metal mouth");
        public static void PlayDoorLocked() => PlayOnce("Metal mouth");
        public static void PlayLeverPull() => PlayOnce("Bone stick");
        public static void PlayLockerOpen() => PlayOnce("Secret container");
        public static void PlayFoundKey() => PlayOnce("Found key");

        /// <summary>
        /// Plays the corresponding sound for a flavor or interactible type.
        /// </summary>
        public static void PlayFlavor(string legendName)
        {
            if (string.IsNullOrWhiteSpace(legendName)) return;

            if (_fileMap.TryGetValue(legendName, out var _))
                PlayOnce(legendName);
            else
                PlayOnce("Frisbee"); // soft default
        }

        // ==================================
        // Internal file handling
        // ==================================
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
                sp.Load();
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
