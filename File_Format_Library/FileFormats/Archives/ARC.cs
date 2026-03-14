using System;
using System.Collections.Generic;
using Toolbox;
using Toolbox.Library;

namespace FirstPlugin
{
    public class ARC : IArchiveFile, IFileFormat
    {
        private readonly U8 _u8 = new U8();

        public FileType FileType { get; set; } = FileType.Archive;
        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new[] { "ARC (U8)" };
        public string[] Extension { get; set; } = new[] { "*.arc" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool CanAddFiles { get; set; }
        public bool CanRenameFiles { get; set; }
        public bool CanReplaceFiles { get; set; }
        public bool CanDeleteFiles { get; set; }

        public IEnumerable<ArchiveFileInfo> Files => _u8.Files;

        public Type[] Types => _u8.Types;

        public bool Identify(System.IO.Stream stream)
        {
            _u8.FileName = FileName;
            _u8.FilePath = FilePath;
            _u8.IFileInfo = IFileInfo;
            return _u8.Identify(stream);
        }

        public void Load(System.IO.Stream stream)
        {
            _u8.FileName = FileName;
            _u8.FilePath = FilePath;
            _u8.IFileInfo = IFileInfo;
            _u8.Load(stream);

            CanSave = _u8.CanSave;
            CanAddFiles = _u8.CanAddFiles;
            CanRenameFiles = _u8.CanRenameFiles;
            CanReplaceFiles = _u8.CanReplaceFiles;
            CanDeleteFiles = _u8.CanDeleteFiles;
        }

        public void Save(System.IO.Stream stream)
        {
            _u8.Save(stream);
        }

        public void ClearFiles()
        {
            _u8.ClearFiles();
        }

        public bool AddFile(ArchiveFileInfo archiveFileInfo)
        {
            return _u8.AddFile(archiveFileInfo);
        }

        public bool DeleteFile(ArchiveFileInfo archiveFileInfo)
        {
            return _u8.DeleteFile(archiveFileInfo);
        }

        public void Unload()
        {
            _u8.Unload();
        }
    }
}
