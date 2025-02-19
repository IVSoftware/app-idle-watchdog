Jimi is right about Hans being right. So, the way I see it we need to put together four elements to solve this.

1. A low-level hook that we can run during calls to `MessageBox.Show(...)`.
2. An `IDisposable` to wrap the hook with, that does reference counting and disposes the hook when it leaves the `using` block.
3. A means so that calling any overload of `MessageBox.Show(...)` within in our scope calls our own method.
4. A suitable `WatchdogTimer` that can be "kicked" (i.e. start-or-restart) when activity is sensed.

And while we're at it, we can optimize by putting messages in a `HashSet<WindowsMessage>` for rapid detection.

___

**IDisposable Low-Level Hook**

Let's knock out the first two requirements using `P\Invoke` along with the `NuGet` package shown (or something like it).

```
// <PackageReference Include="IVSoftware.Portable.Disposable" Version="1.2.0" />
static DisposableHost DHostHook
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
                    _hookProc, 
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
static DisposableHost? _dhostHook = default;
static IntPtr _hookID = IntPtr.Zero;
private static HookProc _hookProc = null!;
```

___

**MessageBox Forwarder**

Next, make a static class at local (or app) scope that behaves (in a sense) like an "impossible" extension for the static `System.Windows.Forms.MessageBox` class. This means it's "business as usual" when it come to invoking message boxes.

```
{
    .
    .
    .
    // MessageBox wrapper static class nested in MainForm
    private static class MessageBox
    {
        private static readonly Dictionary<int, MethodInfo> _showMethodLookup;
        static MessageBox()
        {
            _showMethodLookup = typeof(System.Windows.Forms.MessageBox)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(_ => _.Name == "Show")
                .ToDictionary(
                    _ => _.GetParameters()
                            .Select(p => p.ParameterType)
                            .Aggregate(17, (hash, type) => hash * 31 + (type?.GetHashCode() ?? 0)),
                    _ => _
                );
        }
        public static DialogResult Show(params object[] args)
        {
            // Increment the ref count prior to calling native MessageBox
            using (DHostHook.GetToken())
            {
                int argHash = args
                    .Select(_ => _?.GetType() ?? typeof(object))
                    .Aggregate(17, (hash, type) => hash * 31 + (type?.GetHashCode() ?? 0));

                if (_showMethodLookup.TryGetValue(argHash, out var bestMatch) && bestMatch is not null)
                {
                    return bestMatch.Invoke(null, args) is DialogResult dialogResult
                        ? dialogResult
                        : DialogResult.None;
                }
                return DialogResult.None;
            }
        }
    }
    .
    .
    .
}
```

___

**WatchdogTimer**

Meet requirement #4 using the `NuGet` package shown (or something like it).

___
_We'll monitor its status in the Title Bar of the main window as either "Idle" or "Running"._
___

```
// <PackageReference Include = "IVSoftware.Portable.WatchdogTimer" Version="1.2.1" />
public WatchdogTimer InactivityWatchdog
{
    get
    {
        if (_InactivityWatchdog is null)
        {
            _InactivityWatchdog = new WatchdogTimer 
            { 
                Interval = TimeSpan.FromSeconds(2),
            };
            _InactivityWatchdog.RanToCompletion += (sender, e) =>
            {
                Text = "Idle";
            };
        }
        return _InactivityWatchdog;
    }
}
WatchdogTimer? _InactivityWatchdog = default;
```
___

**Minimal MainForm Example**

```
public MainForm()
{
    InitializeComponent();
    Application.AddMessageFilter(this);
    Disposed += (sender, e) => Application.RemoveMessageFilter(this);
    _hookProc = HookCallback;

    // Button for test
    buttonMsg.Click += (sender, e) =>
    {
        MessageBox.Show("✨ Testing the Hook!");
    };
}

public bool PreFilterMessage(ref Message m)
{
    CheckForActivity((WindowsMessage)m.Msg);
    return false;
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

private void CheckForActivity(WindowsMessage wm_msg)
{
    switch (wm_msg)
    {
        case WindowsMessage.WM_MOUSEMOVE: // Prioritize
        case WindowsMessage when _rapidMessageLookup.Contains(wm_msg):
            InactivityWatchdog.StartOrRestart();
            if(Text != "Running")
            {
                BeginInvoke(() => Text = "Running");
            }
            break;
    }
}

readonly HashSet<WindowsMessage> _rapidMessageLookup = 
    new (Enum.GetValues<WindowsMessage>());

private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode >= 0)
    {
        MSG msg = Marshal.PtrToStructure<MSG>(lParam);
        Debug.WriteLine($"Msg: {(WindowsMessage)msg.message} ({msg.message:X}), hWnd: {msg.hwnd}");
        Debug.WriteLine($"{(WindowsMessage)msg.message} {_rapidMessageLookup.Contains((WindowsMessage)msg.message)}");
        CheckForActivity((WindowsMessage)msg.message);
    }
    return CallNextHookEx(_hookID, nCode, wParam, lParam);
}
```