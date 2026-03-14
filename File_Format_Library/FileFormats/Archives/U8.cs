using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Toolbox;
using Toolbox.Library;
using Toolbox.Library.IO;

namespace FirstPlugin
{
    public class U8 : IArchiveFile, IFileFormat
    {
        private const uint U8Magic = 0x55AA382D;
        private const uint RootNodeOffset = 0x20;
        private const int DataAlignment = 0x20;

        private int _dataAlignment = DataAlignment;
        private byte[] _trailingData = new byte[0];

        public FileType FileType { get; set; } = FileType.Archive;
        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new[] { "U8" };
        public string[] Extension { get; set; } = new[] { "*.u8", "*.cmparc" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool CanAddFiles { get; set; }
        public bool CanRenameFiles { get; set; }
        public bool CanReplaceFiles { get; set; }
        public bool CanDeleteFiles { get; set; }

        private int _lzType;
        private int _lzSize;

        public List<FileEntry> files = new List<FileEntry>();
        public IEnumerable<ArchiveFileInfo> Files => files;

        private class ParsedNode
        {
            public byte Type;
            public uint NameOffset;
            public uint DataOffset;
            public uint DataLength;
            public string Name;
            public string FullPath;
        }

        private class DirectoryNode
        {
            public string Name;
            public string FullPath;
            public List<object> Children = new List<object>();
            public Dictionary<string, DirectoryNode> DirectoryLookup = new Dictionary<string, DirectoryNode>(StringComparer.OrdinalIgnoreCase);
        }

        private class SaveNode
        {
            public byte Type;
            public string Name;
            public int ParentIndex;
            public int EndIndex;
            public int NameOffset;
            public int DataOffset;
            public int DataSize;
            public byte[] Data;
        }

        public bool Identify(Stream stream)
        {
            using (var reader = new FileReader(stream, true))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                byte first = reader.ReadByte();
                if (first == 0x10 || first == 0x11)
                {
                    _lzType = first;
                    reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                    _lzSize = reader.ReadInt32();
                    reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                }
                else
                {
                    _lzType = 0;
                    _lzSize = 0;
                    reader.Position = 0;
                }

                uint signature = reader.ReadUInt32();
                reader.Position = 0;
                return signature == U8Magic;
            }
        }

        public Type[] Types
        {
            get { return new Type[0]; }
        }

        public void ClearFiles()
        {
            files.Clear();
        }

        public string Name
        {
            get { return FileName; }
            set { FileName = value; }
        }

        public void Load(Stream stream)
        {
            // Plain U8 files are safe to round-trip with the current writer.
            // Wrapped variants (for example LZ-prefixed files) need wrapper-preserving
            // save support to avoid changing output format.
            CanSave = _lzType == 0;
            IFileInfo.UseEditMenu = true;
            CanAddFiles = true;
            CanRenameFiles = true;
            CanReplaceFiles = true;
            CanDeleteFiles = true;
            files.Clear();
            _dataAlignment = DataAlignment;
            _trailingData = new byte[0];

            Stream dataStream;
            if (_lzType == 0x11 || _lzType == 0x10)
            {
                using (var sub = new SubStream(stream, 4))
                {
                    byte[] wrapped = sub.ToArray();
                    dataStream = _lzType == 0x11
                        ? new MemoryStream(LZ77_WII.Decompress11(wrapped, _lzSize))
                        : new MemoryStream(LZ77_WII.Decompress10LZ(wrapped, _lzSize));
                }
            }
            else
            {
                dataStream = stream;
            }

            dataStream.Position = 0;
            using (var reader = new FileReader(dataStream, true))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;

                uint magic = reader.ReadUInt32();
                if (magic != U8Magic)
                    throw new InvalidDataException("Invalid U8 magic.");

                uint entriesOffset = reader.ReadUInt32();
                uint entriesLength = reader.ReadUInt32();
                uint firstFileOffset = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();

                reader.SeekBegin(entriesOffset);
                ParsedNode root = ReadNode(reader);
                if (root.Type != 1)
                    throw new InvalidDataException("U8 root node is not a directory.");

                int nodeCount = (int)root.DataLength;
                var nodes = new List<ParsedNode>(nodeCount);
                nodes.Add(root);
                for (int i = 1; i < nodeCount; i++)
                {
                    nodes.Add(ReadNode(reader));
                }

                uint stringTableStart = entriesOffset + (uint)(nodeCount * 12);
                for (int i = 0; i < nodes.Count; i++)
                {
                    reader.SeekBegin(stringTableStart + nodes[i].NameOffset);
                    nodes[i].Name = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated, Encoding.ASCII);
                }

                nodes[0].Name = string.Empty;
                BuildFullPaths(nodes, 1, nodeCount, string.Empty);

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Type != 0)
                        continue;

                    reader.SeekBegin(nodes[i].DataOffset);
                    files.Add(new FileEntry()
                    {
                        FileName = nodes[i].FullPath,
                        FileData = reader.ReadBytes((int)nodes[i].DataLength),
                    });
                }

                // U8 data is canonically aligned to 0x20. Inferring from only the first
                // file offset can overestimate (for example 0x80/0x100) and introduce
                // unnecessary zero-filled gaps between files when saving.
                _dataAlignment = DataAlignment;

                int dataEnd = 0;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Type != 0)
                        continue;

                    long end = (long)nodes[i].DataOffset + nodes[i].DataLength;
                    if (end > dataEnd)
                        dataEnd = (int)end;
                }

                if (dataEnd < dataStream.Length)
                {
                    reader.SeekBegin(dataEnd);
                    _trailingData = reader.ReadBytes((int)(dataStream.Length - dataEnd));
                }
            }
        }

        private static ParsedNode ReadNode(FileReader reader)
        {
            byte type = reader.ReadByte();
            uint nameOffset = (uint)((reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte());
            uint dataOffset = reader.ReadUInt32();
            uint dataLength = reader.ReadUInt32();
            return new ParsedNode()
            {
                Type = type,
                NameOffset = nameOffset,
                DataOffset = dataOffset,
                DataLength = dataLength,
            };
        }

        private static int BuildFullPaths(List<ParsedNode> nodes, int firstIndex, int endIndex, string parentPath)
        {
            int index = firstIndex;
            while (index < endIndex)
            {
                var node = nodes[index];
                string nodePath = string.IsNullOrEmpty(parentPath) ? node.Name : parentPath + "/" + node.Name;

                if (node.Type == 1)
                {
                    node.FullPath = nodePath + "/";
                    nodes[index] = node;
                    index = BuildFullPaths(nodes, index + 1, (int)node.DataLength, nodePath);
                }
                else
                {
                    node.FullPath = nodePath;
                    nodes[index] = node;
                    index++;
                }
            }
            return index;
        }

        public void Unload()
        {
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('\\', '/').Trim('/');
        }

        private static int Align(int value, int alignment)
        {
            int mod = value % alignment;
            return mod == 0 ? value : value + (alignment - mod);
        }

        private static byte[] GetEntryData(FileEntry entry)
        {
            if (entry.FileDataStream != null)
            {
                using (var ms = new MemoryStream())
                {
                    entry.FileDataStream.Position = 0;
                    entry.FileDataStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            return entry.FileData ?? new byte[0];
        }

        private static bool ShouldSaveAttachedFormat(FileEntry entry)
        {
            var format = entry?.FileFormat;
            return format != null && format.CanSave;
        }

        private static DirectoryNode GetOrCreateDirectory(DirectoryNode current, string name)
        {
            if (current.DirectoryLookup.TryGetValue(name, out DirectoryNode existing))
                return existing;

            string fullPath = string.IsNullOrEmpty(current.FullPath) ? name : current.FullPath + "/" + name;
            var next = new DirectoryNode()
            {
                Name = name,
                FullPath = fullPath,
            };
            current.DirectoryLookup.Add(name, next);
            current.Children.Add(next);
            return next;
        }

        private static int FlattenDirectory(DirectoryNode directory, int parentIndex, List<SaveNode> output)
        {
            int index = output.Count;
            output.Add(new SaveNode()
            {
                Type = 1,
                Name = directory.Name,
                ParentIndex = parentIndex,
            });

            for (int i = 0; i < directory.Children.Count; i++)
            {
                if (directory.Children[i] is DirectoryNode childDirectory)
                {
                    FlattenDirectory(childDirectory, index, output);
                }
                else if (directory.Children[i] is FileEntry fileEntry)
                {
                    string normalized = NormalizePath(fileEntry.FileName);
                    int slash = normalized.LastIndexOf('/');
                    string fileName = slash >= 0 ? normalized.Substring(slash + 1) : normalized;

                    output.Add(new SaveNode()
                    {
                        Type = 0,
                        Name = fileName,
                        ParentIndex = index,
                        Data = GetEntryData(fileEntry),
                    });
                }
            }

            output[index].EndIndex = output.Count;
            return index;
        }

        private static void WriteNode(FileWriter writer, SaveNode node)
        {
            writer.Write(node.Type);
            writer.Write((byte)((node.NameOffset >> 16) & 0xFF));
            writer.Write((byte)((node.NameOffset >> 8) & 0xFF));
            writer.Write((byte)(node.NameOffset & 0xFF));

            if (node.Type == 1)
            {
                writer.Write((uint)node.ParentIndex);
                writer.Write((uint)node.EndIndex);
            }
            else
            {
                writer.Write((uint)node.DataOffset);
                writer.Write((uint)node.DataSize);
            }
        }

        public void SaveFile(FileWriter writer)
        {
            writer.SetByteOrder(true);

            foreach (var entry in files)
            {
                if (ShouldSaveAttachedFormat(entry))
                    entry.SaveFileFormat();
            }

            var rootDirectory = new DirectoryNode()
            {
                Name = string.Empty,
                FullPath = string.Empty,
            };

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                string normalized = NormalizePath(file.FileName);
                if (string.IsNullOrEmpty(normalized))
                    continue;

                string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                DirectoryNode current = rootDirectory;
                for (int p = 0; p < parts.Length - 1; p++)
                {
                    current = GetOrCreateDirectory(current, parts[p]);
                }

                file.FileName = normalized;
                current.Children.Add(file);
            }

            var nodes = new List<SaveNode>();
            FlattenDirectory(rootDirectory, 0, nodes);

            if (nodes.Count == 0)
            {
                nodes.Add(new SaveNode()
                {
                    Type = 1,
                    Name = string.Empty,
                    ParentIndex = 0,
                    EndIndex = 1,
                });
            }

            var nameOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
            var stringTable = new MemoryStream();
            var stringWriter = new BinaryWriter(stringTable, Encoding.ASCII, true);

            stringWriter.Write((byte)0);
            nameOffsets[string.Empty] = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                string name = nodes[i].Name ?? string.Empty;
                if (!nameOffsets.TryGetValue(name, out int offset))
                {
                    offset = (int)stringTable.Position;
                    nameOffsets.Add(name, offset);
                    stringWriter.Write(Encoding.ASCII.GetBytes(name));
                    stringWriter.Write((byte)0);
                }

                nodes[i].NameOffset = offset;
                nodes[i].DataSize = nodes[i].Data?.Length ?? 0;
            }

            int nodeSectionSize = (nodes.Count * 12) + (int)stringTable.Length;
            int fileDataOffset = Align((int)RootNodeOffset + nodeSectionSize, _dataAlignment);

            int currentOffset = fileDataOffset;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type != 0)
                    continue;

                currentOffset = Align(currentOffset, _dataAlignment);
                nodes[i].DataOffset = currentOffset;
                currentOffset += nodes[i].DataSize;
            }

            writer.Write(U8Magic);
            writer.Write((uint)RootNodeOffset);
            writer.Write((uint)nodeSectionSize);
            writer.Write((uint)fileDataOffset);
            writer.Write((uint)0);
            writer.Write((uint)0);

            writer.Write((uint)0);
            writer.Write((uint)0);

            writer.SeekBegin(RootNodeOffset);
            for (int i = 0; i < nodes.Count; i++)
            {
                WriteNode(writer, nodes[i]);
            }

            writer.Write(stringTable.ToArray());
            writer.AlignBytes(_dataAlignment);

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type != 0)
                    continue;

                writer.SeekBegin(nodes[i].DataOffset);
                if (nodes[i].Data != null && nodes[i].Data.Length > 0)
                    writer.Write(nodes[i].Data);
            }

            if (_trailingData != null && _trailingData.Length > 0)
                writer.Write(_trailingData);
        }

        public void Save(Stream stream)
        {
            if (_lzType != 0)
                throw new NotSupportedException("This U8 file uses a wrapped variant and cannot be safely saved yet.");

            byte[] output;
            using (var ms = new MemoryStream())
            {
                using (var writer = new FileWriter(ms))
                {
                    SaveFile(writer);
                }
                output = ms.ToArray();
            }

            // Validate output by re-reading the archive before writing to disk.
            using (var verifyStream = new MemoryStream(output))
            {
                var verifier = new U8()
                {
                    FileName = FileName,
                    FilePath = FilePath,
                    IFileInfo = new IFileInfo(),
                };
                if (!verifier.Identify(verifyStream))
                    throw new InvalidDataException("Generated U8 archive failed signature validation.");

                verifyStream.Position = 0;
                verifier.Load(verifyStream);
                verifier.Unload();
            }

            stream.Write(output, 0, output.Length);
        }

        public bool AddFile(ArchiveFileInfo archiveFileInfo)
        {
            files.Add(new FileEntry()
            {
                FileName = NormalizePath(archiveFileInfo.FileName),
                FileData = archiveFileInfo.FileData,
                FileDataStream = archiveFileInfo.FileDataStream,
            });
            return true;
        }

        public bool DeleteFile(ArchiveFileInfo archiveFileInfo)
        {
            var existing = files.FirstOrDefault(x => ReferenceEquals(x, archiveFileInfo));
            if (existing != null)
            {
                files.Remove(existing);
                return true;
            }

            existing = files.FirstOrDefault(x => string.Equals(NormalizePath(x.FileName), NormalizePath(archiveFileInfo.FileName), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                files.Remove(existing);
                return true;
            }

            return false;
        }

        public class FileEntry : ArchiveFileInfo
        {
        }
    }
}
