using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing; // Make sure to add this at the top!
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
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new Window(); // Or new MainWindow() depending on your template

            // 1. Set the Title
            MainWindow.Title = "Phillips Anime";

            // 2. Set the App Icon
            var hwnd = WindowNative.GetWindowHandle(MainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Note: Make sure you converted icon.png to icon.ico in your Assets folder!
            appWindow.SetIcon("Assets\\icon.ico");

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
