using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using System.Linq;

namespace AnimeStreamer.Services
{
    public class GoogleDriveService
    {
        private readonly DriveService _service;
        private readonly GoogleCredential _credential; // Store the credential
        private const string RootAnimeFolderId = "1xvceiMAE4rYz3VGlVTNcbdYKkBApWkp2";

        // ADD YOUR TEMP FOLDER ID HERE
        public const string TempFolderId = "1gHt2ufjQUvZsGUczXafua3bYihH1IjQd";

        public GoogleDriveService()
        {
            #pragma warning disable CS0618
            _credential = GoogleCredential.FromFile("service-account.json")
                                          .CreateScoped(DriveService.ScopeConstants.DriveReadonly); // DriveReadonly is safer!
            #pragma warning restore CS0618 

            _service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "AnimeStreamer"
            });
        }

        public async Task<string> GetAccessTokenAsync()
        {
            // This fetches a fresh Bearer token from Google
            var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            return token;
        }

        public async Task<IList<DriveFile>> GetAnimeFoldersAsync()
        {
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{RootAnimeFolderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.OrderBy = "name";
            var result = await request.ExecuteAsync();
            return result.Files ?? new List<DriveFile>();
        }

        public async Task<IList<DriveFile>> GetVideoFilesInFolderAsync(string folderId)
        {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType contains 'video/' and trashed = false";
            request.Fields = "files(id, name, webContentLink)";
            request.OrderBy = "name";
            var result = await request.ExecuteAsync();
            return result.Files ?? new List<DriveFile>();
        }

        public async Task<IList<DriveFile>> GetSubFoldersAsync(string folderId)
        {
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.OrderBy = "name";
            var result = await request.ExecuteAsync();
            return result.Files ?? new List<DriveFile>();
        }

        public async Task<string> CopyFileToTempAsync(string originalFileId)
        {
            var fileMetadata = new DriveFile
            {
                Name = $"TEMP_STREAM_{Guid.NewGuid()}",
                Parents = new List<string> { TempFolderId }
            };

            var request = _service.Files.Copy(fileMetadata, originalFileId);
            var copiedFile = await request.ExecuteAsync();

            var perm = new Google.Apis.Drive.v3.Data.Permission { Type = "anyone", Role = "reader" };
            await _service.Permissions.Create(perm, copiedFile.Id).ExecuteAsync();

            return copiedFile.Id;
        }

        public async Task DeleteFilePermanentlyAsync(string fileId)
        {
            try
            {
                await _service.Files.Delete(fileId).ExecuteAsync();
                System.Diagnostics.Debug.WriteLine($"[Cleanup] Permanently deleted temp file: {fileId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cleanup Error] Failed to delete {fileId}: {ex.Message}");
            }
        }
    }
}