using AnimeStreamer.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Navigation;
using WinRT.Interop;

namespace AnimeStreamer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        // Expose the main Window so pages can find the root Frame for navigation when needed.
        public static Window? MainWindow { get; private set; }

        // Singleton DriveService — shared across all pages to reuse the HTTP pool and token cache.
        public static GoogleDriveService DriveService { get; } = new GoogleDriveService();

        private Window window = Window.Current;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new Window();

            // 1. Set the Title
            MainWindow.Title = "Phillips Anime";

            // 2. Set the App Icon
            var hwnd = WindowNative.GetWindowHandle(MainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.SetIcon("Assets\\icon.ico");

            // --- NEW BLOCK: Force Dark Mode Title Bar & Window Control Buttons ---
            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;

                // Create the exact #0F0F0F color to match your app's background
                var darkColor = Windows.UI.Color.FromArgb(255, 15, 15, 15);
                var hoverColor = Windows.UI.Color.FromArgb(255, 40, 40, 40); // Slightly lighter for hovering
                var whiteColor = Microsoft.UI.Colors.White;
                var grayColor = Microsoft.UI.Colors.DimGray;

                // Color the Main Title Bar
                titleBar.BackgroundColor = darkColor;
                titleBar.ForegroundColor = whiteColor;
                titleBar.InactiveBackgroundColor = darkColor;
                titleBar.InactiveForegroundColor = grayColor;

                // Color the Maximize, Minimize, and Close Buttons
                titleBar.ButtonBackgroundColor = darkColor;
                titleBar.ButtonForegroundColor = whiteColor;

                titleBar.ButtonHoverBackgroundColor = hoverColor;
                titleBar.ButtonHoverForegroundColor = whiteColor;

                titleBar.ButtonPressedBackgroundColor = darkColor;
                titleBar.ButtonPressedForegroundColor = whiteColor;

                titleBar.ButtonInactiveBackgroundColor = darkColor;
                titleBar.ButtonInactiveForegroundColor = grayColor;
            }
            // --- END NEW BLOCK ---

            // Initialize the main frame and navigate
            Frame rootFrame = new Frame();
            rootFrame.Navigate(typeof(Views.MainPage));
            MainWindow.Content = rootFrame;

            MainWindow.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}