using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Use Google and System.IO.File's as different class names to avoid need the entire namespace every usage
using GFile = Google.Apis.Drive.v3.Data.File;
using LFile = System.IO.File;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class Syncronizer
    {
        public SyncConfig Config { get; private set; }
        public DateTime? StartTime { get; private set; }
        public DateTime? EndTime { get; private set; }

        private Dictionary<string, GFile> _fileCache = new Dictionary<string, GFile>();
        private List<DownloadableFile> _filesToDownload = new List<DownloadableFile>();
        private IEnumerable<string> _scopes = new []{ DriveService.Scope.DriveReadonly };
        private UserCredential _credential = null;
        private DriveService _service = null;
        private ILogger<Syncronizer> _logger = new NullLogger<Syncronizer>(); // By default use NullLogger so we don't have to check the logger every time we want to use it (it could be null, it's optional)

        public Syncronizer(SyncConfig syncConfig, ILogger<Syncronizer> logger)
        {
            Config = syncConfig;
            _logger = logger;
        }

        public async Task Go()
        {
            StartTime = DateTime.Now;
            Authorize();
            CreateService();
            SanatizeDestinationPath();
            // Start finding files to sync and track progress
            var resultsBuilder = FindFilesToSync();
            var results = await SyncFiles(resultsBuilder);
            // Once finished display a summary of the results
            EndTime = DateTime.Now;
            DisplayResults(results);
        }

        private SyncResultsBuilder FindFilesToSync()
        {
            // Track any errors in results
            var results = new SyncResultsBuilder();

            _logger.LogInformation($"Finding files in folder with id {Config.GoogleDriveFolderId}");
            var totalFiles = 0;
            var filePage = GetNextGoogleFilePage();
            while (filePage?.Files != null && filePage.Files.Count > 0)
            {
                // Loop the files in this page
                foreach (var gFile in filePage.Files)
                {
                    totalFiles++;
                    var fullPath = GetFileFolderPath(gFile);

                    _logger.LogInformation($"New File #{totalFiles} \"{fullPath}\"");

                    var localPath = $"{Config.DestinationPath}\\{fullPath}";
                    var downloadFile = new DownloadableFile(gFile, localPath);

                    try
                    {
                        if (LFile.Exists(localPath))
                        {
                            _logger.LogInformation("   File exists");
                            if (Config.ForceDownloads)
                            {
                                _logger.LogInformation($"   Force downloads enabled, will download to: {localPath}");
                                _filesToDownload.Add(downloadFile);
                                continue;
                            }

                            var lFileInfo = new FileInfo(localPath);
                            if (lFileInfo.LastWriteTimeUtc <= (gFile.ModifiedTime ?? gFile.CreatedTime))
                            {
                                _logger.LogInformation($"   Google file is newer, will download to: {localPath}");
                                _filesToDownload.Add(downloadFile);
                            }
                        }
                        else
                        {
                            // File doesn't exist, download it
                            _logger.LogInformation($"   File does not exist, downloading to: {localPath}");
                            _filesToDownload.Add(downloadFile);
                        }
                    }
                    catch (Exception exception)
                    {
                        var error = downloadFile.ToErroredFile(exception);
                        results.ErroredFiles.Add(error);
                    }
                }

                // Get the next page or stop the loop
                if (string.IsNullOrWhiteSpace(filePage.NextPageToken))
                {
                    filePage = null;
                }
                else
                {
                    filePage = GetNextGoogleFilePage(filePage.NextPageToken);
                }
            }

            return results;
        }

        private async Task<SyncResults> SyncFiles(SyncResultsBuilder resultsBuilder)
        {
            _logger.LogInformation($"Sync files starting - {_filesToDownload.Count} files to be downloaded");

            // To increase the download speed we will download multiple files at once in pages
            var page = GetNextDownloadPage();
            while (page.downloads != null && page.downloads.Any())
            {
                _logger.LogInformation($"Starting download for {page.downloads.Count()} files");
                var downloaders = new List<Task>();
                foreach (var download in page.downloads)
                {
                    downloaders.Add(Task.Run(() =>
                    {
                        DownloadFile(download, resultsBuilder);
                    }));
                }
                await Task.WhenAll(downloaders);
                page = GetNextDownloadPage(page.nextPage);
            }

            return resultsBuilder.Build();
        }

        private void DisplayResults(SyncResults results)
        {
            var duration = StartTime - EndTime;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            _logger.LogInformation("Results");
            _logger.LogInformation("==================================================");
            _logger.LogInformation($"Started at:             {StartTime.Value}");
            _logger.LogInformation($"Ended at:               {EndTime.Value}");
            _logger.LogInformation($"Run time:               {duration}");
            _logger.LogInformation($"Successful Downloads:   {results.SuccessfulDownloads.Count}");
            _logger.LogInformation($"Total Downloads:        {results.TotalDownloads}");
            _logger.LogInformation($"Successful Downloads:   {results.SuccessfulDownloads.Count}");
            _logger.LogInformation($"Errored Downloads       {results.ErroredFiles.Count}");

            foreach (var errors in results.ErroredFiles)
            {
                _logger.LogInformation("Error Summary ---------------------");
                _logger.LogInformation($"     - {errors.File.GFile.Name}");
                _logger.LogInformation($"     - {errors.File.Destination}");
                _logger.LogInformation($"     - {errors.ErrorMessage}");
            }
        }

        private (DownloadableFile[] downloads, int nextPage) GetNextDownloadPage()
        {
            return GetNextDownloadPage(1);
        }

        private (DownloadableFile[] downloads, int nextPage) GetNextDownloadPage(int nextPage)
        {
            var total = _filesToDownload.Count;
            var pageSize = 5;
            var skip = pageSize * (nextPage - 1); // Determine how many files to skip to start the next page
            var pages = _filesToDownload
                .Skip(skip)
                .Take(pageSize)
                .ToArray();

            return (pages, ++nextPage);
        }

        private void SanatizeDestinationPath()
        {
            var sanitizedDestination = SanatizePath(Config.DestinationPath);
            if(sanitizedDestination != Config.DestinationPath)
            {
                _logger.LogError($"Warning: Invalid Destination path.  Will use \"{sanitizedDestination}\" instead");
            }
        }

        private string SanatizePath(string path)
        {
            var invalidChars = Path.GetInvalidPathChars();
            var onlyValidChars = path.Where(c => !invalidChars.Contains(c)).ToArray();
            return new string(onlyValidChars);
        }

        private void DownloadFile(DownloadableFile download, SyncResultsBuilder results)
        {
            _logger.LogInformation($"[dl: {download.GFile.Name}] download starting at {download.Destination}");
            var sanitizedDestination = SanatizePath(download.Destination);
            if(sanitizedDestination != download.Destination)
            {
                _logger.LogError($"[dl: {download.GFile.Name}] Warning: Invalid Destination path.  Will use \"{sanitizedDestination}\" instead");
            }
            // Create the directorys (if they dont exist) to avoid exception when making file
            var folderPath = Path.GetDirectoryName(sanitizedDestination);
            Directory.CreateDirectory(folderPath);
            // Create the file in a temp location to avoid overwriting the file and corrupting if the download is broken
            var tempFile = sanitizedDestination + ".temp";
            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    var request = _service.Files.Get(download.GFile.Id);
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
                                    if (download.GFile.Size.HasValue) {
                                        percent = $"{((100 * progress.BytesDownloaded) / download.GFile.Size.Value)}%";
                                    }
                                    else
                                    {
                                        percent = "...";
                                    }

                                    _logger.LogInformation($"[dl: {download.GFile.Name}] {percent}");
                                    break;
                                }
                            case DownloadStatus.Completed:
                                {
                                    _logger.LogInformation($"[dl: {download.GFile.Name}] Download complete.");
                                    results.SuccessfulDownloads.Add(download);
                                    break;
                                }
                            case DownloadStatus.Failed:
                                {
                                    _logger.LogError($"[dl: {download.GFile.Name}] Download failed.  Exception: {progress.Exception.Message}");
                                    results.ErroredFiles.Add(download.ToErroredFile(progress.Exception));
                                    break;
                                }
                        }
                    };
                    request.DownloadWithStatus(stream);
                    stream.Dispose();
                    // Finished downloading, delete the original and rename the temp to the original
                    LFile.Delete(sanitizedDestination);
                    LFile.Move(tempFile, sanitizedDestination);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError($"[dl: {download?.GFile?.Name}] Download failed.  Exception: {exception.Message}");
                _logger.LogError(exception.Message);
                if(download?.GFile != null)
                {
                    var errorFile = download.ToErroredFile(exception);
                    results.ErroredFiles.Add(errorFile);
                }
            }
        }

        private FileList GetNextGoogleFilePage(string nextToken = null)
        {
            FilesResource.ListRequest listRequest = _service.Files.List();
            listRequest.PageSize = 50;
            listRequest.Fields = $"nextPageToken, files(id, name, parents, size)";
            listRequest.Q = $"'{Config.GoogleDriveFolderId}' in parents"; // Only get files where one of the parentId's matches the folder Id we want
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

                _logger.LogInformation("Credential file saved to: " + Config.TokenPath);
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
