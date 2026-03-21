using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text;
using System.Linq;
using GL_EditorFramework.Interfaces;
using GL_EditorFramework.EditorDrawables;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Toolbox.Library.Animations;
using Toolbox.Library.IO;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Toolbox.Library.FBX;

namespace Toolbox.Library.Forms
{
    public partial class ObjectEditorTree : UserControl
    {
        private bool SuppressAfterSelectEvent = false;
        private bool _suppressBackupToggleEvent = false;

        private bool IsSearchPanelDocked
        {
            get
            {
                return dockSearchListToolStripMenuItem.Checked;
            }
            set
            {
                dockSearchListToolStripMenuItem.Checked = value;
            }
        }

        private enum TreeNodeSize
        {
            Small,
            Normal,
            Large,
            ExtraLarge,
        }

        public ObjectEditor ObjectEditor;

        public void BeginUpdate() { treeViewCustom1.BeginUpdate(); }
        public void EndUpdate() { treeViewCustom1.EndUpdate(); }

        public void ReloadArchiveFile(IFileFormat fileFormat)
        {
            this.treeViewCustom1.Nodes.Clear();
            AddIArchiveFile(fileFormat);
        }

        public void AddIArchiveFile(IFileFormat FileFormat)
        {
            var FileRoot = new ArchiveRootNodeWrapper(FileFormat.FileName, (IArchiveFile)FileFormat);
            FileRoot.FillTreeNodes();
            AddNode(FileRoot);

            if (FileFormat is TreeNode) //It can still be both, so add all it's nodes
            {
                foreach (TreeNode n in ((TreeNode)FileFormat).Nodes)
                    FileRoot.Nodes.Add(n);
            }

            if (FileFormat is IArchiveQuickAccess)
            {
                var lookup = ((IArchiveQuickAccess)FileFormat).CategoryLookup;

                TreeNode quickAcessNode = new TreeNode("Quick access");
                AddNode(quickAcessNode);

                Dictionary<string, TreeNode> folders = new Dictionary<string, TreeNode>();
                for (int i = 0; i < FileRoot.FileNodes.Count; i++)
                {
                    string fileName = FileRoot.FileNodes[i].Item1.FileName;
                    string name = System.IO.Path.GetFileName(fileName);
                    string ext = Utils.GetExtension(fileName);

                    var fileNode = FileRoot.FileNodes[i].Item2;

                    string folder = "Other";
                    if (lookup.ContainsKey(ext))
                        folder = lookup[ext];

                    if (!folders.ContainsKey(folder)) {
                        var dirNode = new TreeNode(folder);
                        folders.Add(folder, dirNode);
                        quickAcessNode.Nodes.Add(dirNode);
                    }

                    folders[folder].Nodes.Add(new TreeNode()
                    {
                        Tag = fileNode,
                        Text = name,
                        ImageKey = fileNode.ImageKey,
                        SelectedImageKey = fileNode.SelectedImageKey,
                    });
                    // dirNode.Nodes.Add(FileRoot.FileNodes[i].Item2);
                }
            }

            SelectNode(FileRoot);
            RefreshBackupCheckboxState();
            for (int i = 0; i < FileRoot.FileNodes.Count; i++)
            {
                if (FileRoot.FileNodes[i].Item1.OpenFileFormatOnLoad)
                {
                    if (FileRoot.FileNodes[i].Item2 is ArchiveFileWrapper)
                    {
                        try {
                            ((ArchiveFileWrapper)FileRoot.FileNodes[i].Item2).OpenFileFormat(treeViewCustom1);
                        }
                        catch
                        {

                        }
                    }
                }
            }
        }

        public void AddNodeCollection(TreeNodeCollection nodes, bool ClearNodes)
        {
            // Invoke the treeview to add the nodes
            treeViewCustom1.Invoke((Action)delegate ()
            {
                treeViewCustom1.BeginUpdate(); // No visual updates until we say 
                if (ClearNodes)
                    treeViewCustom1.Nodes.Clear(); // Remove existing nodes

                foreach (TreeNode node in nodes)
                    treeViewCustom1.Nodes.Add(node); // Add the new nodes

                treeViewCustom1.EndUpdate(); // Allow the treeview to update visually
            });

        }

        public TreeNodeCollection GetNodes() { return treeViewCustom1.Nodes; }

        public void AddNode(TreeNode node, bool ClearAllNodes = false)
        {
            if (treeViewCustom1.InvokeRequired)
            {
                // Invoke the treeview to add the nodes
                treeViewCustom1.Invoke((Action)delegate ()
                {
                    AddNodes(node, ClearAllNodes);
                });
            }
            else
            {
                AddNodes(node, ClearAllNodes);
            }
        }

        public void SelectFirstNode() { treeViewCustom1.SelectedNode = treeViewCustom1.Nodes[0]; }

        private void AddNodes(TreeNode node, bool ClearAllNodes = false)
        {
            treeViewCustom1.BeginUpdate(); // No visual updates until we say 
            if (ClearAllNodes)
                ClearNodes();
            treeViewCustom1.Nodes.Add(node); // Add the new nodes
            treeViewCustom1.EndUpdate(); // Allow the treeview to update visually

            RefreshBackupCheckboxState();

            if (node is ISingleTextureIconLoader) {
                LoadGenericTextureIcons((ISingleTextureIconLoader)node);
            }
        }

        public void ClearNodes()
        {
            treeViewCustom1.Nodes.Clear();
        }

        public bool AddFilesToActiveEditor
        {
            get
            {
                return activeEditorChkBox.Checked;
            }
            set
            {
                activeEditorChkBox.Checked = value;
                Runtime.AddFilesToActiveObjectEditor = value;
            }
        }

        public void RefreshBackupCheckboxState()
        {
            var activeFile = GetActiveFile();
            string filePath = activeFile?.FilePath;

            _suppressBackupToggleEvent = true;
            enableBackupsChkBox.Enabled = !string.IsNullOrEmpty(filePath);
            enableBackupsChkBox.Checked = Runtime.IsBackupEnabledForFile(filePath);
            _suppressBackupToggleEvent = false;
        }

        public ObjectEditorTree(ObjectEditor objectEditor)
        {
            InitializeComponent();

            btnPanelDisplay.ForeColor = FormThemes.BaseTheme.DisabledBorderColor;

            UpdateSearchPanelDockState();

            ObjectEditor = objectEditor;

            if (Runtime.ObjectEditor.ListPanelWidth > 0)
                splitContainer1.Panel1.Width = Runtime.ObjectEditor.ListPanelWidth;

            treeViewCustom1.BackColor = FormThemes.BaseTheme.ObjectEditorBackColor;

            AddFilesToActiveEditor = Runtime.AddFilesToActiveObjectEditor;

            foreach (TreeNodeSize nodeSize in (TreeNodeSize[])Enum.GetValues(typeof(TreeNodeSize)))
                nodeSizeCB.Items.Add(nodeSize);

            nodeSizeCB.SelectedIndex = 1;

            RefreshBackupCheckboxState();
        }

        public Viewport GetViewport() => viewport;

        //Attatch a viewport instance here if created.
        //If the editor gets switched, we can keep the previous viewed area when switched back
        Viewport viewport = null;

        bool IsLoaded = false;
        public void LoadViewport(Viewport Viewport)
        {
            viewport = Viewport;

            IsLoaded = true;
        }

        public IFileFormat GetActiveFile()
        {
            if (treeViewCustom1.Nodes.Count == 0)
                return null;

            if (treeViewCustom1.Nodes[0] is IFileFormat)
                return (IFileFormat)treeViewCustom1.Nodes[0];
            if (treeViewCustom1.Nodes[0] is ArchiveBase)
                return (IFileFormat)((ArchiveBase)treeViewCustom1.Nodes[0]).ArchiveFile;
            return null;
        }

        public void LoadEditor(Control control)
        {
            foreach (var ctrl in stPanel2.Controls)
            {
                if (ctrl is STUserControl)
                    ((STUserControl)ctrl).OnControlClosing();
            }

            stPanel2.Controls.Clear();
            stPanel2.Controls.Add(control);
        }

        bool RenderedObjectWasSelected = false;
        private void treeViewCustom1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (SuppressAfterSelectEvent)
                return;

            var node = treeViewCustom1.SelectedNode;

            if (node is Animation || node is IAnimationContainer) {
                OnAnimationSelected(node);
            }

            //Set the current index used determine what bone is selected.
            //Update viewport for selection viewing
            if (node is STBone)
            {
                Runtime.SelectedBoneIndex = ((STBone)node).GetIndex();
            }
            else
                Runtime.SelectedBoneIndex = -1;

            //Set click events for custom nodes
            if (node is TreeNodeCustom)
            {
                ((TreeNodeCustom)node).OnClick(treeViewCustom1);
            }

            if (node.Tag != null && node.Tag is TreeNodeCustom)
                ((TreeNodeCustom)node.Tag).OnClick(treeViewCustom1);

            //Check if it is renderable for updating the viewport
            if (IsRenderable(node))
            {
                LibraryGUI.UpdateViewport();
                RenderedObjectWasSelected = true;
            }
            else
            {
                //Check if the object was previously selected
                //This will disable selection view and other things
                if (RenderedObjectWasSelected)
                {
                    LibraryGUI.UpdateViewport();
                    RenderedObjectWasSelected = false;
                }
            }
        }

        public bool IsRenderable(TreeNode obj)
        {
            if (obj is STGenericModel)
                return true;
            if (obj is STGenericObject)
                return true;
            if (obj is STBone)
                return true;
            if (obj is STSkeleton)
                return true;
            if (obj is STGenericMaterial)
                return true;

            return false;
        }

        private void treeViewCustom1_DoubleClick(object sender, EventArgs e)
        {
            if (treeViewCustom1.SelectedNode == null) return;

            if (treeViewCustom1.SelectedNode is TreeNodeCustom)
            {
                ((TreeNodeCustom)treeViewCustom1.SelectedNode).OnDoubleMouseClick(treeViewCustom1);
            }
            if (treeViewCustom1.SelectedNode.Tag != null && treeViewCustom1.SelectedNode.Tag is TreeNodeCustom)
            {
                ((TreeNodeCustom)treeViewCustom1.SelectedNode.Tag).OnDoubleMouseClick(treeViewCustom1);
            }
        }

        public void UpdateTextureIcon(ISingleTextureIconLoader texturIcon, Image image) {
            treeViewCustom1.ReloadTextureIcons(texturIcon, image);
        }

        public List<Control> GetEditors()
        {
            List<Control> controls = new List<Control>();
            foreach (Control ctrl in stPanel2.Controls)
                controls.Add(ctrl);
            return controls;
        }

        public void FormClosing()
        {
            if (searchForm != null)
            {
                searchForm.OnControlClosing();
                searchForm.Dispose();
            }

            foreach (var control in stPanel2.Controls)
            {
                if (control is STUserControl)
                    ((STUserControl)control).OnControlClosing();
            }

            foreach (var node in TreeViewExtensions.Collect(treeViewCustom1.Nodes))
            {
                if (node is ArchiveRootNodeWrapper)
                {
                    var file = ((ArchiveRootNodeWrapper)node).ArchiveFile;
                    ((IFileFormat)file).Unload();
                }
                else if (node is IFileFormat)
                {
                    ((IFileFormat)node).Unload();
                }
            }
            ClearNodes();
        }

        private ToolStripItem[] GetArchiveMenus(TreeNode node, ArchiveFileInfo info)
        {
            return info.FileWrapper.GetContextMenuItems();
        }

        private void treeViewCustom1_MouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                treeNodeContextMenu.Items.Clear();

                var menuItems = GetMenuItems(e.Node);
                treeNodeContextMenu.Items.AddRange(menuItems);

                //Select the node without the evemt
                //We don't want editors displaying on only right clicking
                SuppressAfterSelectEvent = true;
                treeViewCustom1.SelectedNode = e.Node;
                SuppressAfterSelectEvent = false;

                if (treeNodeContextMenu.Items.Count > 0)
                    treeNodeContextMenu.Show(Cursor.Position);
            }
            else
            {
            }
        }

        private ToolStripItem[] GetMenuItems(TreeNode selectednode)
        {
            List<ToolStripItem> archiveMenus = new List<ToolStripItem>();
            List<ToolStripItem> menuItems = new List<ToolStripItem>();

            Console.WriteLine($"tag {selectednode.Tag }");
            if (selectednode.Tag != null && selectednode.Tag is ArchiveFileInfo)
            {
                //The tag gets set when an archive is replaced by a treenode
                //Todo store this in a better place as devs could possiblly replace this
                //Create menus when an archive node is replaced
                archiveMenus.AddRange(GetArchiveMenus(selectednode, (ArchiveFileInfo)selectednode.Tag));
                Console.WriteLine($"archiveMenus {archiveMenus.Count}");
            }

            if (selectednode is IExportableModel)
            {
                menuItems.Add(new ToolStripMenuItem("Export Model", null, ExportModelAction, Keys.Control | Keys.E));
            }

            bool IsRoot = selectednode.Parent == null;
            bool HasChildren = selectednode.Nodes.Count > 0;

            IContextMenuNode node = null;
            if (selectednode is IContextMenuNode)
            {
                node = (IContextMenuNode)selectednode;
            }
            else if (selectednode.Tag != null && selectednode.Tag is IContextMenuNode)
            {
                node = (IContextMenuNode)selectednode.Tag;
            }

            if (selectednode is IAnimationContainer)
            {
                var anim = ((IAnimationContainer)selectednode).AnimationController;
                if (anim is IContextMenuNode)
                    node = (IContextMenuNode)anim;
            }

            if (node != null)
            {
                if (IsRoot)
                {
                    foreach (var item in node.GetContextMenuItems())
                    {
                        if (item.Text != "Delete" && item.Text != "Remove")
                            menuItems.Add(item);
                    }
                    menuItems.Add(new ToolStripMenuItem("Delete", null, DeleteAction, Keys.Delete));
                }
                else
                {
                    menuItems.AddRange(node.GetContextMenuItems());
                }

                bool HasCollpase = false;
                bool HasExpand = false;
                foreach (var item in node.GetContextMenuItems())
                {
                    if (item.Text == "Collapse All")
                        HasCollpase = true;
                    if (item.Text == "Expand All")
                        HasExpand = true;
                }

                if (!HasCollpase && HasChildren)
                    menuItems.Add(new ToolStripMenuItem("Collapse All", null, CollapseAllAction, Keys.Control | Keys.Q));

                if (!HasExpand && HasChildren)
                    menuItems.Add(new ToolStripMenuItem("Expand All", null, ExpandAllAction, Keys.Control | Keys.P));
            }

            if (archiveMenus.Count > 0)
            {
                if (menuItems.Count > 0)
                {
                    STToolStipMenuItem archiveItem = new STToolStipMenuItem("Archive");
                    treeNodeContextMenu.Items.Add(archiveItem);

                    foreach (var item in archiveMenus)
                        archiveItem.DropDownItems.Add(item);
                }
                else
                {
                    if (archiveMenus.Count > 0)
                        treeNodeContextMenu.Items.AddRange(archiveMenus.ToArray());
                }
            }

            var fileFormat = TryGetActiveFile(selectednode);
            if (fileFormat != null)
            {
                string path = fileFormat.FilePath;
                if (File.Exists(path))
                    menuItems.Add(new ToolStripMenuItem("Open In Explorer", null, SelectFileInExplorer, Keys.Control | Keys.Q));
            }

            Keys currentKey = Keys.A;
            List<Keys> shortcuts = new List<Keys>();
            foreach (ToolStripItem item in menuItems)
            {
                if (item is ToolStripMenuItem) {
                    var menu = item as ToolStripMenuItem;
                    CheckDuplicateShortcuts(menu, currentKey, shortcuts);
                }
            }

            return menuItems.ToArray();
        }

        private void CheckDuplicateShortcuts(ToolStripMenuItem menu, Keys current, List<Keys> shortcuts)
        {
            if (menu.ShowShortcutKeys)
            {
                if (!shortcuts.Contains(menu.ShortcutKeys))
                    shortcuts.Add(menu.ShortcutKeys);
                else
                {
                    //Auto set the key
                    var controlKey = Keys.Control | current;
                    if (!shortcuts.Contains(controlKey))
                    {
                        shortcuts.Add(controlKey);
                        menu.ShortcutKeys = controlKey;
                    }
                    else
                    {
                        menu.ShortcutKeys = Keys.Control | current++;
                        CheckDuplicateShortcuts(menu, current, shortcuts);
                    }
                }
            }
        }

        private IFileFormat TryGetActiveFile(TreeNode node)
        {
            if (node.Tag != null && node.Tag is IFileFormat)
                return (IFileFormat)node.Tag;
            else if (node is IFileFormat)
                return (IFileFormat)node;
            else if (node is ArchiveRootNodeWrapper)
            {
                if (((ArchiveRootNodeWrapper)node).ArchiveFile is IFileFormat)
                    return ((ArchiveRootNodeWrapper)node).ArchiveFile as IFileFormat;
                else
                    return null;
            }
            else
                return null;
        }

        private void ExportModelAction(object sender, EventArgs args)
        {
            var node = treeViewCustom1.SelectedNode as IExportableModel;
            if (node != null)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Autodesk FBX (*.fbx)|*.fbx|Supported Formats|*.dae;";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    ExportModelSettings exportDlg = new ExportModelSettings();
                    if (exportDlg.ShowDialog() == DialogResult.OK)
                    {
                        if (sfd.FileName.ToLower().EndsWith(".fbx"))
                            ExportModelFbx(node, sfd.FileName, exportDlg.Settings);
                        else
                            ExportModel(node, sfd.FileName, exportDlg.Settings);
                    }
                }
            }
        }

        public void ExportModelFbx(IExportableModel exportableModel, string fileName, DAE.ExportSettings settings)
        {
            var model = new STGenericModel();
            model.Materials = exportableModel.ExportableMaterials;
            model.Objects = exportableModel.ExportableMeshes;
            var textures = new List<STGenericTexture>();
            foreach (var tex in exportableModel.ExportableTextures)
                textures.Add(tex);

            Toolbox.Library.FBX.FbxExporter.Export(fileName, settings, model, textures, exportableModel.ExportableSkeleton);
        }

        public void ExportModel(IExportableModel exportableModel, string fileName, DAE.ExportSettings settings)
        {
            var model = new STGenericModel();
            model.Materials = exportableModel.ExportableMaterials;
            model.Objects = exportableModel.ExportableMeshes;
            var textures = new List<STGenericTexture>();
            foreach (var tex in exportableModel.ExportableTextures)
                textures.Add(tex);

            DAE.Export(fileName, settings, model, textures, exportableModel.ExportableSkeleton);
        }

        private void SelectFileInExplorer(object sender, EventArgs args)
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null) {
                var fileFormat = TryGetActiveFile(node);
                string argument = "/select, \"" + fileFormat.FilePath + "\"";
                System.Diagnostics.Process.Start("explorer.exe", argument);
            }
        }

        private void ExpandAllAction(object sender, EventArgs args)
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null)
                node.ExpandAll();
        }

        private void CollapseAllAction(object sender, EventArgs args)
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null)
                node.Collapse();
        }

        private void DeleteAction(object sender, EventArgs args)
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null)
            {
                var result = MessageBox.Show("If you remove this file, any unsaved progress will be lost! Continue?",
                    "Remove Dialog", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    if (node is IFileFormat)
                    {
                        ((IFileFormat)node).Unload();
                    }

                    treeViewCustom1.Nodes.Remove(node);

                    if (treeViewCustom1.Nodes.Count == 0)
                        ResetEditor();

                    //Force garbage collection.
                    GC.Collect();

                    // Wait for all finalizers to complete before continuing.
                    GC.WaitForPendingFinalizers();

                    ((IUpdateForm)Runtime.MainForm).UpdateForm();
                }
            }
        }

        private void ResetEditor()
        {
            foreach (Control control in stPanel2.Controls)
            {
                if (control is STUserControl)
                    ((STUserControl)control).OnControlClosing();
            }

            stPanel2.Controls.Clear();
        }

        private void OnAnimationSelected(TreeNode Node)
        {
            if (Node is Animation)
            {
                Viewport viewport = LibraryGUI.GetActiveViewport();
                if (viewport == null)
                    return;

                if (((Animation)Node).Bones.Count <= 0)
                    ((Animation)Node).OpenAnimationData();

                string AnimName = Node.Text;
                AnimName = Regex.Match(AnimName, @"([A-Z][0-9][0-9])(.*)").Groups[0].ToString();
                if (AnimName.Length > 3)
                    AnimName = AnimName.Substring(3);

                Animation running = new Animation(AnimName);
                running.ReplaceMe((Animation)Node);
                running.Tag = Node;

                Queue<TreeNode> NodeQueue = new Queue<TreeNode>();
                foreach (TreeNode n in treeViewCustom1.Nodes)
                {
                    NodeQueue.Enqueue(n);
                }
                while (NodeQueue.Count > 0)
                {
                    try
                    {
                        TreeNode n = NodeQueue.Dequeue();
                        string NodeName = Regex.Match(n.Text, @"([A-Z][0-9][0-9])(.*)").Groups[0].ToString();
                        if (NodeName.Length <= 3)
                            Console.WriteLine(NodeName);
                        else
                            NodeName = NodeName.Substring(3);
                        if (n is Animation)
                        {
                            if (n == Node)
                                continue;
                            if (NodeName.Equals(AnimName))
                            {
                                running.Children.Add(n);
                            }
                        }
                        if (n is AnimationGroupNode)
                        {
                            foreach (TreeNode tn in n.Nodes)
                                NodeQueue.Enqueue(tn);
                        }
                    }
                    catch
                    {

                    }
                }

                if (LibraryGUI.GetAnimationPanel() != null)
                {
                    LibraryGUI.GetAnimationPanel().CurrentAnimation = running;
                }
            }
            if (Node is IAnimationContainer)
            {
                Viewport viewport = LibraryGUI.GetActiveViewport();
                if (viewport == null)
                    return;

                var running = ((IAnimationContainer)Node).AnimationController;
                if (LibraryGUI.GetAnimationPanel() != null) {
                    Console.WriteLine($"running {running.Name}");

                    LibraryGUI.GetAnimationPanel().CurrentSTAnimation = running;
                }
            }
        }

        public void RemoveFile(TreeNode File)
        {
            if (File is IFileFormat)
            {
                ((IFileFormat)File).Unload();
            }

            treeViewCustom1.Nodes.Remove(File);
        }

        public void ResetControls()
        {
            treeViewCustom1.Nodes.Clear();
            Text = "";

            ResetEditor();
        }

        bool UpdateViewport = false;
        bool SupressUpdateEvent = false;
        private void treeViewCustom1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            UpdateViewport = false;

            if (e.Node is STGenericModel)
            {
                SupressUpdateEvent = true;
                CheckChildNodes(e.Node, e.Node.Checked);
                SupressUpdateEvent = false;
            }

            if (Control.ModifierKeys == Keys.Shift && !SupressUpdateEvent)
            {
                SupressUpdateEvent = true;
                CheckChildNodes(e.Node, e.Node.Checked);
                SupressUpdateEvent = false;
            }

            if (e.Node is STGenericObject && !SupressUpdateEvent)
            {
                UpdateViewport = true;
            }
            else if (e.Node is STBone && !SupressUpdateEvent)
            {
                UpdateViewport = true;
            }

            if (UpdateViewport)
            {
                LibraryGUI.UpdateViewport();
            }
        }

        private void CheckChildNodes(TreeNode node, bool IsChecked)
        {
            foreach (TreeNode n in node.Nodes)
            {
                n.Checked = IsChecked;
                if (n.Nodes.Count > 0)
                {
                    CheckChildNodes(n, IsChecked);
                }
            }

            UpdateViewport = true; //Update viewport on the last node checked
        }

        private void treeViewCustom1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = true;
            bool IsCheckable = (e.Node is STGenericObject || e.Node is STGenericModel
                                                          || e.Node is STBone);
            if (!IsCheckable)
                TreeViewExtensions.HideCheckBox(e.Node);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            AddNewFile();
        }

        private void toolStripButton1_Click(object sender, EventArgs e) {
            AddNewFile();
        }

        private void AddNewFile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = Utils.GetAllFilters(FileManager.GetFileFormats());
            ofd.Multiselect = true;

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (string file in ofd.FileNames)
                    OpenFile(file);

                Cursor.Current = Cursors.Default;
            }
        }

        private void OpenFile(string FileName)
        {
            object file = STFileLoader.OpenFileFormat(FileName);

            if (file is TreeNode)
            {
                var node = (TreeNode)file;
                AddNode(node);
            }
            else if (file is IArchiveFile)
            {
                AddIArchiveFile((IFileFormat)file);
            }
            else
            {
                STErrorDialog.Show("Invalid file type. Cannot add file to object list.", "Object List", "");
            }

            ((IUpdateForm)Runtime.MainForm).UpdateForm();
        }

        private void sortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeViewCustom1.Sort();
        }

        private void createTextureMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var activeFormat = GetActiveFile();
            if (!(activeFormat is IArchiveFile archiveFile))
            {
                MessageBox.Show("Open an archive first.", "Create Texture Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var rows = new List<BrlytTextureUsageRow>();
                var textureDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Start from all texture assets in the archive so unused textures are included as all-False rows.
                foreach (var file in archiveFile.Files)
                {
                    if (!IsLikelyTextureFileName(file.FileName))
                        continue;

                    string key = NormalizeTextureKey(file.FileName);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!textureDisplayNames.ContainsKey(key))
                        textureDisplayNames[key] = CleanupTextureDisplayName(file.FileName);
                }

                foreach (var file in archiveFile.Files)
                {
                    if (!IsLikelyLayoutFileName(file.FileName))
                        continue;

                    object fileFormat;
                    try
                    {
                        fileFormat = file.OpenFile();
                    }
                    catch
                    {
                        continue;
                    }

                    var textures = GetLayoutTextureNames(fileFormat);
                    if (textures == null)
                        continue;

                    var row = new BrlytTextureUsageRow()
                    {
                        BrlytName = CleanupBrlytDisplayName(file.FileName),
                        TextureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    };

                    foreach (var texture in textures)
                    {
                        string key = NormalizeTextureKey(texture);
                        if (string.IsNullOrEmpty(key))
                            continue;

                        if (!textureDisplayNames.ContainsKey(key))
                            textureDisplayNames[key] = CleanupTextureDisplayName(texture);

                        row.TextureKeys.Add(key);
                    }

                    rows.Add(row);
                }

                if (rows.Count == 0)
                {
                    MessageBox.Show("No layout files with texture lists were found in the active archive.", "Create Texture Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                rows = rows.OrderBy(x => x.BrlytName, StringComparer.OrdinalIgnoreCase).ToList();
                var textureKeys = textureDisplayNames.Keys
                    .OrderBy(x => textureDisplayNames[x], StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string archiveName = System.IO.Path.GetFileNameWithoutExtension(activeFormat.FileName ?? "Archive");
                string archiveDirectory = string.Empty;
                if (!string.IsNullOrEmpty(activeFormat.FilePath))
                    archiveDirectory = System.IO.Path.GetDirectoryName(activeFormat.FilePath);

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = $"{archiveName}_TextureMap.ods";
                sfd.Filter = "OpenDocument Spreadsheet (*.ods)|*.ods|HTML Spreadsheet (*.html)|*.html|CSV Spreadsheet (*.csv)|*.csv";
                if (Directory.Exists(archiveDirectory))
                    sfd.InitialDirectory = archiveDirectory;

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                string ext = System.IO.Path.GetExtension(sfd.FileName).ToLowerInvariant();
                if (ext == ".ods")
                    WriteTextureMapOds(sfd.FileName, archiveName, rows, textureKeys, textureDisplayNames);
                else if (ext == ".csv")
                    File.WriteAllText(sfd.FileName, BuildTextureMapCsv(rows, textureKeys, textureDisplayNames), Encoding.UTF8);
                else
                    File.WriteAllText(sfd.FileName, BuildTextureMapHtml(archiveName, rows, textureKeys, textureDisplayNames), Encoding.UTF8);

                MessageBox.Show($"Texture map created:\n{sfd.FileName}", "Create Texture Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                STErrorDialog.Show("Failed to create texture map.", "Create Texture Map", ex.ToString());
            }
        }

        private static IEnumerable<string> GetLayoutTextureNames(object fileFormat)
        {
            if (fileFormat == null)
                return null;

            var header = GetMemberValueIgnoreCase(fileFormat, "header");
            if (header == null)
                return null;

            var texturesObj = GetMemberValueIgnoreCase(header, "Textures");
            if (texturesObj is IEnumerable<string> typed)
                return typed;

            if (texturesObj is System.Collections.IEnumerable enumerable)
            {
                var list = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item != null)
                        list.Add(item.ToString());
                }
                return list;
            }

            return null;
        }

        private static object GetMemberValueIgnoreCase(object source, string memberName)
        {
            if (source == null || string.IsNullOrEmpty(memberName))
                return null;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.IgnoreCase;

            var property = source.GetType().GetProperty(memberName, flags);
            if (property != null)
                return property.GetValue(source);

            var field = source.GetType().GetField(memberName, flags);
            if (field != null)
                return field.GetValue(source);

            return null;
        }

        private static bool IsLikelyLayoutFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string normalized = fileName.Replace('\\', '/');
            string ext = Utils.GetExtension(normalized);

            if (string.Equals(ext, ".brlyt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".blyt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bflyt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bclyt", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.IndexOf("/blyt/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("/brlyt/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static bool IsLikelyTextureFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string normalized = fileName.Replace('\\', '/');
            string ext = Utils.GetExtension(normalized);

            if (string.Equals(ext, ".tpl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bflim", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bclim", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bti", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".tex", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".dds", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".tga", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.IndexOf("/timg/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("/texture/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("/textures/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static string CleanupBrlytDisplayName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            string normalized = fileName.Replace('\\', '/');
            if (normalized.StartsWith("brlyt/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("brlyt/".Length);
            else if (normalized.StartsWith("blyt/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("blyt/".Length);

            return System.IO.Path.GetFileNameWithoutExtension(normalized);
        }

        private static string CleanupTextureDisplayName(string texture)
        {
            if (string.IsNullOrEmpty(texture))
                return string.Empty;

            string normalized = texture.Replace('\\', '/');
            return System.IO.Path.GetFileNameWithoutExtension(normalized);
        }

        private static string NormalizeTextureKey(string texture)
        {
            if (string.IsNullOrWhiteSpace(texture))
                return string.Empty;

            return CleanupTextureDisplayName(texture).Trim().ToLowerInvariant();
        }

        private static string BuildTextureMapHtml(string archiveName, List<BrlytTextureUsageRow> rows,
            List<string> textureKeys, Dictionary<string, string> textureDisplayNames)
        {
            var emptyBrlytColumns = new HashSet<string>(
                rows.Where(x => x.TotalTextureCount == 0).Select(x => x.BrlytName),
                StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8' />");
            sb.AppendLine("<title>Texture Map</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;background:#1a1a1a;color:#f0f0f0;margin:16px;}");
            sb.AppendLine("table{border-collapse:collapse;font-size:12px;}");
            sb.AppendLine("th,td{border:1px solid #555;padding:4px 8px;text-align:center;}");
            sb.AppendLine("th{background:#2c2c2c;position:sticky;top:0;}");
            sb.AppendLine("td.name{text-align:left;background:#242424;}");
            sb.AppendLine("td.true{background:#1f7a1f;color:#ffffff;font-weight:600;}");
            sb.AppendLine("td.false{background:#8a2020;color:#ffffff;font-weight:600;}");
            sb.AppendLine("td.warn,th.warn{background:#f2c94c;color:#111111;font-weight:700;}");
            sb.AppendLine("tr.total td{background:#333333;font-weight:700;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h2>Texture Map - {EscapeHtml(archiveName)}</h2>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Texture</th>");

            foreach (var row in rows)
                sb.AppendLine($"<th class='{(emptyBrlytColumns.Contains(row.BrlytName) ? "warn" : string.Empty)}'>{EscapeHtml(row.BrlytName)}</th>");

            sb.AppendLine("<th>BRLYT Count</th></tr>");

            foreach (var textureKey in textureKeys)
            {
                string textureName = textureDisplayNames[textureKey];
                int brlytCount = 0;
                foreach (var row in rows)
                {
                    if (row.TextureKeys.Contains(textureKey))
                        brlytCount++;
                }

                bool isEmptyTextureRow = brlytCount == 0;
                sb.AppendLine($"<tr><td class='name{(isEmptyTextureRow ? " warn" : string.Empty)}'>{EscapeHtml(textureName)}</td>");

                foreach (var row in rows)
                {
                    bool hasTexture = row.TextureKeys.Contains(textureKey);
                    string cellClass = isEmptyTextureRow || emptyBrlytColumns.Contains(row.BrlytName)
                        ? "warn"
                        : (hasTexture ? "true" : "false");
                    sb.AppendLine($"<td class='{cellClass}'>{(hasTexture ? "True" : "False")}</td>");
                }

                sb.AppendLine($"<td class='{(isEmptyTextureRow ? "warn" : string.Empty)}'>{brlytCount}</td></tr>");
            }

            sb.AppendLine("<tr class='total'><td class='name'>Textures In BRLYT</td>");
            foreach (var row in rows)
                sb.AppendLine($"<td class='{(emptyBrlytColumns.Contains(row.BrlytName) ? "warn" : string.Empty)}'>{row.TotalTextureCount}</td>");
            sb.AppendLine("<td>-</td></tr>");

            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }

        private static string BuildTextureMapCsv(List<BrlytTextureUsageRow> rows,
            List<string> textureKeys, Dictionary<string, string> textureDisplayNames)
        {
            var sb = new StringBuilder();

            var header = new List<string>() { "Texture" };
            header.AddRange(rows.Select(x => EscapeCsv(x.BrlytName)));
            header.Add("BRLYT Count");
            sb.AppendLine(string.Join(",", header));

            foreach (var textureKey in textureKeys)
            {
                int brlytCount = 0;
                var cols = new List<string>() { EscapeCsv(textureDisplayNames[textureKey]) };
                foreach (var row in rows)
                {
                    bool hasTexture = row.TextureKeys.Contains(textureKey);
                    if (hasTexture)
                        brlytCount++;
                    cols.Add(hasTexture ? "True" : "False");
                }
                cols.Add(brlytCount.ToString());
                sb.AppendLine(string.Join(",", cols));
            }

            var totalRow = new List<string>() { "Textures In BRLYT" };
            totalRow.AddRange(rows.Select(x => x.TotalTextureCount.ToString()));
            totalRow.Add("-");
            sb.AppendLine(string.Join(",", totalRow));

            return sb.ToString();
        }

        private static void WriteTextureMapOds(string filePath, string archiveName,
            List<BrlytTextureUsageRow> rows, List<string> textureKeys, Dictionary<string, string> textureDisplayNames)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var writer = new StreamWriter(mimetype.Open(), new UTF8Encoding(false)))
                    writer.Write("application/vnd.oasis.opendocument.spreadsheet");

                WriteZipTextEntry(zip, "content.xml", BuildTextureMapOdsContent(archiveName, rows, textureKeys, textureDisplayNames));
                WriteZipTextEntry(zip, "styles.xml", BuildMinimalOdsStyles());
                WriteZipTextEntry(zip, "meta.xml", BuildMinimalOdsMeta());
                WriteZipTextEntry(zip, "settings.xml", BuildMinimalOdsSettings());
                WriteZipTextEntry(zip, "META-INF/manifest.xml", BuildMinimalOdsManifest());
            }
        }

        private static void WriteZipTextEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static string BuildTextureMapOdsContent(string archiveName,
            List<BrlytTextureUsageRow> rows, List<string> textureKeys, Dictionary<string, string> textureDisplayNames)
        {
            var emptyBrlytColumns = new HashSet<string>(
                rows.Where(x => x.TotalTextureCount == 0).Select(x => x.BrlytName),
                StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.2\">");
            sb.AppendLine("  <office:automatic-styles>");
            sb.AppendLine("    <style:style style:name=\"th\" style:family=\"table-cell\"><style:table-cell-properties fo:background-color=\"#D9D9D9\"/><style:text-properties fo:font-weight=\"bold\"/></style:style>");
            sb.AppendLine("    <style:style style:name=\"ttrue\" style:family=\"table-cell\"><style:table-cell-properties fo:background-color=\"#2E7D32\"/><style:text-properties fo:color=\"#FFFFFF\"/></style:style>");
            sb.AppendLine("    <style:style style:name=\"tfalse\" style:family=\"table-cell\"><style:table-cell-properties fo:background-color=\"#C62828\"/><style:text-properties fo:color=\"#FFFFFF\"/></style:style>");
            sb.AppendLine("    <style:style style:name=\"twarn\" style:family=\"table-cell\"><style:table-cell-properties fo:background-color=\"#F2C94C\"/><style:text-properties fo:color=\"#111111\" fo:font-weight=\"bold\"/></style:style>");
            sb.AppendLine("    <style:style style:name=\"ttotal\" style:family=\"table-cell\"><style:table-cell-properties fo:background-color=\"#EFEFEF\"/><style:text-properties fo:font-weight=\"bold\"/></style:style>");
            sb.AppendLine("  </office:automatic-styles>");
            sb.AppendLine("  <office:body><office:spreadsheet>");
            sb.AppendLine($"    <table:table table:name=\"{EscapeXml(archiveName)}\">");

            sb.AppendLine("      <table:table-row>");
            AppendOdsStringCell(sb, "Texture", "th");
            foreach (var row in rows)
                AppendOdsStringCell(sb, row.BrlytName, emptyBrlytColumns.Contains(row.BrlytName) ? "twarn" : "th");
            AppendOdsStringCell(sb, "BRLYT Count", "th");
            sb.AppendLine("      </table:table-row>");

            foreach (var textureKey in textureKeys)
            {
                sb.AppendLine("      <table:table-row>");
                int brlytCount = 0;
                foreach (var row in rows)
                {
                    bool hasTexture = row.TextureKeys.Contains(textureKey);
                    if (hasTexture)
                        brlytCount++;
                }

                bool isEmptyTextureRow = brlytCount == 0;
                AppendOdsStringCell(sb, textureDisplayNames[textureKey], isEmptyTextureRow ? "twarn" : null);

                foreach (var row in rows)
                {
                    bool hasTexture = row.TextureKeys.Contains(textureKey);
                    string styleName = isEmptyTextureRow || emptyBrlytColumns.Contains(row.BrlytName)
                        ? "twarn"
                        : (hasTexture ? "ttrue" : "tfalse");
                    AppendOdsStringCell(sb, hasTexture ? "True" : "False", styleName);
                }

                AppendOdsStringCell(sb, brlytCount.ToString(), isEmptyTextureRow ? "twarn" : null);
                sb.AppendLine("      </table:table-row>");
            }

            sb.AppendLine("      <table:table-row>");
            AppendOdsStringCell(sb, "Textures In BRLYT", "ttotal");
            foreach (var row in rows)
                AppendOdsStringCell(sb, row.TotalTextureCount.ToString(), emptyBrlytColumns.Contains(row.BrlytName) ? "twarn" : "ttotal");
            AppendOdsStringCell(sb, "-", "ttotal");
            sb.AppendLine("      </table:table-row>");

            sb.AppendLine("    </table:table>");
            sb.AppendLine("  </office:spreadsheet></office:body>");
            sb.AppendLine("</office:document-content>");
            return sb.ToString();
        }

        private static void AppendOdsStringCell(StringBuilder sb, string value, string styleName)
        {
            string style = string.IsNullOrEmpty(styleName) ? string.Empty : $" table:style-name=\"{styleName}\"";
            sb.AppendLine($"        <table:table-cell{style} office:value-type=\"string\"><text:p>{EscapeXml(value)}</text:p></table:table-cell>");
        }

        private static string BuildMinimalOdsStyles()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" office:version=\"1.2\">" +
                   "<office:styles/><office:automatic-styles/><office:master-styles/></office:document-styles>";
        }

        private static string BuildMinimalOdsMeta()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.2\">" +
                   "<office:meta><meta:generator>Scrapper's Toolbox</meta:generator></office:meta></office:document-meta>";
        }

        private static string BuildMinimalOdsSettings()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<office:document-settings xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.2\"><office:settings/></office:document-settings>";
        }

        private static string BuildMinimalOdsManifest()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.2\">" +
                   "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.spreadsheet\"/>" +
                   "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
                   "<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>" +
                   "<manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>" +
                   "<manifest:file-entry manifest:full-path=\"settings.xml\" manifest:media-type=\"text/xml\"/>" +
                   "</manifest:manifest>";
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
                return string.Empty;

            string escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private static string EscapeHtml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private class BrlytTextureUsageRow
        {
            public string BrlytName { get; set; }
            public HashSet<string> TextureKeys { get; set; }
            public int TotalTextureCount => TextureKeys?.Count ?? 0;
        }

        public void SortTreeAscending()
        {
            treeViewCustom1.Sort();
        }

        public void SelectNode(TreeNode node)
        {
            treeViewCustom1.SelectedNode = node;
        }

        private void splitter1_Resize(object sender, EventArgs e)
        {
        }

        private void splitter1_LocationChanged(object sender, EventArgs e)
        {
        }

        private void activeEditorChkBox_CheckedChanged(object sender, EventArgs e)
        {
            AddFilesToActiveEditor = activeEditorChkBox.Checked;
            Console.WriteLine("AddFilesToActiveObjectEditor " + Runtime.AddFilesToActiveObjectEditor);
        }

        private void enableBackupsChkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressBackupToggleEvent)
                return;

            var activeFile = GetActiveFile();
            string filePath = activeFile?.FilePath;
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                Runtime.SetBackupEnabledForFile(filePath, enableBackupsChkBox.Checked);

                if (enableBackupsChkBox.Checked)
                    Runtime.BackupOriginalOnLoad(filePath);
            }
            catch (Exception ex)
            {
                STConsole.WriteLine($"Failed to update backup setting for {filePath}: {ex.Message}");
            }
        }

        private void treeViewCustom1_DragEnter(object sender, DragEventArgs e)
        {
            if (!Runtime.EnableDragDrop) return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
            {
                String[] strGetFormats = e.Data.GetFormats();
                e.Effect = DragDropEffects.None;
            }
        }

        private void treeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var node = treeViewCustom1.SelectedNode;
            if (node == null) return;

            if (node is ArchiveFileWrapper)
            {
                treeViewCustom1.DoDragDrop("dummy", DragDropEffects.Copy);
            }
            else if (node is ArchiveFolderNodeWrapper || node is ArchiveRootNodeWrapper)
            {
                treeViewCustom1.DoDragDrop("dummy", DragDropEffects.Copy);
            }
        }

        private void DropFileOutsideApplication(TreeNode node)
        {
            if (node is ArchiveFileWrapper)
            {
                Runtime.EnableDragDrop = false;

                string fullPath = Write2TempAndGetFullPath(((ArchiveFileWrapper)node).ArchiveFileInfo);

                DataObject dragObj = new DataObject();
                dragObj.SetFileDropList(new System.Collections.Specialized.StringCollection() { fullPath });
                treeViewCustom1.DoDragDrop(dragObj, DragDropEffects.Copy);

                Runtime.EnableDragDrop = true;
            }
            else if (node is ArchiveFolderNodeWrapper || node is ArchiveRootNodeWrapper)
            {
                Runtime.EnableDragDrop = false;

                string[] fullPaths = Write2TempAndGetFullPath(node);

                DataObject dragObj = new DataObject();
                var collection = new System.Collections.Specialized.StringCollection();
                collection.AddRange(fullPaths);
                dragObj.SetFileDropList(collection);
                treeViewCustom1.DoDragDrop(dragObj, DragDropEffects.Copy);

                Runtime.EnableDragDrop = true;
            }
        }

        private string[] Write2TempAndGetFullPath(TreeNode folder)
        {
            var ParentPath = string.Empty;
            if (folder.Parent != null)
                ParentPath = folder.Parent.FullPath;

            return TreeHelper.ExtractAllFiles(ParentPath, folder.Nodes, System.IO.Path.GetTempPath());
        }

        private string Write2TempAndGetFullPath(ArchiveFileInfo file)
        {
            string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), file.FileName);
            using (var writer = new FileStream(tempFilePath, 
                           FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                new MemoryStream(file.FileData).CopyTo(writer);
            }

            return tempFilePath;
        }


        private void treeViewCustom1_DragLeave(object sender, EventArgs e)
        {
        }

        private void treeViewCustom1_DragOver(object sender, DragEventArgs e)
        { 
        }

        private void treeViewCustom1_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
     
        }

        private void treeViewCustom1_DragDrop(object sender, DragEventArgs e)
        {
            if (!Runtime.EnableDragDrop) return;

            Console.WriteLine("test");

            if (e.Effect == DragDropEffects.Copy)
            {
                Console.WriteLine("drop");

                DropFileOutsideApplication(treeViewCustom1.SelectedNode);
            }
            else if (e.Effect == DragDropEffects.All)
            {
                Point pt = treeViewCustom1.PointToClient(new Point(e.X, e.Y));
                var node = treeViewCustom1.GetNodeAt(pt.X, pt.Y);

                if (node != null)
                {
                    treeViewCustom1.SelectedNode = node;

                    bool IsRoot = node is ArchiveRootNodeWrapper;
                    bool IsFolder = node is ArchiveFolderNodeWrapper;
                    bool IsFile = node is ArchiveFileWrapper && node.Parent != null;

                    if (IsRoot || IsFolder || IsFile)
                    {
                        var archiveFile = GetActiveArchive();

                        //Use the parent folder for files if it has any
                        if (IsFile)
                            TreeHelper.AddFiles(treeViewCustom1.SelectedNode.Parent, archiveFile, e.Data.GetData(DataFormats.FileDrop) as string[]);
                        else
                            TreeHelper.AddFiles(treeViewCustom1.SelectedNode, archiveFile, e.Data.GetData(DataFormats.FileDrop) as string[]);
                    }
                }
                else
                {
                    Cursor.Current = Cursors.WaitCursor;

                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    foreach (string filename in files)
                    {
                        ((IMainForm)Runtime.MainForm).OpenFile(filename, Runtime.ObjectEditor.OpenModelsOnOpen);
                    }

                    Cursor.Current = Cursors.Default;
                }
            }
        }

        private ArchiveRootNodeWrapper GetActiveArchive()
        {
            var node = treeViewCustom1.SelectedNode;
            if (node != null && node is ArchiveRootNodeWrapper)
                return (ArchiveRootNodeWrapper)node;
            if (node != null && node is ArchiveFileWrapper)
                return ((ArchiveFileWrapper)node).RootNode;
            if (node != null && node is ArchiveFolderNodeWrapper)
                return ((ArchiveFolderNodeWrapper)node).RootNode;

            return null;
        }

        private void treeViewCustom1_KeyPress(object sender, KeyEventArgs e)
        {
            if (treeViewCustom1.SelectedNode == null) return;

            var Items = GetMenuItems(treeViewCustom1.SelectedNode);
            foreach (ToolStripItem toolstrip in Items)
            {
                if (toolstrip is ToolStripMenuItem)
                {
                    if (((ToolStripMenuItem)toolstrip).ShortcutKeys == e.KeyData)
                        toolstrip.PerformClick();
                }
            }
        }

        private SearchNodePanel searchForm;
        private void searchFormToolStrip_Click(object sender, EventArgs e)
        {
            searchForm = new SearchNodePanel(treeViewCustom1);
            searchForm.Dock = DockStyle.Fill;
            STForm form = new STForm();

            var panel = new STPanel() { Dock = DockStyle.Fill };
            panel.Controls.Add(searchForm);
            form.AddControl(panel);
            form.Text = "Search Window";
            form.Show(this);
        }

        private void dockSearchListToolStripMenuItem_Click(object sender, EventArgs e) {
            UpdateSearchPanelDockState();
        }

        private void UpdateSearchPanelDockState()
        {
            if (IsSearchPanelDocked)
            {
                splitContainer1.Panel1Collapsed = false;
                splitContainer1.Panel1.Controls.Clear();

                searchForm = new SearchNodePanel(treeViewCustom1);
                searchForm.Dock = DockStyle.Fill;
                splitContainer1.Panel1.Controls.Add(searchForm);
            }
            else
            {
                splitContainer1.Panel1Collapsed = true;
            }
        }

        public void LoadGenericTextureIcons(TreeNodeCollection nodes) {
            List<ISingleTextureIconLoader> texIcons = new List<ISingleTextureIconLoader>();
            foreach (var node in nodes)
            {
                if (node is ISingleTextureIconLoader)
                {
                    treeViewCustom1.SingleTextureIcons.Add((ISingleTextureIconLoader)node);
                    texIcons.Add((ISingleTextureIconLoader)node);
                }
            }

            if (texIcons.Count > 0)
                treeViewCustom1.ReloadTextureIcons(texIcons, false);
        }

        public void LoadGenericTextureIcons(ITextureContainer iconList) {
            treeViewCustom1.TextureIcons.Add(iconList);
            treeViewCustom1.ReloadTextureIcons(iconList);
        }

        public void LoadGenericTextureIcons(ISingleTextureIconLoader iconTex) {
            treeViewCustom1.SingleTextureIcons.Add(iconTex);
            treeViewCustom1.ReloadTextureIcons(iconTex, false);
        }

        private void nodeSizeCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            var nodeSize = nodeSizeCB.SelectedItem;
            if (nodeSize != null)
            {
                int Size = 22;

                switch ((TreeNodeSize)nodeSize)
                {
                    case TreeNodeSize.Small: Size = 18;
                        break;
                    case TreeNodeSize.Normal: Size = 22;
                        break;
                    case TreeNodeSize.Large: Size = 30;
                        break;
                    case TreeNodeSize.ExtraLarge: Size = 35;
                        break;
                }

                treeViewCustom1.BeginUpdate();
                treeViewCustom1.ItemHeight = Size;
                treeViewCustom1.ReloadImages(Size, Size);
                treeViewCustom1.ReloadTextureIcons(true);
                treeViewCustom1.EndUpdate();
            }
        }

        private void treeViewCustom1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node == null) return;

            else if (e.Node is ITextureContainer) {
                treeViewCustom1.BeginUpdate();
                LoadGenericTextureIcons((ITextureContainer)e.Node);
                treeViewCustom1.EndUpdate();
            }
            else if (e.Node is ISingleTextureIconLoader) {
                LoadGenericTextureIcons((ISingleTextureIconLoader)e.Node);
            }
            else if (e.Node is ArchiveFolderNodeWrapper) {
                LoadGenericTextureIcons(e.Node.Nodes);
            }
            else if (e.Node is ExplorerFolder)
            {
                treeViewCustom1.BeginUpdate();
                ((ExplorerFolder)e.Node).OnBeforeExpand();
                treeViewCustom1.EndUpdate();
            }
            else if (e.Node is TreeNodeCustom)
            {
                treeViewCustom1.BeginUpdate();
                ((TreeNodeCustom)e.Node).OnExpand();
                treeViewCustom1.EndUpdate();
            }
        }

        private void treeViewCustom1_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null) return;

            if (e.Node is ExplorerFolder)
            {
                treeViewCustom1.BeginUpdate();
                ((ExplorerFolder)e.Node).OnAfterCollapse();
                treeViewCustom1.EndUpdate();
            }
        }

        private bool DisplayEditor = true;
        private void btnPanelDisplay_Click(object sender, EventArgs e)
        {
            if (DisplayEditor) {
                splitContainer2.Panel1Collapsed = true;
                splitContainer2.Panel1.Hide();
                DisplayEditor = false;
                btnPanelDisplay.Text = ">";
            }
            else {
                splitContainer2.Panel1Collapsed = false;
                splitContainer2.Panel1.Show();
                DisplayEditor = true;
                btnPanelDisplay.Text = "<";
            }
        }

        private void splitContainer1_Panel1_Resize(object sender, EventArgs e) {
            Runtime.ObjectEditor.ListPanelWidth = splitContainer1.Panel1.Width;
        }
    }
}
