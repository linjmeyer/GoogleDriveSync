using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
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
using System.Xml;
// Use Google and System.IO.File's as different class names to avoid need the entire namespace every usage
using GFile = Google.Apis.Drive.v3.Data.File;
using LFile = System.IO.File;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class Syncronizer
    {
        public SyncConfig Config { get; private set; }

        private Dictionary<string, GFile> _fileCache = new Dictionary<string, GFile>();
        private List<(GFile, Exception)> _erroredFiles = new List<(GFile, Exception)>();
        private IEnumerable<string> _scopes = new []{ DriveService.Scope.DriveReadonly };
        private UserCredential _credential = null;
        private DriveService _service = null;
        private string _validDestinationPath;

        public Syncronizer(SyncConfig syncConfig)
        {
            Config = syncConfig;
        }

        public void Go()
        {
            Authorize();
            CreateService();
            SanatizeDestinationPath();

            var totalFiles = 0;
            var filePage = GetNextFilePage();
            while (filePage?.Files != null && filePage.Files.Count > 0)
            {
                // Loop the files in this page
                foreach (var gFile in filePage.Files)
                {
                    totalFiles++;
                    var fullPath = GetFileFolderPath(gFile);
                    var shouldCopy = IsMatchForGooglePathSync(fullPath);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"New File #{totalFiles} \"{fullPath}\"");
                    if (shouldCopy)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine("   Should be copied");
                        var localPath = $"{Config.DestinationPath}\\{fullPath}";
                        if(LFile.Exists(localPath))
                        {
                            Console.WriteLine("   File exists");
                            var lFileInfo = new FileInfo(localPath);
                            if(lFileInfo.LastWriteTimeUtc <= (gFile.ModifiedTime ?? gFile.CreatedTime))
                            {
                                Console.WriteLine($"   Google file is newer, downloading to: {localPath}");
                                DownloadFile(gFile, localPath);
                            }
                        }
                        else
                        {
                            // File doesn't exist, download it
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"   File does not exist, downloading to: {localPath}");
                            DownloadFile(gFile, localPath);
                        }
                    }
                    
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

        private void SanatizeDestinationPath()
        {
            var sanitizedDestination = SanatizePath(Config.DestinationPath);
            if(sanitizedDestination != Config.DestinationPath)
            {
                WriteLineError($"Warning: Invalid Destination path.  Will use \"{sanitizedDestination}\" instead");
            }
        }

        private void WriteLineError(string write)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(write);
            Console.ForegroundColor = originalColor;
        }

        private string SanatizePath(string path)
        {
            var invalidChars = Path.GetInvalidPathChars();
            var onlyValidChars = path.Where(c => !invalidChars.Contains(c)).ToArray();
            return new string(onlyValidChars);
        }

        private void DownloadFile(GFile file, string destinationPath)
        {
            var sanitizedDestination = SanatizePath(destinationPath);
            if(sanitizedDestination != destinationPath)
            {
                WriteLineError($"Warning: Invalid Destination path.  Will use \"{sanitizedDestination}\" instead");
            }
            // Create the directorys (if they dont exist) to avoid exception when making file
            var folderPath = Path.GetDirectoryName(destinationPath);
            Directory.CreateDirectory(folderPath);
            // Create the file in a temp location to avoid overwriting the file and corrupting if the download is broken
            var tempFile = destinationPath + ".temp";
            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    var request = _service.Files.Get(file.Id);
                    // Add a handler which will be notified on progress changes.
                    // It will notify on each chunk download and when the
                    // download is completed or failed.
                    request.MediaDownloader.ProgressChanged +=
                    (IDownloadProgress progress) =>
                    {
                        switch (progress.Status)
                        {
                            case DownloadStatus.Downloading:
                                {
                                    string percent;
                                    if (file.Size.HasValue) {
                                        percent = $"...{((100 * progress.BytesDownloaded) / file.Size.Value)}%";
                                    }
                                    else
                                    {
                                        percent = "...";
                                    }

                                    Console.WriteLine("   " + percent);
                                    break;
                                }
                            case DownloadStatus.Completed:
                                {
                                    Console.WriteLine("   Download complete.");
                                    break;
                                }
                            case DownloadStatus.Failed:
                                {
                                    Console.WriteLine("   Download failed.");
                                    break;
                                }
                        }
                    };
                    request.DownloadWithStatus(stream);
                    stream.Dispose();
                    // Finished downloading, delete the original and rename the temp to the original
                    LFile.Delete(destinationPath);
                    LFile.Move(tempFile, destinationPath);
                }
            }
            catch (Exception exception)
            {
                WriteLineError($"Error: {typeof(Exception)} ");
                WriteLineError(exception.Message);
                if(file != null)
                {
                    _erroredFiles.Add((file, exception));
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
            listRequest.Fields = "nextPageToken, files(id, name, parents, size)";
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

        public string GetFileFolderPath(GFile file)
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

        public GFile GetParent(string id)
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
