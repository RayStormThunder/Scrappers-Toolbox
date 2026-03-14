using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.Forms;
using LayoutBXLYT.Cafe;
using FirstPlugin.Forms;

namespace LayoutBXLYT
{
    public partial class LayoutHierarchy : LayoutDocked
    {
        private LayoutEditor ParentEditor;
        private STContextMenuStrip ContexMenu;
        private List<BasePane> CopiedPanes = new List<BasePane>();
        private Panel dragInsertMarker;

        public bool HasCopiedPanes => CopiedPanes.Count > 0;

        public LayoutHierarchy(LayoutEditor layoutEditor)
        {
            ParentEditor = layoutEditor;

            InitializeComponent();

            treeView1.BackColor = FormThemes.BaseTheme.FormBackColor;
            treeView1.ForeColor = FormThemes.BaseTheme.FormForeColor;

            var imgList = new ImageList();
            imgList.ColorDepth = ColorDepth.Depth32Bit;
            imgList.Images.Add("folder", Toolbox.Library.Properties.Resources.Folder);
            imgList.Images.Add("AlignmentPane", FirstPlugin.Properties.Resources.AlignmentPane);
            imgList.Images.Add("WindowPane", FirstPlugin.Properties.Resources.WindowPane);
            imgList.Images.Add("ScissorPane", FirstPlugin.Properties.Resources.ScissorPane);
            imgList.Images.Add("BoundryPane", FirstPlugin.Properties.Resources.BoundryPane);
            imgList.Images.Add("NullPane", FirstPlugin.Properties.Resources.NullPane);
            imgList.Images.Add("PicturePane", FirstPlugin.Properties.Resources.PicturePane);
            imgList.Images.Add("QuickAcess", FirstPlugin.Properties.Resources.QuickAccess);
            imgList.Images.Add("TextPane", FirstPlugin.Properties.Resources.TextPane);
            imgList.Images.Add("material", Toolbox.Library.Properties.Resources.materialSphere);
            imgList.Images.Add("texture", Toolbox.Library.Properties.Resources.Texture);
            imgList.Images.Add("font", Toolbox.Library.Properties.Resources.Font);

            imgList.ImageSize = new Size(22,22);
            treeView1.ImageList = imgList;

            BackColor = FormThemes.BaseTheme.FormBackColor;
            ForeColor = FormThemes.BaseTheme.FormForeColor;

            ContexMenu = new STContextMenuStrip();

            dragInsertMarker = new Panel();
            dragInsertMarker.Height = 3;
            dragInsertMarker.Visible = false;
            dragInsertMarker.Enabled = false;
            dragInsertMarker.BackColor = Color.OrangeRed;
            treeView1.Controls.Add(dragInsertMarker);
            dragInsertMarker.BringToFront();
        }

        private bool isLoaded = false;
        private EventHandler OnProperySelected;
        private BxlytHeader ActiveLayout;

        public class HierarchyState
        {
            public HashSet<string> ExpandedKeys = new HashSet<string>();
            public string SelectedKey;
            public string TopNodeKey;
        }

        public HierarchyState GetStateSnapshot()
        {
            return CaptureHierarchyState();
        }

        public void ApplyStateSnapshot(HierarchyState state)
        {
            if (state == null)
                return;

            treeView1.BeginUpdate();
            RestoreHierarchyState(state);
            treeView1.EndUpdate();
        }

        private HierarchyState CaptureHierarchyState()
        {
            var state = new HierarchyState();
            if (treeView1.Nodes.Count == 0)
                return state;

            foreach (TreeNode node in treeView1.Nodes)
                CaptureNodeState(node, state);

            if (treeView1.SelectedNode != null)
                state.SelectedKey = GetNodeStateKey(treeView1.SelectedNode);

            if (treeView1.TopNode != null)
                state.TopNodeKey = GetNodeStateKey(treeView1.TopNode);

            return state;
        }

        private void CaptureNodeState(TreeNode node, HierarchyState state)
        {
            if (node == null)
                return;

            if (node.IsExpanded)
                state.ExpandedKeys.Add(GetNodeStateKey(node));

            foreach (TreeNode child in node.Nodes)
                CaptureNodeState(child, state);
        }

        private static string GetNodeStateKey(TreeNode node)
        {
            string type = "node";
            if (node.Tag is BasePane) type = "pane";
            else if (node.Tag is GroupPane) type = "group";
            else if (node.Tag is BxlytMaterial) type = "material";
            else if (node.Tag is BxlytHeader) type = "layout";
            else if (node.ImageKey == "texture") type = "texture";
            else if (node.ImageKey == "font") type = "font";

            return $"{type}|{node.FullPath}";
        }

        private void RestoreHierarchyState(HierarchyState state)
        {
            if (state == null || treeView1.Nodes.Count == 0)
                return;

            TreeNode selectedNode = null;
            TreeNode topNode = null;

            foreach (TreeNode node in treeView1.Nodes)
                RestoreNodeState(node, state, ref selectedNode, ref topNode);

            if (selectedNode != null)
                treeView1.SelectedNode = selectedNode;

            if (topNode != null)
            {
                try { treeView1.TopNode = topNode; }
                catch { }
            }
        }

        private void RestoreNodeState(TreeNode node, HierarchyState state, ref TreeNode selectedNode, ref TreeNode topNode)
        {
            if (node == null)
                return;

            string key = GetNodeStateKey(node);
            if (state.ExpandedKeys.Contains(key))
                node.Expand();

            if (selectedNode == null && !string.IsNullOrEmpty(state.SelectedKey) && key == state.SelectedKey)
                selectedNode = node;

            if (topNode == null && !string.IsNullOrEmpty(state.TopNodeKey) && key == state.TopNodeKey)
                topNode = node;

            foreach (TreeNode child in node.Nodes)
                RestoreNodeState(child, state, ref selectedNode, ref topNode);
        }

        public void LoadLayout(BxlytHeader bxlyt, EventHandler onPropertySelected)
        {
            var state = CaptureHierarchyState();

            isLoaded = false;
            OnProperySelected = onPropertySelected;

            ActiveLayout = bxlyt;

            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();

            CreateQuickAccess(bxlyt);
            treeView1.Nodes.Add(new TreeNode("File Settings") {Tag = bxlyt });
            LoadTextures(bxlyt.Textures);
            LoadFonts(bxlyt.Fonts);
            LoadMaterials(bxlyt.Materials);
            treeView1.Nodes.Add(new AnimatedPaneFolder(ParentEditor, "Animated Pane List") { Tag = bxlyt });

            LoadGroup(bxlyt.RootGroup); 
            LoadPane(bxlyt.RootPane);

            // Normalize node text colors from checkbox state so stale styles do not persist until first click.
            ApplyCheckedStateColors(treeView1.Nodes);

            RestoreHierarchyState(state);

            treeView1.EndUpdate();

            // Ensure pane checked/disabled visuals are fully synchronized after initial load and restore.
            RefreshPaneNodeVisualState(treeView1.Nodes);
            treeView1.Refresh();

            isLoaded = true;
        }

        private void RefreshPaneNodeVisualState(TreeNodeCollection nodes)
        {
            if (nodes == null)
                return;

            foreach (TreeNode node in nodes)
            {
                if (node == null)
                    continue;

                if (node.Tag is BasePane)
                {
                    var pane = (BasePane)node.Tag;
                    bool shown = pane.IsRoot || (pane.Visible && pane.DisplayInEditor);
                    node.Checked = shown;
                    node.ForeColor = shown ? treeView1.ForeColor : FormThemes.BaseTheme.DisabledItemColor;
                }
                else
                {
                    node.ForeColor = treeView1.ForeColor;
                }

                if (node.Nodes.Count > 0)
                    RefreshPaneNodeVisualState(node.Nodes);
            }
        }

        private void ApplyCheckedStateColors(TreeNodeCollection nodes)
        {
            if (nodes == null)
                return;

            foreach (TreeNode node in nodes)
            {
                if (node == null)
                    continue;

                if (node.Tag is BasePane)
                {
                    if (node.Checked)
                        node.ForeColor = treeView1.ForeColor;
                    else
                        node.ForeColor = FormThemes.BaseTheme.DisabledItemColor;
                }
                else
                {
                    node.ForeColor = treeView1.ForeColor;
                }

                if (node.Nodes.Count > 0)
                    ApplyCheckedStateColors(node.Nodes);
            }
        }

        public class AnimatedPaneFolder : TreeNodeCustom
        {
            private LayoutEditor ParentEditor;
            private bool Expanded = false;

            public AnimatedPaneFolder(LayoutEditor editor, string text) {
                ParentEditor = editor;
                Text = text;

                Nodes.Add("Empty");
            }

            public override void OnExpand()
            {
                if (Expanded) return;

                var layoutFile = (BxlytHeader)Tag;

                Nodes.Clear();

                Expanded = true;

                var animations = ParentEditor.AnimationFiles;

                foreach (var pane in layoutFile.PaneLookup.Values) {
                    string matName = "";

                    //Find materials
                    var mat = pane.TryGetActiveMaterial();
                    if (mat != null) matName = mat.Name;

                    //search archive
                    var archive = layoutFile.FileInfo.IFileInfo.ArchiveParent;
                    if (archive != null)
                    {
                        foreach (var file in archive.Files)
                        {
                            if (Utils.GetExtension(file.FileName) == ".bflan" &&
                                !animations.Any(x => x.FileName == file.FileName))
                            {
                                if (BxlanHeader.ContainsEntry(file.FileData, new string[2] { pane.Name, matName }))
                                {
                                    var paneNode = CreatePaneWrapper(pane);
                                    Nodes.Add(paneNode);
                                }
                            }
                        }
                    }

                    //Search opened animations
                    for (int i = 0; i < animations?.Count; i++) {
                        if (animations[i].ContainsEntry(pane.Name) || animations[i].ContainsEntry(matName))
                        {
                            var paneNode = CreatePaneWrapper(pane);
                            Nodes.Add(paneNode);
                        }
                    }
                }
            }
        }

        public void SelectNode(TreeNode node)
        {
            treeView1.SelectedNode = node;
            treeView1.Refresh();
        }

        public void UpdateTree()
        {
            treeView1.Refresh();
        }

        public void Reset()
        {
            treeView1.Nodes.Clear();
            isLoaded = false;
        }

        public List<BasePane> GetSelectedPanes()
        {
            List<BasePane> nodes = new List<BasePane>();
            if (treeView1?.SelectedNodes == null)
                return nodes;

            if (treeView1.SelectedNode != null && treeView1.SelectedNode.Tag is BasePane)
                nodes.Add((BasePane)treeView1.SelectedNode.Tag);

            foreach (var node in treeView1.SelectedNodes) {
                if (node != null && node.Tag is BasePane && node != treeView1.SelectedNode)
                    nodes.Add((BasePane)node.Tag);
            }
            return nodes;
        }

        public List<GroupPane> GetSelectedGroups()
        {
            List<GroupPane> nodes = new List<GroupPane>();
            if (treeView1?.SelectedNodes == null)
                return nodes;

            foreach (var node in treeView1.SelectedNodes)
            {
                if (node != null && node.Tag is GroupPane)
                    nodes.Add((GroupPane)node.Tag);
            }
            return nodes;
        }

        private void LoadTextures(List<string> textures)
        {
            ActiveLayout.TextureFolder = new TreeNode("Textures");
            treeView1.Nodes.Add(ActiveLayout.TextureFolder);
            ActiveLayout.TextureFolder.ContextMenuStrip = new ContextMenuStrip();
            ActiveLayout.TextureFolder.ContextMenuStrip.Items.Add(new STToolStipMenuItem("Add", null, (o, e) =>
            {
                ParentEditor.AddTextureFromTextureListWorkflow();
            }));

            for (int i = 0; i < textures.Count; i++)
                AddTextureNode(textures[i], i);
        }

        private void AddTextureNode(string tex, int i)
        {
            TreeNode matNode = new TreeNode(tex);
            matNode.ContextMenuStrip = new ContextMenuStrip();
            matNode.ContextMenuStrip.Items.Add(new STToolStipMenuItem("Edit", null, (o, e) =>
            {
                ParentEditor.EditTextureFromTextureListWorkflow(matNode.Text);
            }));
            matNode.ContextMenuStrip.Items.Add(new STToolStipMenuItem("Remove", null, (o, e) =>
            {
                ParentEditor.RemoveTextureFromTextureListWorkflow(matNode.Text);
            }));
            matNode.ImageKey = "texture";
            matNode.SelectedImageKey = "texture";
            ActiveLayout.TextureFolder.Nodes.Add(matNode);
        }

        private void LoadFonts(List<string> fonts)
        {
            ActiveLayout.FontFolder = new TreeNode("Fonts");
            ActiveLayout.FontFolder.ContextMenuStrip = new ContextMenuStrip();
            ActiveLayout.FontFolder.ContextMenuStrip.Items.Add(new STToolStipMenuItem("Add", null, (o, e) =>
            {
                ActiveLayout.Fonts.Add("NewFont");
                AddFontNode("NewFont", ActiveLayout.Fonts.Count - 1);
            }));

            treeView1.Nodes.Add(ActiveLayout.FontFolder);
            for (int i = 0; i < fonts.Count; i++)
                AddFontNode(fonts[i], i);
        }

        private void AddFontNode(string font, int i)
        {
            TreeNode matNode = new TreeNode(font);
            matNode.ContextMenuStrip = new ContextMenuStrip();
            matNode.ContextMenuStrip.Items.Add(new STToolStipMenuItem("Rename", null, (o, e) =>
            {
                RenameFont(matNode, i);
            }));
            matNode.ContextMenuStrip.Items.Add(new STToolStipMenuItem("Remove", null, (o, e) =>
            {
                ActiveLayout.FontFolder.Nodes.Remove(matNode);
                ActiveLayout.Fonts.Remove(matNode.Text);
            }));
            matNode.ImageKey = "font";
            matNode.SelectedImageKey = "font";
            ActiveLayout.FontFolder.Nodes.Add(matNode);
        }

        private void RenameFont(TreeNode selectedNode, int index)
        {
            RenameDialog dlg = new RenameDialog();
            dlg.SetString(selectedNode.Text);
            if (dlg.ShowDialog() == DialogResult.OK) {
                ActiveLayout.Fonts[index] = dlg.textBox1.Text;
                selectedNode.Text = dlg.textBox1.Text;
            }
        }

        private void LoadMaterials(List<BxlytMaterial> materials)
        {
            ActiveLayout.MaterialFolder = new TreeNode("Materials");
            treeView1.Nodes.Add(ActiveLayout.MaterialFolder);
            for (int i = 0; i < materials.Count; i++)
            {
                MatWrapper matNode = new MatWrapper(materials[i].Name);
                materials[i].NodeWrapper = matNode;
                matNode.Tag = materials[i];
                matNode.ImageKey = "material";
                matNode.SelectedImageKey = "material";
                ActiveLayout.MaterialFolder.Nodes.Add(matNode);
            }
        }

  

        private void CreateQuickAccess(BxlytHeader bxlyt)
        {
            var panes = new List<BasePane>();
            var groupPanes = new List<GroupPane>();
            GetPanes(bxlyt.RootPane,ref panes);
            GetGroupPanes(bxlyt.RootGroup,ref groupPanes);

            TreeNode node = new TreeNode("Quick Access");
            node.ImageKey = "QuickAcess";
            node.SelectedImageKey = "QuickAcess";
            treeView1.Nodes.Add(node);

            TreeNode nullFolder = new TreeNode("Null Panes");
            TreeNode textFolder = new TreeNode("Text Boxes");
            TreeNode windowFolder = new TreeNode("Window Panes");
            TreeNode pictureFolder = new TreeNode("Picture Panes");
            TreeNode boundryFolder = new TreeNode("Boundry Panes");
            TreeNode partsFolder = new TreeNode("Part Panes");
            TreeNode groupFolder = new TreeNode("Groups");

            node.Nodes.Add(nullFolder);
            node.Nodes.Add(textFolder);
            node.Nodes.Add(windowFolder);
            node.Nodes.Add(pictureFolder);
            node.Nodes.Add(boundryFolder);
            node.Nodes.Add(partsFolder);
            node.Nodes.Add(groupFolder);

            for (int i = 0; i < panes.Count; i++)
            {
                var paneNode = CreatePaneWrapper(panes[i]);
                if (panes[i] is IWindowPane) windowFolder.Nodes.Add(paneNode);
                else if (panes[i] is IPicturePane) pictureFolder.Nodes.Add(paneNode);
                else if (panes[i] is IBoundryPane) boundryFolder.Nodes.Add(paneNode);
                else if (panes[i] is IPartPane) partsFolder.Nodes.Add(paneNode);
                else if (panes[i] is ITextPane) textFolder.Nodes.Add(paneNode);
                else nullFolder.Nodes.Add(paneNode);

                if (panes[i] is Cafe.PRT1)
                {
                    var partPane = (Cafe.PRT1)panes[i];
                    foreach (var property in partPane.Properties)
                    {
                        if (property.Property != null)
                        {
                            var propertyNode = CreatePaneWrapper(property.Property);
                            paneNode.Nodes.Add(propertyNode);
                        }
                    }
                }
            }

            for (int i = 0; i < groupPanes.Count; i++)
            {
                var paneNode = new TreeNode() { Text = groupPanes[i].Name };
                paneNode.Tag = groupPanes[i];
                groupFolder.Nodes.Add(paneNode);
            }
        }

        private void GetPanes(BasePane pane, ref List<BasePane> panes)
        {
            panes.Add(pane);
            foreach (var childPane in pane.Childern)
                  GetPanes(childPane,ref panes);
        }

        private void GetGroupPanes(GroupPane pane, ref List<GroupPane> panes)
        {
            panes.Add(pane);
            foreach (GroupPane childPane in pane.Childern)
                GetGroupPanes(childPane, ref panes);
        }

        public static PaneTreeWrapper CreatePaneWrapper(BasePane pane)
        {
            PaneTreeWrapper paneNode = new PaneTreeWrapper();
            paneNode.Text = pane.Name;
            paneNode.Tag = pane;

            // Display-in-editor is an editor/runtime state; keep visible panes visible on initial load.
            if (pane.Visible && !pane.DisplayInEditor)
                pane.DisplayInEditor = true;

            paneNode.Checked = pane.Visible && pane.DisplayInEditor;

            if (!paneNode.Checked)
                paneNode.ForeColor = FormThemes.BaseTheme.DisabledItemColor;

            string imageKey = "";
            if (pane is IWindowPane) imageKey = "WindowPane";
            else if (pane is IPicturePane) imageKey = "PicturePane";
            else if (pane is IBoundryPane) imageKey = "BoundryPane";
            else if (pane is ITextPane) imageKey = "TextPane";
            else imageKey = "NullPane";

            paneNode.ImageKey = imageKey;
            paneNode.SelectedImageKey = imageKey;

            return paneNode;
        }

        private void LoadGroup(GroupPane pane, TreeNode parent = null)
        {
            var paneNode = new TreeNode() { Text = pane.Name, Tag = pane };
            pane.NodeWrapper = paneNode;

            if (parent == null)
                treeView1.Nodes.Add(paneNode);
            else
                parent.Nodes.Add(paneNode);

            foreach (var childPane in pane.Childern)
                LoadGroup(childPane, paneNode);
        }

        private void LoadPane(BasePane pane, TreeNode parent = null)
        {
            var paneNode = CreatePaneWrapper(pane);
            pane.NodeWrapper = paneNode;

            if (parent == null)
                treeView1.Nodes.Add(paneNode);
            else
                parent.Nodes.Add(paneNode);

            foreach (var childPane in pane.Childern)
                LoadPane(childPane, paneNode);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (isLoaded)
                OnProperySelected.Invoke("Select", e);
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (isLoaded)
            {
                if (!e.Node.Checked)
                    e.Node.ForeColor = FormThemes.BaseTheme.DisabledItemColor;
                else
                    e.Node.ForeColor = treeView1.ForeColor;

                OnProperySelected.Invoke("Checked", e);
            }
        }

        private void treeView1_MouseClick(object sender, MouseEventArgs e)
        {

        }

        private void TogglePane(object sender, EventArgs e)
        {
            foreach (TreeNode node in treeView1.SelectedNodes)
                TogglePane(node);
        }

        private void TogglePane(TreeNode node)
        {
            if (node == null)
                return;

            node.Checked = !node.Checked;

            if (node.Tag is BasePane)
            {
                var pane = (BasePane)node.Tag;
                pane.Visible = node.Checked;
                pane.DisplayInEditor = node.Checked;
            }
        }

        private void treeView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (GetSelectedPanes().Count > 1)
                {
                    e.Handled = true;
                    return;
                }

                CopySelectedPanes();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                var targetPane = GetSelectedPanes().FirstOrDefault();
                PastePanesToContext(targetPane);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedPanes();
                e.Handled = true;
                return;
            }

            foreach (var node in treeView1.SelectedNodes) {
                if (node == null || node.Tag == null)
                    continue;

                if (e.KeyCode == Keys.H)
                {
                    if (node.Tag is BasePane)
                        TogglePane(node);
                }
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null)
                return;

            if (e.Button == MouseButtons.Right)
            {
                if (!treeView1.SelectedNodes.Contains(e.Node))
                    treeView1.SelectedNode = e.Node;

                if (e.Node.Tag is BasePane)
                {
                    ContexMenu.Items.Clear();
                    var pane = e.Node.Tag as BasePane;
                    var addPaneMenu = new ToolStripMenuItem("Add Pane");
                    addPaneMenu.DropDownItems.Add(new STToolStipMenuItem("Null Pane", null, (o, args) => AddPaneFromContext(pane, PaneAddType.NullPane)));
                    addPaneMenu.DropDownItems.Add(new STToolStipMenuItem("Picture Pane", null, (o, args) => AddPaneFromContext(pane, PaneAddType.PicturePane)));
                    addPaneMenu.DropDownItems.Add(new STToolStipMenuItem("Text Pane", null, (o, args) => AddPaneFromContext(pane, PaneAddType.TextPane)));
                    addPaneMenu.DropDownItems.Add(new STToolStipMenuItem("Window Pane", null, (o, args) => AddPaneFromContext(pane, PaneAddType.WindowPane)));
                    addPaneMenu.DropDownItems.Add(new STToolStipMenuItem("Boundry Pane", null, (o, args) => AddPaneFromContext(pane, PaneAddType.BoundryPane)));
                    addPaneMenu.DropDownItems.Add(new STToolStipMenuItem("Part Pane", null, (o, args) => AddPaneFromContext(pane, PaneAddType.PartPane))
                    {
                        Enabled = ParentEditor.SupportsPartPane()
                    });
                    ContexMenu.Items.Add(addPaneMenu);
                    ContexMenu.Items.Add(new STToolStipMenuItem("Display Panes", null, TogglePane, Keys.Control | Keys.H));

                    var copyItem = new STToolStipMenuItem("Copy Pane", null, (o, args) => CopySelectedPanes(), Keys.Control | Keys.C);
                    var pasteItem = new STToolStipMenuItem("Paste Pane", null, (o, args) => PastePanesToContext(e.Node.Tag as BasePane), Keys.Control | Keys.V);
                    var deleteItem = new STToolStipMenuItem("Delete Pane", null, (o, args) => DeleteSelectedPanes(), Keys.Delete);
                    copyItem.Enabled = GetSelectedPanes().Count <= 1;
                    deleteItem.Enabled = ((BasePane)e.Node.Tag).Parent != null;
                    pasteItem.Enabled = CopiedPanes.Count > 0;
                    ContexMenu.Items.Add(copyItem);
                    ContexMenu.Items.Add(pasteItem);
                    ContexMenu.Items.Add(deleteItem);

                //    ContexMenu.Items.Add(new STToolStipMenuItem("Display Children Panes", null, TogglePane, Keys.Control | Keys.H));
                    ContexMenu.Show(Cursor.Position);
                }

                if (e.Node.Tag is GroupPane)
                {
                    var group = e.Node.Tag as GroupPane;
                    ContexMenu.Items.Clear();
                    ContexMenu.Items.Add(new STToolStipMenuItem("Rename Group", null, (o, args) => RenameGroupAction(group)));
                    ContexMenu.Items.Add(new STToolStipMenuItem("Add Group", null, (o, args) => ParentEditor.AddGroupAndRefresh(group)));
                    ContexMenu.Items.Add(new STToolStipMenuItem("Delete Group", null, (o, args) => ParentEditor.DeleteGroupAndRefresh(group))
                    {
                        Enabled = group?.Parent != null
                    });
                    ContexMenu.Show(Cursor.Position);
                }

                //Check fonts to open editor if possible
                if (e.Node.ImageKey == "font") {
                    ContexMenu.Items.Clear();
                    ContexMenu.Items.Add(new STToolStipMenuItem("Load Font File", null, loadFontFile, Keys.Control | Keys.L));
                    ContexMenu.Show(Cursor.Position);
                }
            }
        }

        private void loadFontFile(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = Utils.GetAllFilters(new Type[] { typeof(FirstPlugin.BXFNT ), });
            if (ofd.ShowDialog() == DialogResult.OK) {
                var fileFormat = Toolbox.Library.IO.STFileLoader.OpenFileFormat(ofd.FileName);
                if (fileFormat is IArchiveFile)
                {
                    foreach (var file in ((IArchiveFile)fileFormat).Files) {
                        var archiveFile = file.OpenFile();
                        if (archiveFile is FirstPlugin.BXFNT) {

                        }
                    }
                }
                ParentEditor.UpdateViewport();
            }
        }

        private void RenameGroupAction(GroupPane group)
        {
            if (group == null)
                return;

            RenameDialog dlg = new RenameDialog();
            dlg.SetString(group.Name);
            if (dlg.ShowDialog() == DialogResult.OK)
                group.Name = dlg.textBox1.Text;
        }

        private void CopySelectedPanes()
        {
            CopyPanesFromSelection(GetSelectedPanes());
        }

        public void CopyPanesFromSelection(IEnumerable<BasePane> panes)
        {
            CopiedPanes.Clear();

            var selectedPanes = panes?.Where(x => x != null).Distinct().ToList() ?? new List<BasePane>();
            if (selectedPanes.Count == 0)
                return;

            foreach (var pane in GetTopMostPanes(selectedPanes))
                CopiedPanes.Add(DeepCopyPaneTree(pane));
        }

        private void DeleteSelectedPanes()
        {
            var selectedPanes = GetSelectedPanes();
            if (selectedPanes.Count == 0)
                return;

            ParentEditor.DeletePanesAndRefresh(selectedPanes);
        }

        private void PastePanesToContext(BasePane sourcePane)
        {
            if (CopiedPanes.Count == 0 || ActiveLayout?.RootPane == null)
                return;

            BasePane targetParent = ResolvePasteParent(sourcePane) ?? ActiveLayout.RootPane;
            BasePane lastPasted = null;

            foreach (var copiedPane in CopiedPanes)
            {
                var pane = DeepCopyPaneTree(copiedPane);
                lastPasted = AddPastedPaneTree(pane, targetParent);
            }

            ParentEditor.UpdateHiearchyTree();
            ParentEditor.UpdateUndo();
            if (lastPasted != null)
            {
                ParentEditor.UpdateHiearchyNodeSelection(lastPasted);
                ParentEditor.LoadPaneEditorOnSelect(lastPasted);
            }
            ParentEditor.UpdateViewport();
        }

        public void PastePanesFromViewer(BasePane sourcePane)
        {
            PastePanesToContext(sourcePane);
        }

        private BasePane ResolvePasteParent(BasePane sourcePane)
        {
            if (sourcePane == null)
                return ActiveLayout?.RootPane;

            if (IsNullPane(sourcePane))
                return sourcePane;

            return FindNearestNullParent(sourcePane) ?? sourcePane.Parent ?? ActiveLayout?.RootPane;
        }

        private BasePane AddPastedPaneTree(BasePane pane, BasePane parentPane)
        {
            var children = pane.Childern?.ToList() ?? new List<BasePane>();
            pane.Childern = new List<BasePane>();

            ParentEditor.AddNewPastedPane(pane, parentPane);

            foreach (var child in children)
                AddPastedPaneTree(child, pane);

            return pane;
        }

        private BasePane DeepCopyPaneTree(BasePane source)
        {
            if (source == null)
                return null;

            var pane = (BasePane)source.Clone();
            pane.Parent = null;
            pane.NodeWrapper = null;
            pane.LayoutFile = null;

            var children = source.Childern?.ToList() ?? new List<BasePane>();
            pane.Childern = new List<BasePane>();
            foreach (var child in children)
            {
                var childClone = DeepCopyPaneTree(child);
                if (childClone != null)
                {
                    childClone.Parent = pane;
                    pane.Childern.Add(childClone);
                }
            }

            return pane;
        }

        private List<BasePane> GetTopMostPanes(List<BasePane> panes)
        {
            var selected = panes.Where(x => x != null).Distinct().ToList();
            var topMost = new List<BasePane>();

            foreach (var pane in selected)
            {
                bool hasSelectedAncestor = false;
                var parent = pane.Parent;
                while (parent != null)
                {
                    if (selected.Contains(parent))
                    {
                        hasSelectedAncestor = true;
                        break;
                    }
                    parent = parent.Parent;
                }

                if (!hasSelectedAncestor)
                    topMost.Add(pane);
            }

            return topMost;
        }

        private enum PaneAddType
        {
            NullPane,
            PicturePane,
            TextPane,
            WindowPane,
            BoundryPane,
            PartPane,
        }

        private void AddPaneFromContext(BasePane sourcePane, PaneAddType addType)
        {
            if (sourcePane == null)
                return;

            BasePane targetParent = null;
            if (IsNullPane(sourcePane))
                targetParent = sourcePane;
            else
                targetParent = FindNearestNullParent(sourcePane) ?? sourcePane.Parent;

            BasePane pane = null;
            switch (addType)
            {
                case PaneAddType.NullPane:
                    pane = ParentEditor.AddNewNullPane(targetParent);
                    break;
                case PaneAddType.PicturePane:
                    pane = ParentEditor.AddNewPicturePane(targetParent);
                    break;
                case PaneAddType.TextPane:
                    pane = ParentEditor.AddNewTextPane(targetParent);
                    break;
                case PaneAddType.WindowPane:
                    pane = ParentEditor.AddNewWindowPane(targetParent);
                    break;
                case PaneAddType.BoundryPane:
                    pane = ParentEditor.AddNewBoundryPane(targetParent);
                    break;
                case PaneAddType.PartPane:
                    pane = ParentEditor.AddNewPartPane(targetParent);
                    break;
            }

            ParentEditor.AddPaneAndRefresh(pane);
        }

        private BasePane FindNearestNullParent(BasePane pane)
        {
            var current = pane?.Parent;
            while (current != null)
            {
                if (IsNullPane(current))
                    return current;
                current = current.Parent;
            }
            return null;
        }

        private bool IsNullPane(BasePane pane)
        {
            if (pane == null)
                return false;

            return !(pane is IWindowPane) &&
                   !(pane is IPicturePane) &&
                   !(pane is ITextPane) &&
                   !(pane is IBoundryPane) &&
                   !(pane is IPartPane);
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            //Create and expand a file format, then update the tag
            //Allows for faster loading
            if (e.Node.Tag is ArchiveFileInfo)
            {

            }

            if (e.Node is TreeNodeCustom)
                ((TreeNodeCustom)e.Node).OnExpand();
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null)
                return;

            if (e.Button == MouseButtons.Left) {
                if (e.Node.Tag is BasePane)
                    ParentEditor.ShowPaneEditor(e.Node.Tag as BasePane);

                //Check fonts to open editor if possible
                if (e.Node.ImageKey == "font")
                {
                    foreach (var file in FirstPlugin.PluginRuntime.BxfntFiles) {
                        if (file.FileName == e.Node.Text)
                        {
                            Form frm = new Form();

                            BffntEditor editor = new BffntEditor();
                            editor.Text = "Font Editor";
                            editor.Dock = DockStyle.Fill;
                            editor.LoadFontFile(file);
                            editor.OnFontEdited += bxfntEditor_FontEdited;
                            frm.Controls.Add(editor);

                            frm.Show(ParentEditor);
                        }
                    }
                }
            }
        }

        #region NodeDragDrop

        private string NodeMap;
        private TreeNode dragInsertTargetNode;
        private bool dragInsertAfter;
        private TreeNode dragHoverNode;

        private enum PaneDropMode
        {
            None,
            ReorderBefore,
            ReorderAfter,
            NestInside,
        }

        private void SetDragHoverNode(TreeNode node)
        {
            if (Object.ReferenceEquals(dragHoverNode, node))
                return;

            if (dragHoverNode != null)
            {
                dragHoverNode.BackColor = treeView1.BackColor;
                dragHoverNode.ForeColor = treeView1.ForeColor;
            }

            dragHoverNode = node;
            if (dragHoverNode != null)
            {
                dragHoverNode.BackColor = Color.FromArgb(45, 95, 160);
                dragHoverNode.ForeColor = Color.White;
            }

            treeView1.Refresh();
        }

        private PaneDropMode GetPaneDropMode(BasePane draggedPane, BasePane targetPane, TreeNode targetNode, Point targetPoint)
        {
            if (draggedPane == null || targetPane == null || targetNode == null)
                return PaneDropMode.None;

            bool sameParent = draggedPane.Parent != null && targetPane.Parent != null &&
                              Object.ReferenceEquals(draggedPane.Parent, targetPane.Parent);

            if (!sameParent)
                return PaneDropMode.NestInside;

            int localY = targetPoint.Y - targetNode.Bounds.Top;
            int topThreshold = targetNode.Bounds.Height / 3;
            int bottomThreshold = targetNode.Bounds.Height - topThreshold;

            if (localY < topThreshold)
                return PaneDropMode.ReorderBefore;
            if (localY > bottomThreshold)
                return PaneDropMode.ReorderAfter;

            return PaneDropMode.NestInside;
        }

        private bool CanDropNode(TreeNode draggedNode, TreeNode targetNode)
        {
            TreeNode parentNode = targetNode;
            while (parentNode != null)
            {
                if (Object.ReferenceEquals(draggedNode, parentNode))
                    return false;
                parentNode = parentNode.Parent;
            }
            return true;
        }

        private void ClearDragInsertIndicator()
        {
            if (dragInsertTargetNode != null)
            {
                dragInsertTargetNode = null;
            }

            if (dragInsertMarker.Visible)
            {
                dragInsertMarker.Visible = false;
            }
        }

        private void ShowDragInsertIndicator(TreeNode targetNode, bool insertAfter)
        {
            if (targetNode == null)
                return;

            Rectangle bounds = targetNode.Bounds;
            int y = insertAfter ? bounds.Bottom : bounds.Top;

            dragInsertTargetNode = targetNode;
            dragInsertAfter = insertAfter;

            dragInsertMarker.SetBounds(0, Math.Max(0, y - 1), Math.Max(1, treeView1.ClientSize.Width), 3);
            dragInsertMarker.Visible = true;
            dragInsertMarker.BringToFront();
        }

        private void ClearDragFeedback()
        {
            ClearDragInsertIndicator();
            SetDragHoverNode(null);
        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
         /*   if (e.Data.GetDataPresent(typeof(PaneTreeWrapper)))
            {
                Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));
                TreeNode NodeOver = treeView1.GetNodeAt(targetPoint);
                TreeNode NodeMoving = (PaneTreeWrapper)e.Data.GetData(typeof(PaneTreeWrapper));

                if (NodeOver != null && (NodeOver != NodeMoving || (NodeOver.Parent != null && NodeOver.Index == (NodeOver.Parent.Nodes.Count - 1))))
                {
                    int OffsetY = this.treeView1.PointToClient(Cursor.Position).Y - NodeOver.Bounds.Top;
                    int NodeOverImageWidth = this.treeView1.ImageList.Images[NodeOver.ImageIndex].Size.Width + 8;
                    Graphics g = this.treeView1.CreateGraphics();

                    //Folder node

                    if (OffsetY < (NodeOver.Bounds.Height / 3))
                    {
                        TreeNode tnParadox = NodeOver;
                        while (tnParadox.Parent != null)
                        {
                            if (tnParadox.Parent == NodeMoving)
                            {
                                this.NodeMap = "";
                                return;
                            }

                            tnParadox = tnParadox.Parent;
                        }
                    }
                }
            }*/

            #endregion

            
            if (e.Data.GetDataPresent(typeof(PaneTreeWrapper)))
            {
                Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));

                TreeNode targetNode = treeView1.GetNodeAt(targetPoint);
                TreeNode draggedNode = (PaneTreeWrapper)e.Data.GetData(typeof(PaneTreeWrapper));

                if (targetNode == null || draggedNode == null || targetNode == draggedNode)
                    return;

                var draggedPane = draggedNode.Tag as BasePane;
                if (draggedPane == null || draggedPane.IsRoot)
                    return;

                if (!CanDropNode(draggedNode, targetNode))
                    return;

                var targetPane = targetNode.Tag as BasePane;
                if (targetPane == null)
                    return;

                var dropMode = GetPaneDropMode(draggedPane, targetPane, targetNode, targetPoint);

                if (dropMode == PaneDropMode.ReorderBefore || dropMode == PaneDropMode.ReorderAfter)
                {
                    var parentPane = draggedPane.Parent;
                    var parentTreeNode = targetNode.Parent;
                    if (parentPane == null || parentTreeNode == null)
                        return;

                    int targetIndex = parentPane.Childern.IndexOf(targetPane);
                    int oldIndex = parentPane.Childern.IndexOf(draggedPane);
                    if (targetIndex < 0 || oldIndex < 0)
                        return;

                    bool insertAfter = dropMode == PaneDropMode.ReorderAfter;
                    int insertIndex = targetIndex + (insertAfter ? 1 : 0);
                    if (oldIndex < insertIndex)
                        insertIndex--;

                    insertIndex = Math.Max(0, Math.Min(insertIndex, parentPane.Childern.Count - 1));

                    if (oldIndex != insertIndex)
                    {
                        parentPane.Childern.RemoveAt(oldIndex);
                        parentPane.Childern.Insert(insertIndex, draggedPane);

                        draggedNode.Remove();
                        parentTreeNode.Nodes.Insert(insertIndex, draggedNode);
                        parentTreeNode.Expand();

                        ParentEditor.UpdateViewport();
                    }
                }
                else if (dropMode == PaneDropMode.NestInside)
                {
                    draggedPane.Parent.Childern.Remove(draggedPane);
                    draggedNode.Remove();

                    draggedPane.ResetParentTransform(targetPane);
                    draggedPane.Parent = targetPane;
                    targetPane.Childern.Add(draggedPane);

                    targetNode.Nodes.Add(draggedNode);
                    targetNode.Expand();

                    ParentEditor.UpdateViewport();
                }
            }

            ClearDragFeedback();
        }

        private void bxfntEditor_FontEdited(object sender, EventArgs e)
        {

        }

        private void treeView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(PaneTreeWrapper)))
            {
                ClearDragFeedback();
                return;
            }

            Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = treeView1.GetNodeAt(targetPoint);
            TreeNode draggedNode = (PaneTreeWrapper)e.Data.GetData(typeof(PaneTreeWrapper));

            if (targetNode == null || draggedNode == null || targetNode == draggedNode)
            {
                ClearDragFeedback();
                return;
            }

            var draggedPane = draggedNode.Tag as BasePane;
            var targetPane = targetNode.Tag as BasePane;
            if (draggedPane == null || targetPane == null)
            {
                ClearDragFeedback();
                return;
            }

            if (!CanDropNode(draggedNode, targetNode))
            {
                e.Effect = DragDropEffects.None;
                ClearDragFeedback();
                return;
            }

            e.Effect = DragDropEffects.Move;

            var dropMode = GetPaneDropMode(draggedPane, targetPane, targetNode, targetPoint);
            if (dropMode == PaneDropMode.ReorderBefore || dropMode == PaneDropMode.ReorderAfter)
            {
                SetDragHoverNode(null);
                bool insertAfter = dropMode == PaneDropMode.ReorderAfter;
                if (!Object.ReferenceEquals(dragInsertTargetNode, targetNode) || dragInsertAfter != insertAfter)
                {
                    ShowDragInsertIndicator(targetNode, insertAfter);
                }
            }
            else if (dropMode == PaneDropMode.NestInside)
            {
                ClearDragInsertIndicator();
                SetDragHoverNode(targetNode);
            }
            else
            {
                ClearDragFeedback();
            }
        }

        private void treeView1_DragLeave(object sender, EventArgs e)
        {
            ClearDragFeedback();
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }
    }
}
