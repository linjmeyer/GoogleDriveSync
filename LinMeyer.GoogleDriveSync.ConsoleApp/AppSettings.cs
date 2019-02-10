using LinMeyer.GoogleDriveSync.Sync;
using System.ComponentModel.DataAnnotations;

namespace LinMeyer.GoogleDriveSync.ConsoleApp
{
    public class AppSettings
    {
        [Required]
        public SyncConfig SyncConfig { get; set; }
    }
}
