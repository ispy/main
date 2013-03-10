using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using iSpyApplication.Audio.streams;
using iSpyApplication.Audio.talk;
using iSpyApplication.Controls;
using iSpyApplication.Properties;
using iSpyApplication.Video;
using Microsoft.Win32;
using NAudio.Wave;
using System.Reflection;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using PictureBox = AForge.Controls.PictureBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using Timer = System.Timers.Timer;
using iSpy.Common.Audio;

namespace iSpyApplication
{
    /// <summary>
    /// Summary description for MainForm
    /// </summary>
    public partial class MainForm : Form
    {
        private static bool _needsSync;
        private static DateTime _syncLastRequested = DateTime.MinValue;
        public static bool NeedsSync
        {
            get { return _needsSync;}
            set { 
                _needsSync = value;
                if (value)
                    _syncLastRequested = DateTime.Now;
            }
        }
        

        public static bool LoopBack;
        public static bool StopRecordingFlag;
        public static string NL = Environment.NewLine;
        public static Font Drawfont = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font DrawfontBig = new Font(FontFamily.GenericSansSerif, 25, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font Iconfont = new Font(FontFamily.GenericSansSerif, 15, FontStyle.Bold, GraphicsUnit.Pixel);
        public static Brush IconBrush = new SolidBrush(Color.White);
        public static Brush IconBrushOff = new SolidBrush(Color.FromArgb(64, 255, 255, 255));
        public static Brush IconBrushActive = new SolidBrush(Color.Red);
        public static Brush OverlayBrush = new SolidBrush(Color.White);

        public static string NextLog = "";
        public static string Identifier;
        public static DataTable IPTABLE;
        public static bool IPLISTED = true;
        public static bool IPRTSP = false, IPHTTP = true;
        public static string IPADDR = "";
        public static string IPCHANNEL = "0";
        public static string IPMODEL = "";
        public static string IPUN = "";
        public static string IPPORTS = "80";
        public static int IPPORT = 80;
        public static string IPPASS = "";
        public static string IPTYPE = "";
        public static int AFFILIATEID = 0;
        public static string EmailAddress = "", MobileNumber = "";
        public static int ThrottleFramerate = 40;
        public static int CpuUsage, CpuTotal;
        public static int RecordingThreads;
        public object ContextTarget;
        public bool SilentStartup;
        public static List<String> Plugins = new List<String>();
        public static bool NeedsRedraw;
        public McRemoteControlManager.RemoteControlDevice RemoteManager;
        public static List<FilePreview> MasterFileList = new List<FilePreview>();
        public static EncoderParameters EncoderParams;
        public static bool Reallyclose = false;

        public static string Website = "http://www.ispyconnect.com";
        public static string Webserver = "http://www.ispyconnect.com";
        public static string WebserverSecure = "https://www.ispyconnect.com";

        public static int ButtonWidth
        {
            get { return (Convert.ToInt32(Iconfont.Size)+1); }
        }

        internal static LocalServer MWS;

        internal PlayerForm Player;


        public static string PurchaseLink = "http://www.ispyconnect.com/astore.aspx";
        private static int _storageCounter;

        private static Timer _rescanIPTimer;
        private string _lastPath = Program.AppDataPath;
        private static bool _logging;
        private static string _counters = "";
        private static readonly Random Random = new Random();

        private static ViewControllerForm _vc;
        private static int _pingCounter;
        private static ImageCodecInfo _encoder;
        private static readonly StringBuilder LogFile = new StringBuilder(100000);

        private PTZControllerForm _ptzTool;
        private static readonly string LogTemplate =
            "<html><head><title>iSpy v<!--VERSION--> Log File</title><style type=\"text/css\">body,td,th,div {font-family:Verdana;font-size:10px}</style></head><body><h1>Log Start (v<!--VERSION-->): " +
            DateTime.Now.ToLongDateString() +
            "</h1><p><table cellpadding=\"2px\"><!--CONTENT--></table></p></body></html>";
        private static string _lastlog = "";
        private static List<objectsMicrophone> _microphones;
        private static string _browser = String.Empty;
        private static List<objectsFloorplan> _floorplans;
        private static List<objectsCommand> _remotecommands;
        private static List<objectsCamera> _cameras;
        private static List<PTZSettings2Camera> _ptzs;
        private static List<ManufacturersManufacturer> _sources;
        private PerformanceCounter _cpuCounter, _cputotalCounter, _pcMem;
        private static bool _pcMemAvailable;
        private static IPAddress[] _ipv4Addresses, _ipv6Addresses;
        private Timer _houseKeepingTimer;
        private bool _shuttingDown;
        private string _startCommand = "";
        private Timer _updateTimer;
        private bool _closing;
        private FileSystemWatcher _fsw;
        
        private Thread StorageThread;
        private PersistWindowState _mWindowState;

        private LayoutPanel _pnlCameras;

        private static List<LayoutItem> SavedLayout = new List<LayoutItem>();

        private FolderBrowserDialog fbdSaveTo = new FolderBrowserDialog()
        {
            ShowNewFolderButton = true,
            Description = "Select a folder to copy the file to"
        };

        [Obsolete("Use the parameterized constructor")]
        public MainForm()
        {
            InitializeComponent();
        }

        public MainForm(bool silent, string command)
        {
            SilentStartup = silent || Conf.Enable_Password_Protect;

            if (!SilentStartup)
            {
                _mWindowState = new PersistWindowState {Parent = this, RegistryPath = @"Software\ispy\startup"};
            }
            InitializeComponent();

            RenderResources();

            _startCommand = command;


            var r = Antiufo.Controls.Windows7Renderer.Instance;
            toolStripMenu.Renderer = r;
            statusStrip1.Renderer = r;

            _pnlCameras.BackColor = Conf.MainColor.ToColor();
            
            if (SilentStartup)
            {
                ShowInTaskbar = false;
                ShowIcon = false;
                WindowState = FormWindowState.Minimized;
            }

            
            RemoteManager = new McRemoteControlManager.RemoteControlDevice();
            RemoteManager.ButtonPressed += RemoteManagerButtonPressed;
            
            SetPriority();
            Arrange(false);

        }

        private bool IsOnScreen(Form form)
        {
            var screens = Screen.AllScreens;
            foreach (Screen screen in screens)
            {
                var formTopLeft = new Point(form.Left, form.Top);

                if (screen.WorkingArea.Contains(formTopLeft))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void WndProc(ref Message message)
        {
            RemoteManager.ProcessMessage(message);
            base.WndProc(ref message);
        }


        private void RemoteManagerButtonPressed(object sender, McRemoteControlManager.RemoteControlEventArgs e)
        {
            ProcessKey(e.Button.ToString().ToLower());
        }

        public static void SetPriority()
        {
            switch (Conf.Priority)
            {
                case 1:
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                    break;
                case 2:
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case 3:
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                    break;
                case 4:
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
                    break;
            }

        }

       

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        partial void OnDisposing()
        {
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();

            if (_mWindowState != null)
                _mWindowState.Dispose();

            Drawfont.Dispose();
            if (_updateTimer != null)
                _updateTimer.Dispose();
            if (_houseKeepingTimer != null)
                _houseKeepingTimer.Dispose();
            if (_fsw != null)
                _fsw.Dispose();
            if (fbdSaveTo != null)
                fbdSaveTo.Dispose();
        }

        // Close the main form
        private void ExitFileItemClick(object sender, EventArgs e)
        {
            Reallyclose = true;
            Close();
        }

        // On "Help->About"
        private void AboutHelpItemClick(object sender, EventArgs e)
        {
            var form = new AboutForm();
            form.ShowDialog(this);
            form.Dispose();
        }

        private void VolumeControlDoubleClick(object sender, EventArgs e)
        {
            Maximise(sender);
        }

        private void FloorPlanDoubleClick(object sender, EventArgs e)
        {
            Maximise(sender);
        }

        private static string Zeropad(int i)
        {
            if (i > 9)
                return i.ToString(CultureInfo.InvariantCulture);
            return "0" + i;
        }

        private void AddPlugin(FileInfo dll)
        {
            try
            {
                Assembly plugin = Assembly.LoadFrom(dll.FullName);
                object ins = null;
                try
                {
                    ins = plugin.CreateInstance("Plugins.Main", true);
                }
                catch
                {

                }
                if (ins != null)
                {
                    LogMessageToFile("Added: " + dll.FullName);
                    Plugins.Add(dll.FullName);
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
        }

        

        public void Play(string filename, int objectId)
        {

            if (Player == null)
            {
                Player = new PlayerForm();
                Player.Show(this);
                Player.Closed += PlayerClosed;
            }
            Player.Owner = this;
            Player.Activate();
            Player.BringToFront();
            Player.ObjectID = objectId;
            Player.Play(filename);


        }

        private void PlayerClosed(object sender, EventArgs e)
        {
            Player = null;
        }

        private void InitLogging()
        {
            DateTime logdate = DateTime.Now;


            foreach (string s in Directory.GetFiles(Program.AppDataPath, "log_*", SearchOption.TopDirectoryOnly))
            {

                var fi = new FileInfo(s);
                if (fi.CreationTime < DateTime.Now.AddDays(-5))
                    FileOperations.Delete(s);
            }
            NextLog = Zeropad(logdate.Day) + Zeropad(logdate.Month) + logdate.Year;
            int i = 1;
            if (File.Exists(Program.AppDataPath + "log_" + NextLog + ".htm"))
            {
                while (File.Exists(Program.AppDataPath + "log_" + NextLog + "_" + i + ".htm"))
                    i++;
                NextLog += "_" + i;
            }
            try
            {
                File.WriteAllText(Program.AppDataPath + "log_" + NextLog + ".htm", DateTime.Now + Environment.NewLine);
                _logging = true;
            }
            catch (Exception ex)
            {
                if (
                    MessageBox.Show(LocRm.GetString("LogStartError").Replace("[MESSAGE]", ex.Message),
                                    LocRm.GetString("Warning"), MessageBoxButtons.YesNo) == DialogResult.No)
                {

                    Reallyclose = true;
                    Close();
                    return;
                }
            }
        }

        private void MainFormLoad(object sender, EventArgs e)
        {
            UISync.Init(this);

            try
            {
                File.WriteAllText(Program.AppDataPath + "exit.txt", "RUNNING");
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }

            InitLogging();


            EncoderParams = new EncoderParameters(1);
            EncoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Conf.JPEGQuality);

            //this initializes the port mapping collection
            NATUPNPLib.IStaticPortMappingCollection map = NATControl.Mappings;
            if (Conf.MediaDirectory == null || Conf.MediaDirectory == "NotSet")
            {
                Conf.MediaDirectory = Program.AppDataPath + @"WebServerRoot\Media\";
            }
            if (!Directory.Exists(Conf.MediaDirectory))
            {
                string notfound = Conf.MediaDirectory;
                //Conf.MediaDirectory = Program.AppDataPath + @"WebServerRoot\Media\";
                LogErrorToFile("Media directory could not be found (" + notfound + ") - reset it to " +
                                 Program.AppDataPath + @"WebServerRoot\Media\" + " in settings if it doesn't attach.");
            }

            if (!VlcHelper.VlcInstalled)
            {

                LogWarningToFile(
                    "VLC not installed - install VLC (x86) for extra connectivity and inbuilt video playback.");
            }
            else
            {
                var v = VlcHelper.VlcVersion;
                if (v.CompareTo(VlcHelper.VMin) < 0)
                {

                    LogWarningToFile(
                        "Old VLC installed - update VLC (x86) for extra connectivity and inbuilt video playback.");
                }
                else
                {

                    if (v.CompareTo(new Version(2, 0, 2)) == 0)
                    {

                        LogWarningToFile(
                            "VLC v2.0.2 detected - there are known issues with this version of VLC (HTTP streaming is broken for a lot of cameras) - if you are having problems with VLC connectivity we recommend you install v2.0.1 ( http://download.videolan.org/pub/videolan/vlc/2.0.1/ ) or the latest (if available).");
                    }
                }
            }


            _fsw = new FileSystemWatcher
                       {
                           Path = Program.AppDataPath,
                           IncludeSubdirectories = false,
                           Filter = "external_command.txt",
                           NotifyFilter = NotifyFilters.LastWrite
                       };
            _fsw.Changed += FswChanged;
            _fsw.EnableRaisingEvents = true;
            GC.KeepAlive(_fsw);


            Menu = mainMenu;
            notifyIcon1.ContextMenuStrip = ctxtTaskbar;
            Identifier = Guid.NewGuid().ToString();
            MWS = new LocalServer(this)
                      {
                          ServerRoot = Program.AppDataPath + @"WebServerRoot\",
                      };

            if (Conf.Monitor)
            {
                var w = Process.GetProcessesByName("ispymonitor");
                if (w.Length == 0)
                {
                    try
                    {
                        var si = new ProcessStartInfo(Program.AppPath + "/ispymonitor.exe", "ispy");
                        Process.Start(si);
                    }
                    catch
                    {
                    }
                }
            }

            GC.KeepAlive(MWS);

            SetBackground();

            toolStripMenu.Visible = Conf.ShowToolbar;
            statusStrip1.Visible = Conf.ShowStatus;
            Menu = !Conf.ShowFileMenu ? null : mainMenu;


            if (Conf.Fullscreen && !SilentStartup)
            {
                WindowState = FormWindowState.Maximized;
                FormBorderStyle = FormBorderStyle.None;
                WinApi.SetWinFullScreen(Handle);
            }
            if (SilentStartup)
            {
                WindowState = FormWindowState.Minimized;
            }

            statusBarToolStripMenuItem.Checked = menuItem4.Checked = Conf.ShowStatus;
            toolStripToolStripMenuItem.Checked = menuItem6.Checked = Conf.ShowToolbar;
            fileMenuToolStripMenuItem.Checked = menuItem5.Checked = Conf.ShowFileMenu;
            fullScreenToolStripMenuItem1.Checked = menuItem3.Checked = Conf.Fullscreen;
            alwaysOnTopToolStripMenuItem1.Checked = menuItem8.Checked = Conf.AlwaysOnTop;
            mediaPaneToolStripMenuItem.Checked = menuItem7.Checked = Conf.ShowMediaPanel;
            menuItem22.Checked = Conf.LockLayout;
            TopMost = Conf.AlwaysOnTop;

            Iconfont = new Font(FontFamily.GenericSansSerif, Conf.BigButtons ? 22 : 15, FontStyle.Bold,
                                GraphicsUnit.Pixel);

            double dOpacity;

            Double.TryParse(Conf.Opacity.ToString(CultureInfo.InvariantCulture), out dOpacity);
            Opacity = dOpacity/100.0;



            if (Conf.ServerName == "NotSet")
            {
                Conf.ServerName = SystemInformation.ComputerName;
            }

            notifyIcon1.Text = Conf.TrayIconText;
            notifyIcon1.BalloonTipClicked += NotifyIcon1BalloonTipClicked;
            autoLayoutToolStripMenuItem.Checked = Conf.AutoLayout;

            _updateTimer = new Timer(500);
            _updateTimer.Elapsed += UpdateTimerElapsed;
            _updateTimer.AutoReset = true;
            _updateTimer.SynchronizingObject = this;
            GC.KeepAlive(_updateTimer);

            _houseKeepingTimer = new Timer(1000);
            _houseKeepingTimer.Elapsed += HouseKeepingTimerElapsed;
            _houseKeepingTimer.AutoReset = true;
            _houseKeepingTimer.SynchronizingObject = this;
            GC.KeepAlive(_houseKeepingTimer);

            //load plugins
            var plugindir = new DirectoryInfo(Program.AppPath + "Plugins");
            LogMessageToFile("Checking Plugins...");
            foreach (var dll in plugindir.GetFiles("*.dll"))
            {

                AddPlugin(dll);
            }
            foreach (DirectoryInfo d in plugindir.GetDirectories())
            {
                LogMessageToFile(d.Name);
                foreach (var dll in d.GetFiles("*.dll"))
                {
                    AddPlugin(dll);
                }
            }

            resetLayoutToolStripMenuItem1.Enabled = mnuResetLayout.Enabled = false; //reset layout


            NetworkChange.NetworkAddressChanged += NetworkChangeNetworkAddressChanged;
            mediaPaneToolStripMenuItem.Checked = Conf.ShowMediaPanel;
            ShowHideMediaPane();
            if (!String.IsNullOrEmpty(Conf.MediaPanelSize))
            {
                string[] dd = Conf.MediaPanelSize.Split('x');
                int d1 = Convert.ToInt32(dd[0]);
                int d2 = Convert.ToInt32(dd[1]);
                try
                {
                    splitContainer1.SplitterDistance = d1;
                    splitContainer2.SplitterDistance = d2;
                }
                catch
                {

                }
            }
            //load in object list

            if (_startCommand.Trim().StartsWith("open"))
            {
                ParseCommand(_startCommand);
                _startCommand = "";
            }
            else
            {
                if (!File.Exists(Program.AppDataPath + @"XML\objects.xml"))
                {
                    File.Copy(Program.AppPath + @"XML\objects.xml", Program.AppDataPath + @"XML\objects.xml");
                }
                ParseCommand("open " + Program.AppDataPath + @"XML\objects.xml");
            }
            if (_startCommand != "")
            {
                ParseCommand(_startCommand);
            }

            StopAndStartServer();
            var t = new Thread(ConnectServices) {IsBackground = false};
            t.Start();

            if (SilentStartup)
            {

                _mWindowState = new PersistWindowState {Parent = this, RegistryPath = @"Software\ispy\startup"};
            }

            _updateTimer.Start();
            _houseKeepingTimer.Start();

            if (Conf.RunTimes == 0)
                ShowGettingStarted();

            if (File.Exists(Program.AppDataPath+"custom.txt"))
            {
                string[] cfg = File.ReadAllText(Program.AppDataPath + "custom.txt").Split(Environment.NewLine.ToCharArray());

                foreach(string s in cfg)
                {
                    if (!String.IsNullOrEmpty(s))
                    {
                        string[] nv = s.Split('=');
                        if (nv.Length>1)
                        {
                            switch (nv[0].ToLower().Trim())
                            {
                                case "business":
                                    Conf.Vendor = nv[1].Trim();
                                    break;
                                case "link":
                                    PurchaseLink = nv[1].Trim();
                                    break;
                                case "manufacturer":
                                    IPTYPE = Conf.DefaultManufacturer = nv[1].Trim();
                                    break;
                                case "model":
                                    IPMODEL = nv[1].Trim();
                                    break;
                                case "affiliateid":
                                case "affiliate id":
                                case "aid":
                                    int aid = 0;
                                    if (Int32.TryParse(nv[1].Trim(), out aid))
                                    {
                                        AFFILIATEID = aid;
                                    }
                                    break;

                            }
                        }
                    }
                }

                string logo = Program.AppDataPath + "logo.jpg";
                if (!File.Exists(logo))
                    logo = Program.AppDataPath + "logo.png";

                if (File.Exists(logo))
                {
                    try
                    {
                        var bmp = Image.FromFile(logo);
                        var pb = new PictureBox {Image = bmp};
                        pb.Width = pb.Image.Width;
                        pb.Height = pb.Image.Height;

                        pb.Left = _pnlCameras.Width/2 - pb.Width/2;
                        pb.Top = _pnlCameras.Height/2 - pb.Height/2;

                        _pnlCameras.Controls.Add(pb);
                        _pnlCameras.BrandedImage = pb;
                    }
                    catch (Exception ex)
                    {
                        LogExceptionToFile(ex);
                    }
                }
            }
            else
            {
            if (!String.IsNullOrEmpty(Conf.Vendor))
            {
                var pb = new PictureBox();
                switch (Conf.Vendor.ToLower())
                {
                    case "ensidio":
                        pb.Image = Resources.ensidio;
                        PurchaseLink = "http://www.ensidio.com/";
                        break;
                    case "tenvis":
                        pb.Image = Resources.TENVIS;
                        PurchaseLink = "http://www.tenvis.com/";
                        break;
                    case "smartisp":
                        pb.Image = Resources.smartisp;
                        break;
                    case "addplus":
                        pb.Image = Resources.Addplus;
                        break;
                    case "foscam":
                        pb.Image = Resources.foscam;
                        PurchaseLink = "http://www.foscam.com/";
                        break;
                    case "phyxius":
                        pb.Image = Resources.phyxius;
                        break;
                    case "bigdipper":
                        pb.Image = Resources.bigdipper;
                        break;
                    case "allnet gmbh":
                        pb.Image = Resources.ALLNET;
                        PurchaseLink = "http://www.allnet.de/";
                        break;
                    case "eos":
                        pb.Image = Resources.EOSLogo;
                        PurchaseLink = "http://nowyoucansee.com/";
                        break;
                }
                pb.Width = pb.Image.Width;
                pb.Height = pb.Image.Height;

                pb.Left = _pnlCameras.Width/2 - pb.Width/2;
                pb.Top = _pnlCameras.Height/2 - pb.Height/2;

                _pnlCameras.Controls.Add(pb);
                _pnlCameras.BrandedImage = pb;

            }
            }

            Text = string.Format("iSpy v{0}", Application.ProductVersion);
            if (!String.IsNullOrEmpty(Conf.Vendor))
            {
                Text += string.Format(" with {0}", Conf.Vendor);
            }

            LoadCommands();
            if (!SilentStartup && Conf.ViewController)
            {
                ShowViewController();
                viewControllerToolStripMenuItem.Checked = menuItem14.Checked = true;
            }

            pTZControllerToolStripMenuItem.Checked = menuItem18.Checked = pTZControllerToolStripMenuItem1.Checked = Conf.ShowPTZController;

            if (Conf.ShowPTZController)
                ShowHidePTZTool();
            
            Conf.RunTimes++;

            try
            {
                _cputotalCounter = new PerformanceCounter("Processor", "% Processor Time", "_total", true);
                _cpuCounter = new PerformanceCounter("Process", "% Processor Time",
                                                     Process.GetCurrentProcess().ProcessName, true);
                try
                {
                    _pcMem = new PerformanceCounter("Process", "Working Set - Private",
                                                    Process.GetCurrentProcess().ProcessName, true);
                }
                catch
                {
                    //no working set - only total available on windows xp
                    try
                    {
                        _pcMem = new PerformanceCounter("Memory", "Available MBytes");
                        _pcMemAvailable = true;
                    }
                    catch (Exception ex2)
                    {
                        LogExceptionToFile(ex2);
                        _pcMem = null;
                    }
                }

            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
                _cputotalCounter = null;
            }

        }       


        private static void NetworkChangeNetworkAddressChanged(object sender, EventArgs e)
        {
            //schedule update check for a few seconds as a network change involves 2 calls to this event - removing and adding.

            if (_rescanIPTimer == null)
            {
                _rescanIPTimer = new Timer(5000);
                _rescanIPTimer.Elapsed += RescanIPTimerElapsed;
                _rescanIPTimer.Start();
            }

        }


        private static void RescanIPTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _rescanIPTimer.Stop();
            _rescanIPTimer = null;
            if (Conf.IPMode == "IPv4")
            {
                _ipv4Addresses = null;
                bool iplisted = false;
                foreach (IPAddress ip in AddressListIPv4)
                {
                    if (Conf.IPv4Address == ip.ToString())
                        iplisted = true;
                }
                if (!iplisted)
                {

                    _ipv4Address = "";
                    Conf.IPv4Address = AddressIPv4;
                }
                if (iplisted)
                    return;
            }
            if (!String.IsNullOrEmpty(Conf.WSUsername) && !String.IsNullOrEmpty(Conf.WSPassword))
            {
                switch (Conf.IPMode)
                {
                    case "IPv4":
                        LogErrorToFile(
                        "Your IP address has changed. Please set a static IP address for your local computer to ensure uninterrupted connectivity.");
                        if (Conf.DHCPReroute && Conf.IPMode == "IPv4")
                        {
                            //check if IP address has changed
                            if (Conf.UseUPNP)
                            {
                                //change router ports
                                if (NATControl.SetPorts(Conf.ServerPort, Conf.LANPort))
                                    LogMessageToFile("Router port forwarding has been updated. (" +
                                                     Conf.IPv4Address + ")");
                            }
                            else
                            {
                                LogMessageToFile("Please check Use UPNP in web settings to handle this automatically");
                            }
                        }
                        else
                        {
                            LogMessageToFile("Enable DHCP Reroute in Web Settings to handle this automatically");
                        }
                        WsWrapper.ForceSync();
                        break;
                    case "IPv6":
                        _ipv6Addresses = null;
                        bool iplisted = false;
                        foreach (IPAddress ip in AddressListIPv6)
                        {
                            if (Conf.IPv6Address == ip.ToString())
                                iplisted = true;
                        }
                        if (!iplisted)
                        {
                            LogErrorToFile(
                                "Your IP address has changed. Please set a static IP address for your local computer to ensure uninterrupted connectivity.");
                            _ipv6Address = "";
                            Conf.IPv6Address = AddressIPv6;
                        }
                        break;
                }
            }
        }


        private void RenderResources()
        {

            Text = string.Format("iSpy v{0}", Application.ProductVersion);
            if (!String.IsNullOrEmpty(Conf.Vendor))
            {

                Text += string.Format(" with {0}", Conf.Vendor);
            }
            _aboutHelpItem.Text = LocRm.GetString("About");
            _activateToolStripMenuItem.Text = LocRm.GetString("Switchon");
            _addCameraToolStripMenuItem.Text = LocRm.GetString("AddCamera");
            _addFloorPlanToolStripMenuItem.Text = LocRm.GetString("AddFloorplan");
            _addMicrophoneToolStripMenuItem.Text = LocRm.GetString("Addmicrophone");
            autoLayoutToolStripMenuItem.Text = LocRm.GetString("AutoLayout");

            _deleteToolStripMenuItem.Text = LocRm.GetString("remove");
            _editToolStripMenuItem.Text = LocRm.GetString("Edit");
            _exitFileItem.Text = LocRm.GetString("Exit");
            _exitToolStripMenuItem.Text = LocRm.GetString("Exit");
            _fileItem.Text = LocRm.GetString("file");
            fileMenuToolStripMenuItem.Text = LocRm.GetString("Filemenu");
            menuItem5.Text = LocRm.GetString("Filemenu");
            _floorPlanToolStripMenuItem.Text = LocRm.GetString("FloorPlan");
            fullScreenToolStripMenuItem.Text = LocRm.GetString("fullScreen");
            fullScreenToolStripMenuItem1.Text = LocRm.GetString("fullScreen");
            _helpItem.Text = LocRm.GetString("help");
            _helpToolstripMenuItem.Text = LocRm.GetString("help");
            _iPCameraToolStripMenuItem.Text = LocRm.GetString("IpCamera");
            _menuItem24.Text = LocRm.GetString("ShowGettingStarted");
            _listenToolStripMenuItem.Text = LocRm.GetString("Listen");
            _localCameraToolStripMenuItem.Text = LocRm.GetString("LocalCamera");
            _menuItem1.Text = LocRm.GetString("chars_2949165");
            _menuItem10.Text = LocRm.GetString("checkForUpdates");
            _menuItem11.Text = LocRm.GetString("reportBugFeedback");
            _menuItem13.Text = LocRm.GetString("chars_2949165");
            _menuItem15.Text = LocRm.GetString("ResetAllRecordingCounters");
            _menuItem16.Text = LocRm.GetString("View");
            _menuItem17.Text = inExplorerToolStripMenuItem.Text = LocRm.GetString("files");
            _menuItem18.Text = LocRm.GetString("clearCaptureDirectories");
            _menuItem19.Text = LocRm.GetString("saveObjectList");
            _menuItem2.Text = LocRm.GetString("help");
            _menuItem20.Text = LocRm.GetString("Logfile");
            _menuItem21.Text = LocRm.GetString("openObjectList");
            _menuItem22.Text = LocRm.GetString("LogFiles");
            _menuItem23.Text = LocRm.GetString("audiofiles");
            _menuItem25.Text = LocRm.GetString("MediaOnAMobiledeviceiphon");
            _menuItem26.Text = LocRm.GetString("supportIspyWithADonation");
            _menuItem27.Text = LocRm.GetString("chars_2949165");
            _menuItem29.Text = LocRm.GetString("Current");
            _menuItem3.Text = LocRm.GetString("MediaoverTheWeb");
            _menuItem30.Text = LocRm.GetString("chars_2949165");
            _menuItem31.Text = LocRm.GetString("removeAllObjects");
            _menuItem32.Text = LocRm.GetString("chars_2949165");
            _menuItem33.Text = LocRm.GetString("switchOff");
            _menuItem34.Text = LocRm.GetString("Switchon");
            _miOnAll.Text = LocRm.GetString("All");
            _miOffAll.Text = LocRm.GetString("All");
            _miOnSched.Text = LocRm.GetString("Scheduled");
            _miOffSched.Text = LocRm.GetString("Scheduled");
            _miApplySchedule.Text = _applyScheduleToolStripMenuItem1.Text = LocRm.GetString("ApplySchedule");
            _applyScheduleToolStripMenuItem.Text = LocRm.GetString("ApplySchedule");
            _menuItem35.Text = LocRm.GetString("ConfigureremoteCommands");
            _menuItem36.Text = LocRm.GetString("Edit");
            _menuItem37.Text = LocRm.GetString("CamerasAndMicrophones");
            _menuItem38.Text = LocRm.GetString("ViewUpdateInformation");
            _menuItem39.Text = LocRm.GetString("AutoLayoutObjects");
            _menuItem4.Text = LocRm.GetString("ConfigureremoteAccess");
            _menuItem5.Text = LocRm.GetString("GoTowebsite");
            _menuItem6.Text = LocRm.GetString("chars_2949165");
            _menuItem7.Text = LocRm.GetString("videofiles");
            _menuItem8.Text = LocRm.GetString("settings");
            _menuItem9.Text = LocRm.GetString("options");
            _microphoneToolStripMenuItem.Text = LocRm.GetString("Microphone");
            notifyIcon1.Text = LocRm.GetString("Ispy");
            _onMobileDevicesToolStripMenuItem.Text = LocRm.GetString("MobileDevices");

            opacityToolStripMenuItem.Text = LocRm.GetString("Opacity");
            opacityToolStripMenuItem1.Text = LocRm.GetString("Opacity10");
            opacityToolStripMenuItem2.Text = LocRm.GetString("Opacity30");
            opacityToolStripMenuItem3.Text = LocRm.GetString("Opacity100");

            menuItem9.Text = LocRm.GetString("Opacity");
            menuItem10.Text = LocRm.GetString("Opacity10");
            menuItem11.Text = LocRm.GetString("Opacity30");
            menuItem12.Text = LocRm.GetString("Opacity100");


            _positionToolStripMenuItem.Text = LocRm.GetString("Position");
            _recordNowToolStripMenuItem.Text = LocRm.GetString("RecordNow");
            _remoteCommandsToolStripMenuItem.Text = LocRm.GetString("RemoteCommands");
            _resetRecordingCounterToolStripMenuItem.Text = LocRm.GetString("ResetRecordingCounter");
            _resetSizeToolStripMenuItem.Text = LocRm.GetString("ResetSize");
            _setInactiveToolStripMenuItem.Text = LocRm.GetString("switchOff");
            _settingsToolStripMenuItem.Text = LocRm.GetString("settings");
            _showFilesToolStripMenuItem.Text = LocRm.GetString("ShowFiles");
            _showISpy100PercentOpacityToolStripMenuItem.Text = LocRm.GetString("ShowIspy100Opacity");
            _showISpy10PercentOpacityToolStripMenuItem.Text = LocRm.GetString("ShowIspy10Opacity");
            _showISpy30OpacityToolStripMenuItem.Text = LocRm.GetString("ShowIspy30Opacity");
            _showToolstripMenuItem.Text = LocRm.GetString("showIspy");
            statusBarToolStripMenuItem.Text = LocRm.GetString("Statusbar");
            menuItem4.Text = LocRm.GetString("Statusbar");
            _switchAllOffToolStripMenuItem.Text = LocRm.GetString("SwitchAllOff");
            _switchAllOnToolStripMenuItem.Text = LocRm.GetString("SwitchAllOn");
            _takePhotoToolStripMenuItem.Text = LocRm.GetString("TakePhoto");
            _thruWebsiteToolStripMenuItem.Text = LocRm.GetString("Online");
            _toolStripButton1.Text = LocRm.GetString("WebSettings");
            _toolStripButton4.Text = LocRm.GetString("settings");
            _toolStripButton8.Text = LocRm.GetString("Commands");
            _toolStripDropDownButton1.Text = LocRm.GetString("AccessMedia");
            _toolStripDropDownButton2.Text = LocRm.GetString("Add");
            _toolStripMenuItem1.Text = LocRm.GetString("Viewmedia");
            toolStripToolStripMenuItem.Text = LocRm.GetString("toolStrip");
            menuItem6.Text = LocRm.GetString("toolStrip");
            _tsslStats.Text = LocRm.GetString("Loading");
            _unlockToolstripMenuItem.Text = LocRm.GetString("unlock");
            _viewMediaOnAMobileDeviceToolStripMenuItem.Text = LocRm.GetString("ViewMediaOnAMobiledevice");
            _websiteToolstripMenuItem.Text = LocRm.GetString("website");
            _uSbCamerasAndMicrophonesOnOtherToolStripMenuItem.Text =
                LocRm.GetString("CamerasAndMicrophonesOnOtherComputers");
            fullScreenToolStripMenuItem.Text = LocRm.GetString("Fullscreen");
            menuItem3.Text = LocRm.GetString("Fullscreen");
            alwaysOnTopToolStripMenuItem1.Text = LocRm.GetString("AlwaysOnTop");
            menuItem8.Text = LocRm.GetString("AlwaysOnTop");
            llblSelectAll.Text = LocRm.GetString("SelectAll");
            llblDelete.Text = LocRm.GetString("Delete");
            menuItem13.Text = LocRm.GetString("PurchaseMoreCameras");
            _exitToolStripMenuItem.Text = LocRm.GetString("Exit");

            layoutToolStripMenuItem.Text = LocRm.GetString("Layout");
            displayToolStripMenuItem.Text = LocRm.GetString("Display");

            mnuSaveLayout.Text = saveLayoutToolStripMenuItem1.Text = LocRm.GetString("SaveLayout");
            mnuResetLayout.Text = resetLayoutToolStripMenuItem1.Text = LocRm.GetString("ResetLayout");
            mediaPaneToolStripMenuItem.Text = LocRm.GetString("ShowMediaPanel");
            menuItem7.Text = LocRm.GetString("ShowMediaPanel");
            iPCameraWithWizardToolStripMenuItem.Text = LocRm.GetString("IPCameraWithWizard");
            tsbPlugins.Text = LocRm.GetString("Plugins");

            menuItem14.Text = viewControllerToolStripMenuItem.Text = LocRm.GetString("ViewController");

            llblRefresh.Text = LocRm.GetString("Reload");

        }


        private void HouseKeepingTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _houseKeepingTimer.Stop();
            if (NeedsRedraw)
            {
                _pnlCameras.Invalidate();
                NeedsRedraw = false;
            }
            if (_cputotalCounter != null)
            {
                try
                {
                    CpuUsage = Convert.ToInt32(_cpuCounter.NextValue())/Environment.ProcessorCount;
                    CpuTotal = Convert.ToInt32(_cputotalCounter.NextValue());
                    _counters = "CPU: " + CpuUsage + "%";

                    if (_pcMem != null)
                    {
                        if (_pcMemAvailable)
                            _counters += " RAM Available: " + Convert.ToInt32(_pcMem.NextValue()) + "Mb";
                        else
                            _counters += " RAM Usage: " + Convert.ToInt32(_pcMem.RawValue/1048576) + "Mb";
                    }
                    tsslMonitor.Text = _counters;
                }
                catch (Exception ex)
                {
                    _cputotalCounter = null;
                    LogExceptionToFile(ex);
                }
                if (CpuTotal > _conf.CPUMax)
                {
                    if (ThrottleFramerate > 1)
                        ThrottleFramerate--;
                }
                else
                {
                    if (ThrottleFramerate < 40)
                        ThrottleFramerate++;
                }
            }
            else
            {
                _counters = "Stats Unavailable - See Log File";
            }

            _pingCounter++;

            if (_pingCounter == 301)
            {
                _pingCounter = 0;
                //auto save
                try
                {
                    SaveObjects("");
                }
                catch (Exception ex)
                {
                    LogExceptionToFile(ex);
                }
                try
                {
                    SaveConfig();
                }
                catch (Exception ex)
                {
                    LogExceptionToFile(ex);
                }
            }
            try
            {
                if (!MWS.Running)
                {
                    _tsslStats.Text = "Server Error - see log file";
                    if (MWS.NumErr >= 5)
                    {
                        LogMessageToFile("Server not running - restarting");
                        StopAndStartServer();
                    }
                }
                else
                {
                    if (WsWrapper.WebsiteLive)
                    {
                        if (Conf.ServicesEnabled)
                        {
                        _tsslStats.Text = LocRm.GetString("Online");
                        if (LoopBack && Conf.Subscribed)
                                _tsslStats.Text += string.Format(" ({0})", LocRm.GetString("loopback"));
                        else
                        {
                            if (!Conf.Subscribed)
                                    _tsslStats.Text += string.Format(" ({0})", LocRm.GetString("LANonlynotsubscribed"));
                            else
                                    _tsslStats.Text += string.Format(" ({0})", LocRm.GetString("LANonlyNoLoopback"));
                        }
                    }
                    else
                        {
                        _tsslStats.Text = LocRm.GetString("Offline");
                        }
                    }
                    else
                    {
                        _tsslStats.Text = LocRm.GetString("Offline");
                    }
                    
                        
                }             

                if (Conf.ServicesEnabled)
                {
                    try
                    {
                        if (NeedsSync)
                        {
                            DateTime dt = _syncLastRequested;
                            WsWrapper.ForceSync();
                            if (dt==_syncLastRequested)
                                NeedsSync = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Housekeeping Error: " + ex.Message);
                    }


                    if (_pingCounter == 180)
                    {
                        WsWrapper.PingServer();
                    }
                }

                if (Conf.Enable_Storage_Management)
                {
                    _storageCounter++;
                    if (_storageCounter == 3600) // every hour
                    {
                        RunStorageManagement();
                        _storageCounter = 0;
                    }
                }

                if (_pingCounter == 80)
                {
                    var t = new Thread(SaveFileData) {IsBackground = true, Name = "Saving File Data"};
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            WriteLog();
            if (!_shuttingDown)
                _houseKeepingTimer.Start();
        }

        private delegate void RunStorageManagementDelegate();
        public void RunStorageManagement()
        {
            if (InvokeRequired)
            {
                Invoke(new RunStorageManagementDelegate(RunStorageManagement));
                return;
            }

            
            if (StorageThread == null || !StorageThread.IsAlive)
            {
                LogMessageToFile("Running Storage Management");
                StorageThread = new Thread(DeleteOldFiles) { IsBackground = true };
                StorageThread.Start();
            }
            else
                LogMessageToFile("Storage Management is already running");
        }

        private void UpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _updateTimer.Stop();

            foreach (Control c in _pnlCameras.Controls)
            {
                try
                {
                    if (c is CameraWindow)
                    {
                        ((CameraWindow) c).Tick();
                    }
                    if (c is VolumeLevel)
                    {
                        ((VolumeLevel) c).Tick();
                    }
                    if (c is FloorPlanControl)
                    {
                        var fpc = ((FloorPlanControl) c);
                        if (fpc.Fpobject.needsupdate)
                        {
                            fpc.NeedsRefresh = true;
                            fpc.Fpobject.needsupdate = false;
                        }
                        fpc.Tick();
                    }
                }
                catch (Exception ex)
                {
                    LogExceptionToFile(ex);
                }
            }
            if (!_shuttingDown)
                _updateTimer.Start();
        }

        private void FswChanged(object sender, FileSystemEventArgs e)
        {
            _fsw.EnableRaisingEvents = false;
            bool err = true;
            int i = 0;
            try
            {
                string txt = "";
                while (err && i < 5)
                {
                    try
                    {
                        using (var fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (var sr = new StreamReader(fs))
                            {
                                while (sr.EndOfStream == false)
                                {
                                    txt = sr.ReadLine();
                                    err = false;
                                }
                                sr.Close();
                            }
                            fs.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogExceptionToFile(ex);
                        i++;
                        Thread.Sleep(500);
                    }
                }
                if (txt != null)
                    if (txt.Trim() != "")
                        ParseCommand(txt);
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            _fsw.EnableRaisingEvents = true;
        }

        private void ParseCommand(string command)
        {
            if (command == null) throw new ArgumentNullException("command");
            try
            {
                command = Uri.UnescapeDataString(command);
                

                LogMessageToFile("Running External Command: " + command);

                if (command.ToLower().StartsWith("open "))
                {
                    if (InvokeRequired)
                        Invoke(new ExternalCommandDelegate(LoadObjectList), command.Substring(5).Trim('"'));
                    else
                        LoadObjectList(command.Substring(5).Trim('"'));
                }
                int i = command.ToLower().IndexOf("commands ", StringComparison.Ordinal);
                if (i!=-1)
                {
                    string cmd = command.Substring(i+9).Trim('"');
                    string[] commands = cmd.Split('|');
                    foreach (string command2 in commands)
                    {
                        if (!String.IsNullOrEmpty(command2))
                        {
                            if (InvokeRequired)
                                Invoke(new ExternalCommandDelegate(ProcessCommandInternal), command2.Trim('"'));
                            else
                                ProcessCommandInternal(command2.Trim('"'));
                        }
                    }
                }
                if (command.ToLower()=="showform")
                {
                    UISync.Execute(ShowIfUnlocked);
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
                MessageBox.Show(LocRm.GetString("LoadFailed").Replace("[MESSAGE]", ex.Message));
            }
        }

        internal static void ProcessCommandInternal(string command)
        {
            //parse command into new format
            string[] cfg = command.Split(',');
            string newcommand;
            if (cfg.Length == 1)
                newcommand = cfg[0];
            else
            {
                newcommand = cfg[0] + "?ot=" + cfg[1] + "&oid=" + cfg[2];
            }
            MWS.ProcessCommandInternal(newcommand);
        }

        public void SetBackground()
        {
            _pnlCameras.BackColor = Conf.MainColor.ToColor();
            _pnlContent.BackColor = SystemColors.AppWorkspace;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                notifyIcon1.Visible = false;

                notifyIcon1.Icon.Dispose();
                notifyIcon1.Dispose();
            }
            catch
            {
            }
            base.OnClosed(e);
        }

        private void MenuItem2Click(object sender, EventArgs e)
        {
            StartBrowser(Website + "/userguide.aspx");
        }

        internal static string StopAndStartServer()
        {
            string message = "";
            try
            {
                MWS.StopServer();
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }

            Application.DoEvents();
            try
            {
                message = MWS.StartServer();
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            return message;
        }

        private void MenuItem4Click(object sender, EventArgs e)
        {
            WebConnect();
        }

        private void MenuItem5Click(object sender, EventArgs e)
        {
            StartBrowser(Website + "/");
        }

        private void MenuItem10Click(object sender, EventArgs e)
        {
            CheckForUpdates(false);
        }

        private void CheckForUpdates(bool suppressMessages)
        {
            string version = "";
            try
            {
                version = WsWrapper.ProductLatestVersion(11);
                if (version == LocRm.GetString("iSpyDown"))
                {
                    throw new Exception("down");
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
                if (!suppressMessages)
                    MessageBox.Show(LocRm.GetString("CheckUpdateError"), LocRm.GetString("Error"));
            }
            if (version != "" && version != LocRm.GetString("iSpyDown"))
            {
                var verThis = new Version(Application.ProductVersion);
                var verLatest = new Version(version);
                if (verThis < verLatest)
                {
                    var nv = new NewVersionForm();
                    nv.ShowDialog(this);
                    nv.Dispose();
                }
                else
                {
                    if (!suppressMessages)
                        MessageBox.Show(LocRm.GetString("HaveLatest"), LocRm.GetString("Note"), MessageBoxButtons.OK);
                }
            }
        }

        private void MenuItem8Click(object sender, EventArgs e)
        {
            ShowSettings(0);
        }

        public void ShowSettings(int tabindex)
        {
            var settings = new SettingsForm {Owner = this, InitialTab = tabindex};
            if (settings.ShowDialog(this) == DialogResult.OK)
            {
                _pnlCameras.BackColor = Conf.MainColor.ToColor();
                notifyIcon1.Text = Conf.TrayIconText;
            }

            if (settings.ReloadResources)
            {
                RenderResources();
                LoadCommands();
            }
            AddressIPv4 = ""; //forces reload
            AddressIPv6 = "";
            settings.Dispose();
            SaveConfig();
            Refresh();
        }

        private void MenuItem11Click(object sender, EventArgs e)
        {
            using (var fb = new FeedbackForm())
            {
                fb.ShowDialog(this);
            }
        }

        private void MainFormResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                if (Conf.BalloonTips)
                {
                    if (Conf.BalloonTips)
                    {
                        notifyIcon1.BalloonTipText = LocRm.GetString("RunningInTaskBar");
                        notifyIcon1.ShowBalloonTip(1500);
                    }
                }
            }
            else
            {
                if (Conf.AutoLayout)
                    LayoutObjects(0, 0);
                if (!IsOnScreen(this))
                {
                    Location = new Point(0,0);
                }
            }
        }

        private void NotifyIcon1DoubleClick(object sender, EventArgs e)
        {
            ShowIfUnlocked();
        }

        public void ShowIfUnlocked()
        {
            if (Visible == false || WindowState == FormWindowState.Minimized)
            {
                if (Conf.Enable_Password_Protect)
                {
                    using (var cp = new CheckPasswordForm())
                    {
                        cp.ShowDialog(this);
                        if (cp.DialogResult == DialogResult.OK)
                        {
                            ShowForm(-1);
                        }
                    }
                }
                else
                {
                    ShowForm(-1);
                }
            }
            else
            {
                ShowForm(-1);
            }
        }

        private void MainFormFormClosing1(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.WindowsShutDown)
            {
                if (Conf.MinimiseOnClose && !Reallyclose)
                {
                    e.Cancel = true;
                    WindowState = FormWindowState.Minimized;
                    return;
                }
            }
            Exit();
        }

        private void Exit()
        {
            if (_houseKeepingTimer != null)
                _houseKeepingTimer.Stop();
            if (_updateTimer != null)
                _updateTimer.Stop();
            _shuttingDown = true;

            if (Conf.ShowMediaPanel)
                Conf.MediaPanelSize = splitContainer1.SplitterDistance+"x"+splitContainer2.SplitterDistance;

            if (Conf.BalloonTips)
            {
                if (Conf.BalloonTips)
                {
                    notifyIcon1.BalloonTipText = LocRm.GetString("ShuttingDown");
                    notifyIcon1.ShowBalloonTip(1500);
                }
            }
            _closing = true;

            try
            {
                SaveObjects("");
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            } 
            
            try
            {
                SaveConfig();
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }

            try
            {
                if (_talkSource != null)
                {
                    _talkSource.Stop();
                    _talkSource = null;
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            try
            {
                if (_talkTarget != null)
                {
                    _talkTarget.Stop();
                    _talkTarget = null;
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            try
            {
                RemoveObjects();
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            try
            {
                MWS.StopServer();
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
            try
            {
                Application.DoEvents();
                if (Conf.ServicesEnabled)
                {
                    if (WsWrapper.WebsiteLive)
                    {
                        try
                        {
                            WsWrapper.Disconnect();
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }

            try
            {
                File.WriteAllText(Program.AppDataPath + "exit.txt", "OK");
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }

            WriteLog();
        }

        

        private void ControlNotification(object sender, NotificationType e)
        {
            if (Conf.BalloonTips)
            {
                notifyIcon1.BalloonTipText = string.Format("{0}:{1}{2}", String.IsNullOrEmpty(e.OverrideMessage) ? LocRm.GetString(e.Type) : e.OverrideMessage, NL, e.Text);
                notifyIcon1.ShowBalloonTip(1500);
            }
        }

        

        private void NotifyIcon1BalloonTipClicked(object sender, EventArgs e)
        {
            ShowIfUnlocked();
        }

        

        public static string RandomString(int length)
        {
            var b = "";

            for (int i = 0; i < length; i++)
            {
                char ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * Random.NextDouble() + 65)));
                b+=ch;
            }
            return b;
        }

        private void SetNewStartPosition()
        {
            if (Conf.AutoLayout)
                LayoutObjects(0, 0);
        }

        private void VolumeControlRemoteCommand(object sender, VolumeLevel.ThreadSafeCommand e)
        {
            InvokeMethod i = DoInvoke;
            Invoke(i, new object[] {e.Command});
        }

        private void ConnectServices()
        {
            if (Conf.ServicesEnabled)
            {
                if (Conf.UseUPNP)
                {
                    NATControl.SetPorts(Conf.ServerPort, Conf.LANPort);
                }

                string[] result =
                    WsWrapper.TestConnection(Conf.WSUsername, Conf.WSPassword, Conf.Loopback);

                if (result.Length>0 && result[0] == "OK")
                {
                    WsWrapper.Connect();
                    NeedsSync = true;
                    EmailAddress = result[2];
                    MobileNumber = result[4];
                    Conf.Reseller = result[5];

                    Conf.ServicesEnabled = true;
                    Conf.Subscribed = (Convert.ToBoolean(result[1]));

                    UISync.Execute(() => Text = string.Format("iSpy v{0}", Application.ProductVersion));
                    if (Conf.WSUsername != "")
                    {
                        UISync.Execute(() => Text += string.Format(" ({0})", Conf.WSUsername));
                    }
                    if (Conf.Reseller != "")
                    {
                        UISync.Execute(() => Text += string.Format(" Powered by {0}", Conf.Reseller.Split('|')[0]));
                    }

                    if (result[3] == "")
                    {
                        LoopBack = Conf.Loopback;
                        WsWrapper.Connect(Conf.Loopback);
                    }
                    else
                    {
                        LoopBack = false;
                    }
                }
            }
            if (Conf.Enable_Update_Check && !SilentStartup)
            {
                UISync.Execute(() => CheckForUpdates(true));
            }
            SilentStartup = false;
        }

        

        private void SetInactiveToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                var cameraControl = ((CameraWindow) ContextTarget);
                cameraControl.Disable();
            }
            else
            {
                if (ContextTarget is VolumeLevel)
                {
                    var vf = ((VolumeLevel) ContextTarget);
                    vf.Disable();
                }
            }
        }

        private void EditToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                EditCamera(((CameraWindow) ContextTarget).Camobject);
            }
            if (ContextTarget is VolumeLevel)
            {
                EditMicrophone(((VolumeLevel) ContextTarget).Micobject);
            }
            if (ContextTarget is FloorPlanControl)
            {
                EditFloorplan(((FloorPlanControl) ContextTarget).Fpobject);
            }
        }

        private void DeleteToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                RemoveCamera((CameraWindow) ContextTarget, true);
            }
            if (ContextTarget is VolumeLevel)
            {
                RemoveMicrophone((VolumeLevel) ContextTarget, true);
            }
            if (ContextTarget is FloorPlanControl)
            {
                RemoveFloorplan((FloorPlanControl) ContextTarget, true);
            }
        }


        private void ToolStripButton4Click(object sender, EventArgs e)
        {
            ShowSettings(0);
        }

        public static void GoSubscribe()
        {
            OpenUrl(Website + "/subscribe.aspx");
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception)
            {
                try
                {
                    var p = new Process {StartInfo = {FileName = DefaultBrowser, Arguments = url}};
                    p.Start();
                }
                catch (Exception ex2)
                {
                    LogExceptionToFile(ex2);
                }
            }
        }

        private static string DefaultBrowser
        {
            get
            {
                if (!String.IsNullOrEmpty(_browser))
                    return _browser;

                _browser = string.Empty;
                RegistryKey key = null;
                try
                {
                    key = Registry.ClassesRoot.OpenSubKey(@"HTTP\shell\open\command", false);

                    //trim off quotes
                    if (key != null) _browser = key.GetValue(null).ToString().ToLower().Replace("\"", "");
                    if (!_browser.EndsWith(".exe"))
                    {
                        _browser = _browser.Substring(0, _browser.LastIndexOf(".exe", StringComparison.Ordinal) + 4);
                    }
                }
                finally
                {
                    if (key != null) key.Close();
                }
                return _browser;
            }
        }

        private void ActivateToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                var cameraControl = ((CameraWindow) ContextTarget);
                cameraControl.Enable();
            }
            else
            {
                if (ContextTarget is VolumeLevel)
                {
                    var vf = ((VolumeLevel) ContextTarget);
                    vf.Enable();
                }
            }
        }

        private void WebsiteToolstripMenuItemClick(object sender, EventArgs e)
        {
            StartBrowser(Website + "/");
        }

        private void HelpToolstripMenuItemClick(object sender, EventArgs e)
        {
            StartBrowser(Website + "/userguide.aspx");
        }

        private void ShowToolstripMenuItemClick(object sender, EventArgs e)
        {
            ShowForm(-1);
        }

        public void ShowForm(double opacity)
        {
            Activate();
            Visible = true;
            if (WindowState == FormWindowState.Minimized)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }
            if (opacity > -1)
                Opacity = opacity;
            TopMost = true;
            TopMost = false; //need to force a switch to move above other forms
            TopMost = Conf.AlwaysOnTop;
            BringToFront();
            Focus();
        }

        private void UnlockToolstripMenuItemClick(object sender, EventArgs e)
        {
            ShowUnlock();
        }

        private void ShowUnlock()
        {
            var cp = new CheckPasswordForm();
            cp.ShowDialog(this);
            if (cp.DialogResult == DialogResult.OK)
            {
                Activate();
                Visible = true;
                if (WindowState == FormWindowState.Minimized)
                {
                    Show();
                    WindowState = FormWindowState.Normal;
                }
                Focus();
            }
            cp.Dispose();
        }

        private void NotifyIcon1Click(object sender, EventArgs e)
        {
        }

        private void AddCameraToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddCamera(3);
        }

        private void AddMicrophoneToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddMicrophone(0);
        }

        private void CtxtMainFormOpening(object sender, CancelEventArgs e)
        {
            if (ctxtMnu.Visible || ctxtPlayer.Visible)
                e.Cancel = true;
        }


        public static void StartBrowser(string url)
        {
            if (url != "")
                OpenUrl(url);
        }

        private void ExitToolStripMenuItemClick(object sender, EventArgs e)
        {
            Reallyclose = true;
            Close();
        }

        private void MenuItem3Click(object sender, EventArgs e)
        {
            Connect(false);
        }

        private void MenuItem18Click(object sender, EventArgs e)
        {
            if (
                MessageBox.Show(LocRm.GetString("AreYouSure"), LocRm.GetString("Confirm"), MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Warning) == DialogResult.Cancel)
                return;
            string loc = Conf.MediaDirectory + "audio\\";

            if (Directory.Exists(loc))
            {

                string[] files = Directory.GetFiles(loc, "*.*", SearchOption.AllDirectories);
                foreach (string t in files)
                {
                    try
                    {

                        FileOperations.Delete(t);
                    }
                    catch
                    {
                    }
                }
            }
            loc = Conf.MediaDirectory + "video\\";
            if (Directory.Exists(loc))
            {
                string[] files = Directory.GetFiles(loc, "*.*", SearchOption.AllDirectories);
                foreach (string t in files)
                {
                    try
                    {
                        FileOperations.Delete(t);
                    }
                    catch
                    {
                    }
                }
            }
            foreach (objectsCamera oc in Cameras)
            {
                CameraWindow occ = GetCameraWindow(oc.id);
                if (occ != null)
                {
                    occ.FileList.Clear();
                }
            }
            foreach (objectsMicrophone om in Microphones)
            {
                VolumeLevel omc = GetMicrophone(om.id);
                if (omc != null)
                {
                    omc.FileList.Clear();
                }
            }
            LoadPreviews();
            MessageBox.Show(LocRm.GetString("FilesDeleted"), LocRm.GetString("Note"));
        }

        private void MenuItem20Click(object sender, EventArgs e)
        {
            ShowLogFile();
        }

        private void ShowLogFile()
        {
            Process.Start(Program.AppDataPath + "log_" + NextLog + ".htm");
        }

        private void ResetSizeToolStripMenuItemClick(object sender, EventArgs e)
        {
            Minimize(ContextTarget, true);
        }

        private void Minimize(object obj, bool tocontents)
        {
            if (obj == null)
                return;
            if (obj is CameraWindow)
            {
                var cw = (CameraWindow) obj;
                var r = cw.RestoreRect;
                if (r != Rectangle.Empty && !tocontents)
                {
                    cw.Location = r.Location;
                    cw.Height = r.Height;
                    cw.Width = r.Width;
                }
                else
                {
                    if (cw.Camera != null && !cw.Camera.LastFrameNull)
                    {
                        cw.Width = cw.Camera.LastFrameUnmanaged.Width + 2;
                        cw.Height = cw.Camera.LastFrameUnmanaged.Height + 26;
                    }
                    else
                    {
                        cw.Width = 322;
                        cw.Height = 266;
                    }
                }
                cw.Invalidate();
            }

            if (obj is VolumeLevel)
            {
                var cw = (VolumeLevel)obj;
                var r = cw.RestoreRect;
                if (r != Rectangle.Empty && !tocontents)
                {
                    cw.Location = r.Location;
                    cw.Height = r.Height;
                    cw.Width = r.Width;
                }
                else
                {
                    cw.Width = 160;
                    cw.Height = 40;
                }
                cw.Invalidate();
            }

            if (obj is FloorPlanControl)
            {
                var fp = (FloorPlanControl) obj;
                var r = fp.RestoreRect;
                if (r != Rectangle.Empty && !tocontents)
                {
                    fp.Location = r.Location;
                    fp.Height = r.Height;
                    fp.Width = r.Width;
                    fp.Invalidate();
                }
                else
                {
                    if (fp.ImgPlan != null)
                    {
                        fp.Width = fp.ImgPlan.Width + 2;
                        fp.Height = fp.ImgPlan.Height + 26;
                    }
                    else
                    {
                        fp.Width = 322;
                        fp.Height = 266;
                    }
                }
            }
        }

        private void SettingsToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowSettings(0);
        }


        private void MenuItem19Click(object sender, EventArgs e)
        {
            if (Cameras.Count == 0 && Microphones.Count == 0)
            {
                MessageBox.Show(LocRm.GetString("NothingToExport"), LocRm.GetString("Error"));
                return;
            }

            var saveFileDialog = new SaveFileDialog
                                     {
                                         InitialDirectory = _lastPath,
                                         Filter = "iSpy Files (*.ispy)|*.ispy|XML Files (*.xml)|*.xml"
                                     };

            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                string fileName = saveFileDialog.FileName;

                if (fileName.Trim() != "")
                {
                    SaveObjects(fileName);
                    try
                    {
                        var fi = new FileInfo(fileName);
                        _lastPath = fi.DirectoryName;
                    }
                    catch
                    {
                    }
                }
            }
            saveFileDialog.Dispose();
        }


        private void MenuItem21Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.InitialDirectory = _lastPath;
                ofd.Filter = "iSpy Files (*.ispy)|*.ispy|XML Files (*.xml)|*.xml";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = ofd.FileName;
                    try
                    {
                        var fi = new FileInfo(fileName);
                        _lastPath = fi.DirectoryName;
                    }
                    catch
                    {
                    }


                    if (fileName.Trim() != "")
                    {
                        LoadObjectList(fileName.Trim());
                    }
                }
            }
        }

        private void ToolStripMenuItem1Click(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                //id = ((CameraWindow) ContextTarget).Camobject.id.ToString();
                string url = Webserver + "/watch_new.aspx";
                if (WsWrapper.WebsiteLive && Conf.ServicesEnabled)
                {
                    OpenUrl(url);
                }
                else
                    Connect(url, false);
            }
            
            if (ContextTarget is VolumeLevel)
            {
                    //id = ((VolumeLevel) ContextTarget).Micobject.id.ToString();
                    string url = Webserver + "/watch_new.aspx";
                    if (WsWrapper.WebsiteLive && Conf.ServicesEnabled)
                    {
                        OpenUrl(url);
                    }
                    else
                        Connect(url, false);
                }
               
            if (ContextTarget is FloorPlanControl)
                {
                        string url = Webserver + "/watch_new.aspx";
                        if (WsWrapper.WebsiteLive && Conf.ServicesEnabled)
                        {
                            OpenUrl(url);
                        }
                        else
                            Connect(url, false);
                    }

                }

        public void Connect(bool silent)
        {
            Connect(Webserver + "/watch_new.aspx", silent);
        }

        public void Connect(string successUrl, bool silent)
        {
            if (!MWS.Running)
            {
                string message = StopAndStartServer();
                if (message != "")
                {
                    if (!silent)
                        MessageBox.Show(this, message);
                    return;
                }
            }
            if (WsWrapper.WebsiteLive)
            {
                if (Conf.WSUsername != null && Conf.WSUsername.Trim() != "")
                {
                    if (Conf.UseUPNP)
                    {
                        NATControl.SetPorts(Conf.ServerPort, Conf.LANPort);
                    }
                    WsWrapper.Connect();
                    WsWrapper.ForceSync();
                    if (WsWrapper.WebsiteLive)
                    {
                        if (successUrl != "")
                            StartBrowser(successUrl);
                        return;
                    }
                    if (!silent && !_shuttingDown)
                        LogMessageToFile(LocRm.GetString("WebsiteDown"));
                    return;
                }
                var ws = new WebservicesForm();
                ws.ShowDialog(this);
                if (ws.EmailAddress != "")
                    EmailAddress = ws.EmailAddress;
                if (ws.DialogResult == DialogResult.Yes || ws.DialogResult == DialogResult.No)
                {
                    ws.Dispose();
                    Connect(successUrl, silent);
                    return;
                }
                ws.Dispose();
            }
            else
            {
                LogMessageToFile(LocRm.GetString("WebsiteDown"));
            }
        }

        private void MenuItem7Click(object sender, EventArgs e)
        {
            string foldername = Conf.MediaDirectory + "video\\";
            if (!foldername.EndsWith(@"\"))
                foldername += @"\";
            Process.Start(foldername);
        }

        private void MenuItem23Click(object sender, EventArgs e)
        {
            string foldername = Conf.MediaDirectory + "audio\\";
            if (!foldername.EndsWith(@"\"))
                foldername += @"\";
            Process.Start(foldername);
        }

        private void MenuItem25Click(object sender, EventArgs e)
        {
            ViewMobile();
        }


        private void MainFormHelpButtonClicked(object sender, CancelEventArgs e)
        {
            OpenUrl(Website + "/userguide.aspx");
        }

        private void menuItem21_Click(object sender, EventArgs e)
        {
            LayoutOptimised();
        }

        private void ShowISpy10PercentOpacityToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowForm(.1);
        }

        private void ShowISpy30OpacityToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowForm(.3);
        }

        private void ShowISpy100PercentOpacityToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowForm(1);
        }

        private void CtxtTaskbarOpening(object sender, CancelEventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                if (Conf.Enable_Password_Protect)
                {
                    _unlockToolstripMenuItem.Visible = true;
                    _showToolstripMenuItem.Visible =
                        _showISpy10PercentOpacityToolStripMenuItem.Visible =
                        _showISpy30OpacityToolStripMenuItem.Visible =
                        _showISpy100PercentOpacityToolStripMenuItem.Visible = false;
                    _exitToolStripMenuItem.Visible = false;
                    _websiteToolstripMenuItem.Visible = false;
                    _helpToolstripMenuItem.Visible = false;
                    _switchAllOffToolStripMenuItem.Visible = false;
                    _switchAllOnToolStripMenuItem.Visible = false;
                }
                else
                {
                    _unlockToolstripMenuItem.Visible = false;
                    _showToolstripMenuItem.Visible =
                        _showISpy10PercentOpacityToolStripMenuItem.Visible =
                        _showISpy30OpacityToolStripMenuItem.Visible =
                        _showISpy100PercentOpacityToolStripMenuItem.Visible = true;
                    _exitToolStripMenuItem.Visible = true;
                    _websiteToolstripMenuItem.Visible = true;
                    _helpToolstripMenuItem.Visible = true;
                    _switchAllOffToolStripMenuItem.Visible = true;
                    _switchAllOnToolStripMenuItem.Visible = true;
                }
            }
            else
            {
                _showToolstripMenuItem.Visible = false;
                _showISpy10PercentOpacityToolStripMenuItem.Visible =
                    _showISpy30OpacityToolStripMenuItem.Visible =
                    _showISpy100PercentOpacityToolStripMenuItem.Visible = true;
                _unlockToolstripMenuItem.Visible = false;
                _exitToolStripMenuItem.Visible = true;
                _websiteToolstripMenuItem.Visible = true;
                _helpToolstripMenuItem.Visible = true;
                _switchAllOffToolStripMenuItem.Visible = true;
                _switchAllOnToolStripMenuItem.Visible = true;
            }
        }

       

        private void MenuItem26Click(object sender, EventArgs e)
        {
            OpenUrl(Website + "/donate.aspx");
        }

        private void RecordNowToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                var cameraControl = ((CameraWindow) ContextTarget);
                cameraControl.RecordSwitch(!cameraControl.Recording);
            }
            
            if (ContextTarget is VolumeLevel)
            {
                    var volumeControl = ((VolumeLevel) ContextTarget);
                    volumeControl.RecordSwitch(!volumeControl.Recording);
                }
            }

        private void ShowFilesToolStripMenuItemClick(object sender, EventArgs e)
        {
            string foldername;
            if (ContextTarget is CameraWindow)
            {
                var cw = ((CameraWindow) ContextTarget);
                foldername = Conf.MediaDirectory + "video\\" + cw.Camobject.directory +
                                    "\\";
                if (!foldername.EndsWith(@"\"))
                    foldername += @"\";
                Process.Start(foldername);
                cw.Camobject.newrecordingcount = 0;
                return;
            }
            
            if (ContextTarget is VolumeLevel)
            {
                    var vl = ((VolumeLevel) ContextTarget);
                foldername = Conf.MediaDirectory + "audio\\" + vl.Micobject.directory +
                                        "\\";
                    if (!foldername.EndsWith(@"\"))
                        foldername += @"\";
                    Process.Start(foldername);
                    vl.Micobject.newrecordingcount = 0;
                return;
                }
                
            foldername = Conf.MediaDirectory;
                    Process.Start(foldername);
                }

        private void ViewMediaOnAMobileDeviceToolStripMenuItemClick(object sender, EventArgs e)
        {
            ViewMobile();
        }

        private void ViewMobile()
        {
            if (WsWrapper.WebsiteLive && Conf.ServicesEnabled)
            {
                OpenUrl(Webserver + "/mobile/");
            }
            else
                WebConnect();
        }

        private void AddFloorPlanToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddFloorPlan();
        }

        private void ListenToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is VolumeLevel)
            {
                var vf = ((VolumeLevel) ContextTarget);
                vf.Listening = !vf.Listening;
            }
        }

        private void MenuItem31Click(object sender, EventArgs e)
        {
            if (
                MessageBox.Show(LocRm.GetString("AreYouSure"), LocRm.GetString("Confirm"), MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Warning) == DialogResult.Cancel)
                return;
            RemoveObjects();
        }

        private void MenuItem34Click(object sender, EventArgs e)
        {
        }

        

        private void MenuItem33Click(object sender, EventArgs e)
        {
        }

        private void ToolStripButton8Click1(object sender, EventArgs e)
        {
            ShowRemoteCommands();
        }

        private void MenuItem35Click(object sender, EventArgs e)
        {
            ShowRemoteCommands();
        }

        private void ToolStrip1ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
        }

        private void RemoteCommandsToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowRemoteCommands();
        }       

        private void MenuItem37Click(object sender, EventArgs e)
        {
            MessageBox.Show(LocRm.GetString("EditInstruct"), LocRm.GetString("Note"));
        }

        private void PositionToolStripMenuItemClick(object sender, EventArgs e)
        {
            var p = (PictureBox) ContextTarget;
            int w = p.Width;
            int h = p.Height;
            int x = p.Location.X;
            int y = p.Location.Y;

            var le = new LayoutEditorForm {X = x, Y = y, W = w, H = h};


            if (le.ShowDialog(this) == DialogResult.OK)
            {
                PositionPanel(p, new Point(le.X, le.Y), le.W, le.H);
            }
            le.Dispose();
        }

        private static void PositionPanel(PictureBox p, Point xy, int w, int h)
        {
            p.Width = w;
            p.Height = h;
            p.Location = new Point(xy.X, xy.Y);
        }

        private void MenuItem38Click(object sender, EventArgs e)
        {
            StartBrowser(Website + "/producthistory.aspx?productid=11");
        }

        private void MenuItem39Click(object sender, EventArgs e)
        {
        }

        private void TakePhotoToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                var cameraControl = ((CameraWindow) ContextTarget);
                string fn = cameraControl.SaveFrame();
                if (fn != "")
                    OpenUrl(fn);
                //OpenUrl("http://" + IPAddress + ":" + Conf.LANPort + "/livefeed?oid=" + cameraControl.Camobject.id + "&r=" + Random.NextDouble() + "&full=1&auth=" + Identifier);
            }
        }

        private void ToolStripDropDownButton1Click(object sender, EventArgs e)
        {
        }

        private void ThruWebsiteToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (WsWrapper.WebsiteLive && Conf.ServicesEnabled)
            {
                OpenUrl(Webserver + "/watch_new.aspx");
            }
            else
                WebConnect();
        }

        private void OnMobileDevicesToolStripMenuItemClick(object sender, EventArgs e)
        {
            ViewMobile();
        }

        private void LocalCameraToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddCamera(3);
        }

        private void IpCameraToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddCamera(1);
        }

        private void MicrophoneToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddMicrophone(0);
        }

        private void FloorPlanToolStripMenuItemClick(object sender, EventArgs e)
        {
            AddFloorPlan();
        }

        private void MenuItem12Click(object sender, EventArgs e)
        {
            //+26 height for control bar
            LayoutObjects(164, 146);
        }

        private void MenuItem14Click(object sender, EventArgs e)
        {
            LayoutObjects(324, 266);
        }

        private void MenuItem29Click1(object sender, EventArgs e)
        {
            LayoutObjects(0, 0);
        }

        private void ToolStripButton1Click1(object sender, EventArgs e)
        {
            WebConnect();
        }

        private void WebConnect()
        {
            var ws = new WebservicesForm();
            ws.ShowDialog(this);
            if (ws.EmailAddress != "")
            {
                EmailAddress = ws.EmailAddress;
                MobileNumber = ws.MobileNumber;
            }
            if (ws.DialogResult == DialogResult.Yes)
            {
                Connect(false);
            }
            ws.Dispose();
            Text = string.Format("iSpy v{0}", Application.ProductVersion);
            if (Conf.WSUsername != "")
            {
                Text += string.Format(" ({0})", Conf.WSUsername);
            }
        }

        private void MenuItem17Click(object sender, EventArgs e)
        {
        }

        private void ResetRecordingCounterToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                var cw = ((CameraWindow) ContextTarget);
                cw.Camobject.newrecordingcount = 0;
                cw.Custom = false;
                if (cw.VolumeControl != null)
                {
                    cw.VolumeControl.Micobject.newrecordingcount = 0;
                    cw.VolumeControl.Invalidate();
                }
                cw.Invalidate();
            }
            if (ContextTarget is VolumeLevel)
            {
                var vw = ((VolumeLevel) ContextTarget);
                vw.Micobject.newrecordingcount = 0;
                if (vw.Paired)
                {
                    objectsCamera oc = Cameras.SingleOrDefault(p => p.settings.micpair == vw.Micobject.id);
                    if (oc != null)
                    {
                        CameraWindow cw = GetCameraWindow(oc.id);
                        cw.Camobject.newrecordingcount = 0;
                        cw.Invalidate();
                    }
                }
                vw.Invalidate();
            }
        }

        private void MenuItem15Click(object sender, EventArgs e)
        {
            foreach (Control c in _pnlCameras.Controls)
            {
                if (c is CameraWindow)
                {
                    var cameraControl = (CameraWindow) c;
                    cameraControl.Camobject.newrecordingcount = 0;
                    cameraControl.Invalidate();
                }
                if (c is VolumeLevel)
                {
                    var volumeControl = (VolumeLevel) c;
                    volumeControl.Micobject.newrecordingcount = 0;
                    volumeControl.Invalidate();
                }
            }
        }

        private void SwitchAllOnToolStripMenuItemClick(object sender, EventArgs e)
        {
            SwitchObjects(false, true);
        }

        private void SwitchAllOffToolStripMenuItemClick(object sender, EventArgs e)
        {
            SwitchObjects(false, false);
        }

        private void MenuItem22Click1(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
                          {
                              InitialDirectory = Program.AppDataPath,
                              Filter = "iSpy Log Files (*.htm)|*.htm"
                          };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            string fileName = ofd.FileName;

            if (fileName.Trim() != "")
            {
                Process.Start(ofd.FileName);
            }
        }

        private void USbCamerasAndMicrophonesOnOtherToolStripMenuItemClick(object sender, EventArgs e)
        {
            OpenUrl(Website + "/download_ispyserver.aspx");
        }

        private void MenuItem24Click(object sender, EventArgs e)
        {
            SwitchObjects(false, true);
        }

        private void MenuItem40Click(object sender, EventArgs e)
        {
            SwitchObjects(false, false);
        }

        private void MenuItem41Click(object sender, EventArgs e)
        {
            SwitchObjects(true, false);
        }

        private void MenuItem28Click1(object sender, EventArgs e)
        {
            SwitchObjects(true, true);
        }

        private void MenuItem24Click1(object sender, EventArgs e)
        {
            ApplySchedule();
        }

        public void ApplySchedule()
        {
            foreach (objectsCamera cam in _cameras)
            {
                if (cam.schedule.active)
                {
                    CameraWindow cw = GetCamera(cam.id);
                    cw.ApplySchedule();
                }
            }


            foreach (objectsMicrophone mic in _microphones)
            {
                if (mic.schedule.active)
                {
                    VolumeLevel vl = GetMicrophone(mic.id);
                    vl.ApplySchedule();
                }
            }
        }

        private void ApplyScheduleToolStripMenuItemClick1(object sender, EventArgs e)
        {
            ApplySchedule();
        }

        private void ApplyScheduleToolStripMenuItem1Click(object sender, EventArgs e)
        {
            if (ContextTarget is CameraWindow)
            {
                var cameraControl = ((CameraWindow) ContextTarget);
                cameraControl.ApplySchedule();
            }
            if (ContextTarget is VolumeLevel)
            {
                    var vf = ((VolumeLevel) ContextTarget);
                    vf.ApplySchedule();
                }
            }

        private void MenuItem24Click2(object sender, EventArgs e)
        {
            ShowGettingStarted();
        }

        private void ShowGettingStarted()
        {
            var gs = new GettingStartedForm();
            gs.Closed += _gs_Closed;
            gs.Show(this);
            gs.Activate();
        }

        private void _gs_Closed(object sender, EventArgs e)
        {
            if (((GettingStartedForm) sender).LangChanged)
            {
                RenderResources();
                LoadCommands();
                Refresh();
            }
        }

        private void MenuItem28Click2(object sender, EventArgs e)
        {
            LayoutObjects(644, 506);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Reallyclose = true;
            Close();
        }


        private delegate void CloseDelegate();

        public void ExternalClose()
        {
            if (InvokeRequired)
            {
                Invoke(new CloseDelegate(ExternalClose));
                return;
            }
            Reallyclose = true;
            Close();
        }

        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Maximise(ContextTarget);
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            OpenUrl(Website + "/userguide.aspx#4");
        }

        private void inExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string foldername = Conf.MediaDirectory;
            if (!foldername.EndsWith(@"\"))
                foldername += @"\";
            Process.Start(foldername);
        }

        private void menuItem1_Click_1(object sender, EventArgs e)
        {
            LayoutObjects(-1, -1);
        }

        private class UISync
        {
            private static ISynchronizeInvoke _sync;

            public static void Init(ISynchronizeInvoke sync)
            {
                _sync = sync;
            }

            public static void Execute(Action action)
            {
                try
                {
                    _sync.BeginInvoke(action, null);
                }
                catch
                {
                }
            }
        }

        private bool _selectedall;

        private void llblSelectAll_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _selectedall = !_selectedall;
            lock (flowPreview.Controls)
            {
                foreach (PreviewBox pb in flowPreview.Controls)
                    pb.Selected = _selectedall;
                flowPreview.Invalidate(true);
            }
        }


       

        private void llblDelete_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DeleteSelectedMedia();
        }


        
        private void opacityToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowForm(.1);
        }

        private void opacityToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            ShowForm(.3);
        }

        private void opacityToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            ShowForm(1);
        }

        private void autoLayoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoLayoutToolStripMenuItem.Checked = !autoLayoutToolStripMenuItem.Checked;
            Conf.AutoLayout = autoLayoutToolStripMenuItem.Checked;
            if (Conf.AutoLayout)
                LayoutObjects(0, 0);
        }

        private void saveLayoutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveLayout();
            

        }

        private void resetLayoutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ResetLayout();
        }

        private void fullScreenToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MaxMin();
        }

        private void statusBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            statusBarToolStripMenuItem.Checked = menuItem4.Checked = !statusBarToolStripMenuItem.Checked;
            statusStrip1.Visible = statusBarToolStripMenuItem.Checked;

            Conf.ShowStatus = statusBarToolStripMenuItem.Checked;
        }

        private void fileMenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fileMenuToolStripMenuItem.Checked = menuItem5.Checked = !fileMenuToolStripMenuItem.Checked;
            Menu = !fileMenuToolStripMenuItem.Checked ? null : mainMenu;

            Conf.ShowFileMenu = fileMenuToolStripMenuItem.Checked;
        }

        private void toolStripToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripToolStripMenuItem.Checked = menuItem6.Checked = !toolStripToolStripMenuItem.Checked;
            toolStripMenu.Visible = toolStripToolStripMenuItem.Checked;
            Conf.ShowToolbar = toolStripToolStripMenuItem.Checked;
        }

        private void alwaysOnTopToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            alwaysOnTopToolStripMenuItem1.Checked = menuItem8.Checked = !alwaysOnTopToolStripMenuItem1.Checked;
            Conf.AlwaysOnTop = alwaysOnTopToolStripMenuItem1.Checked;
            TopMost = Conf.AlwaysOnTop;
        }

        private void mediaPaneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mediaPaneToolStripMenuItem.Checked = menuItem7.Checked = !mediaPaneToolStripMenuItem.Checked;
            Conf.ShowMediaPanel = mediaPaneToolStripMenuItem.Checked;
            ShowHideMediaPane();
        }

        private void ShowHideMediaPane()
        {
            if (Conf.ShowMediaPanel)
            {
                splitContainer1.Panel2Collapsed = false;
                splitContainer1.Panel2.Show();
            }
            else
            {
                splitContainer1.Panel2Collapsed = true;
                splitContainer1.Panel2.Hide();
            }
        }

        private void iPCameraWithWizardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddCamera(1, true);
        }

        private void menuItem13_Click(object sender, EventArgs e)
        {
            OpenUrl(PurchaseLink);
        }

        private void tsbPlugins_Click(object sender, EventArgs e)
        {
            OpenUrl("http://www.ispyconnect.com/plugins.aspx");
        }

        private void flowPreview_MouseEnter(object sender, EventArgs e)
        {
            flowPreview.Focus();
        }

        private void flowPreview_Click(object sender, EventArgs e)
        {
        }

        private void flCommands_MouseEnter(object sender, EventArgs e)
        {
            flCommands.Focus();
        }

        public void PTZToolUpdate(CameraWindow cw)
        {
            if (_ptzTool!=null)
            {
                _ptzTool.CameraControl = cw;
            }
        }

        

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.PageUp)
            {
                ProcessKey("previous_control");
            }

            if (e.KeyCode == Keys.PageDown)
            {
                ProcessKey("next_control");
            }

            if (e.KeyCode == Keys.P && (e.Control))
            {
                ProcessKey("play");
            }

            if (e.KeyCode == Keys.S && e.Control)
            {
                ProcessKey("stop");
            }

            if (e.KeyCode == Keys.R && e.Control)
            {
                ProcessKey("record");
            }

            if (e.KeyCode == Keys.Z && e.Control)
            {
                ProcessKey("zoom");
            }
            if (e.KeyCode==Keys.F4 && e.Alt)
            {
                ProcessKey("power");
            }
            if (e.KeyCode.ToString() == "D0")
            {
                MaximiseControl(10);
            }
            if (e.KeyCode.ToString() == "D1")
            {
                MaximiseControl(0);
            }
            if (e.KeyCode.ToString() == "D2")
            {
                MaximiseControl(1);
            }
            if (e.KeyCode.ToString() == "D3")
            {
                MaximiseControl(2);
            }
            if (e.KeyCode.ToString() == "D4")
            {
                MaximiseControl(3);
            }
            if (e.KeyCode.ToString() == "D5")
            {
                MaximiseControl(4);
            }
            if (e.KeyCode.ToString() == "D6")
            {
                MaximiseControl(5);
            }
            if (e.KeyCode.ToString() == "D7")
            {
                MaximiseControl(6);
            }
            if (e.KeyCode.ToString() == "D8")
            {
                MaximiseControl(7);
            }
            if (e.KeyCode.ToString() == "D9")
            {
                MaximiseControl(8);
            }
        }

        private void MaximiseControl(int index)
        {
            foreach(Control c in _pnlCameras.Controls)
            {
                if (c.Tag is int)
                {
                    if ((int)c.Tag == index)
                    {
                        Maximise(c, true);
                        c.Focus();
                        break;
                    }
                }
            }
        }

        private void menuItem14_Click(object sender, EventArgs e)
        {
            if (_vc != null)
            {
                _vc.Close();
                _vc = null;
            }
            else
                ShowViewController();
        }

        private void ShowViewController()
        {
            if (_vc == null)
            {
                _vc = new ViewControllerForm(_pnlCameras);
                if (_pnlCameras.Height > 0)
                {
                    double ar = Convert.ToDouble(_pnlCameras.Height)/Convert.ToDouble(_pnlCameras.Width);
                    _vc.Width = 180;
                    _vc.Height = Convert.ToInt32(ar*_vc.Width);
                }
                _vc.TopMost = true;

                _vc.Show();
                _vc.Closed += _vc_Closed;
                viewControllerToolStripMenuItem.Checked = menuItem14.Checked = Conf.ViewController = true;
            }
            else
            {
                _vc.Show();
            }
        }

        private void _vc_Closed(object sender, EventArgs e)
        {
            _vc = null;
            viewControllerToolStripMenuItem.Checked = menuItem14.Checked = Conf.ViewController = false;
        }

        private void _pnlCameras_Scroll(object sender, ScrollEventArgs e)
        {
            if (_vc != null)
                _vc.Redraw();
        }

        private void _toolStripDropDownButton2_Click(object sender, EventArgs e)
        {

        }

        private enum LayoutModes
        {
            bottom,
            left,
            right
        };

        private void menuItem16_Click(object sender, EventArgs e)
        {
            Conf.LayoutMode = (int)LayoutModes.bottom;
            Arrange(true);
        }

        private void menuItem17_Click(object sender, EventArgs e)
        {
            Conf.LayoutMode = (int)LayoutModes.left;
            Arrange(true);
        }

        private void menuItem19_Click(object sender, EventArgs e)
        {
            Conf.LayoutMode = (int)LayoutModes.right;
            Arrange(true);
        }

        private void Arrange(bool ShowIfHidden)
        {
            if (!Conf.ShowMediaPanel)
            {
                if (ShowIfHidden)
                {
                    mediaPaneToolStripMenuItem.Checked = menuItem7.Checked = true;
                    Conf.ShowMediaPanel = true;
                    ShowHideMediaPane();
                }
                else
                    return;
            }

            SuspendLayout();
            try {
                var lm = (LayoutModes) Conf.LayoutMode;

            
                switch (lm)
                {
                    case LayoutModes.bottom:
                        splitContainer1.Orientation = Orientation.Horizontal;
                        splitContainer1.RightToLeft = RightToLeft.No;

                        splitContainer2.Orientation = Orientation.Vertical;
                        splitContainer2.RightToLeft = RightToLeft.No;

                        splitContainer1.SplitterDistance = splitContainer1.Height-200;
                        splitContainer2.SplitterDistance = splitContainer2.Width - 200;
                        break;
                    case LayoutModes.left:
                        splitContainer1.Orientation = Orientation.Vertical;
                        splitContainer1.RightToLeft = RightToLeft.Yes;

                        splitContainer2.Orientation = Orientation.Horizontal;
                        splitContainer2.RightToLeft = RightToLeft.No;

                        splitContainer1.SplitterDistance = splitContainer1.Width - 200;
                        splitContainer2.SplitterDistance = splitContainer2.Height - 200;
                        break;
                    case LayoutModes.right:
                        splitContainer1.Orientation = Orientation.Vertical;
                        splitContainer1.RightToLeft = RightToLeft.No;

                        splitContainer2.Orientation = Orientation.Horizontal;
                        splitContainer2.RightToLeft = RightToLeft.No;

                        splitContainer1.SplitterDistance = splitContainer1.Width - 200;
                        splitContainer2.SplitterDistance = splitContainer2.Height - 200;

                        break;

                }
            }
            catch {}
            ResumeLayout(true);
        }

        private void flowPreview_ControlRemoved(object sender, ControlEventArgs e)
        {
            
        }

        private void menuItem18_Click(object sender, EventArgs e)
        {
            ShowHidePTZTool();
        }

        private void pTZControllerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHidePTZTool();
        }

        private void ShowHidePTZTool()
        {
            bool bShow = true;
            if (_ptzTool != null)
            {
                _ptzTool.Close();
                _ptzTool.Dispose();
                _ptzTool = null;
                bShow = false;
            }
            else
            {
                _ptzTool = new PTZControllerForm { Owner = this };
                _ptzTool.Show(this);
                _ptzTool.Closing += _ptzTool_Closing;
                _ptzTool.CameraControl = null;
                for (int i = 0; i < _pnlCameras.Controls.Count; i++)
                {
                    Control c = _pnlCameras.Controls[i];
                    if (c.Focused && c is CameraWindow)
                    {
                        _ptzTool.CameraControl = (CameraWindow)c;
                        break;
                    }
                }
            }
            pTZControllerToolStripMenuItem.Checked = menuItem18.Checked = pTZControllerToolStripMenuItem1.Checked = bShow;
            Conf.ShowPTZController = bShow;
        }

        void _ptzTool_Closing(object sender, CancelEventArgs e)
        {
            pTZControllerToolStripMenuItem.Checked = menuItem18.Checked = pTZControllerToolStripMenuItem1.Checked = false;
            Conf.ShowPTZController = false;
        }

        private IAudioSource _talkSource;
        private ITalkTarget _talkTarget;
        internal CameraWindow TalkCamera;

        public void TalkTo(CameraWindow cw, bool talk)
        {
            if (_talkSource != null)
            {
                _talkSource.Stop();
                _talkSource = null;
            }
            if (_talkTarget != null)
            {
                _talkTarget.Stop();
                _talkTarget = null;
            }

            if (!talk)
            {
                if (cw.VolumeControl != null)
                {
                    cw.VolumeControl.Listening = false;
                }
                return;
            }
            Application.DoEvents();
            TalkCamera = cw;
            _talkSource = new TalkDeviceStream(Conf.TalkMic) { RecordingFormat = new WaveFormat(8000, 16, 1) };
            _talkSource.AudioSourceError += _talkSource_AudioSourceError;

            if (!_talkSource.IsRunning)
                _talkSource.Start();           

            switch (cw.Camobject.settings.audiomodel)
            {
                default:
                    _talkTarget = new TalkFoscam(cw.Camobject.settings.audioip, cw.Camobject.settings.audioport, cw.Camobject.settings.audiousername, cw.Camobject.settings.audiopassword, _talkSource);
                    break;
                case "iSpyServer":
                    _talkTarget = new TalkiSpyServer(cw.Camobject.settings.audioip, cw.Camobject.settings.audioport, _talkSource);
                    break;
                case "NetworkKinect":
                    _talkTarget = new TalkNetworkKinect(cw.Camobject.settings.audioip, cw.Camobject.settings.audioport, _talkSource);
                    break;
                case "Axis":
                    _talkTarget = new TalkAxis(cw.Camobject.settings.audioip, cw.Camobject.settings.audioport, cw.Camobject.settings.audiousername, cw.Camobject.settings.audiopassword, _talkSource);
                    break;
            }
            
            _talkTarget.TalkStopped += TalkTargetTalkStopped;
            _talkTarget.Start();

            //auto listen
            if (cw.VolumeControl != null)
            {
                cw.VolumeControl.Listening = true;
            }
            
        }

        void _talkSource_AudioSourceError(object sender, iSpy.Common.Audio.AudioSourceErrorEventArgs eventArgs)
        {
            LogErrorToFile(eventArgs.Description);
        }

        void TalkTargetTalkStopped(object sender, EventArgs e)
        {
            if (TalkCamera!=null)
            {
                TalkCamera.Talking = false;
            }
        }

        private void pTZToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void viewControllerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_vc != null)
            {
                _vc.Close();
                _vc = null;
            }
            else
                ShowViewController();
        }

        private void menuItem22_Click(object sender, EventArgs e)
        {
            Conf.LockLayout = !Conf.LockLayout;
            menuItem22.Checked = Conf.LockLayout;
        }

        private void iSpyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ((PreviewBox) ContextTarget).PlayMedia(1);
        }

        private void defaultPlayerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ((PreviewBox)ContextTarget).PlayMedia(2);
        }

        private void websiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ((PreviewBox)ContextTarget).PlayMedia(0);
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var pb = ((PreviewBox) ContextTarget);
            RemovePreviewBox(pb);
        }

        private void pTZControllerToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowHidePTZTool();
        }

        private void llblRefresh_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LoadPreviews();
        }

        private void showInFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var pb = ((PreviewBox)ContextTarget);

            string argument = @"/select, " + pb.FileName;
            Process.Start("explorer.exe", argument);
        }

        private void otherVideoSourceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddCamera(4);
        }

        private void videoFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddCamera(2);
        }

        private void uploadToYouTubeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ((PreviewBox)ContextTarget).Upload(false);
        }

        private void uploadToYouTubePublicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ((PreviewBox)ContextTarget).Upload(true);
            
        }

        
        private void saveToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var pb = ((PreviewBox)ContextTarget);
                var fi = new FileInfo(pb.FileName);

                if (fbdSaveTo.ShowDialog(this) == DialogResult.OK)
                {
                    File.Copy(pb.FileName, fbdSaveTo.SelectedPath + @"\" + fi.Name);                
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
        }

        private void menuItem25_Click(object sender, EventArgs e)
        {
            
        }

        private void menuItem26_Click(object sender, EventArgs e)
        {
            ShowGrid("1x1");
        }      

        private void ShowGrid(string cfg)
        {
            var gv = new GridView(this,cfg);
            gv.Show();
        }

        private void menuItem27_Click(object sender, EventArgs e)
        {
            ShowGrid("2x2");
        }

        private void menuItem28_Click(object sender, EventArgs e)
        {
            ShowGrid("3x3");
        }

        private void menuItem29_Click(object sender, EventArgs e)
        {
            ShowGrid("4x4");
        }

        private void menuItem30_Click(object sender, EventArgs e)
        {
            ShowGrid("5x5");
        }

        private void menuItem31_Click(object sender, EventArgs e)
        {
            var gvc = new GridViewCustomForm();
            gvc.ShowDialog(this);
            if (gvc.DialogResult== DialogResult.OK)
            {
                ShowGrid(gvc.Cols+"x"+gvc.Rows);
            }
            gvc.Dispose();
        }

        private void _tsslStats_Click(object sender, EventArgs e)
        {
            if (!MWS.Running)
            {
                ShowLogFile();
            }
            else
            {
                if (WsWrapper.WebsiteLive)
                {
                    if (Conf.ServicesEnabled)
                    {
                        OpenUrl(!Conf.Subscribed
                                    ? "http://www.ispyconnect.com/subscribe.aspx"
                                    : "http://www.ispyconnect.com/watch_new.aspx");
                    }
                    else
                    {
                        OpenUrl("http://www.ispyconnect.com");
                    }
                }    
            }
            
        }


    }

    
    
}