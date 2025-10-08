using System;
using System.Media;
using System.Threading;

namespace Group13_DesignWeek
{
    /// <summary>
    /// Handles background music and one-shot sound effects.
    /// </summary>
    public class AudioManager
    {
        private SoundPlayer _bgmPlayer;
        private Thread _bgmThread;
        private bool _bgmRunning;

        /// <summary>
        /// Play background music in a loop (async).
        /// </summary>
        public void PlayBackgroundMusic(string filePath)
        {
            StopBackgroundMusic(); // ensure no overlap

            _bgmPlayer = new SoundPlayer(filePath);
            _bgmRunning = true;

            _bgmThread = new Thread(() =>
            {
                try
                {
                    while (_bgmRunning)
                    {
                        _bgmPlayer.PlaySync(); // blocks until file ends
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AudioManager] Error playing background music: {ex.Message}");
                }
            });

            _bgmThread.IsBackground = true;
            _bgmThread.Start();
        }

        /// <summary>
        /// Stop currently playing background music.
        /// </summary>
        public void StopBackgroundMusic()
        {
            _bgmRunning = false;
            try
            {
                _bgmPlayer?.Stop();
            }
            catch { /* safe ignore */ }
        }

        /// <summary>
        /// Play a one-shot sound effect (non-blocking).
        /// </summary>
        public void PlayOneShot(string filePath)
        {
            try
            {
                SoundPlayer sfx = new SoundPlayer(filePath);
                sfx.Play(); // async one-shot
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Error playing sound effect: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up resources.
        /// </summary>
        public void Dispose()
        {
            StopBackgroundMusic();
            _bgmPlayer?.Dispose();
        }
    }
}
