using AnimeStreamer.Services;
using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using StreamApp.ViewModels;

namespace AnimeStreamer.Views
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
    public sealed partial class PlayerPage : Page
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private bool _isUserSeeking = false;
        private static bool _isVlcInitialized = false;

        // CRITICAL FIX: Tracker to prevent WinUI async event looping for chapters
        private int _currentChapterIndex = -1;
        private bool _isProgrammaticChapterChange = false;

        private EpisodeItemViewModel? _currentEpisode;
        private LocalProxyServer? _proxyServer;
        // Use the shared singleton — avoids duplicate HTTP pools and reads service-account.json once
        private readonly GoogleDriveService _driveService = App.DriveService;

        private DispatcherTimer _idleTimer;
        private Windows.Foundation.Point _lastPointerPosition;
        private bool _isMuted = false;
        private int _previousVolume = 100;

        // ESAdded debounce: VLC fires one event per track (video, audio, subtitle).
        // We cancel-and-reschedule on every fire so we only populate AFTER the burst settles,
        // guaranteeing all tracks are registered before we build the UI lists.
        private CancellationTokenSource? _trackPopulateCts;

        public class TrackItem { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

        public PlayerPage()
        {
            if (!_isVlcInitialized)
            {
                Core.Initialize();
                _isVlcInitialized = true;
            }

            this.InitializeComponent();
            VideoView.Initialized += VideoView_Initialized;

            _idleTimer = new DispatcherTimer();
            _idleTimer.Interval = TimeSpan.FromSeconds(3);
            _idleTimer.Tick += IdleTimer_Tick;

            TimelineSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(TimelineSlider_PointerPressed), true);
            TimelineSlider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(TimelineSlider_PointerCaptureLost), true);
            TimelineSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(TimelineSlider_PointerCaptureLost), true);

            TimelineSlider.ThumbToolTipValueConverter = new TimeFormatConverter();
        }

        public class TimeFormatConverter : Microsoft.UI.Xaml.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, string language)
            {
                if (value is double ms)
                {
                    var ts = TimeSpan.FromMilliseconds(ms);
                    return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
                }
                return "00:00";
            }
            public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var backStack = this.Frame.BackStack;
            for (int i = backStack.Count - 1; i >= 0; i--)
            {
                if (backStack[i].SourcePageType == typeof(PlayerPage))
                {
                    backStack.RemoveAt(i);
                }
            }

            if (e.Parameter is EpisodeItemViewModel episode)
            {
                _currentEpisode = episode;
                EpisodeTitleText.Text = episode.Title;

                if (_currentEpisode.NextEpisode == null)
                {
                    NextButton.IsEnabled = false;
                    NextButton.Opacity = 0.5;
                }
                else
                {
                    NextButton.IsEnabled = true;
                    NextButton.Opacity = 1.0;
                }
            }
        }

        private void Accelerator_PlayPause(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { PlayPause(); args.Handled = true; }
        private void Accelerator_Fullscreen(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { ToggleFullscreen(); args.Handled = true; }
        private void Accelerator_Mute(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { ToggleMute(); args.Handled = true; }
        private void Accelerator_Forward(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (_mediaPlayer != null) _mediaPlayer.Time += 10000; args.Handled = true; }
        private void Accelerator_Backward(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (_mediaPlayer != null) _mediaPlayer.Time -= 10000; args.Handled = true; }
        private void Accelerator_VolUp(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { VolumeSlider.Value += 5; args.Handled = true; }
        private void Accelerator_VolDown(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { VolumeSlider.Value -= 5; args.Handled = true; }
        private void Accelerator_Next(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (NextButton.IsEnabled) NextButton_Click(this, new RoutedEventArgs()); args.Handled = true; }
        private void Accelerator_Escape(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var appWindow = GetAppWindow();
            if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
            }
            args.Handled = true;
        }

        private void PlayPause()
        {
            if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Pause();
            else _mediaPlayer?.Play();
        }

        private void ToggleMute()
        {
            if (_mediaPlayer == null) return;

            _isMuted = !_isMuted;
            if (_isMuted)
            {
                _previousVolume = VolumeSlider.Value > 0 ? (int)VolumeSlider.Value : 100;
                VolumeSlider.Value = 0;
                _mediaPlayer.Mute = true;
                MuteButton.Content = "\xE74F";
            }
            else
            {
                VolumeSlider.Value = _previousVolume;
                _mediaPlayer.Mute = false;
                MuteButton.Content = "\xE767";
            }
        }

        private Microsoft.UI.Windowing.AppWindow GetAppWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        }

        private void ToggleFullscreen()
        {
            var appWindow = GetAppWindow();
            if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
            else
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var currentPosition = e.GetCurrentPoint(this).Position;
            if (Math.Abs(currentPosition.X - _lastPointerPosition.X) < 2 && Math.Abs(currentPosition.Y - _lastPointerPosition.Y) < 2) return;

            _lastPointerPosition = currentPosition;
            UIOverlay.Visibility = Visibility.Visible;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            _idleTimer.Stop(); _idleTimer.Start();
        }

        private void IdleTimer_Tick(object? sender, object e)
        {
            _idleTimer.Stop();
            if (!_isUserSeeking)
            {
                UIOverlay.Visibility = Visibility.Collapsed;
                var hiddenCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                hiddenCursor.Dispose();
                ProtectedCursor = hiddenCursor;
            }
        }

#pragma warning disable CS8622
        private async void VideoView_Initialized(object? sender, InitializedEventArgs e)
        {
            var options = e.SwapChainOptions.ToList();

            // GPU Hardware Decode: d3d11va uses the GPU's dedicated video decode engine.
            // It shares the same D3D11 device WinUI's SwapChain already set up,
            // so there is NO conflict. VLC silently falls back to software if unsupported.
            options.Add("--avcodec-hw=d3d11va");

            // Prevent VLC from auto-loading external subtitle files that might
            // override the embedded styled ASS/SSA tracks inside the MKV.
            options.Add("--no-sub-autodetect-file");

            _libVLC = new LibVLC(options.ToArray());
            _mediaPlayer = new MediaPlayer(_libVLC);

            VideoView.MediaPlayer = _mediaPlayer;

            _mediaPlayer.TimeChanged += (s, args) => DispatcherQueue.TryEnqueue(() => UpdatePosition(args.Time));
            _mediaPlayer.LengthChanged += (s, args) => DispatcherQueue.TryEnqueue(() => TimelineSlider.Maximum = args.Length);

            _mediaPlayer.Playing += (s, args) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    BufferingRing.IsActive = false;
                    BufferingRing.Visibility = Visibility.Collapsed;
                    _idleTimer.Start();
                    PlayPauseButton.Content = "\xE769";
                });
            };

            // ESAdded fires once per elementary stream: video, then audio, then each subtitle.
            // Populating on the very first fire means audio/subtitle tracks don't exist yet.
            // The debounce pattern: cancel the previous scheduled call and start a new 700ms
            // countdown on every ESAdded. We only actually populate after the burst settles.
            _mediaPlayer.ESAdded += (s, args) =>
            {
                _trackPopulateCts?.Cancel();
                _trackPopulateCts = new CancellationTokenSource();
                var cts = _trackPopulateCts;

                Task.Delay(700, cts.Token).ContinueWith(_ =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            PopulateTracks();
                            PopulateChapters();
                        });
                    }
                }, TaskContinuationOptions.NotOnCanceled);
            };

            _mediaPlayer.Paused += (s, args) => DispatcherQueue.TryEnqueue(() =>
            {
                PlayPauseButton.Content = "\xE768";
            });

            _mediaPlayer.EncounteredError += (s, args) => DispatcherQueue.TryEnqueue(() =>
            {
                BufferingRing.IsActive = false;
                BufferingRing.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
            });

            // CRITICAL FIX: Track chapter changes before updating UI
            _mediaPlayer.ChapterChanged += (s, args) => {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (ChapterCombo.ItemsSource != null && args.Chapter < ChapterCombo.Items.Count)
                    {
                        _isProgrammaticChapterChange = true;
                        _currentChapterIndex = args.Chapter;
                        ChapterCombo.SelectedIndex = args.Chapter;
                        _isProgrammaticChapterChange = false;
                    }
                });
            };

            if (_currentEpisode != null)
            {
                try
                {
                    _proxyServer = new LocalProxyServer();
                    _proxyServer.Start();

                    string token = await _driveService.GetAccessTokenAsync();
                    if (_mediaPlayer == null || _libVLC == null) return;

                    string proxyUrl = $"http://localhost:{_proxyServer.Port}/video.mkv?id={_currentEpisode.FileId}&token={token}";

                    var media = new Media(_libVLC, proxyUrl, FromType.FromLocation);

                    // 3 seconds of pre-buffer is safer for high-bitrate 1080p over Google Drive.
                    // http-reconnect lets VLC silently resume if Drive drops the connection mid-stream.
                    // clock-jitter=0 tells VLC to trust the file's clock and not try to drift-correct,
                    // which avoids phantom A/V desync on well-encoded files.
                    media.AddOption(":network-caching=3000");
                    media.AddOption(":http-reconnect=1");
                    media.AddOption(":clock-jitter=0");

                    _mediaPlayer.Play(media);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FATAL] Proxy stream failed: {ex.Message}"); }
            }
        }
#pragma warning restore CS8622

        private void PopulateChapters()
        {
            try
            {
                if (_mediaPlayer == null) return;

                // THE FIX: If chapters are already loaded for this video, do NOT rebuild the list!
                // Rebuilding it mid-stream causes WinUI to reset the selection and trigger a fake seek.
                if (ChapterCombo.ItemsSource != null) return;

                if (_mediaPlayer.TitleCount > 0)
                {
                    int currentTitle = _mediaPlayer.Title > -1 ? _mediaPlayer.Title : 0;
                    var chapters = _mediaPlayer.ChapterDescription(currentTitle);

                    if (chapters != null && chapters.Length > 0)
                    {
                        var chapterList = chapters.Select((c, index) => new TrackItem
                        {
                            Id = index,
                            Name = string.IsNullOrEmpty(c.Name) ? $"Chapter {index + 1}" : c.Name
                        }).ToList();

                        ChapterCombo.ItemsSource = chapterList;
                        ChapterPanel.Visibility = Visibility.Visible;

                        // FIX: Wrap the initial assignment with the flag
                        _isProgrammaticChapterChange = true;
                        _currentChapterIndex = _mediaPlayer.Chapter > -1 ? _mediaPlayer.Chapter : 0;
                        ChapterCombo.SelectedIndex = _currentChapterIndex;
                        _isProgrammaticChapterChange = false;
                    }
                    else
                    {
                        ChapterPanel.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    ChapterPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Chapters] Failed to load chapters: {ex.Message}");
                ChapterCombo.Visibility = Visibility.Collapsed;
            }
        }

        private void ChapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // FIX: Ignore programmatic UI updates
            if (_isProgrammaticChapterChange) return;

            if (ChapterCombo.SelectedItem is TrackItem track && _mediaPlayer != null)
            {
                // DOUBLE CHECK: Ensure the UI index AND VLC's native index do not match what was selected
                if (track.Id != _currentChapterIndex && track.Id != _mediaPlayer.Chapter)
                {
                    try
                    {
                        if (_mediaPlayer.State == VLCState.Playing || _mediaPlayer.State == VLCState.Paused)
                        {
                            _currentChapterIndex = track.Id;
                            _mediaPlayer.Chapter = track.Id;
                        }
                    }
                    catch (ArgumentException) { /* Safely ignored */ }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VLC] Error: {ex.Message}"); }
                }
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => PlayPause();
        private void MuteButton_Click(object sender, RoutedEventArgs e) => ToggleMute();
        private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
        private void VideoView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ToggleFullscreen();

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEpisode?.NextEpisode != null)
            {
                this.Frame.Navigate(typeof(PlayerPage), _currentEpisode.NextEpisode);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var appWindow = GetAppWindow();
            if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
            }
            this.Frame.GoBack();
        }

        private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = (int)e.NewValue;
                if (e.NewValue > 0 && _isMuted)
                {
                    _isMuted = false;
                    _mediaPlayer.Mute = false;
                    MuteButton.Content = "\xE767";
                }
                else if (e.NewValue == 0)
                {
                    _isMuted = true;
                    _mediaPlayer.Mute = true;
                    MuteButton.Content = "\xE74F";
                }
            }
        }

        private void UpdatePosition(long currentTime)
        {
            if (!_isUserSeeking && _mediaPlayer != null)
            {
                TimelineSlider.Value = currentTime;
                CurrentTimeText.Text = FormatTime(currentTime);
                TotalTimeText.Text = FormatTime(_mediaPlayer.Length);
            }
        }

        private string FormatTime(long ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
        }

        private void TimelineSlider_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (TimelineSlider.Maximum > 0)
            {
                var point = e.GetCurrentPoint(TimelineSlider).Position;
                double ratio = Math.Clamp(point.X / TimelineSlider.ActualWidth, 0, 1);
                SliderToolTip.Content = FormatTime((long)(ratio * TimelineSlider.Maximum));
            }
        }

        private void TimelineSlider_PointerPressed(object sender, PointerRoutedEventArgs e) { _isUserSeeking = true; }

        private void TimelineSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (_isUserSeeking && _mediaPlayer != null) _mediaPlayer.Time = (long)TimelineSlider.Value;
            _isUserSeeking = false;
        }

        private void PopulateTracks()
        {
            if (_mediaPlayer == null) return;

            // THE FIX: Prevent rebuilding mid-stream to avoid stuttering
            if (AudioTrackCombo.ItemsSource != null) return;

            var audioTracks = _mediaPlayer.AudioTrackDescription.Select(t => new TrackItem { Id = t.Id, Name = t.Name }).ToList();
            var subTracks = _mediaPlayer.SpuDescription.Select(t => new TrackItem { Id = t.Id, Name = t.Name }).ToList();

            AudioTrackCombo.ItemsSource = audioTracks;
            SubtitleTrackCombo.ItemsSource = subTracks;

            AudioTrackCombo.SelectedItem = audioTracks.FirstOrDefault(t => t.Id == _mediaPlayer.AudioTrack);
            SubtitleTrackCombo.SelectedItem = subTracks.FirstOrDefault(t => t.Id == _mediaPlayer.Spu);
        }

        private void AudioTrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AudioTrackCombo.SelectedItem is TrackItem track && _mediaPlayer != null) _mediaPlayer.SetAudioTrack(track.Id);
        }

        private void SubtitleTrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubtitleTrackCombo.SelectedItem is TrackItem track && _mediaPlayer != null) _mediaPlayer.SetSpu(track.Id);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _idleTimer.Stop();
            _trackPopulateCts?.Cancel();
            _trackPopulateCts?.Dispose();
            _trackPopulateCts = null;
            if (VideoView != null) VideoView.MediaPlayer = null;
            if (_mediaPlayer != null) { _mediaPlayer.Stop(); _mediaPlayer.Dispose(); _mediaPlayer = null; }
            if (_libVLC != null) { _libVLC.Dispose(); _libVLC = null; }
            if (_proxyServer != null) { _proxyServer.Stop(); _proxyServer.Dispose(); _proxyServer = null; }
        }
    }
}