using System.ComponentModel.DataAnnotations;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class SyncConfig
    {
        /// <summary>
        /// The name of your app that will display on the Google Oath2 consent screen
        /// </summary>
        [Required]
        public string ApplicationName { get; set; }
        /// <summary>
        /// Path to the Google API's credentials file.  Default is "credentials.json" the same folder.
        /// </summary>
        [Required]
        public string CredentialsPath { get; set; }
        /// <summary>
        /// Where the user's JWT token from Google will be stored.  Default is "token.json"
        /// </summary>
        public string TokenPath { get; set; } = "token.json";
        /// <summary>
        /// Path of the folder that should be downloaded from Google Drive.  Default is the base directory.
        /// </summary>
        [Required]
        public string GoogleDriveFolderId { get; set; }
        /// <summary>
        /// The destination of the files to  be syncronized from Google
        /// </summary>
        [Required]
        public string DestinationPath { get; set; }
        /// <summary>
        /// When true files will always be downloaded from Google Drive even if the source is not newer than the destination
        /// </summary>
        public bool ForceDownloads { get; set; } = false;
        /// <summary>
        /// Determines if the drive folder passed in via GoogleDriveFolderId should be written to disk.  E.g. "YourFolder\file.png" vs just "file.png".  Default is true.
        /// </summary>
        public bool IncludeTopDriveFolder { get; set; } = true;

        public void Validate()
        {
            Validator.ValidateObject(this, new ValidationContext(this), true);
        }
    }
}
