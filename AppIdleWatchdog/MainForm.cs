
using IVSoftware.Portable;
using IVSoftware.Portable.Disposable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AppIdleWatchdog
{
    public partial class MainForm : Form, IMessageFilter
    {
        private static class MessageBox
        {
            private static MethodInfo[] _showMethods;
            static MessageBox()
            {
                _showMethods = typeof(System.Windows.Forms.MessageBox)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(_ => _.Name == "Show")
                 .ToArray();
                { }
            }
            public static DialogResult Show(params object[] args)
            {
                Type[] argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
                MethodInfo? bestMatch = 
                    _showMethods
                    .FirstOrDefault(m =>
                        m
                        .GetParameters()
                        .Select(p => p.ParameterType)
                        .SequenceEqual(argTypes));

                return 
                    bestMatch?.Invoke(null, args) 
                    is 
                    DialogResult dialogResult 
                    ? dialogResult
                    : DialogResult.None;
            }
        }

        // <PackageReference Include="IVSoftware.Portable.Disposable" Version="1.2.0" />
        public DisposableHost DHostHook
        {
            get
            {
                if (_dhostHook is null)
                {
                    _dhostHook = new DisposableHost();
                    _dhostHook.BeginUsing += (sender, e) =>
                    {
                        _hookID = SetWindowsHookEx(
                            WH_GETMESSAGE,
                            HookCallback, 
                            IntPtr.Zero, 
                            GetCurrentThreadId());
                    };
                    _dhostHook.FinalDispose += (sender, e) =>
                    {
                        UnhookWindowsHookEx(_hookID);
                    };
                }
                return _dhostHook;
            }
        }
        DisposableHost? _dhostHook = default;
        private IntPtr _hookID = IntPtr.Zero;

        // <PackageReference Include = "IVSoftware.Portable.WatchdogTimer" Version="1.2.1" />
        public WatchdogTimer InactivityWatchdog
        {
            get
            {
                if (_InactivityWatchdog is null)
                {
                    _InactivityWatchdog = new WatchdogTimer 
                    { 
                        Interval = TimeSpan.FromSeconds(2.5) 
                    };
                    _InactivityWatchdog.RanToCompletion += (sender, e) =>
                    {
                        lock (_lock) Text = "Idle";
                        BeginInvoke(() => Text = "Idle");
                    };
                }
                return _InactivityWatchdog;
            }
        }
        WatchdogTimer? _InactivityWatchdog = default;

        public MainForm()
        {
            InitializeComponent();
            Application.AddMessageFilter(this);
            Disposed += (sender, e) => Application.RemoveMessageFilter(this);
            buttonMsg.Click += (sender, e) =>
            {
                using (DHostHook.GetToken())
                {
                    MessageBox.Show("Testing the TimeOut!");
                }
            };
        }
        // Threadsafe Text Setter
        public new string Text
        {
            get => _threadsafeText;
            set
            {
                lock (_lock)
                {
                    if (!Equals(_threadsafeText, value))
                    {
                        lock (_lock)
                        {
                            _threadsafeText = value;
                        }
                        if (InvokeRequired) BeginInvoke(() => base.Text = _threadsafeText);
                        else base.Text = _threadsafeText;
                    }
                }
            }
        }
        string _threadsafeText = string.Empty;

        object _lock = new object();

        public bool PreFilterMessage(ref Message m)
        {
            CheckForActivity((WindowsMessage)m.Msg);
            return false;
        }

        private void CheckForActivity(WindowsMessage wm_msg)
        {
            switch (wm_msg)
            {
                case WindowsMessage.WM_MOUSEMOVE: // Prioritize
                case WindowsMessage when FastMessageSearch.Contains(wm_msg):
                    InactivityWatchdog.StartOrRestart();
                    if(Text != "Running")
                    {
                        BeginInvoke(() => Text = "Running");
                    }
                    break;
            }
        }

        readonly HashSet<WindowsMessage> FastMessageSearch = 
            new (Enum.GetValues<WindowsMessage>());

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                MSG msg = Marshal.PtrToStructure<MSG>(lParam);
                Debug.WriteLine($"Msg: {(WindowsMessage)msg.message} ({msg.message:X}), hWnd: {msg.hwnd}");
                Debug.WriteLine($"{(WindowsMessage)msg.message} {FastMessageSearch.Contains((WindowsMessage)msg.message)}");
                CheckForActivity((WindowsMessage)msg.message);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        #region P / I N V O K E 
        
        private const int WH_GETMESSAGE = 3; 
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public int time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        #endregion  P / I N V O K E
    }
    enum WindowsMessage
    {
        WM_NCLBUTTONDOWN = 0x00A1,
        WM_NCLBUTTONUP = 0x00A2,
        WM_NCRBUTTONDOWN = 0x00A4,
        WM_NCRBUTTONUP = 0x00A5,
        WM_NCMBUTTONDOWN = 0x00A7,
        WM_NCMBUTTONUP = 0x00A8,
        WM_NCXBUTTONDOWN = 0x00AB,
        WM_NCXBUTTONUP = 0x00AC,
        WM_KEYDOWN = 0x0100,
        WM_KEYUP = 0x0101,
        WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207,
        WM_MBUTTONUP = 0x0208,
        WM_XBUTTONDOWN = 0x020B,
        WM_XBUTTONUP = 0x020C
    }
}
