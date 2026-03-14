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

namespace LayoutBXLYT.CTR
{
    public partial class PaneMatCTRColorEditor : EditorPanelBase
    {
        private PaneEditor ParentEditor;
        private Material ActiveMaterial;

        public PaneMatCTRColorEditor()
        {
            InitializeComponent();

            whiteColorPB.DisplayAlphaSolid = true;
            blackColorBP.DisplayAlphaSolid = true;
        }

        public void LoadMaterial(Material material, PaneEditor paneEditor)
        {
            ActiveMaterial = material;
            ParentEditor = paneEditor;

            whiteColorPB.Color = material.WhiteColor.Color;
            blackColorBP.Color = material.BlackColor.Color;
            tevColor1PB.Color = material.TevConstantColors[0].Color;
            tevColor2PB.Color = material.TevConstantColors[1].Color;
            tevColor3PB.Color = material.TevConstantColors[2].Color;
            tevColor4PB.Color = material.TevConstantColors[3].Color;

            UpdateKeyState();

            chkAlphaInterpolation.Bind(material, "AlphaInterpolation");
        }

        private void UpdateKeyState()
        {
            if (ParentEditor == null || ActiveMaterial == null || !ParentEditor.IsAnimationEditMode)
            {
                foreach (var box in new ColorAlphaBox[] { whiteColorPB, blackColorBP, tevColor1PB, tevColor2PB, tevColor3PB, tevColor4PB })
                {
                    box.ShowBorder = false;
                    box.Refresh();
                }
                return;
            }

            SetBoxKeyed(whiteColorPB, (byte)LMCTarget.WhiteColorRed, (byte)LMCTarget.WhiteColorGreen, (byte)LMCTarget.WhiteColorBlue, (byte)LMCTarget.WhiteColorAlpha);
            SetBoxKeyed(blackColorBP, (byte)LMCTarget.BlackColorRed, (byte)LMCTarget.BlackColorGreen, (byte)LMCTarget.BlackColorBlue, (byte)LMCTarget.BlackColorAlpha);
            SetBoxKeyed(tevColor1PB, (byte)LMCTarget.TevKonstantColor0Red, (byte)LMCTarget.TevKonstantColor0Green, (byte)LMCTarget.TevKonstantColor0Blue, (byte)LMCTarget.TevKonstantColor0Alpha);
            SetBoxKeyed(tevColor2PB, (byte)LMCTarget.TevKonstantColor1Red, (byte)LMCTarget.TevKonstantColor1Green, (byte)LMCTarget.TevKonstantColor1Blue, (byte)LMCTarget.TevKonstantColor1Alpha);
            SetBoxKeyed(tevColor3PB, (byte)LMCTarget.TevKonstantColor2Red, (byte)LMCTarget.TevKonstantColor2Green, (byte)LMCTarget.TevKonstantColor2Blue, (byte)LMCTarget.TevKonstantColor2Alpha);
            tevColor4PB.ShowBorder = false;
            tevColor4PB.Refresh();
        }

        private void SetBoxKeyed(ColorAlphaBox box, byte r, byte g, byte b, byte a)
        {
            bool animated = ParentEditor.IsMaterialColorAnimated(ActiveMaterial, r, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, g, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, b, false)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, a, false);
            box.BorderColor = Color.Red;
            box.ShowBorder = animated;
            box.Refresh();
        }

        private void ApplyMaterialKeys(Material mat, byte r, byte g, byte b, byte a, Color color)
        {
            ParentEditor.ApplyMaterialColorKey(mat, r, color.R, false);
            ParentEditor.ApplyMaterialColorKey(mat, g, color.G, false);
            ParentEditor.ApplyMaterialColorKey(mat, b, color.B, false);
            ParentEditor.ApplyMaterialColorKey(mat, a, color.A, false);
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
        private void ColorPB_Click(object sender, EventArgs e)
        {
            if (dialogActive)
            {
                colorDlg.Focus();
                return;
            }
            dialogActive = true;
            if (sender is ColorAlphaBox)
                colorDlg = new STColorDialog(((ColorAlphaBox)sender).Color);

            colorDlg.FormClosed += delegate
            {
                dialogActive = false;
            };
            colorDlg.ColorChanged += delegate
            {
                var box = (ColorAlphaBox)sender;
                Color oldActive = box.Color;
                Color newActive = colorDlg.NewColor;
                bool relative = Runtime.LayoutEditor.MulticlickBehavior == Runtime.LayoutEditor.MulticlickBehaviorMode.Relative;

                box.Color = newActive;

                ApplyColors(ActiveMaterial, sender, newActive);

                if (sender == whiteColorPB) ApplyMaterialKeys(ActiveMaterial, (byte)LMCTarget.WhiteColorRed, (byte)LMCTarget.WhiteColorGreen, (byte)LMCTarget.WhiteColorBlue, (byte)LMCTarget.WhiteColorAlpha, newActive);
                else if (sender == blackColorBP) ApplyMaterialKeys(ActiveMaterial, (byte)LMCTarget.BlackColorRed, (byte)LMCTarget.BlackColorGreen, (byte)LMCTarget.BlackColorBlue, (byte)LMCTarget.BlackColorAlpha, newActive);
                else if (sender == tevColor1PB) ApplyMaterialKeys(ActiveMaterial, (byte)LMCTarget.TevKonstantColor0Red, (byte)LMCTarget.TevKonstantColor0Green, (byte)LMCTarget.TevKonstantColor0Blue, (byte)LMCTarget.TevKonstantColor0Alpha, newActive);
                else if (sender == tevColor2PB) ApplyMaterialKeys(ActiveMaterial, (byte)LMCTarget.TevKonstantColor1Red, (byte)LMCTarget.TevKonstantColor1Green, (byte)LMCTarget.TevKonstantColor1Blue, (byte)LMCTarget.TevKonstantColor1Alpha, newActive);
                else if (sender == tevColor3PB) ApplyMaterialKeys(ActiveMaterial, (byte)LMCTarget.TevKonstantColor2Red, (byte)LMCTarget.TevKonstantColor2Green, (byte)LMCTarget.TevKonstantColor2Blue, (byte)LMCTarget.TevKonstantColor2Alpha, newActive);

                //Apply to all selected panes
                foreach (BasePane pane in ParentEditor.SelectedPanes)
                {
                    var mat = pane.TryGetActiveMaterial() as CTR.Material;
                    if (mat != null && mat != ActiveMaterial)
                    {
                        Color source = GetColor(mat, sender);
                        Color targetColor = relative ? ApplyRelativeColorDelta(source, oldActive, newActive) : newActive;
                        ApplyColors(mat, sender, targetColor);

                        if (sender == whiteColorPB) ApplyMaterialKeys(mat, (byte)LMCTarget.WhiteColorRed, (byte)LMCTarget.WhiteColorGreen, (byte)LMCTarget.WhiteColorBlue, (byte)LMCTarget.WhiteColorAlpha, targetColor);
                        else if (sender == blackColorBP) ApplyMaterialKeys(mat, (byte)LMCTarget.BlackColorRed, (byte)LMCTarget.BlackColorGreen, (byte)LMCTarget.BlackColorBlue, (byte)LMCTarget.BlackColorAlpha, targetColor);
                        else if (sender == tevColor1PB) ApplyMaterialKeys(mat, (byte)LMCTarget.TevKonstantColor0Red, (byte)LMCTarget.TevKonstantColor0Green, (byte)LMCTarget.TevKonstantColor0Blue, (byte)LMCTarget.TevKonstantColor0Alpha, targetColor);
                        else if (sender == tevColor2PB) ApplyMaterialKeys(mat, (byte)LMCTarget.TevKonstantColor1Red, (byte)LMCTarget.TevKonstantColor1Green, (byte)LMCTarget.TevKonstantColor1Blue, (byte)LMCTarget.TevKonstantColor1Alpha, targetColor);
                        else if (sender == tevColor3PB) ApplyMaterialKeys(mat, (byte)LMCTarget.TevKonstantColor2Red, (byte)LMCTarget.TevKonstantColor2Green, (byte)LMCTarget.TevKonstantColor2Blue, (byte)LMCTarget.TevKonstantColor2Alpha, targetColor);
                    }
                }

                UpdateKeyState();

                ParentEditor.PropertyChanged?.Invoke(sender, e);
            };
            colorDlg.Show();
        }

        private Color GetColor(Material mat, object sender)
        {
            if (sender == whiteColorPB)
                return mat.WhiteColor.Color;
            if (sender == blackColorBP)
                return mat.BlackColor.Color;
            if (sender == tevColor1PB)
                return mat.TevConstantColors[0].Color;
            if (sender == tevColor2PB)
                return mat.TevConstantColors[1].Color;
            if (sender == tevColor3PB)
                return mat.TevConstantColors[2].Color;
            if (sender == tevColor4PB)
                return mat.TevConstantColors[3].Color;

            return Color.White;
        }

        private void ApplyColors(Material mat, object sender, Color color)
        {
            if (sender == whiteColorPB)
                mat.WhiteColor.Color = color;
            else if (sender == blackColorBP)
                mat.BlackColor.Color = color;
            else if (sender == tevColor1PB)
                mat.TevConstantColors[0].Color = color;
            else if (sender == tevColor2PB)
                mat.TevConstantColors[1].Color = color;
            else if (sender == tevColor3PB)
                mat.TevConstantColors[2].Color = color;
            else if (sender == tevColor4PB)
                mat.TevConstantColors[3].Color = color;
        }
    }
}
