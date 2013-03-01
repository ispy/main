using System;
using System.Windows.Forms;

namespace iSpyApplication
{
    public partial class ConfigureObjectCountForm : Form
    {
        public int Objects = 0;

        public ConfigureObjectCountForm()
        {
            InitializeComponent();
            RenderResources();
        }

        private void RenderResources()
        {
            Text = LocRm.GetString("Configure");
            label48.Text = LocRm.GetString("Objects");
            button1.Text = LocRm.GetString("OK");
        }

        private void ForSecondsLoad(object sender, EventArgs e)
        {
            numObjects.Value = Objects;
        }

        private void Button1Click(object sender, EventArgs e)
        {
            Objects = Convert.ToInt32(numObjects.Value);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
