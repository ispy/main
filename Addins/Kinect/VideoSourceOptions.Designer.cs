namespace Kinect
{
	partial class VideoSourceOptions
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.ddlKinectDevice = new System.Windows.Forms.ComboBox();
			this.chkKinectSkeletal = new System.Windows.Forms.CheckBox();
			this.label1 = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// ddlKinectDevice
			// 
			this.ddlKinectDevice.FormattingEnabled = true;
			this.ddlKinectDevice.Location = new System.Drawing.Point(105, 56);
			this.ddlKinectDevice.Name = "ddlKinectDevice";
			this.ddlKinectDevice.Size = new System.Drawing.Size(146, 21);
			this.ddlKinectDevice.TabIndex = 0;
			// 
			// chkKinectSkeletal
			// 
			this.chkKinectSkeletal.AutoSize = true;
			this.chkKinectSkeletal.Location = new System.Drawing.Point(105, 86);
			this.chkKinectSkeletal.Margin = new System.Windows.Forms.Padding(6);
			this.chkKinectSkeletal.Name = "chkKinectSkeletal";
			this.chkKinectSkeletal.Size = new System.Drawing.Size(104, 17);
			this.chkKinectSkeletal.TabIndex = 15;
			this.chkKinectSkeletal.Text = "Enable Skeleton";
			this.chkKinectSkeletal.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 59);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(75, 13);
			this.label1.TabIndex = 16;
			this.label1.Text = "Select device:";
			// 
			// VideoSourceOptions
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.label1);
			this.Controls.Add(this.chkKinectSkeletal);
			this.Controls.Add(this.ddlKinectDevice);
			this.Name = "VideoSourceOptions";
			this.Size = new System.Drawing.Size(336, 225);
			this.Load += new System.EventHandler(this.VideoSourceOptions_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ComboBox ddlKinectDevice;
		private System.Windows.Forms.CheckBox chkKinectSkeletal;
		private System.Windows.Forms.Label label1;
	}
}
