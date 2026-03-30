using Windows.Storage;

namespace AnimeStreamer.Services
{
    public static class ImageCacheService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string CacheFolderName = "AnimeCovers";

        static ImageCacheService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        // Helper method to ensure filenames are always generated exactly the same way
        private static string GetSafeFileName(string identifier)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            string safeName = string.Join("_", identifier.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return $"{safeName}.jpg";
        }

        // --- NEW FEATURE: Check the hard drive without downloading anything ---
        public static async Task<string?> GetCachedImagePathIfExistsAsync(string animeIdentifier)
        {
            try
            {
                string safeFileName = GetSafeFileName(animeIdentifier);
                StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
                StorageFolder cacheFolder = await localCacheFolder.CreateFolderAsync(CacheFolderName, CreationCollisionOption.OpenIfExists);

                // If the file is already on the disk, return its path instantly!
                if (await cacheFolder.TryGetItemAsync(safeFileName) is StorageFile existingFile)
                {
                    return existingFile.Path;
                }
            }
            catch { /* Ignore errors and fallback to internet */ }

            return null; // Return null if the image hasn't been downloaded yet
        }

        public static async Task<string> GetCachedImageAsync(string animeIdentifier, string webUrl)
        {
            if (string.IsNullOrEmpty(webUrl)) return webUrl;

            try
            {
                string safeFileName = GetSafeFileName(animeIdentifier);
                StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
                StorageFolder cacheFolder = await localCacheFolder.CreateFolderAsync(CacheFolderName, CreationCollisionOption.OpenIfExists);

                if (await cacheFolder.TryGetItemAsync(safeFileName) is StorageFile existingFile)
                {
                    return existingFile.Path;
                }

                // Download and save the image using FileIO
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(webUrl);
                StorageFile newFile = await cacheFolder.CreateFileAsync(safeFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBytesAsync(newFile, imageBytes);

                return newFile.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageCache] Error caching image: {ex.Message}");
                return webUrl;
            }
        }
    }
}