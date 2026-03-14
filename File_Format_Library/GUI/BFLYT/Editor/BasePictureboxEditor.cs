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
using Toolbox.Library.IO;
using FirstPlugin;

namespace LayoutBXLYT
{
    public partial class BasePictureboxEditor : EditorPanelBase
    {
        public BasePictureboxEditor()
        {
            InitializeComponent();

            vertexColorBox1.OnColorChanged += OnColorChanged;

            topLeftXUD.ValueChanged += texCoordValue_Changed;
            topLeftYUD.ValueChanged += texCoordValue_Changed;
            topRightXUD.ValueChanged += texCoordValue_Changed;
            topRightYUD.ValueChanged += texCoordValue_Changed;

            bottomLeftXUD.ValueChanged += texCoordValue_Changed;
            bottomLeftYUD.ValueChanged += texCoordValue_Changed;
            bottomRightXUD.ValueChanged += texCoordValue_Changed;
            bottomRightYUD.ValueChanged += texCoordValue_Changed;

            stDropDownPanel1.ResetColors();
            stDropDownPanel3.ResetColors();
        }

        private IPicturePane ActivePane;
        private PaneEditor parentEditor;

        private bool Loaded = false;

        public void LoadPane(IPicturePane pane, PaneEditor paneEditor)
        {
            parentEditor = paneEditor;
            Loaded = false;

            ActivePane = pane;
            vertexColorBox1.TopLeftColor = pane.ColorTopLeft.Color;
            vertexColorBox1.TopRightColor = pane.ColorTopRight.Color;
            vertexColorBox1.BottomLeftColor = pane.ColorBottomLeft.Color;
            vertexColorBox1.BottomRightColor = pane.ColorBottomRight.Color;
            vertexColorBox1.Refresh();

            UpdateKeyState();

            ReloadTexCoord(0);

            Loaded = true;
        }

        private void UpdateKeyState()
        {
            if (parentEditor == null || ActivePane == null || !parentEditor.IsAnimationEditMode)
            {
                vertexColorBox1.ShowBorder = false;
                vertexColorBox1.Refresh();
                return;
            }

            bool animated = parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftTopRed)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftTopGreen)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftTopBlue)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftTopAlpha)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightTopRed)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightTopGreen)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightTopBlue)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightTopAlpha)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftBottomRed)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftBottomGreen)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftBottomBlue)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.LeftBottomAlpha)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightBottomRed)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightBottomGreen)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightBottomBlue)
                || parentEditor.IsPaneVertexColorAnimated((BasePane)ActivePane, LVCTarget.RightBottomAlpha);

            vertexColorBox1.BorderColor = Color.Red;
            vertexColorBox1.ShowBorder = animated;
            vertexColorBox1.Refresh();
        }

        private void ApplyVertexKeys(BasePane pane)
        {
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftTopRed, vertexColorBox1.TopLeftColor.R);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftTopGreen, vertexColorBox1.TopLeftColor.G);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftTopBlue, vertexColorBox1.TopLeftColor.B);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftTopAlpha, vertexColorBox1.TopLeftColor.A);

            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightTopRed, vertexColorBox1.TopRightColor.R);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightTopGreen, vertexColorBox1.TopRightColor.G);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightTopBlue, vertexColorBox1.TopRightColor.B);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightTopAlpha, vertexColorBox1.TopRightColor.A);

            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftBottomRed, vertexColorBox1.BottomLeftColor.R);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftBottomGreen, vertexColorBox1.BottomLeftColor.G);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftBottomBlue, vertexColorBox1.BottomLeftColor.B);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.LeftBottomAlpha, vertexColorBox1.BottomLeftColor.A);

            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightBottomRed, vertexColorBox1.BottomRightColor.R);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightBottomGreen, vertexColorBox1.BottomRightColor.G);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightBottomBlue, vertexColorBox1.BottomRightColor.B);
            parentEditor.ApplyPaneVertexColorKey(pane, LVCTarget.RightBottomAlpha, vertexColorBox1.BottomRightColor.A);
        }

        private void ReloadTexCoord(int index) {
            texCoordIndexCB.Items.Clear();
            for (int i = 0; i < ActivePane.TexCoords?.Length; i++)
                texCoordIndexCB.Items.Add($"TexCoord [{i}]");

            if (ActivePane.TexCoords?.Length > index)
                texCoordIndexCB.SelectedIndex = index;

            if (ActivePane.TexCoords.Length == 3)
                btnAdd.Enabled = false;
            else
                btnAdd.Enabled = true;

            if (ActivePane.TexCoords.Length == 1)
                btnRemove.Enabled = false;
            else
                btnRemove.Enabled = true;
        }

        private void OnColorChanged(object sender, EventArgs e)
        {
            if (!Loaded) return;

            Color oldTL = ActivePane.ColorTopLeft.Color;
            Color oldTR = ActivePane.ColorTopRight.Color;
            Color oldBL = ActivePane.ColorBottomLeft.Color;
            Color oldBR = ActivePane.ColorBottomRight.Color;

            bool relative = Runtime.LayoutEditor.MulticlickBehavior == Runtime.LayoutEditor.MulticlickBehaviorMode.Relative;

            ActivePane.ColorTopLeft.Color = vertexColorBox1.TopLeftColor;
            ActivePane.ColorTopRight.Color = vertexColorBox1.TopRightColor;
            ActivePane.ColorBottomLeft.Color = vertexColorBox1.BottomLeftColor;
            ActivePane.ColorBottomRight.Color = vertexColorBox1.BottomRightColor;

            ApplyVertexKeys((BasePane)ActivePane);

            //Apply to all selected panes
            foreach (BasePane pane in parentEditor.SelectedPanes)
            {
                if (pane is IPicturePane && pane != (BasePane)ActivePane) {
                    var picturePane = (IPicturePane)pane;

                    if (relative)
                    {
                        picturePane.ColorTopLeft.Color = ApplyRelativeColorDelta(picturePane.ColorTopLeft.Color, oldTL, vertexColorBox1.TopLeftColor);
                        picturePane.ColorTopRight.Color = ApplyRelativeColorDelta(picturePane.ColorTopRight.Color, oldTR, vertexColorBox1.TopRightColor);
                        picturePane.ColorBottomLeft.Color = ApplyRelativeColorDelta(picturePane.ColorBottomLeft.Color, oldBL, vertexColorBox1.BottomLeftColor);
                        picturePane.ColorBottomRight.Color = ApplyRelativeColorDelta(picturePane.ColorBottomRight.Color, oldBR, vertexColorBox1.BottomRightColor);
                    }
                    else
                    {
                        picturePane.ColorTopLeft.Color = vertexColorBox1.TopLeftColor;
                        picturePane.ColorTopRight.Color = vertexColorBox1.TopRightColor;
                        picturePane.ColorBottomLeft.Color = vertexColorBox1.BottomLeftColor;
                        picturePane.ColorBottomRight.Color = vertexColorBox1.BottomRightColor;
                    }

                    ApplyVertexKeys((BasePane)pane);
                }
            }

            UpdateKeyState();

            parentEditor.PropertyChanged?.Invoke(sender, e);
        }

        private static Color ApplyRelativeColorDelta(Color source, Color oldActive, Color newActive)
        {
            int r = Math.Max(0, Math.Min(255, source.R + (newActive.R - oldActive.R)));
            int g = Math.Max(0, Math.Min(255, source.G + (newActive.G - oldActive.G)));
            int b = Math.Max(0, Math.Min(255, source.B + (newActive.B - oldActive.B)));
            int a = Math.Max(0, Math.Min(255, source.A + (newActive.A - oldActive.A)));
            return Color.FromArgb(a, r, g, b);
        }

        private void texCoordValue_Changed(object sender, EventArgs e)
        {
            int index = texCoordIndexCB.SelectedIndex;
            if (index < 0 || !Loaded) return;

            ActivePane.TexCoords[index].TopLeft = 
                new Syroot.Maths.Vector2F(topLeftXUD.Value, topLeftYUD.Value);

            ActivePane.TexCoords[index].TopRight =
             new Syroot.Maths.Vector2F(topRightXUD.Value, topRightYUD.Value);

            ActivePane.TexCoords[index].BottomLeft =
          new Syroot.Maths.Vector2F(bottomLeftXUD.Value, bottomLeftYUD.Value);

            ActivePane.TexCoords[index].BottomRight =
             new Syroot.Maths.Vector2F(bottomRightXUD.Value, bottomRightYUD.Value);

            parentEditor.PropertyChanged?.Invoke(sender, e);
        }

        private void texCoordIndexCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = texCoordIndexCB.SelectedIndex;
            if (index < 0) return;

            topLeftXUD.Value = ActivePane.TexCoords[index].TopLeft.X;
            topLeftYUD.Value = ActivePane.TexCoords[index].TopLeft.Y;
            topRightXUD.Value = ActivePane.TexCoords[index].TopRight.X;
            topRightYUD.Value = ActivePane.TexCoords[index].TopRight.Y;

            bottomLeftXUD.Value = ActivePane.TexCoords[index].BottomLeft.X;
            bottomLeftYUD.Value = ActivePane.TexCoords[index].BottomLeft.Y;
            bottomRightXUD.Value = ActivePane.TexCoords[index].BottomRight.X;
            bottomRightYUD.Value = ActivePane.TexCoords[index].BottomRight.Y;
        }

        private void btnResetColors_Click(object sender, EventArgs e)
        {
            vertexColorBox1.TopLeftColor = Color.White;
            vertexColorBox1.TopRightColor = Color.White;
            vertexColorBox1.BottomLeftColor = Color.White;
            vertexColorBox1.BottomRightColor = Color.White;
            vertexColorBox1.Refresh();
        }

        public override void OnControlClosing() {
            vertexColorBox1.DisposeControl();
        }

        private void btnAdd_Click(object sender, EventArgs e) {
            if (ActivePane.Material == null) return;

            if (ActivePane.Material.TextureMaps.Length <= ActivePane.TexCoords.Length) {
                MessageBox.Show($"You should have atleast {ActivePane.Material.TextureMaps.Length + 1} " +
                                 "textures to add new texture coordinates!");
                return;
            }

            ActivePane.TexCoords = ActivePane.TexCoords.AddToArray(new TexCoord()); 
            ReloadTexCoord(ActivePane.TexCoords.Length - 1);
        }

        private void btnRemove_Click(object sender, EventArgs e) {
            int index = texCoordIndexCB.SelectedIndex;
            if (index == -1 || ActivePane.Material == null) return;

            var result = MessageBox.Show($"Are you sure you want to remove texture coordinate {index}? This will make any texture mapped to this to the first one.", 
                                    "Layout Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes) {
                bool removed = ActivePane.Material.RemoveTexCoordSources(index);
                if (removed)
                    ActivePane.TexCoords = ActivePane.TexCoords.RemoveAt(index);

                ReloadTexCoord(ActivePane.TexCoords.Length - 1);
            }
        }
    }
}
