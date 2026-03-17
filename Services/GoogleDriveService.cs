using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace StreamApp.Services
{
    public class GoogleDriveService
    {
        private readonly DriveService _service;

        // PASTE YOUR ROOT ANIME FOLDER ID HERE
        private const string RootAnimeFolderId = "1xvceiMAE4rYz3VGlVTNcbdYKkBApWkp2";

        public GoogleDriveService()
        {
            _service = new DriveService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCKXqpZC1dqAjUu8WvfDTuWBCE5-jCL0V0",
                ApplicationName = "AnimeStreamer"
            });
        }

        // METHOD 1: Fetches the main Anime Folders (For MainPage)
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetAnimeFoldersAsync()
        {
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{RootAnimeFolderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.OrderBy = "name";

            var result = await request.ExecuteAsync();
            return result.Files;
        }

        // METHOD 2: Fetches the video files inside a folder (For FolderPage Episodes)
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetVideoFilesInFolderAsync(string folderId)
        {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name, webContentLink)";
            request.OrderBy = "name";

            var result = await request.ExecuteAsync();
            return result.Files;
        }

        // METHOD 3: Fetches subfolders inside a parent folder (For FolderPage Seasons)
        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetSubFoldersAsync(string folderId)
        {
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.OrderBy = "name";

            var result = await request.ExecuteAsync();
            return result.Files;
        }
    }
}