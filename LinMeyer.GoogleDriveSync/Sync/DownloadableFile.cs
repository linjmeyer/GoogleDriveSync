using Google.Apis.Drive.v3.Data;
using System;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class DownloadableFile
    {
        public File GFile { get; protected set; }
        public string Destination { get; protected set; }
        public string OriginalDestination { get; protected set; }


        public DownloadableFile(File gFile, string destination)
        {
            GFile = gFile;
            Destination = OriginalDestination = destination;
        }

        public ErroredFile ToErroredFile(Exception exception)
        {
            return ErroredFile.FromDownloadableFile(this, exception);
        }
    }
}
