using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreamApp.ViewModels
{
    public class AnimeItemViewModel : INotifyPropertyChanged
    {
        // Provide default values so XAML-generated code can instantiate this view model
        // without needing to set required properties at compile time.
        public string DriveId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        // Initialize with a default local asset so x:Bind NEVER sees a null value!
        private string _coverUrl = "ms-appx:///Assets/StoreLogo.png";
        public string CoverUrl
        {
            get => _coverUrl;
            set
            {
                _coverUrl = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}