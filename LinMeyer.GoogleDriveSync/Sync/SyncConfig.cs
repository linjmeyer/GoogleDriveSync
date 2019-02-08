using System;
using System.Collections.Generic;
using System.Text;

namespace LinMeyer.GoogleDriveSync.Sync
{
    public class SyncConfig
    {
        public string ApplicationName { get; set; }
        public string CredentialsPath { get; set; }
        public string TokenPath { get; set; } = "token.json";
        public string GoogleDriveFolder { get; set; }
        public string DestinationPath { get; set; }
    }
}
