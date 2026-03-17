using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LibVLCSharp.Shared;
using StreamApp.ViewModels;

namespace AnimeStreamer.Views
{
    public sealed partial class PlayerPage : Page
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;

        public PlayerPage()
        {
            this.InitializeComponent();

            // 1. Initialize the VLC Core engine
            Core.Initialize();

            // 2. Setup the Player
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            VideoView.MediaPlayer = _mediaPlayer;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is EpisodeItemViewModel episode)
            {
                // BYPASS THE GOOGLE DRIVE VIRUS SCAN PAGE
                // We construct a direct alt=media URL using your API Key
                string apiKey = "AIzaSyCWj6XgWH-JTZghH1e_GiOu4FqP9CxhjEk";
                string directStreamUrl = $"https://www.googleapis.com/drive/v3/files/{episode.FileId}?alt=media&key={apiKey}";

                // Load the URL and instantly start playing
                var media = new Media(_libVLC, directStreamUrl, FromType.FromLocation);
                _mediaPlayer.Play(media);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // CRITICAL: Stop playback and flush memory when you leave the page!
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _libVLC.Dispose();
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