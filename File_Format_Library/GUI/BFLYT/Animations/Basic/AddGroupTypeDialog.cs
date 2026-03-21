using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library.Forms;

namespace LayoutBXLYT
{
    public partial class AddGroupTypeDialog : STForm
    {
        private BxlanPaiEntry ActiveGroup;

        public AddGroupTypeDialog(BxlanPaiEntry animGroup)
        {
            InitializeComponent();
            CanResize = false;
            ActiveGroup = animGroup;

            if (animGroup.Target == AnimationTarget.Pane)
            {
                AddTypeOption("PaneSRT");
                AddTypeOption("Visibility");
                AddTypeOption("TextureSRT");
                AddTypeOption("VertexColor");
                AddTypeOption("MaterialColor");
            }
            else
            {
                AddTypeOption("MaterialColor");
                AddTypeOption("TexturePattern");
                AddTypeOption("IndTextureSRT");
                AddTypeOption("AlphaTest");
                AddTypeOption("FontShadow");
                AddTypeOption("PerCharacterTransformCurve");
            }

            stComboBox1.SelectedIndex = 0;
        }

        private void AddTypeOption(string option)
        {
            if (string.IsNullOrEmpty(option))
                return;

            if (!stComboBox1.Items.Contains(option))
                stComboBox1.Items.Add(option);
        }

        public BxlanPaiTag AddEntry()
        {
            string tagValue = "";

            if (ActiveGroup is BFLAN.PaiEntry)
                tagValue = BxlanPaiTag.CafeTypeDefine.FirstOrDefault(x => x.Value == (string)stComboBox1.SelectedItem).Key;
            if (ActiveGroup is BRLAN.PaiEntry)
                tagValue = BxlanPaiTag.RevTypeDefine.FirstOrDefault(x => x.Value == (string)stComboBox1.SelectedItem).Key;
            if (ActiveGroup is BCLAN.PaiEntry)
                tagValue = BxlanPaiTag.CtrTypeDefine.FirstOrDefault(x => x.Value == (string)stComboBox1.SelectedItem).Key;

            return ActiveGroup.AddEntry(tagValue);
        }
    }
}
