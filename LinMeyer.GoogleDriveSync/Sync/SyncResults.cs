using System.Collections.Generic;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class SyncResults
    {
        public IReadOnlyList<DownloadableFile> SuccessfulDownloads { get; private set; }
        public IReadOnlyList<ErroredFile> ErroredFiles { get; private set; }

        // Metrics that are useful and should not be updated once set
        public int TotalDownloads { get; private set; }
        
        public SyncResults(SyncResultsBuilder builder)
        {
            SuccessfulDownloads = builder.SuccessfulDownloads.AsReadOnly();
            ErroredFiles = builder.ErroredFiles.AsReadOnly();

            TotalDownloads = SuccessfulDownloads.Count + ErroredFiles.Count;
        }
    }
}
