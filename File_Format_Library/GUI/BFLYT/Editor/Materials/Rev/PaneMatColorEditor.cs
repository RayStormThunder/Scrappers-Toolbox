using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.Forms;
using LayoutBXLYT.Revolution;
using System.Windows.Forms;
using System.Globalization;

namespace LayoutBXLYT
{
    public partial class PaneMatRevColorEditor : EditorPanelBase
    {
        private class FrameColorEditInfo
        {
            public BxlanHeader Animation;
            public string PaneName;
            public float Frame;
            public BxlanPaiTag Tag;
            public BxlanPaiTagEntry REntry;
            public BxlanPaiTagEntry GEntry;
            public BxlanPaiTagEntry BEntry;
            public string R;
            public string G;
            public string B;
            public string A;
            public bool IsRevMaterial;
            public Color LastAppliedColor;
        }

        private PaneEditor ParentEditor;
        private Material ActiveMaterial;

        public PaneMatRevColorEditor()
        {
            InitializeComponent();

            whiteColorPB.DisplayAlphaSolid = true;
            blackColorBP.DisplayAlphaSolid = true;

            animationColorsPanel.Visible = true;
        }

        public void LoadMaterial(Material material, PaneEditor paneEditor)
        {
            ActiveMaterial = material;
            ParentEditor = paneEditor;

            whiteColorPB.Color = material.WhiteColor.Color;
            blackColorBP.Color = material.BlackColor.Color;
            materialColorPB.Color = material.MatColor.Color;
            colorReg3PB.Color = material.ColorRegister3.Color;
            tevColor1PB.Color = material.TevColor1.Color;
            tevColor2PB.Color = material.TevColor2.Color;
            tevColor3PB.Color = material.TevColor3.Color;
            tevColor4PB.Color = material.TevColor4.Color;

            UpdateKeyState();
            BuildAnimationColorsList();

            chkAlphaInterpolation.Bind(material, "AlphaInterpolation");
        }

        private void BuildAnimationColorsList()
        {
            animationColorsPanel.Controls.Clear();

            if (ParentEditor == null || ActiveMaterial == null)
                return;

            var animations = ParentEditor.GetAnimations();
            if (animations == null || animations.Count == 0)
                return;

            string materialName = ActiveMaterial?.Name;
            if (string.IsNullOrEmpty(materialName))
                materialName = ParentEditor.SelectedPanes?.FirstOrDefault(x => x != null)?.TryGetActiveMaterial()?.Name;

            int y = 6;

            foreach (var anim in animations)
            {
                string animName = !string.IsNullOrEmpty(anim.AnimationTag?.Name)
                    ? anim.AnimationTag.Name
                    : System.IO.Path.GetFileNameWithoutExtension(anim.FileName);

                foreach (var lmcTag in FindMaterialColorTagsForAnimation(anim, materialName))
                {
                    var keyFrames = lmcTag.Entries
                        .SelectMany(x => x.KeyFrames ?? new List<KeyFrame>())
                        .Select(x => x.Frame)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();

                    foreach (var frame in keyFrames)
                    {
                        STLabel header = new STLabel();
                        header.AutoSize = true;
                        header.Font = new Font(header.Font, FontStyle.Bold);
                        header.Location = new Point(6, y);
                        header.Text = $"{animName} - frame {frame.ToString("0.###", CultureInfo.InvariantCulture)}";
                        animationColorsPanel.Controls.Add(header);
                        y += 18;

                        bool anyFrameRows = false;
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "WhiteColorRed", "WhiteColorGreen", "WhiteColorBlue", "WhiteColorAlpha", 255, 255, 255, 255, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "BlackColorRed", "BlackColorGreen", "BlackColorBlue", "BlackColorAlpha", 0, 0, 0, 0, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "ColorReg3Red", "ColorReg3Green", "ColorReg3Blue", "ColorReg3Alpha", 255, 255, 255, 255, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "MatColorRed", "MatColorGreen", "MatColorBlue", "MatColorAlpha", 255, 255, 255, 255, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "TevColor1Red", "TevColor1Green", "TevColor1Blue", "TevColor1Alpha", 255, 255, 255, 255, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "TevColor2Red", "TevColor2Green", "TevColor2Blue", "TevColor2Alpha", 255, 255, 255, 255, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "TevColor3Red", "TevColor3Green", "TevColor3Blue", "TevColor3Alpha", 255, 255, 255, 255, ref y);
                        anyFrameRows |= AddColorRow(anim, materialName, lmcTag, frame, "TevColor4Red", "TevColor4Green", "TevColor4Blue", "TevColor4Alpha", 255, 255, 255, 255, ref y);

                        y += 8;
                    }
                }
            }
        }

        private IEnumerable<BxlanPaiTag> FindMaterialColorTagsForAnimation(BxlanHeader anim, string materialName)
        {
            var entries = anim.AnimationInfo?.Entries;
            if (entries == null)
                yield break;

            if (string.IsNullOrEmpty(materialName))
                yield break;

            var materialEntries = entries.Where(x =>
                x.Target == AnimationTarget.Material && string.Equals(x.Name, materialName, StringComparison.Ordinal)).ToList();

            // Fallback for legacy files where target typing is inconsistent.
            if (materialEntries.Count == 0)
                materialEntries = entries.Where(x => string.Equals(x.Name, materialName, StringComparison.Ordinal)).ToList();

            foreach (var entry in materialEntries)
            {
                if (entry.Tags == null)
                    continue;

                foreach (var tag in entry.Tags.Where(x => x.Tag != null && x.Tag.EndsWith("LMC")))
                    yield return tag;
            }
        }

        private bool AddColorRow(BxlanHeader anim, string paneName, BxlanPaiTag lmcTag, float frame, string r, string g, string b, string a,
            int defaultR, int defaultG, int defaultB, int defaultA, ref int y)
        {
            var rEntry = FindChannelEntry(lmcTag, r);
            var gEntry = FindChannelEntry(lmcTag, g);
            var bEntry = FindChannelEntry(lmcTag, b);
            var aEntry = FindChannelEntry(lmcTag, a);

            if (!HasKeyAtFrame(rEntry, frame) && !HasKeyAtFrame(gEntry, frame) &&
                !HasKeyAtFrame(bEntry, frame) && !HasKeyAtFrame(aEntry, frame))
            return false;

            Color color = Color.FromArgb(
                ClampByte(EvaluateValueAtFrame(aEntry, frame, defaultA)),
                ClampByte(EvaluateValueAtFrame(rEntry, frame, defaultR)),
                ClampByte(EvaluateValueAtFrame(gEntry, frame, defaultG)),
                ClampByte(EvaluateValueAtFrame(bEntry, frame, defaultB)));

            ColorAlphaBox box = new ColorAlphaBox();
            box.DisplayAlphaSolid = true;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Location = new Point(10, y);
            box.Size = new Size(90, 45);
            box.Color = color;
            box.Tag = new FrameColorEditInfo()
            {
                Animation = anim,
                PaneName = paneName,
                Frame = frame,
                Tag = lmcTag,
                REntry = rEntry,
                GEntry = gEntry,
                BEntry = bEntry,
                R = r,
                G = g,
                B = b,
                A = a,
                IsRevMaterial = lmcTag.Tag != null && lmcTag.Tag.StartsWith("R") && lmcTag.Tag.EndsWith("LMC"),
                LastAppliedColor = color,
            };
            box.Click += new EventHandler(this.ColorPB_Click);
            animationColorsPanel.Controls.Add(box);
            y += 51;
            return true;
        }

        private byte? GetTargetFromName(BxlanPaiTag tag, string channelName)
        {
            if (tag != null && tag.Tag != null && tag.Tag.StartsWith("R") && tag.Tag.EndsWith("LMC"))
            {
                if (Enum.TryParse(channelName, out RevLMCTarget revTarget))
                    return (byte)revTarget;
            }
            else
            {
                if (Enum.TryParse(channelName, out LMCTarget target))
                    return (byte)target;
            }
            return null;
        }

        private Color EvaluateFrameColor(BxlanPaiTag tag, FrameColorEditInfo info, Color fallback)
        {
            var rEntry = FindChannelEntry(tag, info.R);
            var gEntry = FindChannelEntry(tag, info.G);
            var bEntry = FindChannelEntry(tag, info.B);
            var aEntry = FindChannelEntry(tag, info.A);

            return Color.FromArgb(
                ClampByte(EvaluateValueAtFrame(aEntry, info.Frame, fallback.A)),
                ClampByte(EvaluateValueAtFrame(rEntry, info.Frame, fallback.R)),
                ClampByte(EvaluateValueAtFrame(gEntry, info.Frame, fallback.G)),
                ClampByte(EvaluateValueAtFrame(bEntry, info.Frame, fallback.B)));
        }

        private void ApplyFrameColorKeys(FrameColorEditInfo info, Color color, Color? oldActive)
        {
            if (info == null || info.Animation == null || string.IsNullOrEmpty(info.PaneName))
                return;

            bool relative = Runtime.LayoutEditor.MulticlickBehavior == Runtime.LayoutEditor.MulticlickBehaviorMode.Relative;

            // Always write using the exact entries backing the clicked frame swatch.
            ApplyFrameColorKeysDirect(info, color);

            foreach (BasePane pane in ParentEditor.SelectedPanes)
            {
                if (pane == null || string.IsNullOrEmpty(pane.Name))
                    continue;

                var targetMaterialName = pane.TryGetActiveMaterial()?.Name;
                if (string.IsNullOrEmpty(targetMaterialName))
                    continue;

                if (string.Equals(targetMaterialName, info.PaneName, StringComparison.Ordinal))
                    continue;

                var targetTag = FindMatchingMaterialTagForPane(info.Animation, targetMaterialName, info.Tag, info.R, info.G, info.B);
                if (targetTag != null)
                {
                    Color targetColor = color;
                    if (relative && oldActive.HasValue)
                    {
                        Color source = EvaluateFrameColor(targetTag, info, color);
                        targetColor = ApplyRelativeHueShift(source, oldActive.Value, color);
                    }

                    ApplyFrameColorKeysForPane(info, targetColor, targetMaterialName, targetTag, false);
                }
            }

            info.LastAppliedColor = color;
        }

        private void ApplyFrameColorKeysDirect(FrameColorEditInfo info, Color color)
        {
            if (info?.Tag == null)
                return;

            bool isRevTag = IsRevTag(info.Tag);

            var rEntry = info.REntry;
            var gEntry = info.GEntry;
            var bEntry = info.BEntry;

            // If an entry was not present when loaded, create one on demand using the stored channel name.
            if (rEntry == null)
            {
                var rTarget = GetTargetFromName(info.Tag, info.R);
                if (rTarget.HasValue)
                    rEntry = GetOrCreateTagEntry(info.Tag, rTarget.Value, isRevTag);
            }
            if (gEntry == null)
            {
                var gTarget = GetTargetFromName(info.Tag, info.G);
                if (gTarget.HasValue)
                    gEntry = GetOrCreateTagEntry(info.Tag, gTarget.Value, isRevTag);
            }
            if (bEntry == null)
            {
                var bTarget = GetTargetFromName(info.Tag, info.B);
                if (bTarget.HasValue)
                    bEntry = GetOrCreateTagEntry(info.Tag, bTarget.Value, isRevTag);
            }

            if (rEntry != null)
                SetFrameValue(rEntry, info.Frame, color.R);
            if (gEntry != null)
                SetFrameValue(gEntry, info.Frame, color.G);
            if (bEntry != null)
                SetFrameValue(bEntry, info.Frame, color.B);
        }

        private void ApplyFrameColorKeysForPane(FrameColorEditInfo info, Color color, string paneName, BxlanPaiTag targetTag, bool allowCreate)
        {
            if (targetTag == null)
                return;

            bool isRevTag = IsRevTag(targetTag);
            var r = GetTargetFromName(targetTag, info.R);
            var g = GetTargetFromName(targetTag, info.G);
            var b = GetTargetFromName(targetTag, info.B);

            var rEntry = FindChannelEntry(targetTag, info.R);
            var gEntry = FindChannelEntry(targetTag, info.G);
            var bEntry = FindChannelEntry(targetTag, info.B);

            bool hasR = allowCreate || HasKeyAtFrame(rEntry, info.Frame);
            bool hasG = allowCreate || HasKeyAtFrame(gEntry, info.Frame);
            bool hasB = allowCreate || HasKeyAtFrame(bEntry, info.Frame);

            if (r.HasValue && hasR)
                ApplyTagKeyAtFrame(targetTag, r.Value, color.R, isRevTag, info.Frame);
            if (g.HasValue && hasG)
                ApplyTagKeyAtFrame(targetTag, g.Value, color.G, isRevTag, info.Frame);
            if (b.HasValue && hasB)
                ApplyTagKeyAtFrame(targetTag, b.Value, color.B, isRevTag, info.Frame);
        }

        private void ApplyTagKeyAtFrame(BxlanPaiTag tag, byte target, float value, bool isRevTag, float frame)
        {
            var entry = GetOrCreateTagEntry(tag, target, isRevTag);
            SetFrameValue(entry, frame, value);
        }

        private void SetFrameValue(BxlanPaiTagEntry entry, float frame, float value)
        {
            const float eps = 0.001f;
            var frameKeys = entry.KeyFrames.Where(x => Math.Abs(x.Frame - frame) <= eps).ToList();
            if (frameKeys.Count > 0)
            {
                // Some files contain duplicate keys on the same frame; update all so values persist.
                foreach (var key in frameKeys)
                    key.Value = value;
                return;
            }

            var newKey = new KeyFrame(frame) { Value = value };
            entry.KeyFrames.Add(newKey);
            entry.KeyFrames = entry.KeyFrames.OrderBy(x => x.Frame).ToList();
        }

        private BxlanPaiTagEntry GetOrCreateTagEntry(BxlanPaiTag tag, byte target, bool isRevTag)
        {
            var entry = tag.Entries.FirstOrDefault(x => x.AnimationTarget == target);
            if (entry != null)
                return entry;

            if (isRevTag)
                entry = new RevLMCTagEntry(target, (byte)CurveType.Step);
            else
                entry = new LMCTagEntry(target, (byte)CurveType.Step);

            tag.Entries.Add(entry);
            return entry;
        }

        private BxlanPaiTag FindMatchingMaterialTagForPane(BxlanHeader animation, string materialName, BxlanPaiTag sourceTag,
            string rName, string gName, string bName)
        {
            if (animation?.AnimationInfo?.Entries == null || string.IsNullOrEmpty(materialName))
                return null;

            bool sourceIsRev = IsRevTag(sourceTag);
            string sourceTagName = sourceTag?.Tag;

            var entries = animation.AnimationInfo.Entries
                .Where(x => x.Target == AnimationTarget.Material && string.Equals(x.Name, materialName, StringComparison.Ordinal))
                .ToList();

            if (entries.Count == 0)
                entries = animation.AnimationInfo.Entries.Where(x => string.Equals(x.Name, materialName, StringComparison.Ordinal)).ToList();

            foreach (var entry in entries)
            {
                if (entry.Tags == null)
                    continue;

                // First try to match by exact tag name (most accurate for split LMC/RLMC data).
                if (!string.IsNullOrEmpty(sourceTagName))
                {
                    var exact = entry.Tags.FirstOrDefault(x => string.Equals(x?.Tag, sourceTagName, StringComparison.Ordinal));
                    if (exact != null)
                        return exact;
                }

                // Fallback: same LMC family + contains the expected channels.
                foreach (var tag in entry.Tags)
                {
                    if (tag?.Tag == null || !tag.Tag.EndsWith("LMC"))
                        continue;

                    if (IsRevTag(tag) != sourceIsRev)
                        continue;

                    bool hasR = FindChannelEntry(tag, rName) != null;
                    bool hasG = FindChannelEntry(tag, gName) != null;
                    bool hasB = FindChannelEntry(tag, bName) != null;
                    if (hasR || hasG || hasB)
                        return tag;
                }
            }

            return null;
        }

        private bool IsRevTag(BxlanPaiTag tag)
        {
            return tag?.Tag != null && tag.Tag.StartsWith("R") && tag.Tag.EndsWith("LMC");
        }

        private BxlanPaiTagEntry FindChannelEntry(BxlanPaiTag lmcTag, string expectedName)
        {
            return lmcTag.Entries.FirstOrDefault(x =>
                string.Equals(GetNormalizedChannelName(lmcTag, x), expectedName, StringComparison.Ordinal));
        }

        private string GetNormalizedChannelName(BxlanPaiTag lmcTag, BxlanPaiTagEntry entry)
        {
            if (entry == null)
                return string.Empty;

            // RLMC channels are often stored as LMCTagEntry values. Normalize by target index.
            if (!string.IsNullOrEmpty(lmcTag.Tag) && lmcTag.Tag.StartsWith("R") && lmcTag.Tag.EndsWith("LMC"))
                return ((RevLMCTarget)entry.AnimationTarget).ToString();

            return entry.TargetName;
        }

        private bool HasKeyAtFrame(BxlanPaiTagEntry entry, float frame)
        {
            if (entry == null)
                return false;

            const float eps = 0.001f;
            return entry.KeyFrames.Any(x => Math.Abs(x.Frame - frame) <= eps);
        }

        private float EvaluateValueAtFrame(BxlanPaiTagEntry entry, float frame, float defaultValue)
        {
            if (entry == null || entry.KeyFrames.Count == 0)
                return defaultValue;

            var keys = entry.KeyFrames.OrderBy(x => x.Frame).ToList();
            var left = keys.Where(x => x.Frame <= frame).LastOrDefault();
            var right = keys.Where(x => x.Frame >= frame).FirstOrDefault();

            if (left == null)
                return keys.First().Value;
            if (right == null)
                return keys.Last().Value;
            if (Math.Abs(left.Frame - right.Frame) <= 0.0001f)
                return left.Value;

            float t = (frame - left.Frame) / (right.Frame - left.Frame);
            if (entry.CurveType == CurveType.Hermite)
            {
                float diff = frame - left.Frame;
                return Toolbox.Library.Animations.InterpolationHelper.Herp(left.Value, right.Value, left.Slope, right.Slope, diff, t);
            }
            return left.Value;
        }

        private int ClampByte(float value)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private void UpdateKeyState()
        {
            if (ParentEditor == null || ActiveMaterial == null || !ParentEditor.IsAnimationEditMode)
            {
                foreach (var box in new ColorAlphaBox[] { whiteColorPB, blackColorBP, materialColorPB, colorReg3PB, tevColor1PB, tevColor2PB, tevColor3PB, tevColor4PB })
                {
                    box.ShowBorder = false;
                    box.Refresh();
                }
                return;
            }

            SetBoxKeyed(whiteColorPB, (byte)RevLMCTarget.WhiteColorRed, (byte)RevLMCTarget.WhiteColorGreen, (byte)RevLMCTarget.WhiteColorBlue, (byte)RevLMCTarget.WhiteColorAlpha);
            SetBoxKeyed(blackColorBP, (byte)RevLMCTarget.BlackColorRed, (byte)RevLMCTarget.BlackColorGreen, (byte)RevLMCTarget.BlackColorBlue, (byte)RevLMCTarget.BlackColorAlpha);
            SetBoxKeyed(materialColorPB, (byte)RevLMCTarget.MatColorRed, (byte)RevLMCTarget.MatColorGreen, (byte)RevLMCTarget.MatColorBlue, (byte)RevLMCTarget.MatColorAlpha);
            SetBoxKeyed(colorReg3PB, (byte)RevLMCTarget.ColorReg3Red, (byte)RevLMCTarget.ColorReg3Green, (byte)RevLMCTarget.ColorReg3Blue, (byte)RevLMCTarget.ColorReg3Alpha);
            SetBoxKeyed(tevColor1PB, (byte)RevLMCTarget.TevColor1Red, (byte)RevLMCTarget.TevColor1Green, (byte)RevLMCTarget.TevColor1Blue, (byte)RevLMCTarget.TevColor1Alpha);
            SetBoxKeyed(tevColor2PB, (byte)RevLMCTarget.TevColor2Red, (byte)RevLMCTarget.TevColor2Green, (byte)RevLMCTarget.TevColor2Blue, (byte)RevLMCTarget.TevColor2Alpha);
            SetBoxKeyed(tevColor3PB, (byte)RevLMCTarget.TevColor3Red, (byte)RevLMCTarget.TevColor3Green, (byte)RevLMCTarget.TevColor3Blue, (byte)RevLMCTarget.TevColor3Alpha);
            SetBoxKeyed(tevColor4PB, (byte)RevLMCTarget.TevColor4Red, (byte)RevLMCTarget.TevColor4Green, (byte)RevLMCTarget.TevColor4Blue, (byte)RevLMCTarget.TevColor4Alpha);
        }

        private void SetBoxKeyed(ColorAlphaBox box, byte r, byte g, byte b, byte a)
        {
            bool animated = ParentEditor.IsMaterialColorAnimated(ActiveMaterial, r, true)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, g, true)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, b, true)
                || ParentEditor.IsMaterialColorAnimated(ActiveMaterial, a, true);
            box.BorderColor = Color.Red;
            box.ShowBorder = animated;
            box.Refresh();
        }

        private void ApplyMaterialKeys(Material mat, byte r, byte g, byte b, byte a, Color color)
        {
            ParentEditor.ApplyMaterialColorKey(mat, r, color.R, true);
            ParentEditor.ApplyMaterialColorKey(mat, g, color.G, true);
            ParentEditor.ApplyMaterialColorKey(mat, b, color.B, true);
            ParentEditor.ApplyMaterialColorKey(mat, a, color.A, true);
        }

        private static Color ApplyRelativeHueShift(Color source, Color oldActive, Color newActive)
        {
            float hueDelta = newActive.GetHue() - oldActive.GetHue();
            float hue = source.GetHue() + hueDelta;
            while (hue < 0f) hue += 360f;
            while (hue >= 360f) hue -= 360f;

            return FromHsl(hue, source.GetSaturation(), source.GetBrightness(), source.A);
        }

        private static Color FromHsl(float hue, float saturation, float lightness, int alpha)
        {
            if (saturation <= 0f)
            {
                int gray = (int)Math.Round(lightness * 255f);
                gray = Math.Max(0, Math.Min(255, gray));
                return Color.FromArgb(alpha, gray, gray, gray);
            }

            float c = (1f - Math.Abs(2f * lightness - 1f)) * saturation;
            float x = c * (1f - Math.Abs((hue / 60f) % 2f - 1f));
            float m = lightness - c / 2f;

            float rPrime;
            float gPrime;
            float bPrime;
            if (hue < 60f)
            {
                rPrime = c; gPrime = x; bPrime = 0f;
            }
            else if (hue < 120f)
            {
                rPrime = x; gPrime = c; bPrime = 0f;
            }
            else if (hue < 180f)
            {
                rPrime = 0f; gPrime = c; bPrime = x;
            }
            else if (hue < 240f)
            {
                rPrime = 0f; gPrime = x; bPrime = c;
            }
            else if (hue < 300f)
            {
                rPrime = x; gPrime = 0f; bPrime = c;
            }
            else
            {
                rPrime = c; gPrime = 0f; bPrime = x;
            }

            float r = rPrime + m;
            float g = gPrime + m;
            float b = bPrime + m;

            return Color.FromArgb(
                alpha,
                Math.Max(0, Math.Min(255, (int)Math.Round(r * 255f))),
                Math.Max(0, Math.Min(255, (int)Math.Round(g * 255f))),
                Math.Max(0, Math.Min(255, (int)Math.Round(b * 255f))));
        }

        private STColorDialog colorDlg;
        private bool dialogActive = false;
        private void ColorPB_Click(object sender, EventArgs e)
        {
            if (!(sender is ColorAlphaBox))
                return;

            if (dialogActive)
            {
                // Rebind to the newly clicked swatch instead of keeping the old target.
                colorDlg?.Close();
                dialogActive = false;
            }

            dialogActive = true;
            colorDlg = new STColorDialog(((ColorAlphaBox)sender).Color);
            bool frameColorEdit = ((ColorAlphaBox)sender).Tag is FrameColorEditInfo;
            var frameInfo = frameColorEdit ? ((ColorAlphaBox)sender).Tag as FrameColorEditInfo : null;

            colorDlg.FormClosed += delegate
            {
                dialogActive = false;

                if (frameColorEdit)
                {
                    // Commit final value on close to guarantee persistence of the latest picker state.
                    ApplyFrameColorKeys(frameInfo, ((ColorAlphaBox)sender).Color, frameInfo?.LastAppliedColor);
                    frameInfo?.Animation?.ToGenericAnimation(ActiveMaterial?.ParentLayout);
                    BuildAnimationColorsList();
                }
            };
            colorDlg.ColorChanged += delegate
            {
                var box = (ColorAlphaBox)sender;
                Color oldActive = box.Color;
                Color newActive = colorDlg.NewColor;
                bool relative = Runtime.LayoutEditor.MulticlickBehavior == Runtime.LayoutEditor.MulticlickBehaviorMode.Relative;

                box.Color = newActive;

                if (sender is ColorAlphaBox && ((ColorAlphaBox)sender).Tag is FrameColorEditInfo)
                {
                    ApplyFrameColorKeys((FrameColorEditInfo)((ColorAlphaBox)sender).Tag, newActive, oldActive);
                    ParentEditor.PropertyChanged?.Invoke(sender, e);
                    return;
                }

                ApplyColors(ActiveMaterial, sender, newActive);

                if (sender == whiteColorPB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.WhiteColorRed, (byte)RevLMCTarget.WhiteColorGreen, (byte)RevLMCTarget.WhiteColorBlue, (byte)RevLMCTarget.WhiteColorAlpha, newActive);
                else if (sender == blackColorBP) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.BlackColorRed, (byte)RevLMCTarget.BlackColorGreen, (byte)RevLMCTarget.BlackColorBlue, (byte)RevLMCTarget.BlackColorAlpha, newActive);
                else if (sender == materialColorPB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.MatColorRed, (byte)RevLMCTarget.MatColorGreen, (byte)RevLMCTarget.MatColorBlue, (byte)RevLMCTarget.MatColorAlpha, newActive);
                else if (sender == colorReg3PB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.ColorReg3Red, (byte)RevLMCTarget.ColorReg3Green, (byte)RevLMCTarget.ColorReg3Blue, (byte)RevLMCTarget.ColorReg3Alpha, newActive);
                else if (sender == tevColor1PB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.TevColor1Red, (byte)RevLMCTarget.TevColor1Green, (byte)RevLMCTarget.TevColor1Blue, (byte)RevLMCTarget.TevColor1Alpha, newActive);
                else if (sender == tevColor2PB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.TevColor2Red, (byte)RevLMCTarget.TevColor2Green, (byte)RevLMCTarget.TevColor2Blue, (byte)RevLMCTarget.TevColor2Alpha, newActive);
                else if (sender == tevColor3PB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.TevColor3Red, (byte)RevLMCTarget.TevColor3Green, (byte)RevLMCTarget.TevColor3Blue, (byte)RevLMCTarget.TevColor3Alpha, newActive);
                else if (sender == tevColor4PB) ApplyMaterialKeys(ActiveMaterial, (byte)RevLMCTarget.TevColor4Red, (byte)RevLMCTarget.TevColor4Green, (byte)RevLMCTarget.TevColor4Blue, (byte)RevLMCTarget.TevColor4Alpha, newActive);

                //Apply to all selected panes
                foreach (BasePane pane in ParentEditor.SelectedPanes)
                {
                    var mat = pane.TryGetActiveMaterial() as Revolution.Material;
                    if (mat != null && mat != ActiveMaterial) {
                        Color source = GetColor(mat, sender);
                        Color targetColor = relative ? ApplyRelativeHueShift(source, oldActive, newActive) : newActive;
                        ApplyColors(mat, sender, targetColor);

                        if (sender == whiteColorPB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.WhiteColorRed, (byte)RevLMCTarget.WhiteColorGreen, (byte)RevLMCTarget.WhiteColorBlue, (byte)RevLMCTarget.WhiteColorAlpha, targetColor);
                        else if (sender == blackColorBP) ApplyMaterialKeys(mat, (byte)RevLMCTarget.BlackColorRed, (byte)RevLMCTarget.BlackColorGreen, (byte)RevLMCTarget.BlackColorBlue, (byte)RevLMCTarget.BlackColorAlpha, targetColor);
                        else if (sender == materialColorPB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.MatColorRed, (byte)RevLMCTarget.MatColorGreen, (byte)RevLMCTarget.MatColorBlue, (byte)RevLMCTarget.MatColorAlpha, targetColor);
                        else if (sender == colorReg3PB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.ColorReg3Red, (byte)RevLMCTarget.ColorReg3Green, (byte)RevLMCTarget.ColorReg3Blue, (byte)RevLMCTarget.ColorReg3Alpha, targetColor);
                        else if (sender == tevColor1PB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.TevColor1Red, (byte)RevLMCTarget.TevColor1Green, (byte)RevLMCTarget.TevColor1Blue, (byte)RevLMCTarget.TevColor1Alpha, targetColor);
                        else if (sender == tevColor2PB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.TevColor2Red, (byte)RevLMCTarget.TevColor2Green, (byte)RevLMCTarget.TevColor2Blue, (byte)RevLMCTarget.TevColor2Alpha, targetColor);
                        else if (sender == tevColor3PB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.TevColor3Red, (byte)RevLMCTarget.TevColor3Green, (byte)RevLMCTarget.TevColor3Blue, (byte)RevLMCTarget.TevColor3Alpha, targetColor);
                        else if (sender == tevColor4PB) ApplyMaterialKeys(mat, (byte)RevLMCTarget.TevColor4Red, (byte)RevLMCTarget.TevColor4Green, (byte)RevLMCTarget.TevColor4Blue, (byte)RevLMCTarget.TevColor4Alpha, targetColor);
                    }
                }

                UpdateKeyState();

                BuildAnimationColorsList();

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
            if (sender == materialColorPB)
                return mat.MatColor.Color;
            if (sender == colorReg3PB)
                return mat.ColorRegister3.Color;
            if (sender == tevColor1PB)
                return mat.TevColor1.Color;
            if (sender == tevColor2PB)
                return mat.TevColor2.Color;
            if (sender == tevColor3PB)
                return mat.TevColor3.Color;
            if (sender == tevColor4PB)
                return mat.TevColor4.Color;

            return Color.White;
        }

        private void ApplyColors(Material mat, object sender, Color color)
        {
            if (sender == whiteColorPB)
                mat.WhiteColor.Color = color;
            else if (sender == blackColorBP)
                mat.BlackColor.Color = color;
            else if (sender == materialColorPB)
                mat.MatColor.Color = color;
            else if (sender == colorReg3PB)
                mat.ColorRegister3.Color = color;
            else if (sender == tevColor1PB)
                mat.TevColor1.Color = color;
            else if (sender == tevColor2PB)
                mat.TevColor2.Color = color;
            else if (sender == tevColor3PB)
                mat.TevColor3.Color = color;
            else if (sender == tevColor4PB)
                mat.TevColor4.Color = color;

            if (!mat.HasMaterialColor && mat.MatColor != STColor8.White) {
                mat.HasMaterialColor = true;
            }

            mat.MarkEdited();

        }
    }
}
