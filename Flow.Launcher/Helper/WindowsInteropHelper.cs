﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Point = System.Windows.Point;

namespace Flow.Launcher.Helper;

public class WindowsInteropHelper
{
    private static HWND _hwnd_shell;
    private static HWND _hwnd_desktop;

    //Accessors for shell and desktop handlers
    //Will set the variables once and then will return them
    private static HWND HWND_SHELL
    {
        get
        {
            return _hwnd_shell != HWND.Null ? _hwnd_shell : _hwnd_shell = PInvoke.GetShellWindow();
        }
    }

    private static HWND HWND_DESKTOP
    {
        get
        {
            return _hwnd_desktop != HWND.Null ? _hwnd_desktop : _hwnd_desktop = PInvoke.GetDesktopWindow();
        }
    }

    const string WINDOW_CLASS_CONSOLE = "ConsoleWindowClass";
    const string WINDOW_CLASS_WINTAB = "Flip3D";
    const string WINDOW_CLASS_PROGMAN = "Progman";
    const string WINDOW_CLASS_WORKERW = "WorkerW";

    public unsafe static bool IsWindowFullscreen()
    {
        //get current active window
        var hWnd = PInvoke.GetForegroundWindow();

        if (hWnd.Equals(HWND.Null))
        {
            return false;
        }

        //if current active window is desktop or shell, exit early
        if (hWnd.Equals(HWND_DESKTOP) || hWnd.Equals(HWND_SHELL))
        {
            return false;
        }

        string windowClass;
        const int capacity = 256;
        Span<char> buffer = stackalloc char[capacity];
        int validLength;
        fixed (char* pBuffer = buffer)
        {
            validLength = PInvoke.GetClassName(hWnd, pBuffer, capacity);
        }

        windowClass = buffer[..validLength].ToString();


        //for Win+Tab (Flip3D)
        if (windowClass == WINDOW_CLASS_WINTAB)
        {
            return false;
        }

        PInvoke.GetWindowRect(hWnd, out var appBounds);

        //for console (ConsoleWindowClass), we have to check for negative dimensions
        if (windowClass == WINDOW_CLASS_CONSOLE)
        {
            return appBounds.top < 0 && appBounds.bottom < 0;
        }

        //for desktop (Progman or WorkerW, depends on the system), we have to check
        if (windowClass is WINDOW_CLASS_PROGMAN or WINDOW_CLASS_WORKERW)
        {
            var hWndDesktop = PInvoke.FindWindowEx(hWnd, HWND.Null, "SHELLDLL_DefView", null);
            hWndDesktop = PInvoke.FindWindowEx(hWndDesktop, HWND.Null, "SysListView32", "FolderView");
            if (hWndDesktop.Value != (IntPtr.Zero))
            {
                return false;
            }
        }

        Rectangle screenBounds = Screen.FromHandle(hWnd).Bounds;
        return (appBounds.bottom - appBounds.top) == screenBounds.Height &&
               (appBounds.right - appBounds.left) == screenBounds.Width;
    }

    /// <summary>
    ///     disable windows toolbar's control box
    ///     this will also disable system menu with Alt+Space hotkey
    /// </summary>
    public static void DisableControlBox(Window win)
    {
        var hwnd = new HWND(new WindowInteropHelper(win).Handle);
        
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        
        if (style == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        
        var previousStyle = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE,
            style);

        if (previousStyle == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>
    /// Transforms pixels to Device Independent Pixels used by WPF
    /// </summary>
    /// <param name="visual">current window, required to get presentation source</param>
    /// <param name="unitX">horizontal position in pixels</param>
    /// <param name="unitY">vertical position in pixels</param>
    /// <returns>point containing device independent pixels</returns>
    public static Point TransformPixelsToDIP(Visual visual, double unitX, double unitY)
    {
        Matrix matrix;
        var source = PresentationSource.FromVisual(visual);
        if (source is not null)
        {
            matrix = source.CompositionTarget.TransformFromDevice;
        }
        else
        {
            using var src = new HwndSource(new HwndSourceParameters());
            matrix = src.CompositionTarget.TransformFromDevice;
        }

        return new Point((int)(matrix.M11 * unitX), (int)(matrix.M22 * unitY));
    }

    #region Alt Tab

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int IntSetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
    private static extern void SetLastError(int dwErrorCode);

    private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        SetLastError(0); // Clear any existing error

        if (IntPtr.Size == 4) return new IntPtr(IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong)));

        return IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
    }

    private static int IntPtrToInt32(IntPtr intPtr)
    {
        return unchecked((int)intPtr.ToInt64());
    }

    /// <summary>
    ///     Hide windows in the Alt+Tab window list
    /// </summary>
    /// <param name="window">To hide a window</param>
    public static void HideFromAltTab(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE).ToInt32();

        // Add TOOLWINDOW style, remove APPWINDOW style
        exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;

        SetWindowLong(helper.Handle, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    ///     Restore window display in the Alt+Tab window list.
    /// </summary>
    /// <param name="window">To restore the displayed window</param>
    public static void ShowInAltTab(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE).ToInt32();

        // Remove the TOOLWINDOW style and add the APPWINDOW style.
        exStyle = (exStyle & ~WS_EX_TOOLWINDOW) | WS_EX_APPWINDOW;

        SetWindowLong(helper.Handle, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    ///     To obtain the current overridden style of a window.
    /// </summary>
    /// <param name="window">To obtain the style dialog window</param>
    /// <returns>current extension style value</returns>
    public static int GetCurrentWindowStyle(Window window)
    {
        var helper = new WindowInteropHelper(window);
        return GetWindowLong(helper.Handle, GWL_EXSTYLE).ToInt32();
    }

    #endregion
}
