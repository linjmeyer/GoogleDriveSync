using System;
using Google.Apis.Drive.v3.Data;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class ErroredFile
    {
        public DownloadableFile File { get; private set; }
        public Exception Exception { get; private set; }
        public string ErrorMessage => Exception?.Message;

        public ErroredFile(DownloadableFile file, Exception exception)
        {
            File = file;
            Exception = exception;
        }

        public static ErroredFile FromDownloadableFile(DownloadableFile download, Exception exception)
        {
            return new ErroredFile(download, exception);
        }
    }
}
