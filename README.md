# GoogleDriveSync
A basic .NET Core app for downloading data from Google Drive onto a local disk.  The "sync" is only one direction - download from Google to a local or network path.

# Why
I built it to back up my Google Photos folder onto a network file share.  Google's official tool "Backup and Sync" does not support UNC shares.  There are other tools to do this but generally they are not free or the free version has limits that don't work for me (e.g. less than 1000 files, etc.)
