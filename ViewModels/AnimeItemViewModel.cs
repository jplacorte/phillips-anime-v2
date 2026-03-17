using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreamApp.ViewModels
{
    public class AnimeItemViewModel : INotifyPropertyChanged
    {
        // Safe defaults to prevent XAML x:Bind crashes
        public string DriveId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        // The new Folder Icon as the default fallback (with the semicolon!)
        private string _coverUrl = "ms-appx:///Assets/FolderIcon.jpg";

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