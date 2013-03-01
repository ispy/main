using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Microsoft.Kinect;
using System.AddIn;
using iSpy.Common.AddIns;

namespace Kinect
{
	public partial class VideoSourceOptions : UserControl, IVideoSourceSettingsView
	{
		UserControl IVideoSourceSettingsView.GetUserControl()
		{
			return this as UserControl;
		}

		private Common.Logging.ILog Log = Common.Logging.LogManager.GetLoggerForCurrentClass();

		public VideoSourceOptions()
		{
			InitializeComponent();
		}

		private void VideoSourceOptions_Load(object sender, EventArgs e)
		{
			int deviceCount = 0;
			try
			{
				foreach (var potentialSensor in KinectSensor.KinectSensors)
				{
					if (potentialSensor.Status == KinectStatus.Connected)
					{
						deviceCount++;
						ddlKinectDevice.Items.Add(potentialSensor.UniqueKinectId);

					}
				}
			}
			catch (Exception ex)
			{
				//Type error if not installed
				Log.Error("Kinect supporting libraries not installed.", ex);
			}
			if (deviceCount > 0)
			{
				if (ddlKinectDevice.SelectedIndex == -1)
					ddlKinectDevice.SelectedIndex = 0;
			}
			else
			{
				this.Enabled = false;
			}

			if (NV("type") == "kinect")
			{
				try
				{
					chkKinectSkeletal.Checked = Convert.ToBoolean(NV("KinectSkeleton"));
				}
				catch { }
			}

		}

		string _settings;
		private void LoadSettingsString(string settings)
		{
			this._settings = settings;

			foreach (var potentialSensor in KinectSensor.KinectSensors)
			{
				if (NV("type") == "kinect")
				{
					if (NV("UniqueKinectId") == potentialSensor.UniqueKinectId)
					{
						ddlKinectDevice.SelectedIndex = ddlKinectDevice.Items.Count - 1;
					}
				}
			}

		}

		private string NV(string name)
		{
			if (String.IsNullOrEmpty(_settings))
				return "";
			name = name.ToLower().Trim();
			string[] settings = _settings.Split(',');
			foreach (string[] nv in settings.Select(s => s.Split('=')).Where(nv => nv[0].ToLower().Trim() == name))
			{
				return nv[1];
			}
			return "";
		}

	}
}
