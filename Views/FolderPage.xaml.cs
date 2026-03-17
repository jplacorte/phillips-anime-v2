using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// 1. ADD THIS NAMESPACE FOR NAVIGATION:
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamApp.Services;
using StreamApp.ViewModels;

namespace AnimeStreamer.Views
{
    public sealed partial class FolderPage : Page
    {
        private readonly GoogleDriveService _driveService;
        public ObservableCollection<EpisodeItemViewModel> Episodes { get; } = new();

        private string? _currentFolderId;
        private string? _currentAnimeTitle;

        public FolderPage()
        {
            this.InitializeComponent();
            _driveService = new GoogleDriveService();
            EpisodesList.ItemsSource = Episodes;
        }

        // 2. ADD THIS METHOD TO CATCH THE INCOMING DATA:
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Check if the data passed during navigation is our AnimeItemViewModel
            if (e.Parameter is AnimeItemViewModel selectedAnime)
            {
                // Feed it directly into your loading logic!
                LoadEpisodes(selectedAnime);
            }
        }

        public void LoadEpisodes(AnimeItemViewModel anime)
        {
            _currentFolderId = anime.DriveId;
            _currentAnimeTitle = anime.Title;

            // This will change the text from "Anime Title" to the real name!
            AnimeTitleText.Text = anime.Title;

            FetchEpisodesAsync();
        }

        private async void FetchEpisodesAsync()
        {
            if (string.IsNullOrEmpty(_currentFolderId)) return;

            EpisodesLoadingRing.IsActive = true;
            ErrorText.Visibility = Visibility.Collapsed; // Hide any previous errors
            Episodes.Clear();

            try
            {
                var files = await Task.Run(() => _driveService.GetVideoFilesInFolderAsync(_currentFolderId));

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    // CHECK: Did Google Drive return an empty list?
                    if (files == null || files.Count == 0)
                    {
                        EpisodesLoadingRing.IsActive = false;
                        ErrorText.Text = "No video files found inside this folder. Verify the files exist and are shared publicly ('Anyone with the link').";
                        ErrorText.Visibility = Visibility.Visible;
                        return;
                    }

                    int episodeCounter = 1;
                    foreach (var file in files)
                    {
                        if (file.Name == null || file.Id == null) continue;

                        bool isOva = file.Name.ToLower().Contains("ova");
                        string cleanTitle = EpisodeNameParser.FormatEpisodeName(_currentAnimeTitle ?? "Unknown", episodeCounter, isOva);

                        Episodes.Add(new EpisodeItemViewModel
                        {
                            FileId = file.Id,
                            Title = cleanTitle,
                            StreamUrl = file.WebContentLink
                        });

                        if (!isOva) episodeCounter++;
                    }

                    EpisodesLoadingRing.IsActive = false;
                });
            }
            catch (System.Exception ex)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    EpisodesLoadingRing.IsActive = false;
                    // Print the exact API crash to the screen
                    ErrorText.Text = $"Failed to load episodes: {ex.Message}";
                    ErrorText.Visibility = Visibility.Visible;
                });
            }
        }

        private void EpisodesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedEpisode = (EpisodeItemViewModel)e.ClickedItem;
            System.Diagnostics.Debug.WriteLine($"Selected Episode: {clickedEpisode.StreamUrl}");
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