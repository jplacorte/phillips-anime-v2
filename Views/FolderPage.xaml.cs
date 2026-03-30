using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamApp.Services;
using AnimeStreamer.Services;
using StreamApp.ViewModels;
using System.Linq;

namespace AnimeStreamer.Views
{
    public sealed partial class FolderPage : Page
    {
        private readonly GoogleDriveService _driveService = new GoogleDriveService();

        public ObservableCollection<EpisodeItemViewModel> Episodes { get; } = new();
        public ObservableCollection<AnimeItemViewModel> Subfolders { get; } = new();

        private string? _currentFolderId;
        private string? _currentAnimeTitle;

        public FolderPage()
        {
            this.InitializeComponent();

            // CRITICAL: Keep episodes loaded when returning from PlayerPage
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            EpisodesList.ItemsSource = Episodes;
            SubfolderGrid.ItemsSource = Subfolders;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // CRITICAL: If we are navigating "Back" from the PlayerPage, DO NOT re-fetch!
            if (e.NavigationMode == NavigationMode.Back) return;

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
                        var hiddenFolders = new[] { "specials", "special", "fanart", "fanarts", "extras", "extra", "extrafanart", "extra fanart" };

                        foreach (var f in folders)
                        {
                            if (f.Id == null || f.Name == null) continue;
                            if (hiddenFolders.Contains(f.Name.ToLower())) continue;

                            hasContent = true;
                            SubfolderGrid.Visibility = Visibility.Visible;

                            // --- THE FIX: Just use the exact folder name from Google Drive! ---
                            string fullTitle = f.Name.Trim();

                            Subfolders.Add(new AnimeItemViewModel
                            {
                                DriveId = f.Id,
                                Title = fullTitle
                            });
                        }

                        _ = FetchSubfolderCoversAsync();
                    }

                    // 2. Process Video Files
                    if (files != null && files.Count > 0)
                    {
                        hasContent = true;
                        EpisodesList.Visibility = Visibility.Visible;

                        var firstNormalEpisode = files.FirstOrDefault(f => f.Name != null && !f.Name.ToLower().Contains("ova"));
                        int episodeCounter = (firstNormalEpisode != null && firstNormalEpisode.Name.Contains("00")) ? 0 : 1;
                        int ovaCounter = 1;

                        foreach (var file in files)
                        {
                            if (file.Name == null || file.Id == null) continue;

                            bool isOva = file.Name.ToLower().Contains("ova");
                            var matches = System.Text.RegularExpressions.Regex.Matches(file.Name, @"(?<!\d)\d+\.\d+(?!\d)");
                            var validDecimal = matches.Cast<System.Text.RegularExpressions.Match>().FirstOrDefault(m => m.Value != "5.1" && m.Value != "7.1");

                            bool isDecimalEpisode = validDecimal != null;
                            string episodeNumString;

                            if (isDecimalEpisode)
                            {
                                episodeNumString = validDecimal!.Value;
                                if (episodeNumString.IndexOf('.') == 1) episodeNumString = "0" + episodeNumString;
                            }
                            else
                            {
                                episodeNumString = (isOva ? ovaCounter : episodeCounter).ToString("D2");
                            }

                            string prefix = isOva ? "OVA" : "E";
                            Episodes.Add(new EpisodeItemViewModel
                            {
                                FileId = file.Id,
                                Title = $"{_currentAnimeTitle} - {prefix}{episodeNumString}",
                                StreamUrl = file.WebContentLink
                            });

                            if (!isDecimalEpisode)
                            {
                                if (isOva) ovaCounter++;
                                else episodeCounter++;
                            }
                        }
                    }

                    if (!hasContent)
                    {
                        ErrorText.Text = "This folder is empty. Verify the files exist and are shared publicly ('Anyone with the link').";
                        ErrorText.Visibility = Visibility.Visible;
                    }

                    if (Episodes.Count > 1)
                    {
                        for (int i = 0; i < Episodes.Count - 1; i++) Episodes[i].NextEpisode = Episodes[i + 1];
                    }

                    EpisodesLoadingRing.IsActive = false;
                });
            }
            catch { /* handle ex */ }
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

                // REMOVED THE HARDCODED TASK.DELAY HERE!
            }
        }

        private void SubfolderGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedSubfolder = (AnimeItemViewModel)e.ClickedItem;
            this.Frame.Navigate(typeof(FolderPage), clickedSubfolder);
        }

        // Note: We added the 'async' keyword to the method signature!
        private async void EpisodesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedEpisode = (EpisodeItemViewModel)e.ClickedItem;

            // 1. Turn on the loading screen
            LoadingOverlay.Visibility = Visibility.Visible;

            // 2. CRITICAL FIX: Yield to the UI thread for 50ms so it actually draws the overlay!
            await Task.Delay(50);

            // 3. Now trigger the heavy navigation to the PlayerPage
            this.Frame.Navigate(typeof(PlayerPage), clickedEpisode);

            // 4. Hide the overlay immediately after navigation finishes, 
            // so it is clean and ready when the user clicks the "Back" button later!
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack) this.Frame.GoBack();
        }
    }
}