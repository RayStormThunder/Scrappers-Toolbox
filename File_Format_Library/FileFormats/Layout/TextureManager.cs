using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.IO;
using FirstPlugin;
using System.Windows.Forms;
using System.IO;

namespace LayoutBXLYT
{
    public class TextureManager : IDisposable
    {
        public BxlytHeader LayoutFile;

        public Dictionary<string, BNTX> BinaryContainers = new Dictionary<string, BNTX>();

        //The archive to put textures in if necessary
        public Dictionary<string, IArchiveFile> ArchiveFile = new Dictionary<string, IArchiveFile>();

        public IArchiveFile ArchiveParent
        {
            get { return LayoutFile.FileInfo.IFileInfo.ArchiveParent; }
        }

        public PlatformType Platform = PlatformType.WiiU;

        public enum PlatformType
        {
            WiiU, //bflim
            ThreeDS, //bclim and bflim
            DS, //
            Gamecube, //bti
            Switch, //bntx
            Wii, //TPL
        }

        public STGenericTexture EditTexture(string name)
        {
            STGenericTexture texture = null;
            switch (Platform)
            {
                case PlatformType.Switch:
                    {
                        foreach (var bntx in BinaryContainers.Values)
                        {
                            Console.WriteLine("bntx " + name + " " + bntx.Textures.ContainsKey(name));
                            if (bntx.Textures.ContainsKey(name))
                            {
                                OpenFileDialog ofd = new OpenFileDialog();
                                ofd.Filter = bntx.Textures[name].ReplaceFilter;
                                if (ofd.ShowDialog() == DialogResult.OK)
                                {
                                    bntx.Textures[name].Replace(ofd.FileName);
                                    return bntx.Textures[name];
                                }
                            }
                        }
                    }
                    break;
                case PlatformType.WiiU:
                    {
                        if (ArchiveParent == null) return null;

                        foreach (var file in ArchiveParent.Files)
                        {
                            if (FileMatchesTextureName(file.FileName, name))
                            {
                                var fileFormat = file.FileFormat;
                                if (fileFormat == null)
                                    fileFormat = file.OpenFile();

                                if (fileFormat is BFLIM)
                                {
                                    OpenFileDialog ofd = new OpenFileDialog();
                                    ofd.Filter = ((BFLIM)fileFormat).ReplaceFilter;
                                    ofd.FileName = name;

                                    if (ofd.ShowDialog() == DialogResult.OK)
                                    {
                                        ((BFLIM)fileFormat).Replace(ofd.FileName);
                                        ((BFLIM)fileFormat).Text = name;
                                        return (BFLIM)fileFormat;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case PlatformType.ThreeDS:
                    {
                        if (ArchiveParent == null) return null;

                        foreach (var file in ArchiveParent.Files)
                        {
                            if (FileMatchesTextureName(file.FileName, name))
                            {
                                var fileFormat = file.FileFormat;
                                if (fileFormat == null)
                                    fileFormat = file.OpenFile();

                                if (fileFormat is BCLIM)
                                {
                                    var bclim = (BCLIM)fileFormat;

                                    OpenFileDialog ofd = new OpenFileDialog();
                                    ofd.Filter = FileFilters.REV_TEX;
                                    ofd.FileName = Path.GetFileNameWithoutExtension(name);

                                    if (ofd.ShowDialog() == DialogResult.OK)
                                    {
                                        bclim.Replace(ofd.FileName);
                                        bclim.Text = Path.GetFileNameWithoutExtension(file.FileName);
                                        return bclim;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case PlatformType.Wii:
                    if (ArchiveParent == null) return null;
                    foreach (var file in ArchiveParent.Files)
                    {
                        if (FileMatchesTextureName(file.FileName, name))
                        {
                            var fileFormat = file.FileFormat;
                            if (fileFormat == null)
                                fileFormat = file.OpenFile();

                            if (fileFormat is TPL)
                            {
                                var tpl = (TPL)fileFormat;
                                var tex = tpl.TextureList.FirstOrDefault();
                                if (tex == null)
                                    continue;

                                OpenFileDialog ofd = new OpenFileDialog();
                                ofd.Filter = tex.ReplaceFilter;
                                ofd.FileName = Path.GetFileNameWithoutExtension(name);

                                if (ofd.ShowDialog() == DialogResult.OK)
                                {
                                    tex.Replace(ofd.FileName);
                                    tex.Text = Path.GetFileNameWithoutExtension(file.FileName);
                                    return tex;
                                }
                            }
                        }
                    }
                    break;
            }
            return texture;
        }

        public void RemoveTextures(List<STGenericTexture> textures) {
            foreach (var tex in textures)
                RemoveTexture(tex);
        }

        public void RemoveTexture(STGenericTexture texture)
        {
            switch (Platform)
            {
                case PlatformType.WiiU:
                    {
                        var archive = ArchiveParent;
                        if (archive == null) return;

                        ArchiveFileInfo fileInfoDelete = null;
                        foreach (var file in archive.Files)
                        {
                            if (file.FileName.Contains(texture.Text))
                                fileInfoDelete = file;
                        }

                        if (fileInfoDelete != null)
                            archive.DeleteFile(fileInfoDelete);
                    }
                    break;
                case PlatformType.Switch:
                    {
                        foreach (var bntx in BinaryContainers.Values)
                        {
                            if (bntx.Textures.ContainsKey(texture.Text))
                                bntx.RemoveTexture(bntx.Textures[texture.Text]);
                        }
                    }
                    break;
                default:
                    {
                        var archive = ArchiveParent;
                        if (archive == null) return;

                        ArchiveFileInfo fileInfoDelete = null;
                        foreach (var file in archive.Files)
                        {
                            if (file.FileName.Contains(texture.Text))
                                fileInfoDelete = file;
                        }

                        if (fileInfoDelete != null)
                            archive.DeleteFile(fileInfoDelete);
                    }
                    break;
            }
        }

        public List<STGenericTexture> AddTextures()
        {
            List<STGenericTexture> textures = new List<STGenericTexture>();

            switch (Platform)
            {
                case PlatformType.WiiU:
                    {
                        var archive = ArchiveParent;
                        if (archive == null) return null;

                        var matches = archive.Files.Where(p => p.FileName.Contains("bflim")).ToList();
                        string textureFolder = "timg";
                        if (matches.Count > 0)
                            textureFolder = System.IO.Path.GetDirectoryName(matches[0].FileName);

                        var bflim = BFLIM.CreateNewFromImage();

                        if (bflim == null)
                            return textures;

                        textures.Add(bflim);

                        var mem = new System.IO.MemoryStream();
                        bflim.Save(mem);
                        archive.AddFile(new ArchiveFileInfo()
                        {
                            FileData = mem.ToArray(),
                            FileName = System.IO.Path.Combine(textureFolder, bflim.Text).Replace('\\','/'),
                        });
                    }
                    break;
                case PlatformType.ThreeDS:
                    {
                        var archive = ArchiveParent;
                        if (archive == null) return null;

                        OpenFileDialog ofd = new OpenFileDialog();
                        ofd.Multiselect = true;
                        ofd.Filter = "Layout Image (*.bclim)|*.bclim|All files (*.*)|*.*";
                        if (ofd.ShowDialog() != DialogResult.OK)
                            return textures;

                        string textureFolder = GetTextureFolderFromArchive(archive, ".bclim", "timg");

                        foreach (var filePath in ofd.FileNames)
                        {
                            var fileFormat = STFileLoader.OpenFileFormat(filePath);
                            if (!(fileFormat is BCLIM))
                                continue;

                            var bclim = (BCLIM)fileFormat;
                            var baseName = Path.GetFileNameWithoutExtension(filePath);
                            bclim.Text = baseName;

                            using (var mem = new MemoryStream())
                            {
                                bclim.Save(mem);
                                archive.AddFile(new ArchiveFileInfo()
                                {
                                    FileData = mem.ToArray(),
                                    FileName = Path.Combine(textureFolder, baseName + ".bclim").Replace('\\', '/'),
                                });
                            }

                            textures.Add(bclim);
                        }
                    }
                    break;
                case PlatformType.Switch:
                    {
                        BNTX bntx = null;
                        if (BinaryContainers.Count == 0)
                        {
                            //Create a new one if none exist
                            //Method for saving these will come in the save dialog
                            bntx = new BNTX();
                            bntx.IFileInfo = new IFileInfo();
                            bntx.FileName = "textures";
                            bntx.Load(new System.IO.MemoryStream(BNTX.CreateNewBNTX("textures")));
                            BinaryContainers.Add("textures", bntx);
                        }
                        else
                        {
                            //Use first container for now as archives only use one
                            bntx = BinaryContainers.Values.FirstOrDefault();
                        }

                        var importedTextures = bntx.ImportTexture();

                        //Load all the additional textues
                        for (int i = 0; i < importedTextures.Count; i++)
                            textures.Add(importedTextures[i]);
                    }
                    break;
                case PlatformType.Wii:
                    {
                        var archive = ArchiveParent;
                        if (archive == null) return null;

                        string textureFolder = GetTextureFolderFromArchive(archive, ".tpl", "timg");

                        var tpl = TPL.CreateNewFromImage();
                        if (tpl == null)
                            return textures;

                        using (var mem = new MemoryStream())
                        {
                            tpl.Save(mem);
                            archive.AddFile(new ArchiveFileInfo()
                            {
                                FileData = mem.ToArray(),
                                FileName = Path.Combine(textureFolder, tpl.FileName).Replace('\\', '/'),
                            });
                        }

                        var tex = tpl.TextureList.FirstOrDefault();
                        if (tex != null)
                        {
                            tex.Text = Path.GetFileNameWithoutExtension(tpl.FileName);
                            textures.Add(tex);
                        }
                    }
                    break;
            }

            return textures;
        }

        private static string GetTextureFolderFromArchive(IArchiveFile archive, string extension, string defaultFolder)
        {
            var match = archive.Files.FirstOrDefault(p =>
                p.FileName != null && p.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return defaultFolder;

            return Path.GetDirectoryName(match.FileName);
        }

        private static bool FileMatchesTextureName(string fileName, string textureName)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(textureName))
                return false;

            var fileBase = Path.GetFileNameWithoutExtension(fileName);
            var texBase = Path.GetFileNameWithoutExtension(textureName);

            return string.Equals(fileName, textureName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileBase, textureName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileBase, texBase, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            BinaryContainers.Clear();
            ArchiveFile.Clear();
        }
    }
}
