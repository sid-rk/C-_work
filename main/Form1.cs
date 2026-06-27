using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WindowsFormsApp1.Controls;
using WindowsFormsApp1.Models;
using WindowsFormsApp1.Services;

namespace WindowsFormsApp1
{
    public partial class Form1 : UIForm
    {
        private AudioPlayerEngine _player;
        private DatabaseService _db;
        private VideoPlayerService _videoPlayer;

        private List<MusicFile> _musicLibrary = new List<MusicFile>();
        private List<Playlist> _playlists = new List<Playlist>();
        private List<string> _videoFiles = new List<string>();
        private int _currentPlaylistId = -1;
        private bool _isDraggingProgress = false;
        private bool _isPlaying = false;
        private bool _isVideoMode = false;

        public Form1()
        {
            InitializeComponent();
            // 基于 SunnyUI 框架 (https://github.com/yhuse/SunnyUI, MIT License) 实现深色主题
            this.Style = UIStyle.Dark;
            this.Text = "MediaMate";
            this.TitleHeight = 36;
            this.ShowIcon = false;
            this.MinimumSize = new Size(960, 680);
            this.Size = new Size(1100, 720);

            _player = new AudioPlayerEngine();
            _db = new DatabaseService();
            _videoPlayer = new VideoPlayerService();

            SubscribePlayerEvents();
            BuildUI();
            LoadMusicLibrary();
            LoadPlaylists();
        }

        private void BuildUI()
        {
            // ===== 顶部工具栏 =====
            var toolStrip = new Panel { Height = 36, Dock = DockStyle.Top, BackColor = Color.FromArgb(48, 48, 48) };

            var btnAdd = new UIButton { Text = "  Add Music", Width = 150, Height = 28, Left = 6, Top = 4 };
            btnAdd.Click += BtnAdd_Click;

            var btnPlaylist = new UIButton { Text = "  New Playlist", Width = 150, Height = 28, Left = 162, Top = 4 };
            btnPlaylist.Click += BtnNewPlaylist_Click;

            var btnMode = new UIButton { Text = "  Seq", Width = 100, Height = 28, Left = 318, Top = 4 };
            btnMode.Click += (s, e) =>
            {
                _player.CurrentPlayMode = (AudioPlayerEngine.PlayMode)(((int)_player.CurrentPlayMode + 1) % 4);
                btnMode.Text = _player.CurrentPlayMode.ToString().Replace("Sequential", "Seq").Replace("RepeatAll", "All").Replace("RepeatOne", "One");
            };

            var btnDelete = new UIButton { Text = "  Remove", Width = 100, Height = 28, Left = 424, Top = 4 };
            btnDelete.Click += BtnRemove_Click;

            var btnVis = new UIButton { Text = "  Spectrum", Width = 120, Height = 28, Left = 530, Top = 4 };
            btnVis.Click += (s, e) => tabControl1.SelectedIndex = tabControl1.SelectedIndex == 0 ? 1 : 0;

            var btnVideo = new UIButton { Text = "  Video", Width = 100, Height = 28, Left = 656, Top = 4 };
            btnVideo.Click += BtnVideo_Click;

            btnCloseVideo = new UIButton { Text = "  Close Vid", Width = 110, Height = 28, Left = 762, Top = 4, Visible = false };
            btnCloseVideo.Click += BtnCloseVideo_Click;

            toolStrip.Controls.AddRange(new Control[] { btnAdd, btnPlaylist, btnMode, btnDelete, btnVis, btnVideo, btnCloseVideo });

            // ===== 底部控制栏 =====
            var controlBar = new Panel { Dock = DockStyle.Bottom, Height = 90, BackColor = Color.FromArgb(35, 35, 35) };

            // 进度条 + 时间
            var progressRow = new Panel { Width = controlBar.Width, Height = 22, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            trackProgress = new TrackBar
            {
                Minimum = 0, Maximum = 10000, TickStyle = TickStyle.None,
                Width = progressRow.Width - 140, Height = 20,
                BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.FromArgb(0, 150, 255),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Left = 4
            };
            trackProgress.MouseDown += TrackProgress_MouseDown;
            trackProgress.MouseUp += (s, e) => _isDraggingProgress = false;

            lblTime = new Label
            {
                Text = "00:00 / 00:00", ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent, Font = new Font("Segoe UI", 9F),
                AutoSize = false, Size = new Size(130, 20),
                Left = trackProgress.Width + 8, Top = 2
            };
            progressRow.Controls.Add(trackProgress);
            progressRow.Controls.Add(lblTime);

            // 播放按钮行
            var btnRow = new Panel { Width = controlBar.Width, Height = 48, Top = 26, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };

            var btnPrev = new UIButton { Text = "⏮", Font = new Font("Segoe UI", 14F), Size = new Size(38, 38), Left = 8, Top = 5 };
            btnPrev.Click += (s, e) => _player.PlayPrevious();
            var btnPlayPause = new UIButton { Text = "▶", Font = new Font("Segoe UI", 16F), Size = new Size(44, 44), Left = 50, Top = 2 };
            btnPlayPause.Click += BtnPlayPause_Click;
            var btnNext = new UIButton { Text = "⏭", Font = new Font("Segoe UI", 14F), Size = new Size(38, 38), Left = 98, Top = 5 };
            btnNext.Click += (s, e) => _player.PlayNext();
            var btnStop = new UIButton { Text = "⏹", Font = new Font("Segoe UI", 14F), Size = new Size(38, 38), Left = 140, Top = 5 };
            btnStop.Click += BtnStop_Click;

            btnRow.Controls.AddRange(new Control[] { btnPrev, btnPlayPause, btnNext, btnStop });

            // 音量
            lblVolume = new Label
            {
                Text = "Vol", ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent, Font = new Font("Segoe UI", 9F),
                Left = btnRow.Width - 140, Top = 14, AutoSize = true
            };
            lblVolume.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            trackVolume = new TrackBar
            {
                Minimum = 0, Maximum = 100, Value = 80, TickStyle = TickStyle.None,
                Width = 100, Height = 20,
                BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.FromArgb(0, 150, 255),
                Left = btnRow.Width - 110, Top = 14, Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            trackVolume.Scroll += TrackVolume_Scroll;

            controlBar.Controls.Add(progressRow);
            controlBar.Controls.Add(btnRow);
            controlBar.Controls.Add(lblVolume);
            controlBar.Controls.Add(trackVolume);

            // ===== 主内容区 =====
            tabControl1 = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };

            listLibrary = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White, BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F), DisplayMember = "DisplayName"
            };
            listLibrary.SelectedIndexChanged += ListLibrary_SelectedIndexChanged;
            tabControl1.TabPages.Add(new TabPage("Library") { BackColor = Color.FromArgb(50, 50, 50) });
            tabControl1.TabPages[0].Controls.Add(listLibrary);

            listPlaylistTracks = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White, BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F), DisplayMember = "DisplayName"
            };
            listPlaylistTracks.DoubleClick += ListPlaylistTracks_DoubleClick;
            tabControl1.TabPages.Add(new TabPage("Playlist") { BackColor = Color.FromArgb(50, 50, 50) });
            tabControl1.TabPages[1].Controls.Add(listPlaylistTracks);

            listVideos = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White, BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F)
            };
            listVideos.DoubleClick += ListVideos_DoubleClick;
            tabControl1.TabPages.Add(new TabPage("Videos") { BackColor = Color.FromArgb(50, 50, 50) });
            tabControl1.TabPages[2].Controls.Add(listVideos);

            listPlaylists = new ListBox
            {
                Dock = DockStyle.Bottom, Height = 80,
                BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.FromArgb(200, 200, 200),
                BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 8F), DisplayMember = "Name"
            };
            listPlaylists.SelectedIndexChanged += ListPlaylists_SelectedIndexChanged;

            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(tabControl1);
            leftPanel.Controls.Add(listPlaylists);

            // 右面板：封面信息 + 频谱
            var rightPanel = new Panel { Dock = DockStyle.Fill };

            picAlbumArt = new PictureBox
            {
                Size = new Size(160, 160), BackColor = Color.FromArgb(45, 45, 45),
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            var bmp = new Bitmap(160, 160);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(45, 45, 45));
                using (var brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                    g.DrawString("♪", new Font("Segoe UI", 48), brush, 50, 40);
            }
            picAlbumArt.Image = bmp;

            lblNowPlaying = new Label
            {
                Text = "No track selected", ForeColor = Color.White,
                BackColor = Color.Transparent, Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true, Width = 300
            };

            lblArtist = new Label
            {
                Text = "", ForeColor = Color.FromArgb(150, 150, 150),
                BackColor = Color.Transparent, Font = new Font("Segoe UI", 9F),
                AutoSize = true, Width = 300
            };

            var infoPanel = new Panel { Height = 180, Dock = DockStyle.Top, BackColor = Color.FromArgb(40, 40, 40) };
            picAlbumArt.Location = new Point(12, 8);
            lblNowPlaying.Location = new Point(188, 30);
            lblArtist.Location = new Point(188, 62);
            infoPanel.Controls.AddRange(new Control[] { picAlbumArt, lblNowPlaying, lblArtist });

            spectrumControl = new SpectrumVisualizer { Dock = DockStyle.Fill };
            videoView = new LibVLCSharp.WinForms.VideoView { Dock = DockStyle.Fill, BackColor = Color.Black, Visible = false };

            var splitContent = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(50, 50, 50)
            };
            splitContent.Panel1.Controls.Add(infoPanel);
            splitContent.Panel2.Controls.Add(spectrumControl);
            splitContent.Panel2.Controls.Add(videoView);
            splitContent.SplitterDistance = 200;

            rightPanel.Controls.Add(splitContent);

            // 主水平分割
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill, SplitterWidth = 4, SplitterDistance = 320,
                BackColor = Color.FromArgb(35, 35, 35)
            };
            mainSplit.Panel1.Controls.Add(leftPanel);
            mainSplit.Panel2.Controls.Add(rightPanel);

            this.Controls.Add(mainSplit);
            this.Controls.Add(toolStrip);
            this.Controls.Add(controlBar);
        }

        private void SubscribePlayerEvents()
        {
            _player.TrackChanged += track => { if (InvokeRequired) Invoke(new Action(() => OnTrackChanged(track))); else OnTrackChanged(track); };
            _player.PositionChanged += pos => { if (!_isDraggingProgress) { if (InvokeRequired) Invoke(new Action(() => UpdatePosition(pos))); else UpdatePosition(pos); } };
            _player.FftDataAvailable += data => { if (InvokeRequired) Invoke(new Action(() => spectrumControl?.UpdateData(data))); else spectrumControl?.UpdateData(data); };
            _player.PlaybackCompleted += OnAudioPlaybackCompleted;
        }

        private void OnAudioPlaybackCompleted()
        {
            if (_isVideoMode) return;
            if (InvokeRequired) Invoke(new Action(() => _player.PlayNext())); else _player.PlayNext();
        }

        private void OnTrackChanged(MusicFile track)
        {
            if (track == null) return;
            lblNowPlaying.Text = track.Title;
            lblArtist.Text = $"{track.Artist} | {track.Album}";
            _isPlaying = true;
        }

        private void UpdatePosition(double pos)
        {
            double dur = _player.Duration;
            if (dur > 0)
            {
                trackProgress.Value = (int)((pos / dur) * 10000);
                lblTime.Text = $"{FormatTime(pos)} / {FormatTime(dur)}";
            }
        }

        private void TrackProgress_MouseDown(object sender, MouseEventArgs e)
        {
            _isDraggingProgress = true;
            double ratio = Math.Max(0, Math.Min(1, e.X / (double)trackProgress.Width));
            trackProgress.Value = (int)(ratio * 10000);
            if (_isVideoMode) _videoPlayer.SetPosition(ratio);
            else _player.Seek(ratio * _player.Duration);
        }

        private void TrackVolume_Scroll(object sender, EventArgs e)
        {
            if (_isVideoMode) _videoPlayer.SetVolume(trackVolume.Value);
            else _player.Volume = trackVolume.Value / 100f;
        }

        private void BtnPlayPause_Click(object sender, EventArgs e)
        {
            if (_isVideoMode) { _videoPlayer.TogglePlayPause(); }
            else { _player.TogglePlayPause(); }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_isVideoMode) ExitVideoMode(); else _player.Stop();
            _isPlaying = false;
            spectrumControl.Reset();
            trackProgress.Value = 0;
            lblTime.Text = "00:00 / 00:00";
        }

        private void BtnVideo_Click(object sender, EventArgs e)
        {
            _player.PlaybackCompleted -= OnAudioPlaybackCompleted;
            _player.Stop();
            _player.SetQueue(new List<MusicFile>(), 0);
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All Files|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    foreach (var f in dlg.FileNames)
                        if (!_videoFiles.Contains(f)) { _videoFiles.Add(f); listVideos.Items.Add(Path.GetFileName(f)); }
                    PlayVideoFile(dlg.FileNames[0]);
                }
            }
        }

        private void ListVideos_DoubleClick(object sender, EventArgs e)
        {
            if (listVideos.SelectedIndex >= 0 && listVideos.SelectedIndex < _videoFiles.Count)
                PlayVideoFile(_videoFiles[listVideos.SelectedIndex]);
        }

        private void BtnCloseVideo_Click(object sender, EventArgs e) => ExitVideoMode();

        private void PlayVideoFile(string path)
        {
            _player.PlaybackCompleted -= OnAudioPlaybackCompleted;
            _player.Stop(); _player.SetQueue(new List<MusicFile>(), 0);
            _isVideoMode = true;
            spectrumControl.Visible = false; videoView.Visible = true; btnCloseVideo.Visible = true;
            trackProgress.Value = 0; lblTime.Text = "00:00 / 00:00";
            videoView.MediaPlayer = _videoPlayer.Player;
            lblNowPlaying.Text = Path.GetFileNameWithoutExtension(path); lblArtist.Text = "Video Mode";
            _videoPlayer.Play(path); _isPlaying = true;
        }

        private void ExitVideoMode()
        {
            _videoPlayer.Stop(); _isVideoMode = false;
            spectrumControl.Visible = true; videoView.Visible = false; videoView.MediaPlayer = null; btnCloseVideo.Visible = false;
            _player.PlaybackCompleted += OnAudioPlaybackCompleted;
            lblNowPlaying.Text = "No track selected"; lblArtist.Text = "";
            _isPlaying = false; spectrumControl.Reset();
            trackProgress.Value = 0; lblTime.Text = "00:00 / 00:00";
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.aac;*.m4a|All Files|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    foreach (var f in dlg.FileNames.Where(File.Exists))
                    {
                        _db.InsertMusicFile(new MusicFile
                        {
                            Title = Path.GetFileNameWithoutExtension(f),
                            Artist = "Unknown Artist",
                            Album = "Unknown Album",
                            FilePath = f,
                            Format = Path.GetExtension(f).TrimStart('.').ToUpper(),
                            FileSize = new FileInfo(f).Length
                        });
                    }
                    LoadMusicLibrary();
                }
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0 && listLibrary.SelectedItem is MusicFile file &&
                MessageBox.Show($"Remove '{file.Title}'?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            { _db.DeleteMusicFile(file.Id); LoadMusicLibrary(); }
        }

        private void BtnNewPlaylist_Click(object sender, EventArgs e)
        {
            string name = ShowInputDialog("Enter playlist name:", "New Playlist");
            if (!string.IsNullOrWhiteSpace(name)) { _db.CreatePlaylist(name); LoadPlaylists(); }
        }

        private void ListLibrary_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listLibrary.SelectedItem is MusicFile f)
            { _player.SetQueue(_musicLibrary, _musicLibrary.IndexOf(f)); _player.PlayIndex(_musicLibrary.IndexOf(f)); }
        }

        private void ListPlaylistTracks_DoubleClick(object sender, EventArgs e)
        {
            if (listPlaylistTracks.SelectedItem is MusicFile f && _currentPlaylistId >= 0)
            {
                var t = _db.GetTracksInPlaylist(_currentPlaylistId);
                _player.SetQueue(t, t.IndexOf(f)); _player.PlayIndex(t.IndexOf(f));
            }
        }

        private void ListPlaylists_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listPlaylists.SelectedItem is Playlist p)
            {
                _currentPlaylistId = p.Id;
                listPlaylistTracks.Items.Clear();
                foreach (var t in _db.GetTracksInPlaylist(p.Id)) listPlaylistTracks.Items.Add(t);
            }
        }

        private void LoadMusicLibrary()
        {
            _musicLibrary = _db.GetAllMusicFiles();
            listLibrary.Items.Clear();
            foreach (var f in _musicLibrary) listLibrary.Items.Add(f);
        }

        private void LoadPlaylists()
        {
            _playlists = _db.GetAllPlaylists();
            listPlaylists.Items.Clear();
            foreach (var p in _playlists) listPlaylists.Items.Add(p);
        }

        private string ShowInputDialog(string prompt, string title)
        {
            var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(350, 140) };
            var l = new Label { Text = prompt, Location = new Point(12, 12), Size = new Size(320, 20) };
            var t = new TextBox { Location = new Point(12, 38), Size = new Size(320, 24), Text = "My Playlist" };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(170, 75), Size = new Size(75, 28) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(255, 75), Size = new Size(75, 28) };
            f.Controls.AddRange(new Control[] { l, t, ok, cancel }); f.AcceptButton = ok; f.CancelButton = cancel;
            return f.ShowDialog(this) == DialogResult.OK ? t.Text : null;
        }

        private string FormatTime(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) return "00:00";
            var ts = TimeSpan.FromSeconds(s);
            return ts.Hours > 0 ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private TabControl tabControl1;
        private ListBox listLibrary, listPlaylistTracks, listPlaylists, listVideos;
        private PictureBox picAlbumArt;
        private Label lblNowPlaying, lblArtist, lblTime, lblVolume;
        private TrackBar trackProgress, trackVolume;
        private SpectrumVisualizer spectrumControl;
        private LibVLCSharp.WinForms.VideoView videoView;
        private UIButton btnCloseVideo;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _player?.Dispose(); _videoPlayer?.Dispose(); base.OnFormClosing(e);
        }
    }
}
