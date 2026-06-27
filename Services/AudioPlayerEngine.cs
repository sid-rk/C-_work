using NAudio.Wave;
using NAudio.Vorbis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using WindowsFormsApp1.Models;

namespace WindowsFormsApp1.Services
{
    public class AudioPlayerEngine : IDisposable
    {
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFileReader;
        private VorbisWaveReader _vorbisReader;
        private bool _isOgg;
        private bool _isPaused = false;

        private System.Timers.Timer _positionTimer;
        private const int FftLength = 1024;

        public MusicFile CurrentTrack { get; private set; }
        public PlaybackState PlaybackState => _outputDevice?.PlaybackState ?? PlaybackState.Stopped;

        public event Action<MusicFile> TrackChanged;
        public event Action PlaybackCompleted;
        public event Action<double> PositionChanged;
        public event Action<float[]> FftDataAvailable;

        private List<MusicFile> _playQueue = new List<MusicFile>();
        private int _currentIndex = -1;
        private Random _random = new Random();
        private List<int> _shuffleOrder = new List<int>();

        public enum PlayMode
        {
            Sequential,
            RepeatOne,
            RepeatAll,
            Shuffle
        }
        public PlayMode CurrentPlayMode { get; set; } = PlayMode.Sequential;

        public double Duration => GetDuration();
        public double CurrentPosition => GetPosition();
        public float Volume
        {
            get => _outputDevice?.Volume ?? 1.0f;
            set { if (_outputDevice != null) _outputDevice.Volume = Math.Max(0, Math.Min(1, value)); }
        }

        public AudioPlayerEngine()
        {
            _positionTimer = new System.Timers.Timer(50);
            _positionTimer.Elapsed += OnPositionTimerElapsed;
        }

        // ========== Load & Play ==========

        public void LoadAndPlay(MusicFile file)
        {
            if (!File.Exists(file.FilePath)) return;

            Stop();
            _isPaused = false;

            try
            {
                string ext = Path.GetExtension(file.FilePath).ToLower();
                _isOgg = ext == ".ogg";

                if (_isOgg)
                {
                    _vorbisReader = new VorbisWaveReader(file.FilePath);
                    GetVorbisMetadata(_vorbisReader, file);
                }
                else
                {
                    _audioFileReader = new AudioFileReader(file.FilePath);
                    GetMetadata(file);
                }

                _outputDevice = new WaveOutEvent();
                _outputDevice.PlaybackStopped += OnPlaybackStopped;

                if (_isOgg)
                    _outputDevice.Init(_vorbisReader);
                else
                    _outputDevice.Init(_audioFileReader);

                CurrentTrack = file;
                TrackChanged?.Invoke(file);
                _positionTimer.Start();
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Cannot play file: {ex.Message}", "Playback Error",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        public void Play()
        {
            if (_isPaused && _outputDevice != null)
            {
                _outputDevice.Play();
                _positionTimer.Start();
                _isPaused = false;
            }
        }

        public void Pause()
        {
            if (_outputDevice?.PlaybackState == PlaybackState.Playing)
            {
                _outputDevice.Pause();
                _positionTimer.Stop();
                _isPaused = true;
            }
        }

        public void Stop()
        {
            _positionTimer.Stop();
            _isPaused = false;

            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }

            if (_vorbisReader != null)
            {
                _vorbisReader.Dispose();
                _vorbisReader = null;
            }

            CurrentTrack = null;
        }

        public void TogglePlayPause()
        {
            if (_outputDevice != null && PlaybackState == PlaybackState.Playing)
                Pause();
            else if (_isPaused)
                Play();
        }

        public void Seek(double positionSeconds)
        {
            if (_isOgg && _vorbisReader != null)
            {
                int sampleRate = _vorbisReader.WaveFormat.SampleRate;
                _vorbisReader.Position = (long)(positionSeconds * sampleRate * 2);
            }
            else if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(positionSeconds);
            }
        }

        // ========== Queue Management ==========

        public void SetQueue(List<MusicFile> tracks, int startIndex = 0)
        {
            _playQueue = tracks.ToList();
            _currentIndex = Math.Max(0, Math.Min(startIndex, _playQueue.Count - 1));
            GenerateShuffleOrder();
        }

        public void PlayIndex(int index)
        {
            if (index < 0 || index >= _playQueue.Count) return;
            _currentIndex = index;
            LoadAndPlay(_playQueue[_currentIndex]);
        }

        public void PlayNext()
        {
            if (_playQueue.Count == 0) return;

            switch (CurrentPlayMode)
            {
                case PlayMode.RepeatOne:
                    LoadAndPlay(_playQueue[_currentIndex]);
                    return;

                case PlayMode.Shuffle:
                    _currentIndex = _shuffleOrder[(_shuffleOrder.IndexOf(_currentIndex) + 1) % _playQueue.Count];
                    break;

                case PlayMode.Sequential:
                case PlayMode.RepeatAll:
                default:
                    _currentIndex++;
                    break;
            }

            // Wrap around to the beginning when reaching the end
            if (_currentIndex >= _playQueue.Count)
            {
                _currentIndex = 0;
            }

            LoadAndPlay(_playQueue[_currentIndex]);
        }

        public void PlayPrevious()
        {
            if (_playQueue.Count == 0) return;

            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _playQueue.Count - 1;

            LoadAndPlay(_playQueue[_currentIndex]);
        }

        // ========== FFT Visualization ==========

        public float[] GetFftData()
        {
            if (_isOgg && _vorbisReader != null)
            {
                return ComputeFftFromStream(_vorbisReader);
            }
            else if (_audioFileReader != null)
            {
                return ComputeFftFromSampleProvider(_audioFileReader);
            }
            return new float[FftLength / 2];
        }

        // Read bytes from WaveStream (VorbisWaveReader, etc.)
        private float[] ComputeFftFromStream(WaveStream stream)
        {
            if (stream.Position >= stream.Length) return new float[FftLength / 2];

            try
            {
                // Read bytes from the stream
                byte[] byteBuffer = new byte[FftLength * 4]; // 4 bytes per float sample
                int bytesRead = stream.Read(byteBuffer, 0, byteBuffer.Length);

                // Convert bytes to float samples
                float[] samples = new float[bytesRead / 4];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = BitConverter.ToSingle(byteBuffer, i * 4);
                }

                return ComputeFftFromSamples(samples);
            }
            catch
            {
                return new float[FftLength / 2];
            }
        }

        // Read float samples from ISampleProvider (AudioFileReader)
        private float[] ComputeFftFromSampleProvider(ISampleProvider provider)
        {
            try
            {
                float[] samples = new float[FftLength];
                int samplesRead = provider.Read(samples, 0, FftLength);
                if (samplesRead < FftLength)
                {
                    Array.Clear(samples, samplesRead, FftLength - samplesRead);
                }

                return ComputeFftFromSamples(samples);
            }
            catch
            {
                return new float[FftLength / 2];
            }
        }

        private float[] ComputeFftFromSamples(float[] samples)
        {
            // Apply Hanning window
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (samples.Length - 1))));
            }

            // Fill to FftLength if needed
            float[] padded = new float[FftLength];
            Array.Copy(samples, padded, Math.Min(samples.Length, FftLength));

            // Convert to NAudio.Dsp.Complex array
            var fft = new NAudio.Dsp.Complex[FftLength];
            for (int i = 0; i < FftLength; i++)
            {
                fft[i].X = padded[i];
                fft[i].Y = 0;
            }

            // Run FFT
            NAudio.Dsp.FastFourierTransform.FFT(true, (int)Math.Log(FftLength, 2), fft);

            // Extract magnitude
            var magnitudes = new float[FftLength / 2];
            for (int i = 0; i < FftLength / 2; i++)
            {
                magnitudes[i] = (float)Math.Sqrt(fft[i].X * fft[i].X + fft[i].Y * fft[i].Y);
            }

            return magnitudes;
        }

        // ========== Timer & Event Handlers ==========

        private void OnPositionTimerElapsed(object sender, ElapsedEventArgs e)
        {
            double pos = GetPosition();
            PositionChanged?.Invoke(pos);

            float[] fftData = GetFftData();
            FftDataAvailable?.Invoke(fftData);
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _positionTimer.Stop();
            _isPaused = false;
            PlaybackCompleted?.Invoke();
        }

        // ========== Helpers ==========

        private double GetDuration()
        {
            if (_isOgg && _vorbisReader != null)
                return _vorbisReader.TotalTime.TotalSeconds;
            else if (_audioFileReader != null)
                return _audioFileReader.TotalTime.TotalSeconds;
            return 0;
        }

        private double GetPosition()
        {
            if (_isOgg && _vorbisReader != null)
            {
                int sampleRate = _vorbisReader.WaveFormat.SampleRate;
                return (double)_vorbisReader.Position / (sampleRate * 2);
            }
            else if (_audioFileReader != null)
                return _audioFileReader.CurrentTime.TotalSeconds;
            return 0;
        }

        private void GetMetadata(MusicFile file)
        {
            file.Title = Path.GetFileNameWithoutExtension(file.FilePath);
            file.Artist = "Unknown Artist";
            file.Album = "Unknown Album";

            if (_audioFileReader != null)
                file.Duration = _audioFileReader.TotalTime.TotalSeconds;
        }

        private void GetVorbisMetadata(VorbisWaveReader reader, MusicFile file)
        {
            file.Duration = reader.TotalTime.TotalSeconds;
            file.Title = Path.GetFileNameWithoutExtension(file.FilePath);
            file.Artist = "Unknown Artist";
            file.Album = "Unknown Album";
        }

        private void GenerateShuffleOrder()
        {
            _shuffleOrder = Enumerable.Range(0, _playQueue.Count).OrderBy(x => _random.Next()).ToList();
        }

        public void Dispose()
        {
            Stop();
            _positionTimer?.Dispose();
            _outputDevice?.Dispose();
            _audioFileReader?.Dispose();
            _vorbisReader?.Dispose();
        }
    }
}
