using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamApp.Services;
using StreamApp.ViewModels;

namespace AnimeStreamer.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly GoogleDriveService _driveService;
        public ObservableCollection<AnimeItemViewModel> AnimeLibrary { get; } = new();

        public MainPage()
        {
            this.InitializeComponent();

            _driveService = new GoogleDriveService();
            AnimeGrid.ItemsSource = AnimeLibrary;

            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Safe way to start loading data after UI is ready
            _ = LoadAnimes();
        }

        private async Task LoadAnimes()
        {
            try
            {
                // Reset UI State safely on UI thread
                LoadingRing.IsActive = true;
                ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

                // 1. Heavy background work (fetch from Drive)
                var folders = await Task.Run(() => _driveService.GetAnimeFoldersAsync());

                // 2. Synchronous update jump BACK to UI thread (No async/await here!)
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

                        // populate the collection instantly
                        foreach (var folder in folders)
                        {
                            // Use ?? coalescing to guarantee we pass non-null values to required properties
                            AnimeLibrary.Add(new AnimeItemViewModel
                            {
                                DriveId = folder.Id ?? "UNKNOWN_ID",
                                Title = folder.Name ?? "Unknown Folder"
                            });
                        }

                        LoadingRing.IsActive = false;

                        // 3. Start fetching images independently, rate-limited
                        _ = FetchAllCoversAsync();
                    }
                    catch (System.Exception uiEx)
                    {
                        // Catch crashes occurring during UI population (e.g., null model fields)
                        LoadingRing.IsActive = false;
                        ErrorText.Text = $"UI Rendering Error:\n{uiEx.Message}";
                        ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    }
                });
            }
            catch (System.Exception ex)
            {
                // Heavy failure on background thread. Marshal error message back to UI.
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingRing.IsActive = false;
                    ErrorText.Text = $"Google Drive API Error:\n{ex.Message}";
                    ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                });
            }
        }

        private async Task FetchAllCoversAsync()
        {
            foreach (var anime in AnimeLibrary)
            {
                try
                {
                    var coverUrl = await JikanService.GetCoverUrlAsync(anime.Title);
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        // Marshalling necessary here too
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            anime.CoverUrl = coverUrl;
                        });
                    }
                }
                catch
                {
                    // If one cover fails, don't crash the loop
                }

                // Strict Jikan API rate limit
                await Task.Delay(400);
            }
        }

        private void AnimeGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedAnime = (AnimeItemViewModel)e.ClickedItem;
            System.Diagnostics.Debug.WriteLine($"Clicked: {clickedAnime.Title}");

            try
            {
                bool navigated = false;

                if (this.Frame != null)
                {
                    // Passing the entire object here
                    navigated = this.Frame.Navigate(typeof(FolderPage), clickedAnime);
                }

                if (!navigated)
                {
                    try
                    {
                        var root = App.MainWindow?.Content as Frame;
                        if (root != null)
                        {
                            navigated = root.Navigate(typeof(FolderPage), clickedAnime);
                        }
                    }
                    catch { /* ignore fallback failures */ }
                }

                if (!navigated)
                {
                    ErrorText.Text = "Navigation failed: could not find a Frame host.";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
            catch (System.Exception ex) // <-- This is the missing catch block!
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex}");
                ErrorText.Text = "Navigation error: " + ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}