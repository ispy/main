using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using iSpy.Common.AddIns;

namespace Kinect
{
	[System.AddIn.AddIn("Kinect video source", Version="1.0.0.0", Description="Addin provides XBOX Kinect sensors connected via local or via network to an iSpyServer.")]
	public class KinectVideoSource
	{

		//private iSpy.Common.VideoSourceHost _host;

		public override IVideoSourceSettingsView GetSettingsControl()
		{
			return new VideoSourceOptions();
		}

	}
}
