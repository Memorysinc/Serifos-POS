using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FirewallControl.App.Services;

/// <summary>
/// Aplica el ícono multi-resolución Sefiros POS en Windows (barra de título, clase Win32 y barra de tareas Win10/Win11)
/// mediante WM_SETICON, GCLP_HICON/GCLP_HICONSM, SetCurrentProcessExplicitAppUserModelID e IPropertyStore.
/// </summary>
public static class AppIconHelper
{
    public const string AppUserModelId = "Sefiros.POS.Net10.App";

    private const uint WM_GETICON = 0x007F;
    private const uint WM_SETICON = 0x0080;
    private static readonly IntPtr ICON_SMALL = IntPtr.Zero;
    private static readonly IntPtr ICON_BIG = new(1);

    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;

    private const int SM_CXICON = 11;
    private const int SM_CYICON = 12;
    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const ushort VT_LPWSTR = 31;

    private static readonly Guid IPropertyStoreGuid = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly Guid AppUserModelFmtId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchCommand = new() { fmtid = AppUserModelFmtId, pid = 2 };
    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchIconResource = new() { fmtid = AppUserModelFmtId, pid = 3 };
    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchDisplayNameResource = new() { fmtid = AppUserModelFmtId, pid = 4 };
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new() { fmtid = AppUserModelFmtId, pid = 5 };

    private static readonly object SyncLock = new();
    private static bool _appIdInitialized;
    private static bool _initialized;
    private static IntPtr _hIconSmall;
    private static IntPtr _hIconBig;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;
        public IntPtr extra;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear([In, Out] ref PROPVARIANT pvar);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLongW")]
    private static extern uint SetClassLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongW")]
    private static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconFromResourceEx(
        byte[] pbIconBits,
        uint cbIconBits,
        [MarshalAs(UnmanagedType.Bool)] bool fIcon,
        uint dwVersion,
        int cxDesired,
        int cyDesired,
        uint uFlags);

    public static void InitializeProcessAppUserModelId()
    {
        if (_appIdInitialized)
            return;

        lock (SyncLock)
        {
            if (_appIdInitialized)
                return;
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            }
            catch
            {
                // Ignore on non-supported environments
            }
            finally
            {
                _appIdInitialized = true;
            }
        }
    }

    public static void ApplyCrispRendering(Window window)
    {
        if (window == null)
            return;

        if (!window.UseLayoutRounding)
            window.UseLayoutRounding = true;
        if (!window.SnapsToDevicePixels)
            window.SnapsToDevicePixels = true;

        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);
    }

    public static void ApplySefirosIcon(Window window)
    {
        if (window == null)
            return;

        ApplyCrispRendering(window);
        InitializeProcessAppUserModelId();
        EnsureNativeHandlesLoaded();

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            ApplyNativeHwndIcons(hwnd, window.ShowInTaskbar);
        }
        else
        {
            window.SourceInitialized -= OnWindowSourceInitialized;
            window.SourceInitialized += OnWindowSourceInitialized;
        }

        window.Loaded -= OnWindowLoadedReapplyIcon;
        window.Loaded += OnWindowLoadedReapplyIcon;
    }

    public static (IntPtr Big, IntPtr Small, IntPtr ClassBig, IntPtr ClassSmall) InspectWindowIconHandles(Window window)
    {
        if (window == null)
            return default;

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return default;

        IntPtr big = SendMessage(hwnd, WM_GETICON, ICON_BIG, IntPtr.Zero);
        IntPtr small = SendMessage(hwnd, WM_GETICON, ICON_SMALL, IntPtr.Zero);
        IntPtr classBig = GetClassLongPtrSafe(hwnd, GCLP_HICON);
        IntPtr classSmall = GetClassLongPtrSafe(hwnd, GCLP_HICONSM);
        return (big, small, classBig, classSmall);
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window w)
            return;
        w.SourceInitialized -= OnWindowSourceInitialized;
        IntPtr hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd != IntPtr.Zero)
            ApplyNativeHwndIcons(hwnd, w.ShowInTaskbar);
    }

    private static void OnWindowLoadedReapplyIcon(object sender, RoutedEventArgs e)
    {
        if (sender is not Window w)
            return;
        w.Loaded -= OnWindowLoadedReapplyIcon;
        IntPtr hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd != IntPtr.Zero)
            ApplyNativeHwndIcons(hwnd, w.ShowInTaskbar);
    }

    private static void ApplyNativeHwndIcons(IntPtr hwnd, bool applyTaskbarStore)
    {
        EnsureNativeHandlesLoaded();
        if (_hIconBig != IntPtr.Zero)
        {
            SendMessage(hwnd, WM_SETICON, ICON_BIG, _hIconBig);
            SetClassLongPtrSafe(hwnd, GCLP_HICON, _hIconBig);
        }
        if (_hIconSmall != IntPtr.Zero)
        {
            SendMessage(hwnd, WM_SETICON, ICON_SMALL, _hIconSmall);
            SetClassLongPtrSafe(hwnd, GCLP_HICONSM, _hIconSmall);
        }

        if (applyTaskbarStore)
            ApplyWindowTaskbarPropertyStore(hwnd);
    }

    private static void ApplyWindowTaskbarPropertyStore(IntPtr hwnd)
    {
        IPropertyStore? store = null;
        try
        {
            Guid iid = IPropertyStoreGuid;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store == null)
                return;

            SetPropertyString(store, PKEY_AppUserModel_ID, AppUserModelId);
            SetPropertyString(store, PKEY_AppUserModel_RelaunchDisplayNameResource, "Sefiros POS");

            string? processPath = Environment.ProcessPath;
            string? diskIco = ResolveDiskIcoPath();
            if (IsCurrentProcessSefirosApp(processPath) && !string.IsNullOrWhiteSpace(processPath))
            {
                SetPropertyString(store, PKEY_AppUserModel_RelaunchCommand, processPath);
                SetPropertyString(store, PKEY_AppUserModel_RelaunchIconResource, $"{processPath},0");
            }
            else if (!string.IsNullOrWhiteSpace(diskIco))
            {
                SetPropertyString(store, PKEY_AppUserModel_RelaunchIconResource, $"{diskIco},0");
            }

            store.Commit();
        }
        catch
        {
            // Ignore in restricted shell environments
        }
        finally
        {
            if (store != null && Marshal.IsComObject(store))
            {
                Marshal.ReleaseComObject(store);
            }
        }
    }

    private static void SetPropertyString(IPropertyStore store, PROPERTYKEY key, string value)
    {
        PROPVARIANT pv = default;
        try
        {
            pv.vt = VT_LPWSTR;
            pv.pwszVal = Marshal.StringToCoTaskMemUni(value);
            store.SetValue(ref key, ref pv);
        }
        finally
        {
            PropVariantClear(ref pv);
        }
    }

    private static bool IsCurrentProcessSefirosApp(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return false;
        string name = Path.GetFileNameWithoutExtension(processPath);
        return name.Contains("FirewallControl", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Sefiros", StringComparison.OrdinalIgnoreCase);
    }

    private static IntPtr SetClassLongPtrSafe(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8
            ? SetClassLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(unchecked((int)SetClassLong32(hWnd, nIndex, unchecked((uint)dwNewLong.ToInt32()))));

    private static IntPtr GetClassLongPtrSafe(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8
            ? GetClassLongPtr64(hWnd, nIndex)
            : new IntPtr(unchecked((int)GetClassLong32(hWnd, nIndex)));

    private static void EnsureNativeHandlesLoaded()
    {
        if (_initialized)
            return;

        lock (SyncLock)
        {
            if (_initialized)
                return;

            try
            {
                int cxBig = GetSystemMetrics(SM_CXICON);
                int cyBig = GetSystemMetrics(SM_CYICON);
                if (cxBig <= 0) cxBig = 32;
                if (cyBig <= 0) cyBig = 32;

                int cxSmall = GetSystemMetrics(SM_CXSMICON);
                int cySmall = GetSystemMetrics(SM_CYSMICON);
                if (cxSmall <= 0) cxSmall = 16;
                if (cySmall <= 0) cySmall = 16;

                if (IsCurrentProcessSefirosApp(Environment.ProcessPath))
                {
                    IntPtr hMod = GetModuleHandle(null);
                    if (hMod != IntPtr.Zero)
                    {
                        _hIconBig = LoadImage(hMod, (IntPtr)32512, IMAGE_ICON, cxBig, cyBig, 0);
                        _hIconSmall = LoadImage(hMod, (IntPtr)32512, IMAGE_ICON, cxSmall, cySmall, 0);
                    }
                }

                string? diskIco = ResolveDiskIcoPath();
                if ((_hIconBig == IntPtr.Zero || _hIconSmall == IntPtr.Zero) && !string.IsNullOrWhiteSpace(diskIco))
                {
                    if (_hIconBig == IntPtr.Zero)
                        _hIconBig = LoadImage(IntPtr.Zero, diskIco, IMAGE_ICON, cxBig, cyBig, LR_LOADFROMFILE);
                    if (_hIconSmall == IntPtr.Zero)
                        _hIconSmall = LoadImage(IntPtr.Zero, diskIco, IMAGE_ICON, cxSmall, cySmall, LR_LOADFROMFILE);
                }

                if (_hIconBig == IntPtr.Zero || _hIconSmall == IntPtr.Zero)
                {
                    byte[]? icoBytes = LoadSefirosIcoBytes(diskIco);
                    if (icoBytes != null && icoBytes.Length > 22)
                    {
                        if (_hIconBig == IntPtr.Zero)
                            _hIconBig = CreateBestIconHandle(icoBytes, cxBig);
                        if (_hIconSmall == IntPtr.Zero)
                            _hIconSmall = CreateBestIconHandle(icoBytes, cxSmall);
                    }
                }
            }
            catch
            {
                // Ignore if running in minimal headless environment
            }
            finally
            {
                _initialized = true;
            }
        }
    }

    private static string? ResolveDiskIcoPath()
    {
        string direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "sefiros_pos.ico");
        if (File.Exists(direct))
            return direct;

        string? cur = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(cur); i++)
        {
            string candidate = Path.Combine(cur, "Assets", "sefiros_pos.ico");
            if (File.Exists(candidate))
                return candidate;
            string srcCandidate = Path.Combine(cur, "src", "FirewallControl.App", "Assets", "sefiros_pos.ico");
            if (File.Exists(srcCandidate))
                return srcCandidate;
            cur = Directory.GetParent(cur)?.FullName;
        }

        return null;
    }

    private static byte[]? LoadSefirosIcoBytes(string? diskPath)
    {
        try
        {
            var res = Application.GetResourceStream(
                new Uri("pack://application:,,,/Sefiros.App;component/Assets/sefiros_pos.ico"));
            if (res?.Stream != null)
            {
                using var ms = new MemoryStream();
                res.Stream.CopyTo(ms);
                res.Stream.Dispose();
                return ms.ToArray();
            }
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(diskPath) && File.Exists(diskPath))
            return File.ReadAllBytes(diskPath);

        return null;
    }

    private static IntPtr CreateBestIconHandle(byte[] icoBytes, int preferredSize)
    {
        ushort type = BitConverter.ToUInt16(icoBytes, 2);
        ushort count = BitConverter.ToUInt16(icoBytes, 4);
        if (type != 1 || count == 0)
            return IntPtr.Zero;

        int bestIdx = -1;
        int bestScore = int.MaxValue;
        int bestDim = preferredSize;

        for (int i = 0; i < count; i++)
        {
            int entryOff = 6 + (i * 16);
            if (entryOff + 16 > icoBytes.Length)
                break;

            int width = icoBytes[entryOff] == 0 ? 256 : icoBytes[entryOff];
            uint size = BitConverter.ToUInt32(icoBytes, entryOff + 8);
            uint offset = BitConverter.ToUInt32(icoBytes, entryOff + 12);
            if (size == 0 || offset + size > icoBytes.Length)
                continue;

            int diff = Math.Abs(width - preferredSize);
            if (diff < bestScore)
            {
                bestScore = diff;
                bestIdx = i;
                bestDim = width;
            }
        }

        if (bestIdx < 0)
            return IntPtr.Zero;

        int chosenOff = 6 + (bestIdx * 16);
        uint imgSize = BitConverter.ToUInt32(icoBytes, chosenOff + 8);
        uint imgOffset = BitConverter.ToUInt32(icoBytes, chosenOff + 12);

        byte[] payload = new byte[imgSize];
        Buffer.BlockCopy(icoBytes, (int)imgOffset, payload, 0, (int)imgSize);

        return CreateIconFromResourceEx(
            payload,
            imgSize,
            fIcon: true,
            dwVersion: 0x00030000,
            cxDesired: bestDim,
            cyDesired: bestDim,
            uFlags: 0);
    }
}
