using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        // Added '?' to clear the CS8618 warnings
        private string? _currentFolderId;
        private string? _currentAnimeTitle;

        public FolderPage()
        {
            this.InitializeComponent(); // The compiler will now find this!
            _driveService = new GoogleDriveService();
            EpisodesList.ItemsSource = Episodes;
        }

        public void LoadEpisodes(AnimeItemViewModel anime)
        {
            _currentFolderId = anime.DriveId;
            _currentAnimeTitle = anime.Title;

            AnimeTitleText.Text = anime.Title;

            FetchEpisodesAsync();
        }

        private async void FetchEpisodesAsync()
        {
            if (string.IsNullOrEmpty(_currentFolderId)) return;

            EpisodesLoadingRing.IsActive = true;
            Episodes.Clear();

            try
            {
                var files = await Task.Run(() => _driveService.GetVideoFilesInFolderAsync(_currentFolderId));

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    if (files != null)
                    {
                        int episodeCounter = 1;
                        foreach (var file in files)
                        {
                            // Safety checks to ensure we don't pass nulls
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
                    }
                    EpisodesLoadingRing.IsActive = false;
                });
            }
            catch (System.Exception ex)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    EpisodesLoadingRing.IsActive = false;
                    System.Diagnostics.Debug.WriteLine($"Failed to load episodes: {ex.Message}"); // Clears the CS0168 warning
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
            // Simple logic to go back to the previous page
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
    }
}