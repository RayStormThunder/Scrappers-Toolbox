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

namespace LayoutBXLYT
{
    public partial class LayoutAnimList : LayoutDocked
    {
        private EventHandler OnProperySelected;
        private bool isLoaded = false;
        private bool suppressSelectionEvent = false;
        private bool hasPreferredAnimations = false;
        private string activeLayoutName = string.Empty;
        private ListViewItem lastValidAnimationItem = null;
        private ListViewItem ignoreNextGraySelectionItem = null;
        private LayoutEditor ParentEditor;
        private ImageList imgList = new ImageList();

        public LayoutAnimList(LayoutEditor parentEditor, EventHandler onPropertySelected)
        {
            OnProperySelected = onPropertySelected;
            ParentEditor = parentEditor;
            InitializeComponent();

            BackColor = FormThemes.BaseTheme.FormBackColor;
            ForeColor = FormThemes.BaseTheme.FormForeColor;

            listView1.BackColor = FormThemes.BaseTheme.FormBackColor;
            listView1.ForeColor = FormThemes.BaseTheme.FormForeColor;
            listView1.FullRowSelect = true;
            listView1.MultiSelect = false;

            imgList = new ImageList()
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(24, 24),
            };

            imgList.Images.Add("LayoutAnimation", FirstPlugin.Properties.Resources.LayoutAnimation);

            listView1.SmallImageList = imgList;
            listView1.LargeImageList = imgList;
            listView1.Sorting = SortOrder.None;
        }

        private static bool IsAnimationFile(ArchiveFileInfo file)
        {
            var ext = Utils.GetExtension(file.FileName);
            return ext == ".brlan" || ext == ".bclan" || ext == ".bflan";
        }

        private bool IsPreferredAnimationName(string animFileName)
        {
            if (string.IsNullOrEmpty(activeLayoutName))
                return false;

            var name = System.IO.Path.GetFileNameWithoutExtension(animFileName).ToLower();
            return name.Contains(activeLayoutName);
        }

        public void SearchAnimations(BxlytHeader bxlyt)
        {
            isLoaded = false;
            lastValidAnimationItem = null;

            listView1.Items.Clear();

            var layoutFile = bxlyt.FileInfo;
            var parentArchive = layoutFile.IFileInfo.ArchiveParent;
            if (parentArchive == null) return;

            activeLayoutName = System.IO.Path.GetFileNameWithoutExtension(bxlyt.FileName).ToLower();

            var files = parentArchive.Files.Where(IsAnimationFile).ToList();
            var preferred = files.Where(x => IsPreferredAnimationName(x.FileName)).ToList();
            hasPreferredAnimations = preferred.Count > 0;

            List<ArchiveFileInfo> ordered = new List<ArchiveFileInfo>();
            if (hasPreferredAnimations)
            {
                ordered.AddRange(preferred);
                ordered.AddRange(files.Where(x => !IsPreferredAnimationName(x.FileName)));
            }
            else
            {
                // Keep archive order if no matching animations exist.
                ordered.AddRange(files);
            }

            listView1.BeginUpdate();
            foreach (var file in ordered)
            {
                LoadAnimation(file);
            }
            listView1.EndUpdate();

            isLoaded = true;
        }

        public void LoadAnimation(ArchiveFileInfo archiveEntry)
        {
            var item = new ListViewItem(System.IO.Path.GetFileName(archiveEntry.FileName))
            {
                Tag = archiveEntry,
                ImageKey = "LayoutAnimation",
            };

            if (hasPreferredAnimations && !IsPreferredAnimationName(archiveEntry.FileName))
                item.ForeColor = Color.Gray;

            listView1.Items.Add(item);
        }

        public void LoadAnimation(BxlanHeader bxlan)
        {
            if (bxlan == null)
                return;

            isLoaded = false;
            listView1.BeginUpdate();
            listView1.Items.Add(new ListViewItem(bxlan.FileName)
            {
                Tag = bxlan,
                ImageKey = "LayoutAnimation",
            });
            listView1.EndUpdate();

            isLoaded = true;
        }

        public ListView.SelectedListViewItemCollection GetSelectedAnimations => listView1.SelectedItems;

        public IEnumerable<ListViewItem> GetAllAnimationItems()
        {
            return listView1.Items.Cast<ListViewItem>();
        }

        private bool IsSelectableWithoutPrompt(ListViewItem item)
        {
            return !hasPreferredAnimations || item.ForeColor != Color.Gray;
        }

        private ListViewItem GetFirstValidAnimationItem()
        {
            foreach (ListViewItem item in listView1.Items)
            {
                if (IsSelectableWithoutPrompt(item))
                    return item;
            }
            return null;
        }

        private ListViewItem GetFallbackAnimationItem()
        {
            if (lastValidAnimationItem != null &&
                lastValidAnimationItem.ListView == listView1 &&
                IsSelectableWithoutPrompt(lastValidAnimationItem))
            {
                return lastValidAnimationItem;
            }

            return GetFirstValidAnimationItem();
        }

        private void SelectSingleItem(ListViewItem item, bool applySelection)
        {
            suppressSelectionEvent = true;
            foreach (ListViewItem selectedItem in listView1.SelectedItems)
                selectedItem.Selected = false;

            if (item != null)
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
            }
            suppressSelectionEvent = false;

            if (applySelection && item != null && isLoaded)
            {
                var args = new ListViewItemSelectionChangedEventArgs(item, item.Index, true);
                OnProperySelected.Invoke("Select", args);
            }
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (suppressSelectionEvent)
                return;

            if (isLoaded && e.IsSelected && hasPreferredAnimations && e.Item.ForeColor == Color.Gray)
            {
                if (ignoreNextGraySelectionItem == e.Item)
                {
                    ignoreNextGraySelectionItem = null;
                    SelectSingleItem(GetFallbackAnimationItem(), false);
                    return;
                }

                var requestedItem = e.Item;
                var fallbackItem = GetFallbackAnimationItem();

                // Immediately switch away from gray selection before prompting.
                SelectSingleItem(fallbackItem, fallbackItem != null);
                if (fallbackItem != null)
                    lastValidAnimationItem = fallbackItem;

                var result = MessageBox.Show(
                    "This animation appears to belong to a different layout. Apply it anyway?",
                    "Apply Animation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    SelectSingleItem(requestedItem, true);
                    lastValidAnimationItem = requestedItem;
                }
                else
                {
                    // Some ListView selection flows immediately reselect the clicked item after NO.
                    // Ignore that one reselect to prevent a second prompt.
                    ignoreNextGraySelectionItem = requestedItem;
                    SelectSingleItem(fallbackItem, fallbackItem != null);
                }

                return;
            }

            if (isLoaded && e.IsSelected && IsSelectableWithoutPrompt(e.Item))
                lastValidAnimationItem = e.Item;

            if (isLoaded)
                OnProperySelected.Invoke("Select", e);

            if (listView1.SelectedItems.Count > 0)
            {
                var bxlan = listView1.SelectedItems[0].Tag as BxlanHeader;
              //  if (bxlan != null)
               //     ParentEditor.ShowBxlanEditor(bxlan);
            }
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                var bxlan = listView1.SelectedItems[0].Tag as BxlanHeader;
                if (bxlan != null)
                    ParentEditor.ShowBxlanEditor(bxlan);
            }
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
        
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e) {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

            foreach (ListViewItem item in listView1.SelectedItems) {
                var bxlan = item.Tag as BxlanHeader;
                var fileFormat = bxlan.FileInfo;
                //Check parent archive for raw data to export
                if (!fileFormat.CanSave && fileFormat.IFileInfo.ArchiveParent != null)
                {
                    foreach (var file in fileFormat.IFileInfo.ArchiveParent.Files)
                    {
                        if (file.FileName == fileFormat.FileName) {
                            files.Add(file.FileName, file.FileData);
                        }
                    }
                }
                else
                {
                    var mem = new System.IO.MemoryStream();
                    bxlan.FileInfo.Save(mem);
                    files.Add(fileFormat.FileName, mem.ToArray());
                }
            }

            if (files.Count == 1)
            {
                string name = files.Keys.FirstOrDefault();

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = System.IO.Path.GetFileName(name);
                sfd.DefaultExt = System.IO.Path.GetExtension(name);

                if (sfd.ShowDialog() == DialogResult.OK) {
                    System.IO.File.WriteAllBytes(sfd.FileName, files.Values.FirstOrDefault());
                }
            }
            if (files.Count > 1)
            {
                FolderSelectDialog dlg = new FolderSelectDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    foreach (var file in files) {
                        string name = System.IO.Path.GetFileName(file.Key);
                        System.IO.File.WriteAllBytes($"{dlg.SelectedPath}/{name}", file.Value);
                    }
                }
            }
        }
    }
}
