using System;
using System.Windows.Forms;

namespace iSpyApplication
{
    public partial class ShareCamera : Form
    {
        public ShareCamera()
        {
            InitializeComponent();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            string make = txtMake.Text.Trim();
            string model = txtModel.Text.Trim();

            if (make=="" || model=="")
            {
                MessageBox.Show(this, "Please enter the make and the model");
                return;
            }
            btnAdd.Enabled = false;
            AddCameraToDatabase(make, model, FindCamerasForm.LastConfig.Prefix, FindCamerasForm.LastConfig.Source,
                       FindCamerasForm.LastConfig.URL, FindCamerasForm.LastConfig.Cookies, FindCamerasForm.LastConfig.Flags, FindCamerasForm.LastConfig.Port);
            Close();
        }

        private static void AddCameraToDatabase(string type, string model, string prefix, string source, string url, string cookies, string flags, int port)
        {
            try
            {
                var r = new Reporting.Reporting { Timeout = 8000 };
                r.AddCamera2(type, model, prefix, source, url, cookies, flags, port);
                r.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }

        private void ShareCamera_Load(object sender, EventArgs e)
        {
            txtMake.Text = FindCamerasForm.LastConfig.Iptype;
            txtModel.Text = FindCamerasForm.LastConfig.Ipmodel;
            lblType.Text = FindCamerasForm.LastConfig.Source;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
