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
```

