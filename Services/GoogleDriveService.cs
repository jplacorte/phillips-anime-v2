using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
// Alias to fix the "Ambiguous Reference" error
using DriveFile = Google.Apis.Drive.v3.Data.File;
using System.Linq;
using System.Diagnostics;

namespace AnimeStreamer.Services
{
    public class GoogleDriveService
    {
        private readonly DriveService _service;
        // Your current Root ID from the source
        private const string RootAnimeFolderId = "1xvceiMAE4rYz3VGlVTNcbdYKkBApWkp2";

        public GoogleDriveService()
        {
            _service = new DriveService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCKXqpZC1dqAjUu8WvfDTuWBCE5-jCL0V0", //
                ApplicationName = "AnimeStreamer"
            });
        }

        public async Task<IList<DriveFile>> GetAnimeFoldersAsync()
        {
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{RootAnimeFolderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.OrderBy = "name";
            var result = await request.ExecuteAsync();
            return result.Files;
        }

        public async Task<IList<DriveFile>> GetVideoFilesInFolderAsync(string folderId)
        {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name, webContentLink)";
            request.OrderBy = "name";
            var result = await request.ExecuteAsync();
            return result.Files;
        }

        public async Task<IList<DriveFile>> GetSubFoldersAsync(string folderId)
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