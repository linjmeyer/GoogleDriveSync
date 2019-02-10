using System.Collections.Generic;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class SyncResultsBuilder
    {
        public List<DownloadableFile> SuccessfulDownloads { get; private set; } = new List<DownloadableFile>();
        public List<ErroredFile> ErroredFiles { get; private set; } = new List<ErroredFile>();

        public SyncResultsBuilder()
        {

        }

        public SyncResults Build()
        {
            return new SyncResults(this);
        }
    }
}
