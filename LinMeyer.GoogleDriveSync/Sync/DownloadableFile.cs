using Google.Apis.Drive.v3.Data;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class DownloadableFile
    {
        public File GFile { get; private set; }
        public string Destination { get; private set; }
        public string OriginalDestination { get; private set; }


        public DownloadableFile(File gFile, string destination)
        {
            GFile = gFile;
            Destination = OriginalDestination = destination;
        }
    }
}
