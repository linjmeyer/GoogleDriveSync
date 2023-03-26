# Not Maintained

I created this mainly to backup Google Photos, but they are no longer synced to Google Drive.  Instead you must use the Google Photos API.  

You can check out this tool if that is your use case as well: [LinMeyer.GooglePhotosBackup](https://github.com/linjmeyer/GooglePhotosBackup)

# GoogleDriveSync
A basic .NET Core app for downloading data from Google Drive onto a local disk.  The "sync" is only one direction - download from Google to a local or network path.

# Why
I built it to back up my Google Photos folder onto a network file share.  Google's official tool "Backup and Sync" does not support UNC shares.  There are other tools to do this but generally they are not free or the free version has limits that don't work for me (e.g. less than 1000 files, etc.)
