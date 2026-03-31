using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace AnimeStreamer.Services
{
    public class GoogleDriveService
    {
        private readonly DriveService _service;
        private readonly GoogleCredential _credential;
        private const string RootAnimeFolderId = "1xvceiMAE4rYz3VGlVTNcbdYKkBApWkp2";

        public const string TempFolderId = "1gHt2ufjQUvZsGUczXafua3bYihH1IjQd";

        public GoogleDriveService()
        {
            string appFolder = System.AppContext.BaseDirectory;
            string keyPath = System.IO.Path.Combine(appFolder, "service-account.json");
#pragma warning disable CS0618
            _credential = GoogleCredential.FromFile(keyPath)
                                          .CreateScoped(DriveService.ScopeConstants.DriveReadonly);
#pragma warning restore CS0618

            _service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "AnimeStreamer"
            });
        }

        public async Task<string> GetAccessTokenAsync()
        {
            var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            return token;
        }

        public async Task<IList<DriveFile>> GetAnimeFoldersAsync()
        {
            var allFolders = new List<DriveFile>();
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{RootAnimeFolderId}' in parents and trashed = false";

            // CRITICAL FIX: Add nextPageToken and increase PageSize to 1000
            request.Fields = "nextPageToken, files(id, name)";
            request.PageSize = 1000;

            do
            {
                var response = await request.ExecuteAsync();
                if (response.Files != null)
                {
                    allFolders.AddRange(response.Files);
                }
                request.PageToken = response.NextPageToken;

            } while (!string.IsNullOrEmpty(request.PageToken));

            return allFolders.OrderBy(f => f.Name).ToList();
        }

        public async Task<IList<DriveFile>> GetVideoFilesInFolderAsync(string folderId)
        {
            var allFiles = new List<DriveFile>();
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and mimeType contains 'video/' and trashed = false";

            // CRITICAL FIX: Add nextPageToken and increase PageSize to 1000
            request.Fields = "nextPageToken, files(id, name, webContentLink)";
            request.PageSize = 1000;

            do
            {
                var response = await request.ExecuteAsync();
                if (response.Files != null)
                {
                    allFiles.AddRange(response.Files);
                }
                request.PageToken = response.NextPageToken;

            } while (!string.IsNullOrEmpty(request.PageToken));

            return allFiles.OrderBy(f => f.Name).ToList();
        }

        public async Task<IList<DriveFile>> GetSubFoldersAsync(string folderId)
        {
            var allSubfolders = new List<DriveFile>();
            var request = _service.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and '{folderId}' in parents and trashed = false";

            // CRITICAL FIX: Add nextPageToken and increase PageSize to 1000
            request.Fields = "nextPageToken, files(id, name)";
            request.PageSize = 1000;

            do
            {
                var response = await request.ExecuteAsync();
                if (response.Files != null)
                {
                    allSubfolders.AddRange(response.Files);
                }
                request.PageToken = response.NextPageToken;

            } while (!string.IsNullOrEmpty(request.PageToken));

            return allSubfolders.OrderBy(f => f.Name).ToList();
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