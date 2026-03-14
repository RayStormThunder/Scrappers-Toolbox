using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library;

namespace LayoutBXLYT
{
    internal static class LayoutAnimClipboard
    {
        private static BxlanPaiEntry CopiedPaiEntry;
        private static BxlanPaiTag CopiedTag;
        private static BxlanPaiTagEntry CopiedTagEntry;
        private static KeyFrame CopiedKeyFrame;

        public static bool HasPaiEntry => CopiedPaiEntry != null;
        public static bool HasTag => CopiedTag != null;
        public static bool HasTagEntry => CopiedTagEntry != null;
        public static bool HasKeyFrame => CopiedKeyFrame != null;

        public static void CopyPaiEntry(BxlanPaiEntry paiEntry)
        {
            CopiedPaiEntry = ClonePaiEntry(paiEntry);
        }

        public static void CopyTag(BxlanPaiTag tag)
        {
            CopiedTag = CloneTag(tag);
        }

        public static void CopyTagEntry(BxlanPaiTagEntry entry)
        {
            CopiedTagEntry = CloneTagEntry(entry);
        }

        public static void CopyKeyFrame(KeyFrame keyFrame)
        {
            CopiedKeyFrame = CloneKeyFrame(keyFrame);
        }

        public static BxlanPaiTag GetTagClone()
        {
            return CopiedTag == null ? null : CloneTag(CopiedTag);
        }

        public static BxlanPaiEntry GetPaiEntryClone()
        {
            return CopiedPaiEntry == null ? null : ClonePaiEntry(CopiedPaiEntry);
        }

        public static BxlanPaiTagEntry GetTagEntryClone()
        {
            return CopiedTagEntry == null ? null : CloneTagEntry(CopiedTagEntry);
        }

        public static KeyFrame GetKeyFrameClone()
        {
            return CopiedKeyFrame == null ? null : CloneKeyFrame(CopiedKeyFrame);
        }

        private static BxlanPaiTag CloneTag(BxlanPaiTag source)
        {
            var copy = CreateTagInstance(source);
            foreach (var entry in source.Entries)
                copy.Entries.Add(CloneTagEntry(entry));
            NormalizeIndexes(copy.Entries);
            return copy;
        }

        private static BxlanPaiEntry ClonePaiEntry(BxlanPaiEntry source)
        {
            var copy = new BxlanPaiEntry()
            {
                Name = source.Name,
                Target = source.Target,
            };

            foreach (var tag in source.Tags)
                copy.Tags.Add(CloneTag(tag));

            return copy;
        }

        private static BxlanPaiTag CreateTagInstance(BxlanPaiTag source)
        {
            var type = source.GetType();
            var ctor = type.GetConstructor(new[] { typeof(string) });
            BxlanPaiTag copy = ctor != null
                ? (BxlanPaiTag)ctor.Invoke(new object[] { source.Tag })
                : new BxlanPaiTag(source.Tag);

            // Keep REV's internal unknown value when available to avoid writer mismatches.
            var unknownProperty = type.GetProperty("Unknown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (unknownProperty != null && unknownProperty.CanRead && unknownProperty.CanWrite)
                unknownProperty.SetValue(copy, unknownProperty.GetValue(source));

            return copy;
        }

        private static BxlanPaiTagEntry CloneTagEntry(BxlanPaiTagEntry source)
        {
            var type = source.GetType();
            var ctor = type.GetConstructor(new[] { typeof(byte), typeof(byte) });
            BxlanPaiTagEntry copy = ctor != null
                ? (BxlanPaiTagEntry)ctor.Invoke(new object[] { source.AnimationTarget, (byte)source.CurveType })
                : new BxlanPaiTagEntry(source.AnimationTarget, (byte)source.CurveType);

            copy.Index = source.Index;
            foreach (var key in source.KeyFrames)
                copy.KeyFrames.Add(CloneKeyFrame(key));
            return copy;
        }

        private static KeyFrame CloneKeyFrame(KeyFrame source)
        {
            return new KeyFrame(source.Frame)
            {
                Value = source.Value,
                Slope = source.Slope,
            };
        }

        public static void NormalizeIndexes(List<BxlanPaiTagEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
                entries[i].Index = (byte)i;
        }
    }

    public class LayoutAnimTreeLoader
    {
        public static void LoadAnimationEntry(BxlanPaiEntry entry, TreeNode root, TreeView treeView)
        {
            var nodeEntry = new GroupAnimWrapper(entry.Name) { Tag = entry };
            if (root != null)
                root.Nodes.Add(nodeEntry);
            else
                treeView.Nodes.Add(nodeEntry);

            for (int i = 0; i < entry.Tags.Count; i++)
            {
                var nodeTag = new GroupWrapper(entry.Tags[i].Type) { Tag = entry.Tags[i] };
                nodeEntry.Nodes.Add(nodeTag);
                for (int j = 0; j < entry.Tags[i].Entries.Count; j++)
                    LoadTagEntry(entry.Tags[i].Entries[j], nodeTag, j);
            }
        }

        private static void LoadTagEntry(BxlanPaiTagEntry entry, TreeNode root, int index)
        {
            var nodeEntry = new GroupTargetWrapper(entry.TargetName) { Tag = entry };
            root.Nodes.Add(nodeEntry);

            for (int i = 0; i < entry.KeyFrames.Count; i++)
            {
                var keyNode = new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(i, entry.KeyFrames[i])) { Tag = entry.KeyFrames[i] };
                nodeEntry.Nodes.Add(keyNode);
            }
        }
    }

    public class AnimInfoWrapper : TreeNode, IContextMenuNode
    {
        private BxlytHeader ParentLayout;
        public BxlanPAI1 bxlanPai => (BxlanPAI1)Tag;

        public AnimInfoWrapper(string text, BxlytHeader parentLayout)
        {
            Text = text;
            ParentLayout = parentLayout;
        }

        public ToolStripItem[] GetContextMenuItems()
        {
            List<ToolStripItem> Items = new List<ToolStripItem>();
            Items.Add(new ToolStripMenuItem("Add Animation Group", null, AddGroup, Keys.Control | Keys.A));
            Items.Add(new ToolStripMenuItem("Paste Animation Group", null, PasteAnimationGroup, Keys.Control | Keys.V)
            {
                Enabled = LayoutAnimClipboard.HasPaiEntry,
            });
            Items.Add(new ToolStripMenuItem("Clear Groups", null, ClearGroups, Keys.None));
            return Items.ToArray();
        }

        private void AddGroup(object sender, EventArgs e)
        {
            AddAnimGroupDialog dlg = new AddAnimGroupDialog(bxlanPai, ParentLayout);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var entry = dlg.AddEntry();
                var nodeEntry = new GroupAnimWrapper(entry.Name) { Tag = entry };
                Nodes.Add(nodeEntry);
            }
        }

        private void ClearGroups(object sender, EventArgs e)
        {
            bxlanPai.Entries.Clear();
            Nodes.Clear();
        }

        private void PasteAnimationGroup(object sender, EventArgs e)
        {
            var entry = LayoutAnimClipboard.GetPaiEntryClone();
            if (entry == null)
                return;

            bxlanPai.Entries.Add(entry);
            LayoutAnimTreeLoader.LoadAnimationEntry(entry, this, null);
        }
    }

    public class GroupAnimWrapper : TreeNode, IContextMenuNode
    {
        public BxlanPAI1 AnimInfo => (BxlanPAI1)Parent.Tag;

        public BxlanPaiEntry PaiEntry => (BxlanPaiEntry)Tag;

        public GroupAnimWrapper(string text)
        {
            Text = text;
        }

        public ToolStripItem[] GetContextMenuItems()
        {
            List<ToolStripItem> Items = new List<ToolStripItem>();
            Items.Add(new ToolStripMenuItem("Copy Animation Group", null, CopyAnimationGroup, Keys.Control | Keys.C));
            Items.Add(new ToolStripMenuItem("Paste Animation Group", null, PasteAnimationGroup, Keys.Control | Keys.Shift | Keys.V)
            {
                Enabled = LayoutAnimClipboard.HasPaiEntry,
            });
            Items.Add(new ToolStripMenuItem("Add Animation Group", null, AddGroup, Keys.Control | Keys.A));
            Items.Add(new ToolStripMenuItem("Paste Group", null, PasteGroup, Keys.Control | Keys.V)
            {
                Enabled = LayoutAnimClipboard.HasTag,
            });
            Items.Add(new ToolStripMenuItem("Remove Group", null, RemoveGroup, Keys.Delete));
            Items.Add(new ToolStripMenuItem("Clear Groups", null, ClearGroups, Keys.None));
            return Items.ToArray();
        }

        private void RemoveGroup(object sender, EventArgs e)
        {
            AnimInfo.Entries.Remove(PaiEntry);
            Parent.Nodes.Remove(this);
        }

        private void CopyAnimationGroup(object sender, EventArgs e)
        {
            LayoutAnimClipboard.CopyPaiEntry(PaiEntry);
        }

        private void PasteAnimationGroup(object sender, EventArgs e)
        {
            var entry = LayoutAnimClipboard.GetPaiEntryClone();
            if (entry == null)
                return;

            int insertIndex = AnimInfo.Entries.IndexOf(PaiEntry);
            if (insertIndex < 0)
            {
                AnimInfo.Entries.Add(entry);
                var newNode = new GroupAnimWrapper(entry.Name) { Tag = entry };
                Parent.Nodes.Add(newNode);
                for (int i = 0; i < entry.Tags.Count; i++)
                {
                    var tagNode = new GroupWrapper(entry.Tags[i].Type) { Tag = entry.Tags[i] };
                    newNode.Nodes.Add(tagNode);
                    for (int j = 0; j < entry.Tags[i].Entries.Count; j++)
                    {
                        var targetNode = new GroupTargetWrapper(entry.Tags[i].Entries[j].TargetName) { Tag = entry.Tags[i].Entries[j] };
                        tagNode.Nodes.Add(targetNode);
                        for (int k = 0; k < entry.Tags[i].Entries[j].KeyFrames.Count; k++)
                            targetNode.Nodes.Add(new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(k, entry.Tags[i].Entries[j].KeyFrames[k])) { Tag = entry.Tags[i].Entries[j].KeyFrames[k] });
                    }
                }
                return;
            }

            AnimInfo.Entries.Insert(insertIndex + 1, entry);
            var nodeEntry = new GroupAnimWrapper(entry.Name) { Tag = entry };
            Parent.Nodes.Insert(Parent.Nodes.IndexOf(this) + 1, nodeEntry);

            for (int i = 0; i < entry.Tags.Count; i++)
            {
                var nodeTag = new GroupWrapper(entry.Tags[i].Type) { Tag = entry.Tags[i] };
                nodeEntry.Nodes.Add(nodeTag);
                for (int j = 0; j < entry.Tags[i].Entries.Count; j++)
                {
                    var nodeTarget = new GroupTargetWrapper(entry.Tags[i].Entries[j].TargetName) { Tag = entry.Tags[i].Entries[j] };
                    nodeTag.Nodes.Add(nodeTarget);
                    for (int k = 0; k < entry.Tags[i].Entries[j].KeyFrames.Count; k++)
                        nodeTarget.Nodes.Add(new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(k, entry.Tags[i].Entries[j].KeyFrames[k])) { Tag = entry.Tags[i].Entries[j].KeyFrames[k] });
                }
            }
        }

        private void AddGroup(object sender, EventArgs e)
        {
            AddGroupTypeDialog dlg = new AddGroupTypeDialog(PaiEntry);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var tag = dlg.AddEntry();
                var nodeEntry = new GroupWrapper(tag.Type) { Tag = tag };
                Nodes.Add(nodeEntry);
            }
        }

        private void PasteGroup(object sender, EventArgs e)
        {
            var tag = LayoutAnimClipboard.GetTagClone();
            if (tag == null)
                return;

            PaiEntry.Tags.Add(tag);
            var nodeEntry = new GroupWrapper(tag.Type) { Tag = tag };
            Nodes.Add(nodeEntry);

            for (int i = 0; i < tag.Entries.Count; i++)
            {
                var targetNode = new GroupTargetWrapper(tag.Entries[i].TargetName) { Tag = tag.Entries[i] };
                nodeEntry.Nodes.Add(targetNode);
                for (int j = 0; j < tag.Entries[i].KeyFrames.Count; j++)
                    targetNode.Nodes.Add(new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(j, tag.Entries[i].KeyFrames[j])) { Tag = tag.Entries[i].KeyFrames[j] });
            }
        }

        private void ClearGroups(object sender, EventArgs e)
        {
            PaiEntry.Tags.Clear();
            Nodes.Clear();
        }
    }

    public class GroupWrapper : TreeNode, IContextMenuNode
    {
        public BxlanPaiEntry ParentPaiEntry => (BxlanPaiEntry)Parent.Tag;

        public BxlanPaiTag GroupTag => (BxlanPaiTag)Tag;

        public GroupWrapper(string text)
        {
            Text = text;
        }

        public ToolStripItem[] GetContextMenuItems()
        {
            List<ToolStripItem> Items = new List<ToolStripItem>();
            Items.Add(new ToolStripMenuItem("Copy Group", null, CopyGroup, Keys.Control | Keys.C));
            Items.Add(new ToolStripMenuItem("Add Target", null, AddTarget, Keys.Control | Keys.A));
            Items.Add(new ToolStripMenuItem("Paste Target", null, PasteTarget, Keys.Control | Keys.V)
            {
                Enabled = LayoutAnimClipboard.HasTagEntry,
            });
            Items.Add(new ToolStripMenuItem("Remove Group", null, RemoveGroup, Keys.Delete));
            Items.Add(new ToolStripMenuItem("Clear Targets", null, ClearTargets, Keys.Control | Keys.Shift | Keys.C));
            return Items.ToArray();
        }

        private void CopyGroup(object sender, EventArgs e)
        {
            LayoutAnimClipboard.CopyTag(GroupTag);
        }

        private void RemoveGroup(object sender, EventArgs e)
        {
            ParentPaiEntry.Tags.Remove(GroupTag);
            Parent.Nodes.Remove(this);
        }

        private void AddTarget(object sender, EventArgs e)
        {
            AddGroupTargetDialog dlg = new AddGroupTargetDialog();
            bool canLoad = dlg.LoadTag(GroupTag);
            if (dlg.ShowDialog() == DialogResult.OK && canLoad)
            {
                BxlanPaiTagEntry target = dlg.GetGroupTarget();
                target.Index = (byte)GroupTag.Entries.Count;
                GroupTag.Entries.Add(target);

                var nodeEntry = new GroupTargetWrapper(target.TargetName) { Tag = target };
                Nodes.Add(nodeEntry);
            }
        }

        private void ClearTargets(object sender, EventArgs e)
        {
            GroupTag.Entries.Clear();
            Nodes.Clear();
        }

        private void PasteTarget(object sender, EventArgs e)
        {
            var target = LayoutAnimClipboard.GetTagEntryClone();
            if (target == null)
                return;

            GroupTag.Entries.Add(target);
            LayoutAnimClipboard.NormalizeIndexes(GroupTag.Entries);

            var nodeEntry = new GroupTargetWrapper(target.TargetName) { Tag = target };
            Nodes.Add(nodeEntry);
            for (int i = 0; i < target.KeyFrames.Count; i++)
                nodeEntry.Nodes.Add(new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(i, target.KeyFrames[i])) { Tag = target.KeyFrames[i] });
        }
    }


    public class GroupTargetWrapper : TreeNode, IContextMenuNode
    {
        public BxlanPaiTag GroupTag => (BxlanPaiTag)Parent.Tag;

        public BxlanPaiTagEntry TypeTag => (BxlanPaiTagEntry)Tag;

        public GroupTargetWrapper(string text)
        {
            Text = text;
        }

        public ToolStripItem[] GetContextMenuItems()
        {
            List<ToolStripItem> Items = new List<ToolStripItem>();
            Items.Add(new ToolStripMenuItem("Copy Target", null, CopyTarget, Keys.Control | Keys.C));
            Items.Add(new ToolStripMenuItem("Add Keyframe", null, AddKey, Keys.Control | Keys.A));
            Items.Add(new ToolStripMenuItem("Paste Key", null, PasteKey, Keys.Control | Keys.V)
            {
                Enabled = LayoutAnimClipboard.HasKeyFrame,
            });
            Items.Add(new ToolStripMenuItem("Remove Target", null, RemoveTarget, Keys.Delete));
            Items.Add(new ToolStripMenuItem("Clear Keys", null, RemoveKeys, Keys.Control | Keys.Shift | Keys.C));
            return Items.ToArray();
        }

        private void CopyTarget(object sender, EventArgs e)
        {
            LayoutAnimClipboard.CopyTagEntry(TypeTag);
        }

        private void AddKey(object sender, EventArgs e)
        {
            float frame = 0;
            if (TypeTag.KeyFrames.Count > 0)
                frame = TypeTag.KeyFrames.Max(k => k.Frame) + 1;

            var keyFrame = new KeyFrame(frame);
            var keyNode = new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(TypeTag.KeyFrames.Count, keyFrame))
            { Tag = keyFrame };

            TypeTag.KeyFrames.Add(keyFrame);
            Nodes.Add(keyNode);
        }

        private void RemoveKeys(object sender, EventArgs e)
        {
            TypeTag.KeyFrames.Clear();
            Nodes.Clear();
        }

        private void RemoveTarget(object sender, EventArgs e)
        {
            GroupTag.Entries.Remove(TypeTag);
            LayoutAnimClipboard.NormalizeIndexes(GroupTag.Entries);
            Parent.Nodes.Remove(this);
        }

        private void PasteKey(object sender, EventArgs e)
        {
            var keyFrame = LayoutAnimClipboard.GetKeyFrameClone();
            if (keyFrame == null)
                return;

            TypeTag.KeyFrames.Add(keyFrame);
            var keyNode = new KeyNodeWrapper(KeyNodeWrapper.FormatKeyNodeText(TypeTag.KeyFrames.Count - 1, keyFrame))
            { Tag = keyFrame };
            Nodes.Add(keyNode);
        }

        public void UpdateKeys(TreeNode removedNode)
        {
            int index = 0;
            foreach (TreeNode node in Nodes)
            {
                if (node == removedNode)
                    continue;

                var keyNode = node as KeyNodeWrapper;
                if (keyNode != null)
                    node.Text = KeyNodeWrapper.FormatKeyNodeText(index++, keyNode.KeyFrame);
            }
        }
    }

    public class KeyNodeWrapper : TreeNode, IContextMenuNode
    {
        public BxlanPaiTagEntry TypeTag => (BxlanPaiTagEntry)Parent.Tag;

        public KeyFrame KeyFrame => (KeyFrame)Tag;

        public KeyNodeWrapper(string text)
        {
            Text = text;
        }

        public static string FormatKeyNodeText(int index, KeyFrame keyFrame)
        {
            if (keyFrame == null)
                return $"Key {index}";

            string frameText = keyFrame.Frame.ToString("0.###", CultureInfo.InvariantCulture);
            string slopeText = keyFrame.Slope.ToString("0.###", CultureInfo.InvariantCulture);
            return $"Key {index} [Frame {frameText}, Slope {slopeText}]";
        }

        public ToolStripItem[] GetContextMenuItems()
        {
            List<ToolStripItem> Items = new List<ToolStripItem>();
            Items.Add(new ToolStripMenuItem("Copy Key", null, CopyKey, Keys.Control | Keys.C));
            Items.Add(new ToolStripMenuItem("Remove Key", null, RemoveKey, Keys.Delete));
            return Items.ToArray();
        }

        private void CopyKey(object sender, EventArgs e)
        {
            LayoutAnimClipboard.CopyKeyFrame(KeyFrame);
        }

        private void RemoveKey(object sender, EventArgs e)
        {
            TypeTag.KeyFrames.Remove(KeyFrame);
            ((GroupTargetWrapper)Parent).UpdateKeys(this);
            Parent.Nodes.Remove(this);
        }
    }
}
