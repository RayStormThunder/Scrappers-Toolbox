using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Toolbox.Library.Forms
{
	public class MultiselectTreeView : TreeView
	{
		#region Public Properties

		private readonly List<TreeNode> m_SelectedNodes = new List<TreeNode>();
		private bool isUpdatingSelection = false;
		public List<TreeNode> SelectedNodes
		{
			get
			{
				var selected = new List<TreeNode>(m_SelectedNodes.Count);
				for (int i = m_SelectedNodes.Count - 1; i >= 0; i--)
				{
					var node = m_SelectedNodes[i];
					if (!IsValidNode(node))
					{
						m_SelectedNodes.RemoveAt(i);
						continue;
					}
					selected.Add(node);
				}
				selected.Reverse();

				// Keep the current primary selection first so property panels target
				// the most recently Ctrl-clicked node.
				if (base.SelectedNode != null)
				{
					int index = selected.IndexOf(base.SelectedNode);
					if (index > 0)
					{
						selected.RemoveAt(index);
						selected.Insert(0, base.SelectedNode);
					}
				}

				return selected;
			}
			set
			{
				SetSelection(value ?? new List<TreeNode>());
			}
		}

		#endregion

		public MultiselectTreeView()
		{
			HideSelection = false;
			FullRowSelect = true;
			base.SelectedNode = null;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			TreeNode clicked = ResolveNodeFromMouse(e.Location);

			bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;

			if (clicked == null)
			{
				if (!ctrl && e.Button == MouseButtons.Left)
					ClearSelection();
				base.OnMouseDown(e);
				return;
			}

			if (ctrl && e.Button == MouseButtons.Left)
			{
				bool wasSelected = m_SelectedNodes.Contains(clicked);
				ToggleSelection(clicked);
				if (!wasSelected)
					SetPrimarySelection(clicked);
				return;
			}

			if (!m_SelectedNodes.Contains(clicked))
				SetSelection(new List<TreeNode>() { clicked });

			base.OnMouseDown(e);
		}

		protected override void OnAfterSelect(TreeViewEventArgs e)
		{
			if (!isUpdatingSelection && e.Node != null)
				SetSelection(new List<TreeNode>() { e.Node });

			base.OnAfterSelect(e);
		}

		protected override void OnBeforeSelect(TreeViewCancelEventArgs e)
		{
			bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
			if (ctrl && e.Action == TreeViewAction.ByMouse)
			{
				// Prevent native single-selection behavior from overriding our Ctrl-click toggles.
				e.Cancel = true;
			}

			base.OnBeforeSelect(e);
		}

		private void ToggleSelection(TreeNode node)
		{
			if (!IsValidNode(node))
				return;

			if (m_SelectedNodes.Contains(node))
			{
				m_SelectedNodes.Remove(node);
				ApplyNodeSelectedTheme(node, false);

				if (base.SelectedNode == node)
				{
					isUpdatingSelection = true;
					base.SelectedNode = m_SelectedNodes.Count > 0 ? m_SelectedNodes[m_SelectedNodes.Count - 1] : null;
					isUpdatingSelection = false;
				}
			}
			else
			{
				m_SelectedNodes.Add(node);
				ApplyNodeSelectedTheme(node, true);
			}

			InvalidateNode(node);
		}

		private void ClearSelection()
		{
			BeginUpdate();
			for (int i = 0; i < m_SelectedNodes.Count; i++)
				ApplyNodeSelectedTheme(m_SelectedNodes[i], false);

			m_SelectedNodes.Clear();
			base.SelectedNode = null;
			EndUpdate();
		}

		private void SetPrimarySelection(TreeNode node)
		{
			if (!IsValidNode(node))
				return;

			isUpdatingSelection = true;
			base.SelectedNode = node;
			isUpdatingSelection = false;
			InvalidateNode(node);
		}

		private void SetSelection(List<TreeNode> nodes)
		{
			BeginUpdate();
			for (int i = 0; i < m_SelectedNodes.Count; i++)
				ApplyNodeSelectedTheme(m_SelectedNodes[i], false);

			m_SelectedNodes.Clear();
			foreach (var node in nodes)
			{
				if (IsValidNode(node) && !m_SelectedNodes.Contains(node))
				{
					m_SelectedNodes.Add(node);
					ApplyNodeSelectedTheme(node, true);
				}
			}

			isUpdatingSelection = true;
			base.SelectedNode = m_SelectedNodes.Count > 0 ? m_SelectedNodes[0] : null;
			isUpdatingSelection = false;
			EndUpdate();
		}

		private bool IsValidNode(TreeNode node)
		{
			return node != null && node.TreeView == this;
		}

		private void ApplyNodeSelectedTheme(TreeNode node, bool selected)
		{
			if (!IsValidNode(node))
				return;

			node.BackColor = selected ? SystemColors.Highlight : Color.Empty;
			if (selected)
				node.ForeColor = SystemColors.HighlightText;
			else if (UsesCheckedVisibilityState(node) && !node.Checked)
				node.ForeColor = FormThemes.BaseTheme.DisabledItemColor;
			else
				node.ForeColor = ForeColor;
		}

		private bool UsesCheckedVisibilityState(TreeNode node)
		{
			if (!IsValidNode(node) || node.Tag == null)
				return false;

			// Only pane hierarchy nodes use Checked as a visibility state.
			var type = node.Tag.GetType();
			while (type != null)
			{
				if (type.Name == "BasePane")
					return true;
				type = type.BaseType;
			}

			return false;
		}

		private void InvalidateNode(TreeNode node)
		{
			if (!IsValidNode(node))
				return;

			if (!node.Bounds.IsEmpty)
				Invalidate(node.Bounds);
			else
				Invalidate();
		}

		private TreeNode ResolveNodeFromMouse(Point location)
		{
			var hit = HitTest(location);
			if (hit?.Node != null)
				return hit.Node;

			if (!FullRowSelect)
				return null;

			// Fallback to row-based hit testing so Ctrl-click works anywhere on a selected row.
			for (TreeNode node = TopNode; node != null; node = node.NextVisibleNode)
			{
				if (node.Bounds.Top <= location.Y && location.Y < node.Bounds.Bottom)
					return node;
				if (node.Bounds.Top > location.Y)
					break;
			}

			return null;
		}
	}
}
