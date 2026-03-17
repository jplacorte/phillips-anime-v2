using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        // We now have TWO collections!
        public ObservableCollection<EpisodeItemViewModel> Episodes { get; } = new();
        public ObservableCollection<AnimeItemViewModel> Subfolders { get; } = new();

        private string? _currentFolderId;
        private string? _currentAnimeTitle;

        public FolderPage()
        {
            this.InitializeComponent();
            _driveService = new GoogleDriveService();

            EpisodesList.ItemsSource = Episodes;
            SubfolderGrid.ItemsSource = Subfolders;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is AnimeItemViewModel selectedAnime)
            {
                LoadEpisodes(selectedAnime);
            }
        }

        public void LoadEpisodes(AnimeItemViewModel anime)
        {
            _currentFolderId = anime.DriveId;
            _currentAnimeTitle = anime.Title;
            AnimeTitleText.Text = anime.Title;
            FetchContentsAsync();
        }

        private async void FetchContentsAsync()
        {
            if (string.IsNullOrEmpty(_currentFolderId)) return;

            EpisodesLoadingRing.IsActive = true;
            ErrorText.Visibility = Visibility.Collapsed;
            Episodes.Clear();
            Subfolders.Clear();

            try
            {
                // Fetch BOTH Subfolders and Video Files concurrently
                var folderTask = Task.Run(() => _driveService.GetSubFoldersAsync(_currentFolderId));
                var fileTask = Task.Run(() => _driveService.GetVideoFilesInFolderAsync(_currentFolderId));

                await Task.WhenAll(folderTask, fileTask);

                var folders = folderTask.Result;
                var files = fileTask.Result;

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    bool hasContent = false;

                    // 1. Process Subfolders (Seasons)
                    if (folders != null && folders.Count > 0)
                    {
                        // Define the list of folder names to hide
                        var hiddenFolders = new[] { "specials", "special", "fanart", "fanarts", "extras", "extra" };

                        foreach (var f in folders)
                        {
                            if (f.Id == null || f.Name == null) continue;

                            // Check if the current folder's name matches any in the hidden list
                            bool shouldHide = hiddenFolders.Contains(f.Name.ToLower());

                            if (shouldHide)
                            {
                                continue; // Skip this folder and move to the next one
                            }

                            hasContent = true;
                            SubfolderGrid.Visibility = Visibility.Visible;

                            // If the subfolder is just called "Season 2", prefix it with the Anime Name so Jikan can find it!
                            string fullTitle = f.Name.ToLower().Contains(_currentAnimeTitle!.ToLower())
                                ? f.Name
                                : $"{_currentAnimeTitle} {f.Name}";

                            Subfolders.Add(new AnimeItemViewModel
                            {
                                DriveId = f.Id,
                                Title = fullTitle
                            });
                        }

                        // Fire off the background cover fetcher for the seasons
                        _ = FetchSubfolderCoversAsync();
                    }

                    // 2. Process Video Files (Episodes)
                    if (files != null && files.Count > 0)
                    {
                        hasContent = true;
                        EpisodesList.Visibility = Visibility.Visible;

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
                    }

                    if (!hasContent)
                    {
                        ErrorText.Text = "This folder is empty. Verify the files exist and are shared publicly ('Anyone with the link').";
                        ErrorText.Visibility = Visibility.Visible;
                    }

                    EpisodesLoadingRing.IsActive = false;
                });
            }
            catch (System.Exception ex)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    EpisodesLoadingRing.IsActive = false;
                    ErrorText.Text = $"Failed to load contents: {ex.Message}";
                    ErrorText.Visibility = Visibility.Visible;
                });
            }
        }

        private async Task FetchSubfolderCoversAsync()
        {
            foreach (var sub in Subfolders)
            {
                try
                {
                    var coverUrl = await JikanService.GetCoverUrlAsync(sub.Title);
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        this.DispatcherQueue.TryEnqueue(() => { sub.CoverUrl = coverUrl; });
                    }
                }
                catch { /* Ignore */ }
                await Task.Delay(400); // Respect Jikan rate limit
            }
        }

        // NEW: Handles clicking a Season/Subfolder
        private void SubfolderGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedSubfolder = (AnimeItemViewModel)e.ClickedItem;
            // Recursion! We navigate to a NEW FolderPage, passing the subfolder's data
            this.Frame.Navigate(typeof(FolderPage), clickedSubfolder);
        }

        // Handles clicking a Video File
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