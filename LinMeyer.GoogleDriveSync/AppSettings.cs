using System;
using System.Collections.Generic;
using System.Text;

namespace LinMeyer.GoogleDriveSync
{
    public class AppSettings
    {
        /// <summary>
        /// Path to the Google API's credentials file.  Default is "credentials.json" the same folder.
        /// </summary>
        public string GoogleCredentialsFilePath { get; set; } = "credentials.json";

        /// <summary>
        /// Path of the folder that should be downloaded from Google Drive.  Default is the base directory.
        /// </summary>
        public string GoogleDriveFolder { get; set; } = string.Empty;

        /// <summary>
        /// The name of your app that will display on the Google Oath2 consent screen
        /// </summary>
        public string GoogleApplicationName { get; set; }

        /// <summary>
        /// The destination of the files to  be syncronized from Google
        /// </summary>
        public string DestinationPath { get; set; }
    }
}
