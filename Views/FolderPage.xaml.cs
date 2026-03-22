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
                        var hiddenFolders = new[] { "specials", "special", "fanart", "fanarts", "extras", "extra", "extrafanart", "extra fanart" };

                        foreach (var f in folders)
                        {
                            if (f.Id == null || f.Name == null) continue;

                            bool shouldHide = hiddenFolders.Contains(f.Name.ToLower());
                            if (shouldHide) continue;

                            hasContent = true;
                            SubfolderGrid.Visibility = Visibility.Visible;

                            string fullTitle = f.Name.ToLower().Contains(_currentAnimeTitle!.ToLower())
                                ? f.Name
                                : $"{_currentAnimeTitle} {f.Name}";

                            Subfolders.Add(new AnimeItemViewModel
                            {
                                DriveId = f.Id,
                                Title = fullTitle
                            });
                        }

                        _ = FetchSubfolderCoversAsync();
                    }

                    // 2. Process Video Files (Episodes)
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

                            // Scan for decimals (e.g. 1.5), explicitly ignoring 5.1 or 7.1 surround sound tags
                            var matches = System.Text.RegularExpressions.Regex.Matches(file.Name, @"(?<!\d)\d+\.\d+(?!\d)");
                            var validDecimal = matches.Cast<System.Text.RegularExpressions.Match>()
                                                      .FirstOrDefault(m => m.Value != "5.1" && m.Value != "7.1");

                            bool isDecimalEpisode = validDecimal != null;
                            string episodeNumString;

                            if (isDecimalEpisode)
                            {
                                // Keep the exact number formatting found in the file name
                                episodeNumString = validDecimal!.Value;

                                // If the decimal is single-digit (e.g. "1.5"), pad it to "01.5" to match E01 format
                                if (episodeNumString.IndexOf('.') == 1)
                                {
                                    episodeNumString = "0" + episodeNumString;
                                }
                            }
                            else
                            {
                                // Use "D2" to automatically add leading zeros to 1-9 (e.g., 01, 02, 03)
                                episodeNumString = (isOva ? ovaCounter : episodeCounter).ToString("D2");
                            }

                            // FORMAT: "Title - E01" or "Title - OVA01"
                            string prefix = isOva ? "OVA" : "E";
                            string cleanTitle = $"{_currentAnimeTitle} - {prefix}{episodeNumString}";

                            Episodes.Add(new EpisodeItemViewModel
                            {
                                FileId = file.Id,
                                Title = cleanTitle,
                                StreamUrl = file.WebContentLink
                            });

                            // Only increment the counter if it was a normal, whole-number episode!
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

                    // Link the Next Episodes together for the Auto-Play feature
                    if (Episodes.Count > 1)
                    {
                        for (int i = 0; i < Episodes.Count - 1; i++)
                        {
                            Episodes[i].NextEpisode = Episodes[i + 1];
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
                await Task.Delay(400);
            }
        }

        private void SubfolderGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedSubfolder = (AnimeItemViewModel)e.ClickedItem;
            this.Frame.Navigate(typeof(FolderPage), clickedSubfolder);
        }

        private void EpisodesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedEpisode = (EpisodeItemViewModel)e.ClickedItem;
            LoadingOverlay.Visibility = Visibility.Visible;
            this.Frame.Navigate(typeof(PlayerPage), clickedEpisode);
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