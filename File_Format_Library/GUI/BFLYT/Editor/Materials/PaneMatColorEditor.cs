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
using FirstPlugin;

namespace LayoutBXLYT
{
    public partial class PaneMatColorEditor : EditorPanelBase
    {
        private PaneEditor ParentEditor;
        private BxlytMaterial ActiveMaterial;

        public PaneMatColorEditor()
        {
            InitializeComponent();

            whiteColorPB.DisplayAlphaSolid = true;
            blackColorBP.DisplayAlphaSolid = true;
        }

        public void LoadMaterial(BxlytMaterial material, PaneEditor paneEditor)
        {
            ActiveMaterial = material;
            ParentEditor = paneEditor;

            whiteColorPB.Color = material.WhiteColor.Color;
            blackColorBP.Color = material.BlackColor.Color;

            UpdateKeyState();

            chkAlphaInterpolation.Bind(material, "AlphaInterpolation");
        }

        private void UpdateKeyState()
        {
            if (ParentEditor == null || ActiveMaterial == null || !ParentEditor.IsAnimationEditMode)
            {
                whiteColorPB.ShowBorder = false;
                blackColorBP.ShowBorder = false;
                whiteColorPB.Refresh();
                blackColorBP.Refresh();
                return;
            }

            bool whiteAnimated = ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.WhiteColorRed, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.WhiteColorGreen, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.WhiteColorBlue, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.WhiteColorAlpha, false);

            bool blackAnimated = ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.BlackColorRed, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.BlackColorGreen, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.BlackColorBlue, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, (byte)LMCTarget.BlackColorAlpha, false);

            whiteColorPB.BorderColor = Color.Red;
            blackColorBP.BorderColor = Color.Red;
            whiteColorPB.ShowBorder = whiteAnimated;
            blackColorBP.ShowBorder = blackAnimated;
            whiteColorPB.Refresh();
            blackColorBP.Refresh();
        }

        private void ApplyMaterialKeys(BxlytMaterial mat, bool isWhite, Color color)
        {
            if (isWhite)
            {
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.WhiteColorRed, color.R, false);
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.WhiteColorGreen, color.G, false);
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.WhiteColorBlue, color.B, false);
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.WhiteColorAlpha, color.A, false);
            }
            else
            {
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.BlackColorRed, color.R, false);
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.BlackColorGreen, color.G, false);
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.BlackColorBlue, color.B, false);
                ParentEditor.ApplyMaterialColorKey(mat, (byte)LMCTarget.BlackColorAlpha, color.A, false);
            }
        }

        private static Color ApplyRelativeColorDelta(Color source, Color oldActive, Color newActive)
        {
            int r = Math.Max(0, Math.Min(255, source.R + (newActive.R - oldActive.R)));
            int g = Math.Max(0, Math.Min(255, source.G + (newActive.G - oldActive.G)));
            int b = Math.Max(0, Math.Min(255, source.B + (newActive.B - oldActive.B)));
            int a = Math.Max(0, Math.Min(255, source.A + (newActive.A - oldActive.A)));
            return Color.FromArgb(a, r, g, b);
        }

        private STColorDialog colorDlg;
        private bool dialogActive = false;
        private void whiteColorPB_Click(object sender, EventArgs e)
        {
            if (dialogActive)
            {
                colorDlg.Focus();
                return;
            }

            dialogActive = true;
            colorDlg = new STColorDialog(whiteColorPB.Color);
            colorDlg.FormClosed += delegate
            {
                dialogActive = false;
            };
            colorDlg.ColorChanged += delegate
            {
                Color oldActive = ActiveMaterial.WhiteColor.Color;
                Color newActive = colorDlg.NewColor;

                whiteColorPB.Color = newActive;
                ActiveMaterial.WhiteColor.Color = newActive;
                ApplyMaterialKeys(ActiveMaterial, true, newActive);

                bool relative = Runtime.LayoutEditor.MulticlickBehavior == Runtime.LayoutEditor.MulticlickBehaviorMode.Relative;

                //Apply to all selected panes
                foreach (BasePane pane in ParentEditor.SelectedPanes)
                {
                    var mat = pane.TryGetActiveMaterial();
                    if (mat != null && mat != ActiveMaterial)
                    {
                        Color targetColor = relative
                            ? ApplyRelativeColorDelta(mat.WhiteColor.Color, oldActive, newActive)
                            : newActive;
                        mat.WhiteColor.Color = targetColor;
                        ApplyMaterialKeys(mat, true, targetColor);
                    }
                }

                UpdateKeyState();

                ParentEditor.PropertyChanged?.Invoke(sender, e);
            };
            colorDlg.Show();
        }

        private void blackColorBP_Click(object sender, EventArgs e)
        {
            if (dialogActive)
            {
                colorDlg.Focus();
                return;
            }

            dialogActive = true;
            colorDlg = new STColorDialog(blackColorBP.Color);
            colorDlg.FormClosed += delegate
            {
                dialogActive = false;
            };
            colorDlg.ColorChanged += delegate
            {
                Color oldActive = ActiveMaterial.BlackColor.Color;
                Color newActive = colorDlg.NewColor;

                blackColorBP.Color = newActive;
                ActiveMaterial.BlackColor.Color = newActive;
                ApplyMaterialKeys(ActiveMaterial, false, newActive);

                bool relative = Runtime.LayoutEditor.MulticlickBehavior == Runtime.LayoutEditor.MulticlickBehaviorMode.Relative;

                //Apply to all selected panes
                foreach (BasePane pane in ParentEditor.SelectedPanes)
                {
                    var mat = pane.TryGetActiveMaterial();
                    if (mat != null && mat != ActiveMaterial)
                    {
                        Color targetColor = relative
                            ? ApplyRelativeColorDelta(mat.BlackColor.Color, oldActive, newActive)
                            : newActive;
                        mat.BlackColor.Color = targetColor;
                        ApplyMaterialKeys(mat, false, targetColor);
                    }
                }

                UpdateKeyState();

                ParentEditor.PropertyChanged?.Invoke(sender, e);
            };
            colorDlg.Show();
        }
    }
}
