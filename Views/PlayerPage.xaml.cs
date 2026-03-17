using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LibVLCSharp.Shared;
using StreamApp.ViewModels;
using System.Diagnostics;

namespace AnimeStreamer.Views
{
    public sealed partial class PlayerPage : Page
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private string? _streamUrl;

        public PlayerPage()
        {
            this.InitializeComponent();

            // Wait until the page is fully drawn on the screen to start VLC
            this.Loaded += PlayerPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is EpisodeItemViewModel episode)
            {
                string apiKey = "AIzaSyCWj6XgWH-JTZghH1e_GiOu4FqP9CxhjEk";

                // BYPASS THE GOOGLE VIRUS SCAN
                // Added acknowledgeAbuse=true to force the download of large files!
                _streamUrl = $"https://www.googleapis.com/drive/v3/files/{episode.FileId}?alt=media&key={apiKey}&acknowledgeAbuse=true";
            }
        }

        private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Initialize the VLC Core engine
            Core.Initialize();

            _libVLC = new LibVLC();

            // 2. Route VLC's internal logs to Visual Studio's Output window! 
            // If it fails to play, check the VS Output tab to see exactly what Google Drive said.
            _libVLC.Log += (s, ev) => Debug.WriteLine($"[VLC]: {ev.Message}");

            // 3. Setup the Player
            _mediaPlayer = new MediaPlayer(_libVLC);
            VideoView.MediaPlayer = _mediaPlayer;

            // 4. Turn off the Loading Ring the exact millisecond the video starts rendering
            _mediaPlayer.Playing += (s, args) =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    BufferingRing.IsActive = false;
                    BufferingRing.Visibility = Visibility.Collapsed;
                });
            };

            // 5. Play the stream
            if (!string.IsNullOrEmpty(_streamUrl))
            {
                var media = new Media(_libVLC, _streamUrl, FromType.FromLocation);

                // Increase network caching to 3 seconds for heavy MKV files to prevent stuttering
                media.AddOption(":network-caching=3000");

                _mediaPlayer.Play(media);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // CRITICAL: Stop playback and flush memory when you leave the page!
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Dispose();
            }

            if (_libVLC != null)
            {
                _libVLC.Dispose();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
    }
}