using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class Syncronizer
    {
        public SyncConfig Config { get; private set; }

        private Dictionary<string, Google.Apis.Drive.v3.Data.File> _fileCache = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();
        private IEnumerable<string> _scopes = new []{ DriveService.Scope.DriveReadonly };
        private UserCredential _credential = null;
        private DriveService _service = null;

        public Syncronizer(SyncConfig syncConfig)
        {
            Config = syncConfig;
        }

        public void Go()
        {
            Authorize();
            CreateService();

            var totalFiles = 0;
            var filePage = GetNextFilePage();
            while (filePage?.Files != null && filePage.Files.Count > 0)
            {
                // Loop the files in this page
                foreach (var file in filePage.Files)
                {
                    totalFiles++;
                    var fullPath = GetFileFolderPath(file);
                    var shouldCopy = IsMatchForGooglePathSync(fullPath);
                    Console.ForegroundColor = shouldCopy ? ConsoleColor.Green : ConsoleColor.White;
                    Console.WriteLine($"{totalFiles} - {fullPath} ({file.Id})");
                }

                // Get the next page or stop the loop
                if(string.IsNullOrWhiteSpace(filePage.NextPageToken))
                {
                    filePage = null;
                }
                else
                {
                    filePage = GetNextFilePage(filePage.NextPageToken);
                }
            }
        }

        private bool IsMatchForGooglePathSync(string filePath)
        {
            if(string.IsNullOrWhiteSpace(Config.GoogleDriveFolder))
            {
                return true;
            }

            return filePath.StartsWith(Config.GoogleDriveFolder);
        }

        private FileList GetNextFilePage(string nextToken = null)
        {
            FilesResource.ListRequest listRequest = _service.Files.List();
            listRequest.PageSize = 50;
            listRequest.Fields = "nextPageToken, files(id, name, parents)";
            listRequest.PageToken = nextToken;
            var listResponse = listRequest.Execute();
            return listResponse;
        }

        private void Authorize()
        {
            if(_credential != null)
            {
                throw new InvalidOperationException("Authorization cannot be called twice");
            }

            using (var stream = new FileStream(Config.CredentialsPath, FileMode.Open, FileAccess.Read))
            {
                _credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    _scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(Config.TokenPath, true)).Result;

                Console.WriteLine("Credential file saved to: " + Config.TokenPath);
            }
        }

        public string GetFileFolderPath(Google.Apis.Drive.v3.Data.File file)
        {
            var name = file.Name;

            if (file?.Parents == null || file.Parents.Count() == 0)
            {
                return name;
            }

            var path = new List<string>();

            while (true)
            {
                var parent = GetParent(file.Parents[0]);

                // Stop when we find the root dir
                if (parent.Parents == null || parent.Parents.Count() == 0)
                {
                    break;
                }

                path.Insert(0, parent.Name);
                file = parent;
            }
            path.Add(name);
            return path.Aggregate((current, next) => Path.Combine(current, next));
        }

        public Google.Apis.Drive.v3.Data.File GetParent(string id)
        {
            // Parent Files are like folders - most folders have multiple child files
            // This means many parents will be duplicate requests, cache them to avoid redundant API calls to Google
            if(_fileCache.ContainsKey(id))
            {
                return _fileCache[id];
            }

            // New file, get it from Google Drive
            var request = _service.Files.Get(id);
            request.Fields = "name,parents";
            var parent = request.Execute();

            // Cache file for re-use
            _fileCache.Add(id, parent);

            return parent;
        }

        private void CreateService()
        {
            if(_service != null)
            {
                throw new InvalidOperationException("Cannot call CreateService twice");
            }

            // Create Drive API service.
            _service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = Config.ApplicationName,
            });
        }
    }
}
