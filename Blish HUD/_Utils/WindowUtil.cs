using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework;

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable InconsistentNaming

namespace Blish_HUD {

    internal static class WindowUtil {

        private static readonly Logger Logger = Logger.GetLogger(typeof(WindowUtil));

        private const uint WS_EX_TOPMOST       = 0x00000008;
        private const uint WS_EX_TRANSPARENT   = 0x00000020;
        private const uint WS_EX_TOOLWINDOW    = 0x00000080;
        private const uint WS_EX_CONTROLPARENT = 0x00010000;
        private const uint WS_EX_APPWINDOW     = 0x00040000;
        private const uint WS_EX_LAYERED       = 0x00080000;
        private const uint WS_EX_NOACTIVATE    = 0x08000000;

        private const int GWL_STYLE   = -16;
        private const int GWL_EXSTYLE = -20;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const uint CS_VREDRAW = 0x0001;
        private const uint CS_HREDRAW = 0x0002;

        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint SWP_NOSIZE         = 0x0001;
        private const uint SWP_NOMOVE         = 0x0002;

        private const int MINIMIZED_POS = -32000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        [DllImport("Dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMarInset);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern long GetClassName(IntPtr hwnd, StringBuilder lpClassName, long nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, UIntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private struct Margins {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private enum GW : uint {
            HWNDFIRST    = 0,
            HWNDLAST     = 1,
            HWNDNEXT     = 2,
            HWNDPREV     = 3,
            OWNER        = 4,
            CHILD        = 5,
            ENABLEDPOPUP = 6
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, GW uCmd);

        internal static void SetForegroundWindowEx(IntPtr handle) => SetForegroundWindow(handle);

        private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex) => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

        private static int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong) => IntPtr.Size != 8 ? SetWindowLong32(hWnd, nIndex, dwNewLong) : (int) SetWindowLongPtr64(hWnd, nIndex, new UIntPtr(dwNewLong));

        private static void SetWindowParam(IntPtr winHandle, bool showInTaskbar = false) {
            uint windowParam = WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_CONTROLPARENT | WS_EX_LAYERED;

            if (Blish_HUD.GameIntegration.AudioIntegration.IsRunningUnderWine) {
                // On macOS, clicks on the overlay activate it and Wine then reports the desktop as the
                // foreground window, so GW2 is considered unfocused. Keep the overlay from being activated.
                windowParam |= WS_EX_NOACTIVATE;
            }

            if (showInTaskbar) {
                ShowWindow(winHandle, SW_HIDE);
                windowParam |= WS_EX_APPWINDOW;
            } else {
                windowParam |= WS_EX_TOOLWINDOW;
            }

            SetWindowLong(winHandle, GWL_EXSTYLE, windowParam);

            if (showInTaskbar) {
                ShowWindow(winHandle, SW_SHOW);
            }
        }

        internal static void SetShowInTaskbar(IntPtr winHandle, bool showInTaskbar) {
            SetWindowParam(winHandle, showInTaskbar);
        }

        internal static void SetupOverlay(IntPtr winHandle) {
            SetWindowLong(winHandle, GWL_STYLE, CS_HREDRAW | CS_VREDRAW);

            // Let Gw2InstanceIntegration sync behavior with game runtime
            SetShowInTaskbar(winHandle, false);

            if (WineLayeredPresenter.Enabled) {
                // Frames are pushed with UpdateLayeredWindow (see WineLayeredPresenter).
                // SetLayeredWindowAttributes must not be called, otherwise UpdateLayeredWindow fails.
            } else if (UseWineColorKey) {
                // Wine has no DWM composition (DwmExtendFrameIntoClientArea is a stub), so the
                // transparent clear color ends up opaque black. Experimental black color key.
                Logger.Info("Running under Wine: using a black color key for overlay transparency.");
                SetLayeredWindowAttributes(winHandle, 0x00000000, 255, LWA_COLORKEY);
            } else {
                SetLayeredWindowAttributes(winHandle, 0, 255, LWA_ALPHA);
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private static readonly uint _currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        /// <summary>
        /// Under Wine on macOS, clicking an overlay window activates it (WS_EX_TRANSPARENT does not pass
        /// the click through to GW2), and Form.ActiveForm stays null. Without this check GW2 is considered
        /// unfocused, input hooks are disabled and Blish HUD text boxes cannot receive keyboard input.
        /// Treat any foreground window owned by this process as "GW2 still has focus" under Wine.
        /// </summary>
        private static bool IsOwnWindowUnderWine(IntPtr windowHandle) {
            if (windowHandle == IntPtr.Zero || !Blish_HUD.GameIntegration.AudioIntegration.IsRunningUnderWine) return false;

            GetWindowThreadProcessId(windowHandle, out uint processId);
            return processId == _currentProcessId;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

        /// <summary>
        /// Under Wine on macOS, clicking the overlay can leave the desktop window (#32769) as the
        /// foreground window. If the cursor is over the GW2 client area at that moment, the user is
        /// interacting with the overlay: treat GW2 as still focused.
        /// </summary>
        private static bool IsWineDesktopFocusOverGw2(IntPtr foreground, RECT gw2ClientRect, System.Drawing.Point gw2ScreenOrigin) {
            if (!Blish_HUD.GameIntegration.AudioIntegration.IsRunningUnderWine) return false;
            if (foreground != IntPtr.Zero && foreground != GetDesktopWindow()) return false;
            if (!GetCursorPos(out var cursor)) return false;

            int left   = gw2ScreenOrigin.X + gw2ClientRect.Left;
            int top    = gw2ScreenOrigin.Y + gw2ClientRect.Top;
            int right  = gw2ScreenOrigin.X + gw2ClientRect.Right;
            int bottom = gw2ScreenOrigin.Y + gw2ClientRect.Bottom;

            return cursor.X >= left && cursor.X < right && cursor.Y >= top && cursor.Y < bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        private static string DescribeWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) return "0x0";

            GetWindowThreadProcessId(hWnd, out uint pid);
            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);

            return $"0x{hWnd.ToInt64():X} pid={pid} class=\"{GetClassNameOfWindow(hWnd)}\" title=\"{title}\"";
        }

        private static void LogForegroundWindowDetails(IntPtr foreground, IntPtr gw2Window, IntPtr overlayWindow) {
            try {
                Logger.Debug($"[Wine focus] foreground={DescribeWindow(foreground)} | gw2={DescribeWindow(gw2Window)} | overlay={DescribeWindow(overlayWindow)} | thisPid={_currentProcessId} | activeForm={(Form.ActiveForm != null)} | getActiveWindow={DescribeWindow(GetActiveWindow())} | getFocus={DescribeWindow(GetFocus())}");
            } catch (Exception e) {
                Logger.Debug(e, "[Wine focus] failed to describe windows");
            }
        }

        private const uint LWA_COLORKEY = 0x00000001;
        private const uint LWA_ALPHA    = 0x00000002;

        /// <summary>
        /// Experimental: under Wine, use a black color key for transparency (BLISHHUD_WINE_COLORKEY=1).
        /// Did not work with D3D rendering on macOS; kept as an opt-in fallback.
        /// </summary>
        private static bool UseWineColorKey =>
            Blish_HUD.GameIntegration.AudioIntegration.IsRunningUnderWine
         && Environment.GetEnvironmentVariable("BLISHHUD_WINE_COLORKEY") == "1";

        public enum OverlayUpdateResponse {
            WithFocus,
            WithoutFocus,
            Minimized,
            Errored
        }

        private static Rectangle pos;

        internal static (OverlayUpdateResponse Response, bool Minimized, int ErrorCode) UpdateOverlay(IntPtr winHandle, IntPtr gw2WindowHandle, bool wasOnTop) {
            var clientRect = new RECT();
            bool errGetClientRectResult = GetClientRect(gw2WindowHandle, ref clientRect);

            // Probably errors caused by gw2 closing at the time of the call or the call is super early and has the wrong handle somehow
            if (!errGetClientRectResult) {
                int errorCode = Marshal.GetLastWin32Error();

                Logger.Warn($"{nameof(GetClientRect)} failed with error code {errorCode}.");
                return (OverlayUpdateResponse.Errored, false, errorCode);
            }

            var screenPoint = System.Drawing.Point.Empty;
            bool errClientToScreen = ClientToScreen(gw2WindowHandle, ref screenPoint);

            // Probably errors caused by gw2 closing at the time of the call
            if (!errClientToScreen) {
                int errorCode = Marshal.GetLastWin32Error();

                Logger.Warn($"{nameof(ClientToScreen)} failed with error code {errorCode}.");
                return (OverlayUpdateResponse.Errored, screenPoint.X == MINIMIZED_POS, errorCode);
            }

            GameService.Debug.StartTimeFunc("GetForegroundWindow");
            var activeWindowHandle = GetForegroundWindow();
            GameService.Debug.StopTimeFunc("GetForegroundWindow");

            // If Guild Wars 2 is not the focused application, set Blish HUD to
            // be above Guild Wars 2, but below the next application's window in z-order
            if (activeWindowHandle != gw2WindowHandle && Form.ActiveForm == null && !IsOwnWindowUnderWine(activeWindowHandle)
             && !IsWineDesktopFocusOverGw2(activeWindowHandle, clientRect, screenPoint)) {
                if (wasOnTop) {
                    Logger.Debug("GW2 is no longer the active window.");

                    if (Blish_HUD.GameIntegration.AudioIntegration.IsRunningUnderWine) {
                        LogForegroundWindowDetails(activeWindowHandle, gw2WindowHandle, winHandle);
                    }

                    var nextHandle = GetWindow(gw2WindowHandle, GW.HWNDPREV);
                    if (nextHandle != IntPtr.Zero && nextHandle != winHandle) {
                        SetWindowPos(winHandle, nextHandle, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                    }
                }
                return (OverlayUpdateResponse.WithoutFocus, screenPoint.X == MINIMIZED_POS, 0);
            }

            if (!wasOnTop) {
                Logger.Debug("GW2 is now the active window - reactivating the overlay.");
            }

            if (clientRect.Left + screenPoint.X != pos.X || clientRect.Top + screenPoint.Y != pos.Y || clientRect.Right - clientRect.Left != pos.Width || clientRect.Bottom - clientRect.Top != pos.Height || wasOnTop == false) {
                pos = new Rectangle(
                    clientRect.Left + screenPoint.X,
                    clientRect.Top + screenPoint.Y,
                    clientRect.Right - clientRect.Left,
                    clientRect.Bottom - clientRect.Top
                );

                GameService.Graphics.Resolution = new Point(
                    pos.Width,
                    pos.Height
                );

                SetWindowPos(winHandle, HWND_TOPMOST, clientRect.Left + screenPoint.X, clientRect.Top + screenPoint.Y, clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top, 0);

                var marg = new Margins {
                    cxLeftWidth    = 0,
                    cyTopHeight    = 0,
                    cxRightWidth   = clientRect.Right,
                    cyBottomHeight = clientRect.Bottom
                };

                DwmExtendFrameIntoClientArea(winHandle, ref marg);
            }

            return (OverlayUpdateResponse.WithFocus, screenPoint.X == MINIMIZED_POS, 0);
        }

        internal static string GetClassNameOfWindow(IntPtr hwnd) {
            string        className = "";
            StringBuilder classText = null;
            try {
                int cls_max_length = 1000;
                classText = new StringBuilder("", cls_max_length + 5);
                GetClassName(hwnd, classText, cls_max_length + 2);

                if (!string.IsNullOrEmpty(classText.ToString()))
                    className = classText.ToString();
            } catch (Exception ex) {
                className = ex.Message;
            }
            return className;
        }

    }
}
