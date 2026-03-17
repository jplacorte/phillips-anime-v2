using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using LibVLCSharp.Shared;
using StreamApp.ViewModels;
using System;
using System.Linq;

namespace AnimeStreamer.Views
{
    public sealed partial class PlayerPage : Page
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private string? _streamUrl;
        private bool _isUserSeeking = false;

        public PlayerPage()
        {
            this.InitializeComponent();
            this.Loaded += PlayerPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is EpisodeItemViewModel episode)
            {
                string apiKey = "AIzaSyCWj6XgWH-JTZghH1e_GiOu4FqP9CxhjEk";
                _streamUrl = $"https://www.googleapis.com/drive/v3/files/{episode.FileId}?alt=media&key={apiKey}&acknowledgeAbuse=true";
            }
        }

        private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
        {
            Core.Initialize();
            _libVLC = new LibVLC("--verbose=2");
            _mediaPlayer = new MediaPlayer(_libVLC);
            VideoView.MediaPlayer = _mediaPlayer;

            // Update UI events
            _mediaPlayer.TimeChanged += (s, args) => DispatcherQueue.TryEnqueue(() => UpdatePosition(args.Time));
            _mediaPlayer.LengthChanged += (s, args) => DispatcherQueue.TryEnqueue(() => TimelineSlider.Maximum = args.Length);

            _mediaPlayer.Playing += (s, args) => DispatcherQueue.TryEnqueue(() => {
                BufferingRing.IsActive = false;
                BufferingRing.Visibility = Visibility.Collapsed;
                PopulateTracks();
            });

            if (!string.IsNullOrEmpty(_streamUrl))
            {
                var media = new Media(_libVLC, _streamUrl, FromType.FromLocation);
                media.AddOption(":network-caching=3000");
                media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _mediaPlayer.Play(media);
            }
        }

        private void UpdatePosition(long currentTime)
        {
            if (!_isUserSeeking && _mediaPlayer != null)
            {
                TimelineSlider.Value = currentTime;
                TimeText.Text = $"{FormatTime(currentTime)} / {FormatTime(_mediaPlayer.Length)}";
            }
        }

        private string FormatTime(long ms)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            return t.ToString(@"mm\:ss");
        }

        private void PopulateTracks()
        {
            if (_mediaPlayer == null) return;
            AudioTrackCombo.ItemsSource = _mediaPlayer.AudioTrackDescription.Select(t => new { t.Id, t.Name }).ToList();
            SubtitleTrackCombo.ItemsSource = _mediaPlayer.SpuDescription.Select(t => new { t.Id, t.Name }).ToList();
        }

        // --- Event Handlers for XAML ---
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Pause();
            else _mediaPlayer?.Play();
        }

        private void TimelineSlider_PointerPressed(object sender, PointerRoutedEventArgs e) => _isUserSeeking = true;

        private void TimelineSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_mediaPlayer != null) _mediaPlayer.Time = (long)TimelineSlider.Value;
            _isUserSeeking = false;
        }

        private void AudioTrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AudioTrackCombo.SelectedItem is dynamic track && _mediaPlayer != null)
                _mediaPlayer.SetAudioTrack(track.Id);
        }

        private void SubtitleTrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubtitleTrackCombo.SelectedItem is dynamic track && _mediaPlayer != null)
                _mediaPlayer.SetSpu(track.Id);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}