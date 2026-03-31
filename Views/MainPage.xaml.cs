using AnimeStreamer.Services;
using StreamApp.Services;
using StreamApp.ViewModels;
using System.Collections.ObjectModel;

namespace AnimeStreamer.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly GoogleDriveService _driveService = new GoogleDriveService();
        public ObservableCollection<AnimeItemViewModel> AnimeLibrary { get; } = new();

        public MainPage()
        {
            this.InitializeComponent();

            // CRITICAL: Tells WinUI to keep this page alive when navigating away!
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            AnimeGrid.ItemsSource = AnimeLibrary;
            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // CRITICAL: Only fetch from Drive if the library is empty.
            if (AnimeLibrary.Count == 0)
            {
                _ = LoadAnimes();
            }
        }

        private async Task LoadAnimes()
        {
            try
            {
                LoadingRing.IsActive = true;
                ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

                var folders = await Task.Run(() => _driveService.GetAnimeFoldersAsync());

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (folders == null || folders.Count == 0)
                        {
                            LoadingRing.IsActive = false;
                            ErrorText.Text = "Connected to Drive, but no sub-folders were found.";
                            ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                            return;
                        }

                        foreach (var folder in folders)
                        {
                            AnimeLibrary.Add(new AnimeItemViewModel
                            {
                                DriveId = folder.Id ?? "UNKNOWN_ID",
                                Title = folder.Name ?? "Unknown Folder"
                            });
                        }

                        LoadingRing.IsActive = false;
                        _ = FetchAllCoversAsync();
                    }
                    catch (System.Exception uiEx)
                    {
                        // Fix for line 68 warning
                        System.Diagnostics.Debug.WriteLine($"[UI Load Error] {uiEx.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                // Fix for line 71 warning
                System.Diagnostics.Debug.WriteLine($"[Drive API Error] {ex.Message}");
            }
        }

        private async Task FetchAllCoversAsync()
        {
            foreach (var anime in AnimeLibrary)
            {
                try
                {
                    // If cached, this returns instantly. If not, JikanService handles the delay!
                    var coverUrl = await JikanService.GetCoverUrlAsync(anime.Title);
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        this.DispatcherQueue.TryEnqueue(() => { anime.CoverUrl = coverUrl; });
                    }
                }
                catch { /* Ignore */ }

                // REMOVED THE HARDCODED TASK.DELAY HERE!
            }
        }

        private void AnimeGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedAnime = (AnimeItemViewModel)e.ClickedItem;
            try
            {
                bool navigated = false;
                if (this.Frame != null) navigated = this.Frame.Navigate(typeof(FolderPage), clickedAnime);

                if (!navigated)
                {
                    var root = App.MainWindow?.Content as Frame;
                    if (root != null) navigated = root.Navigate(typeof(FolderPage), clickedAnime);
                }
            }
            catch { /* handle ex */ }
        }
    }
}