using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FirstPlugin;
using Toolbox.Library.Forms;
using Toolbox.Library;
using System.Threading;
using WeifenLuo.WinFormsUI.Docking;
using System.IO;

namespace LayoutBXLYT
{
    public partial class LayoutTextureList : LayoutDocked
    {
        private LayoutEditor ParentEditor;
        private BxlytHeader ActiveLayout;
        private Dictionary<string, STGenericTexture> TextureList;

        ImageList imgListSmall = new ImageList();
        ImageList imgListBig = new ImageList();

        public LayoutTextureList()
        {
            InitializeComponent();

            listViewTpyeCB.Items.Add(View.Details);
            listViewTpyeCB.Items.Add(View.LargeIcon);
            listViewTpyeCB.Items.Add(View.List);
            listViewTpyeCB.Items.Add(View.SmallIcon);
            listViewTpyeCB.Items.Add(View.Tile);
            listViewTpyeCB.SelectedIndex = 0;
            listViewCustom1.FullRowSelect = true;
            btnEdit.Enabled = false;

            imgListSmall = new ImageList()
            {
                ImageSize = new Size(30, 30),
                ColorDepth = ColorDepth.Depth32Bit,
            };
            imgListBig = new ImageList()
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(80, 80),
            };
        }

        public void Reset()
        {
            if (Thread != null && Thread.IsAlive)
                Thread.Abort();

            for (int i = 0; i < imgListBig.Images.Count; i++)
                imgListBig.Images[i].Dispose();

            for (int i = 0; i < imgListSmall.Images.Count; i++)
                imgListSmall.Images[i].Dispose();

            imgListBig.Images.Clear();
            imgListSmall.Images.Clear();
            listViewCustom1.Items.Clear();

            isLoaded = false;
        }

        private bool isLoaded = false;
        private Thread Thread;

        private class TextureListState
        {
            public string SelectedText;
            public string TopText;
        }

        private TextureListState pendingStateRestore;

        private TextureListState CaptureTextureListState()
        {
            var state = new TextureListState();
            if (listViewCustom1.SelectedItems.Count > 0)
                state.SelectedText = listViewCustom1.SelectedItems[0].Text;

            try
            {
                if (listViewCustom1.TopItem != null)
                    state.TopText = listViewCustom1.TopItem.Text;
            }
            catch { }

            return state;
        }

        private void RestoreTextureListState(TextureListState state)
        {
            if (state == null)
                return;

            if (!string.IsNullOrEmpty(state.SelectedText))
            {
                var selectedItem = listViewCustom1.Items
                    .Cast<ListViewItem>()
                    .FirstOrDefault(x => string.Equals(x.Text, state.SelectedText, StringComparison.OrdinalIgnoreCase));
                if (selectedItem != null)
                    selectedItem.Selected = true;
            }

            if (!string.IsNullOrEmpty(state.TopText))
            {
                var topItem = listViewCustom1.Items
                    .Cast<ListViewItem>()
                    .FirstOrDefault(x => string.Equals(x.Text, state.TopText, StringComparison.OrdinalIgnoreCase));
                if (topItem != null)
                    topItem.EnsureVisible();
            }
        }

        private void QueueTextureListStateRestore(TextureListState state)
        {
            pendingStateRestore = state;
        }

        public void LoadTextures(LayoutEditor parentEditor, BxlytHeader header, 
            Dictionary<string, STGenericTexture> textureList)
        {
            var restoreState = pendingStateRestore ?? CaptureTextureListState();
            pendingStateRestore = null;

            ParentEditor = parentEditor;
            TextureList = textureList;
            ActiveLayout = header;
            btnAdd.Enabled = CanAddTextures();
            btnEdit.Enabled = false;
            listViewCustom1.Items.Clear();
            imgListSmall.Images.Clear();
            imgListSmall.Images.Add(FirstPlugin.Properties.Resources.MissingTexture);
            imgListBig.Images.Clear();
            imgListBig.Images.Add(FirstPlugin.Properties.Resources.MissingTexture);

            listViewCustom1.LargeImageList = imgListBig;
            listViewCustom1.SmallImageList = imgListSmall;
            
            listViewCustom1.BeginUpdate();
            foreach (var texture in header.Textures)
            {
                ListViewItem item = new ListViewItem();
                item.Text = texture;
                item.ImageIndex = 0;
                listViewCustom1.Items.Add(item);
            }

            RestoreTextureListState(restoreState);

            //Load textures after on a seperate thread

            if (Thread != null && Thread.IsAlive)
                Thread.Abort();

            Thread = new Thread((ThreadStart)(() =>
            {
                int index = 0;
                foreach (var texture in header.Textures)
                {
                    string resolvedKey;
                    STGenericTexture resolvedTexture;
                    if (TryResolveTexture(texture, out resolvedKey, out resolvedTexture))
                    {
                        if (header is BCLYT.Header)
                        {
                            //Skip certain formats like bcn ones
                            if (STGenericTexture.IsCompressed(resolvedTexture.Format))
                                continue;
                        }

                        LoadTextureIcon(index, resolvedTexture);
                    }
                    index++;
                }
            }));
            Thread.Start();

            listViewCustom1.EndUpdate();

            isLoaded = true;
        }

        public void DeselectTextureList() {
            listViewCustom1.SelectedItems.Clear();
        }

        private void listViewTpyeCB_SelectedIndexChanged(object sender, EventArgs e) {
            if (isLoaded)
                listViewCustom1.View = (View)listViewTpyeCB.SelectedItem;
        }

        private void LoadTextureIcon(int index, STGenericTexture texture)
        {
            Bitmap temp = texture.GetBitmap();
            if (temp == null)
                return;

            temp = texture.GetComponentBitmap(temp, true);
            temp = BitmapExtension.CreateImageThumbnail(temp, 80, 80);

            if (listViewCustom1.InvokeRequired)
            {
                listViewCustom1.Invoke((MethodInvoker)delegate {
                    var item = listViewCustom1.Items[index];
                    item.ImageIndex = imgListBig.Images.Count;
                    item.SubItems.Add(texture.Format.ToString());
                    item.SubItems.Add(texture.Width.ToString());
                    item.SubItems.Add(texture.Height.ToString());
                    item.SubItems.Add(texture.DataSize);

                    // Running on the UI thread
                    imgListBig.Images.Add(temp);
                    imgListSmall.Images.Add(temp);

                    var dummy = imgListBig.Handle;
                    var dummy2 = imgListSmall.Handle;
                });
            }

            temp.Dispose();
        }

        private void LayoutTextureList_DragDrop(object sender, DragEventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string filename in files)
                OpenTextureFile(filename);

            Cursor.Current = Cursors.Default;
        }

        private void OpenTextureFile(string fileName)
        {

        }

        private void listViewCustom1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
            {
                String[] strGetFormats = e.Data.GetFormats();
                e.Effect = DragDropEffects.None;
            }
        }

        private void listViewCustom1_MouseClick(object sender, MouseEventArgs e)
        {
            if (listViewCustom1.SelectedItems.Count == 0)
                return;

            var item = listViewCustom1.SelectedItems[0];
            if (e.Button == MouseButtons.Right)
            {
                STContextMenuStrip menu = new STContextMenuStrip();
                menu.Items.Add(new STToolStipMenuItem("Export", null, ActionExportTexture));
                menu.Items.Add(new STToolStipMenuItem("Replace", null, ActionReplaceTexture));
                menu.Items.Add(new STToolStripSeparator());
                menu.Items.Add(new STToolStipMenuItem("Edit Texture", null, (o, args) => EditTexture()) { Enabled = CanEditTextures() });
                menu.Items.Add(new STToolStipMenuItem("Remove Texture", null, (o, args) => RemoveSelectedTextures()));

                menu.Show(Cursor.Position);
            }
        }

        private void ActionExportTexture(object sender, EventArgs e)
        {
            List<STGenericTexture> textures = new List<STGenericTexture>();
            foreach (ListViewItem item in listViewCustom1.SelectedItems)
            {
                string resolvedKey;
                STGenericTexture resolvedTexture;
                if (TryResolveTexture(item.Text, out resolvedKey, out resolvedTexture))
                    textures.Add(resolvedTexture);
            }

            if (textures.Count == 1) {
                textures[0].ExportImage();
            }
            else if (textures.Count > 1)
            {
                List<string> Formats = new List<string>();
                Formats.Add("Microsoft DDS (.dds)");
                Formats.Add("Portable Graphics Network (.png)");
                Formats.Add("Joint Photographic Experts Group (.jpg)");
                Formats.Add("Bitmap Image (.bmp)");
                Formats.Add("Tagged Image File Format (.tiff)");

                FolderSelectDialog sfd = new FolderSelectDialog();

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = sfd.SelectedPath;

                    BatchFormatExport form = new BatchFormatExport(Formats);
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        foreach (STGenericTexture tex in textures)
                        {
                            if (form.Index == 0)
                                tex.SaveDDS(folderPath + '\\' + tex.Text + ".dds");
                            else if (form.Index == 1)
                                tex.SaveBitMap(folderPath + '\\' + tex.Text + ".png");
                            else if (form.Index == 2)
                                tex.SaveBitMap(folderPath + '\\' + tex.Text + ".jpg");
                            else if (form.Index == 3)
                                tex.SaveBitMap(folderPath + '\\' + tex.Text + ".bmp");
                            else if (form.Index == 4)
                                tex.SaveBitMap(folderPath + '\\' + tex.Text + ".tiff");
                        }
                    }
                }
            }

            textures.Clear();
        }

        private void ActionReplaceTexture(object sender, EventArgs e) {
            EditTexture();
        }

        private void listViewCustom1_ItemDrag(object sender, ItemDragEventArgs e)  {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void btnRemove_Click(object sender, EventArgs e) {
            RemoveSelectedTextures();
        }

        public void ExecuteAddTextureWorkflow()
        {
            if (!CanAddTextures())
            {
                MessageBox.Show("This layout was not loaded from an archive with TPL files.",
                    "Layout Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            AddTextureFromArchiveTplList();
        }

        public void ExecuteEditTextureWorkflow(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return;

            if (!CanEditTextures())
            {
                MessageBox.Show("This layout was not loaded from an archive with TPL files.",
                    "Layout Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EditTextureFromArchiveTplList(textureName);
        }

        public void ExecuteRemoveTextureWorkflow(IEnumerable<string> textureNames)
        {
            var names = textureNames?.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names == null || names.Count == 0)
                return;

            var result = MessageBox.Show("Are you sure you want to remove these textures?",
                "Layout Edtior", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            var hierarchyState = ParentEditor?.CaptureHierarchyViewState();
            var textureListState = CaptureTextureListState();

            foreach (var texture in names)
            {
                string textureKey;
                STGenericTexture textureValue;

                if (TryResolveTexture(texture, out textureKey, out textureValue))
                {
                    ActiveLayout.TextureManager.RemoveTexture(textureValue);
                    foreach (var bntx in PluginRuntime.bntxContainers)
                    {
                        if (bntx.Textures.ContainsKey(textureKey))
                            bntx.Textures.Remove(textureKey);
                    }
                    if (PluginRuntime.bflimTextures.ContainsKey(textureKey))
                        PluginRuntime.bflimTextures.Remove(textureKey);

                    TextureList.Remove(textureKey);
                }

                ActiveLayout.RemoveTexture(texture);
            }

            ParentEditor.UpdateViewport();
            ParentEditor.ReloadHiearchyTree();
            ParentEditor.RestoreHierarchyViewState(hierarchyState);
            QueueTextureListStateRestore(textureListState);
            ParentEditor.UpdateLayoutTextureList();
            RefreshSelectionPaneEditor();
        }

        private void RemoveSelectedTextures()
        {
            if (listViewCustom1.SelectedItems.Count == 0)
                return;

            var selectedNames = listViewCustom1.SelectedItems
                .Cast<ListViewItem>()
                .Select(x => x.Text)
                .ToList();

            ExecuteRemoveTextureWorkflow(selectedNames);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            ExecuteAddTextureWorkflow();
        }

        private void listViewCustom1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && listViewCustom1.SelectedItems.Count > 0) {
                RemoveSelectedTextures();
            }
        }

        private void btnEdit_Click(object sender, EventArgs e) {
            EditTexture();
        }

        private void EditTexture()
        {
            if (listViewCustom1.SelectedItems.Count == 0)
                return;

            string textName = listViewCustom1.SelectedItems[0].Text;
            ExecuteEditTextureWorkflow(textName);
        }

        private bool CanAddTextures()
        {
            IArchiveFile archive;
            List<string> tplFiles;
            return TryGetArchiveTplFiles(out archive, out tplFiles) && tplFiles.Count > 0;
        }

        private bool CanEditTextures()
        {
            return CanAddTextures();
        }

        private bool TryResolveTexture(string listName, out string key, out STGenericTexture texture)
        {
            key = null;
            texture = null;

            if (TextureList == null || string.IsNullOrEmpty(listName))
                return false;

            if (TextureList.ContainsKey(listName))
            {
                key = listName;
                texture = TextureList[listName];
                return true;
            }

            string normalizedInput = NormalizeTextureName(listName);

            var match = TextureList.FirstOrDefault(x =>
                string.Equals(x.Key, listName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeTextureName(x.Key), normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(x.Key), listName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(x.Value?.Text ?? string.Empty), listName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeTextureName(x.Value?.Text ?? string.Empty), normalizedInput, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(match.Key) && match.Value != null)
            {
                key = match.Key;
                texture = match.Value;
                return true;
            }

            STGenericTexture archiveTexture;
            string archiveKey;
            if (TryLoadArchiveTextureByName(listName, out archiveKey, out archiveTexture))
            {
                key = archiveKey;
                texture = archiveTexture;

                // Cache by requested name too so list entries can immediately resolve thumbnails and metadata.
                if (!string.IsNullOrEmpty(listName))
                    TextureList[listName] = archiveTexture;
                return true;
            }

            return false;
        }

        private void EnsureTextureAlias(string listName, string sourceName)
        {
            if (TextureList == null || string.IsNullOrEmpty(listName))
                return;

            string resolvedKey;
            STGenericTexture resolvedTexture;
            if (TryLoadArchiveTextureByName(sourceName, out resolvedKey, out resolvedTexture) ||
                TryResolveTexture(sourceName, out resolvedKey, out resolvedTexture) ||
                TryResolveTexture(listName, out resolvedKey, out resolvedTexture))
            {
                TextureList[listName] = resolvedTexture;
            }
        }

        private bool TryLoadArchiveTextureByName(string textureName, out string archiveKey, out STGenericTexture texture)
        {
            archiveKey = null;
            texture = null;

            var archive = ActiveLayout?.FileInfo?.IFileInfo?.ArchiveParent;
            if (archive == null || string.IsNullOrEmpty(textureName))
                return false;

            string norm = NormalizeTextureName(textureName);
            var file = archive.Files.FirstOrDefault(x =>
                x?.FileName != null &&
                Path.GetExtension(x.FileName).Equals(".tpl", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(Path.GetFileName(x.FileName), textureName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(NormalizeTextureName(x.FileName), norm, StringComparison.OrdinalIgnoreCase)));

            if (file == null)
                return false;

            var opened = file.FileFormat ?? file.OpenFile();
            var tpl = opened as TPL;
            var firstTexture = tpl?.TextureList?.FirstOrDefault();
            if (firstTexture == null)
                return false;

            archiveKey = file.FileName.Replace('\\', '/');
            texture = firstTexture;

            TextureList[archiveKey] = firstTexture;
            TextureList[Path.GetFileName(archiveKey)] = firstTexture;
            return true;
        }

        private bool TryGetArchiveTplFiles(out IArchiveFile archive, out List<string> tplFiles)
        {
            archive = ActiveLayout?.FileInfo?.IFileInfo?.ArchiveParent;
            tplFiles = new List<string>();
            if (archive == null)
                return false;

            tplFiles = archive.Files
                .Where(x => x?.FileName != null && Path.GetExtension(x.FileName).Equals(".tpl", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.FileName.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return tplFiles.Count > 0;
        }

        private void AddTextureFromArchiveTplList()
        {
            IArchiveFile archive;
            List<string> tplFiles;
            if (!TryGetArchiveTplFiles(out archive, out tplFiles))
                return;

            var used = new HashSet<string>(ActiveLayout.Textures.Select(NormalizeTextureName), StringComparer.OrdinalIgnoreCase);
            var available = tplFiles.Where(x => !used.Contains(NormalizeTextureName(x))).ToList();
            if (available.Count == 0)
            {
                MessageBox.Show("No additional TPL files are available to add.", "Layout Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = PromptSelectTexture("Add Texture", "Select TPL to add", available, archive);
            if (string.IsNullOrEmpty(selected))
                return;

            string addedName = Path.GetFileName(selected);
            var hierarchyState = ParentEditor?.CaptureHierarchyViewState();
            var textureListState = CaptureTextureListState();
            ActiveLayout.AddTexture(addedName);
            EnsureTextureAlias(addedName, selected);
            ParentEditor.ReloadHiearchyTree();
            ParentEditor.RestoreHierarchyViewState(hierarchyState);
            QueueTextureListStateRestore(textureListState);
            ParentEditor.UpdateLayoutTextureList();
            ParentEditor.UpdateViewport();
            RefreshSelectionPaneEditor();
        }

        private void EditTextureFromArchiveTplList(string currentTextureName)
        {
            IArchiveFile archive;
            List<string> tplFiles;
            if (!TryGetArchiveTplFiles(out archive, out tplFiles))
                return;

            int index = FindTextureIndexByName(currentTextureName);
            if (index < 0)
            {
                MessageBox.Show($"Unable to resolve selected texture '{currentTextureName}'.", "Layout Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var currentNormalized = NormalizeTextureName(ActiveLayout.Textures[index]);
            var used = new HashSet<string>(ActiveLayout.Textures
                .Where((x, i) => i != index)
                .Select(NormalizeTextureName), StringComparer.OrdinalIgnoreCase);

            var available = tplFiles
                .Where(x => !used.Contains(NormalizeTextureName(x)))
                .ToList();

            if (available.Count == 0)
            {
                MessageBox.Show("No replacement TPL files are available.", "Layout Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = PromptSelectTexture("Edit Texture", "Select replacement TPL", available, archive, currentNormalized);
            if (string.IsNullOrEmpty(selected))
                return;

            string oldTextureName = ActiveLayout.Textures[index];
            string replacedName = Path.GetFileName(selected);
            var hierarchyState = ParentEditor?.CaptureHierarchyViewState();
            var textureListState = CaptureTextureListState();
            ActiveLayout.Textures[index] = replacedName;
            EnsureTextureAlias(replacedName, selected);
            UpdateMaterialTextureReferences(oldTextureName, replacedName);
            ParentEditor.ReloadHiearchyTree();
            ParentEditor.RestoreHierarchyViewState(hierarchyState);
            QueueTextureListStateRestore(textureListState);
            ParentEditor.UpdateLayoutTextureList();
            ParentEditor.UpdateViewport();
            RefreshSelectionPaneEditor();
        }

        private void UpdateMaterialTextureReferences(string oldTextureName, string newTextureName)
        {
            if (ActiveLayout?.Materials == null || string.IsNullOrEmpty(oldTextureName) || string.IsNullOrEmpty(newTextureName))
                return;

            string oldNorm = NormalizeTextureName(oldTextureName);
            foreach (var mat in ActiveLayout.Materials)
            {
                if (mat?.TextureMaps == null)
                    continue;

                for (int i = 0; i < mat.TextureMaps.Length; i++)
                {
                    var map = mat.TextureMaps[i];
                    if (map == null || string.IsNullOrEmpty(map.Name))
                        continue;

                    if (string.Equals(map.Name, oldTextureName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(NormalizeTextureName(map.Name), oldNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        map.Name = newTextureName;
                    }
                }

                if (mat.animController?.TexturePatterns != null && mat.animController.TexturePatterns.Count > 0)
                {
                    var keys = mat.animController.TexturePatterns.Keys.ToList();
                    foreach (var key in keys)
                    {
                        var patternName = mat.animController.TexturePatterns[key];
                        if (string.IsNullOrEmpty(patternName))
                            continue;

                        if (string.Equals(patternName, oldTextureName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(NormalizeTextureName(patternName), oldNorm, StringComparison.OrdinalIgnoreCase))
                        {
                            mat.animController.TexturePatterns[key] = newTextureName;
                        }
                    }
                }
            }
        }

        private void RefreshSelectionPaneEditor()
        {
            var selected = ParentEditor?.SelectedPanes;
            if (selected != null && selected.Count > 0)
                ParentEditor.LoadPaneEditorOnSelect(selected.ToList());
            else
                ParentEditor?.RefreshEditors();
        }

        private int FindTextureIndexByName(string textureName)
        {
            if (ActiveLayout?.Textures == null)
                return -1;

            for (int i = 0; i < ActiveLayout.Textures.Count; i++)
            {
                if (string.Equals(ActiveLayout.Textures[i], textureName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeTextureName(ActiveLayout.Textures[i]), NormalizeTextureName(textureName), StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string NormalizeTextureName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return Path.GetFileNameWithoutExtension(value.Replace('\\', '/'));
        }

        private string PromptSelectTexture(string title, string label, List<string> options, IArchiveFile archive, string currentNormalized = null)
        {
            using (var form = new Form())
            using (var listView = new ListView())
            using (var imageList = new ImageList())
            using (var header = new Label())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                form.StartPosition = FormStartPosition.CenterParent;
                form.Width = 720;
                form.Height = 420;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.BackColor = Color.FromArgb(45, 45, 48);
                form.ForeColor = Color.Gainsboro;

                header.Text = label;
                header.Dock = DockStyle.Top;
                header.Height = 26;
                header.TextAlign = ContentAlignment.MiddleLeft;
                header.BackColor = Color.FromArgb(45, 45, 48);
                header.ForeColor = Color.Gainsboro;

                imageList.ImageSize = new Size(56, 56);
                imageList.ColorDepth = ColorDepth.Depth32Bit;

                listView.Dock = DockStyle.Fill;
                listView.View = View.LargeIcon;
                listView.HideSelection = false;
                listView.MultiSelect = false;
                listView.LargeImageList = imageList;
                listView.BackColor = Color.FromArgb(37, 37, 38);
                listView.ForeColor = Color.Gainsboro;

                imageList.Images.Add(FirstPlugin.Properties.Resources.MissingTexture);
                int selectedIndex = -1;

                for (int i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    var preview = LoadArchiveTplPreview(option, archive);
                    if (preview != null)
                        imageList.Images.Add(preview);
                    else
                        imageList.Images.Add(FirstPlugin.Properties.Resources.MissingTexture);

                    var item = new ListViewItem(option)
                    {
                        ImageIndex = imageList.Images.Count - 1,
                        Tag = option,
                    };
                    listView.Items.Add(item);

                    if (selectedIndex == -1 && !string.IsNullOrEmpty(currentNormalized) &&
                        string.Equals(NormalizeTextureName(option), currentNormalized, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                    }
                }

                if (selectedIndex < 0 && listView.Items.Count > 0)
                    selectedIndex = 0;
                if (selectedIndex >= 0)
                    listView.Items[selectedIndex].Selected = true;

                listView.DoubleClick += (s, e) =>
                {
                    if (listView.SelectedItems.Count > 0)
                    {
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                };

                var panel = new Panel();
                panel.Dock = DockStyle.Bottom;
                panel.Height = 40;
                panel.BackColor = Color.FromArgb(45, 45, 48);

                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.Width = 90;
                okButton.Height = 26;
                okButton.Left = form.ClientSize.Width - 196;
                okButton.Top = 7;
                okButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;

                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Width = 90;
                cancelButton.Height = 26;
                cancelButton.Left = form.ClientSize.Width - 100;
                cancelButton.Top = 7;
                cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;

                panel.Controls.Add(okButton);
                panel.Controls.Add(cancelButton);

                form.Controls.Add(listView);
                form.Controls.Add(panel);
                form.Controls.Add(header);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog(this) == DialogResult.OK && listView.SelectedItems.Count > 0)
                    return listView.SelectedItems[0].Tag?.ToString();

                return null;
            }
        }

        private Image LoadArchiveTplPreview(string tplFilePath, IArchiveFile archive)
        {
            try
            {
                if (TextureList != null)
                {
                    var fromCache = TextureList.FirstOrDefault(x =>
                        string.Equals(x.Key, tplFilePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(NormalizeTextureName(x.Key), NormalizeTextureName(tplFilePath), StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(fromCache.Key) && fromCache.Value != null)
                    {
                        var bmp = fromCache.Value.GetBitmap();
                        if (bmp != null)
                            return BitmapExtension.CreateImageThumbnail(bmp, 56, 56);
                    }
                }

                var file = archive?.Files?.FirstOrDefault(x =>
                    string.Equals(x.FileName?.Replace('\\', '/'), tplFilePath, StringComparison.OrdinalIgnoreCase));
                if (file == null)
                    return null;

                var opened = file.FileFormat ?? file.OpenFile();
                var tpl = opened as TPL;
                var tex = tpl?.TextureList?.FirstOrDefault();
                if (tex == null)
                    return null;

                var preview = tex.GetBitmap();
                if (preview == null)
                    return null;

                return BitmapExtension.CreateImageThumbnail(preview, 56, 56);
            }
            catch
            {
                return null;
            }
        }

        private void listViewCustom1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewCustom1.SelectedItems.Count > 0 && CanEditTextures())
                btnEdit.Enabled = true;
            else
                btnEdit.Enabled = false;
        }
    }
}
