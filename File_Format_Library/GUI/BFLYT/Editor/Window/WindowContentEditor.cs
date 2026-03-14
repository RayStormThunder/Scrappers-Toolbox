using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library.Forms;

namespace LayoutBXLYT
{
    public partial class WindowContentEditor : EditorPanelBase
    {
        private IWindowPane ActivePane;
        private bool Loaded = false;
        private PaneEditor parentEditor;
        private BxlytWindowFrame activeFrame;

        public WindowContentEditor()
        {
            InitializeComponent();

            vertexColorBox1.OnColorChanged += OnColorChanged;

            stDropDownPanel1.ResetColors();
            stDropDownPanel2.ResetColors();
            stDropDownPanel3.ResetColors();
        }

        public void LoadPane(IWindowPane pane, BxlytWindowFrame frame, PaneEditor paneEditor)
        {
            Loaded = false;
            activeFrame = frame;
            ActivePane = pane;
            parentEditor = paneEditor;

            frameUpUD.Value = pane.FrameElementTop;
            frameDownUD.Value = pane.FrameElementBottm;
            frameLeftUD.Value = pane.FrameElementLeft;
            frameRightUD.Value = pane.FrameElementRight;

            vertexColorBox1.TopLeftColor = pane.Content.ColorTopLeft.Color;
            vertexColorBox1.TopRightColor = pane.Content.ColorTopRight.Color;
            vertexColorBox1.BottomLeftColor = pane.Content.ColorBottomLeft.Color;
            vertexColorBox1.BottomRightColor = pane.Content.ColorBottomRight.Color;
            vertexColorBox1.Refresh();

            UpdateKeyState();

            if (frame == null)
                texRotateCB.ResetBind();
            else
            {
                texRotateCB.Bind(typeof(WindowFrameTexFlip), frame, "TextureFlip");
                texRotateCB.SelectedItem = frame.TextureFlip;
            }

            chkMaterialForAll.Bind(pane, "UseOneMaterialForAll");
            chkUseVtxColorsOnFrames.Bind(pane, "UseVertexColorForAll");
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

        private void OnColorChanged(object sender, EventArgs e)
        {
            if (!Loaded) return;

            ActivePane.Content.ColorTopLeft.Color = vertexColorBox1.TopLeftColor;
            ActivePane.Content.ColorTopRight.Color = vertexColorBox1.TopRightColor;
            ActivePane.Content.ColorBottomLeft.Color = vertexColorBox1.BottomLeftColor;
            ActivePane.Content.ColorBottomRight.Color = vertexColorBox1.BottomRightColor;

            ApplyVertexKeys((BasePane)ActivePane);
            UpdateKeyState();

            parentEditor.PropertyChanged?.Invoke(sender, e);
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

        private void frameUD_ValueChanged(object sender, EventArgs e)
        {
            if (!Loaded) return;

            ActivePane.FrameElementTop = (ushort)frameUpUD.Value;
            ActivePane.FrameElementRight = (ushort)frameRightUD.Value;
            ActivePane.FrameElementLeft = (ushort)frameLeftUD.Value;
            ActivePane.FrameElementBottm = (ushort)frameDownUD.Value;

            parentEditor.PropertyChanged?.Invoke(sender, e);
        }

        private void texRotateCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!Loaded || activeFrame == null) return;

            activeFrame.TextureFlip = (WindowFrameTexFlip)texRotateCB.SelectedItem;
            parentEditor.PropertyChanged?.Invoke(sender, e);
        }
    }
}
