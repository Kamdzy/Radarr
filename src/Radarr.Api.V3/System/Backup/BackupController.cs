using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Crypto;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Backup;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.System.Backup
{
    [V3ApiController("system/backup")]
    public class BackupController : Controller
    {
        private readonly IBackupService _backupService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;

        private static readonly List<string> ValidExtensions = new () { ".zip", ".db", ".xml" };

        public BackupController(IBackupService backupService,
                            IAppFolderInfo appFolderInfo,
                            IDiskProvider diskProvider)
        {
            _backupService = backupService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
        }

        [HttpGet]
        public List<BackupResource> GetBackupFiles()
        {
            var backups = _backupService.GetBackups();

            return backups.Select(b => new BackupResource
                {
                    Id = GetBackupId(b),
                    Name = b.Name,
                    Path = $"/backup/{b.Type.ToString().ToLower()}/{b.Name}",
                    Type = b.Type,
                    Size = b.Size,
                    Time = b.Time
                })
                .OrderByDescending(b => b.Time)
                .ToList();
        }

        [RestDeleteById]
        public object DeleteBackup(int id)
        {
            var backup = GetBackup(id);

            if (backup == null)
            {
                throw new NotFoundException();
            }

            var path = GetBackupPath(backup);

            if (!_diskProvider.FileExists(path))
            {
                throw new NotFoundException();
            }

            _diskProvider.DeleteFile(path);

            return new { };
        }

        [HttpPost("restore/{id:int}")]
        public object Restore([FromRoute] int id)
        {
            var backup = GetBackup(id);

            if (backup == null)
            {
                throw new NotFoundException();
            }

            var path = GetBackupPath(backup);

            _backupService.Restore(path);

            return new
            {
                RestartRequired = true
            };
        }

        [HttpPost("restore/upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = 5000000000)]
        public object UploadAndRestore()
        {
            var files = Request.Form.Files;

            if (files.Empty())
            {
                throw new BadRequestException("file must be provided");
            }

            var file = files[0];
            var extension = Path.GetExtension(file.FileName);

            if (!ValidExtensions.Contains(extension))
            {
                throw new UnsupportedMediaTypeException($"Invalid extension, must be one of: {ValidExtensions.Join(", ")}");
            }

            var path = Path.Combine(_appFolderInfo.TempFolder, $"radarr_backup_restore{extension}");

            _diskProvider.SaveStream(file.OpenReadStream(), path);
            _backupService.Restore(path);

            // Cleanup restored file
            _diskProvider.DeleteFile(path);

            return new
            {
                RestartRequired = true
            };
        }

        private string GetBackupPath(NzbDrone.Core.Backup.Backup backup)
        {
            return Path.Combine(_backupService.GetBackupFolder(backup.Type), backup.Name);
        }

        private int GetBackupId(NzbDrone.Core.Backup.Backup backup)
        {
            return HashConverter.GetHashInt31($"backup-{backup.Type}-{backup.Name}");
        }

        private NzbDrone.Core.Backup.Backup GetBackup(int id)
        {
            return _backupService.GetBackups().SingleOrDefault(b => GetBackupId(b) == id);
        }
    }
}
