using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iSpy.Common.AddIns
{
	public interface IVideoSourceSettingsView
	{

		System.Windows.Forms.UserControl GetUserControl();
		void LoadSettingsString(string settings);

	}
}
