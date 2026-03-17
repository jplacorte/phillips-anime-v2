using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace StreamApp.Services
{
    public class GoogleDriveService
    {
        private readonly DriveService _service;
        private const string RootAnimeFolderId = "1xvceiMAE4rYz3VGlVTNcbdYKkBApWkp2"; // Make sure your ID is here!

        public GoogleDriveService()
        {
            _service = new DriveService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCKXqpZC1dqAjUu8WvfDTuWBCE5-jCL0V0", // Your API Key
                ApplicationName = "AnimeStreamer"
            });
        }

        // Method 1: Fetches the main Anime Folders (This is what MainPage needs)
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetAnimeFoldersAsync()
        {
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{RootAnimeFolderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.OrderBy = "name";

            var result = await request.ExecuteAsync();

            // FIXED: Remove dangerous (List<File>) cast and return raw IList
            return result.Files;
        }

        // Method 2: Fetches the video files INSIDE a clicked folder (This is what FolderPage needs)
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetVideoFilesInFolderAsync(string folderId)
        {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name, webContentLink)";
            request.OrderBy = "name";

            var result = await request.ExecuteAsync();
            return result.Files;
        }
    }
}