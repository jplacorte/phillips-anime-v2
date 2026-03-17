using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAnimes();
        }

       private async void LoadAnimes()
{
    try
    {
        LoadingRing.IsActive = true;
        ErrorText.Visibility = Visibility.Collapsed;

        // 1. Fetch from Google Drive on a background thread
        var folders = await Task.Run(() => _driveService.GetAnimeFoldersAsync());

        // 2. Jump to the UI thread
        this.DispatcherQueue.TryEnqueue(() =>
        {
            // NEW: A try-catch INSIDE the UI thread to stop silent XAML crashes
            try
            {
                if (folders == null || folders.Count == 0)
                {
                    LoadingRing.IsActive = false;
                    ErrorText.Text = "Connected to Drive, but no sub-folders were found.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                foreach (var folder in folders)
                {
                    AnimeLibrary.Add(new AnimeItemViewModel
                    {
                        // Use ?? to guarantee we never pass a null value to the required properties
                        DriveId = folder.Id ?? "UNKNOWN_ID",
                        Title = folder.Name ?? "Unknown Folder"
                    });
                }

                LoadingRing.IsActive = false;

                // 3. Start fetching covers
                _ = FetchAllCoversAsync();
            }
            catch (System.Exception uiEx)
            {
                // If the UI engine crashes while adding items, catch it here!
                LoadingRing.IsActive = false;
                ErrorText.Text = $"UI Rendering Error:\n{uiEx.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        });
    }
    catch (System.Exception ex)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            LoadingRing.IsActive = false;
            ErrorText.Text = $"API Error:\n{ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
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
                        // Safely update the image property on the UI thread
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            anime.CoverUrl = coverUrl;
                        });
                    }
                }
                catch
                {
                    // Ignore individual image download failures so the loop continues
                }

                // Strict rate limit for Jikan API
                await Task.Delay(400);
            }
        }

        private async void AnimeGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedAnime = (AnimeItemViewModel)e.ClickedItem;
            System.Diagnostics.Debug.WriteLine($"Clicked: {clickedAnime.Title}");

            // Navigate immediately when an item is clicked

            // Navigate to the folder page and pass the DriveId as the parameter.
            // Try a few strategies to ensure navigation works in different host setups.
            try
            {
                bool navigated = false;

                if (this.Frame != null)
                {
                    navigated = this.Frame.Navigate(typeof(FolderPage), clickedAnime.DriveId);
                }

                if (!navigated)
                {
                    // Fallback: try to find the root Frame from App.MainWindow.Content
                    try
                    {
                        var root = App.MainWindow?.Content as Frame;
                        if (root != null)
                        {
                            navigated = root.Navigate(typeof(FolderPage), clickedAnime.DriveId);
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
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex}");
                ErrorText.Text = "Navigation error: " + ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
