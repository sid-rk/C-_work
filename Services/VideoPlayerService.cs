using System;
using System.Windows.Forms;
using LibVLCSharp.Shared;

namespace WindowsFormsApp1.Services
{
    public class VideoPlayerService : IDisposable
    {
        private LibVLC _libVlc;
        private MediaPlayer _mediaPlayer;
        private bool _initialized = false;

        public MediaPlayer Player => _mediaPlayer;
        public bool IsInitialized => _initialized;
        public LibVLC LibVlc => _libVlc;

        public event Action Playing;
        public event Action Stopped;
        public event Action EncounteredError;
        public event Action<double> PositionChanged;
        public event Action<string> MediaEnded;

        public VideoPlayerService()
        {
            try
            {
                Core.Initialize();
                _libVlc = new LibVLC("--verbose=0", "--network-caching=300");
                _mediaPlayer = new MediaPlayer(_libVlc);

                _mediaPlayer.Playing += (s, e) => Playing?.Invoke();
                _mediaPlayer.Stopped += (s, e) => Stopped?.Invoke();
                _mediaPlayer.EncounteredError += (s, e) => EncounteredError?.Invoke();
                _mediaPlayer.EndReached += (s, e) => MediaEnded?.Invoke("end");
                _mediaPlayer.PositionChanged += (s, e) => PositionChanged?.Invoke(_mediaPlayer.Position);

                _initialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize video engine: {ex.Message}", "LibVLC Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _initialized = false;
            }
        }

        private Media _currentMedia;

        public void Play(string filePath)
        {
            if (!_initialized || _mediaPlayer == null) return;

            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _currentMedia = new Media(_libVlc, filePath);
            _mediaPlayer.Play(_currentMedia);
        }

        public void PlayUrl(string url)
        {
            if (!_initialized || _mediaPlayer == null) return;

            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _currentMedia = new Media(_libVlc, new Uri(url));
            _currentMedia.AddOption(":network-caching=300");
            _mediaPlayer.Play(_currentMedia);
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        public void Pause()
        {
            if (_mediaPlayer?.IsPlaying == true)
                _mediaPlayer.Pause();
        }

        public void Resume()
        {
            if (_mediaPlayer?.IsPlaying == false)
                _mediaPlayer.Play();
        }

        public void TogglePlayPause()
        {
            if (_mediaPlayer == null) return;
            if (_mediaPlayer.IsPlaying)
                _mediaPlayer.Pause();
            else
                _mediaPlayer.Play();
        }

        public void SetPosition(double pos)
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Position = (float)Math.Max(0, Math.Min(1, pos));
        }

        public void SetVolume(int volume)
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = Math.Max(0, Math.Min(200, volume));
        }

        public long Duration => _mediaPlayer?.Length ?? 0;
        public long Time => _mediaPlayer?.Time ?? 0;
        public float Position => _mediaPlayer?.Position ?? 0;
        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

        public void Dispose()
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
        }
    }
}
