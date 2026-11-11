using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

namespace Blish_HUD {

    /// <summary>
    /// Presents frames through UpdateLayeredWindow (per-pixel alpha) when running under Wine.
    /// Wine has no DWM composition, so the regular swap chain present shows an opaque black window.
    /// Wine's macOS driver does support per-pixel alpha layered windows, so each frame is read back
    /// from the back buffer and pushed to the layered window instead of being presented.
    /// Set BLISHHUD_WINE_LAYERED=0 to disable.
    /// </summary>
    internal static class WineLayeredPresenter {

        private static readonly Logger Logger = Logger.GetLogger(typeof(WineLayeredPresenter));

        private static readonly Lazy<bool> _enabled = new Lazy<bool>(() => {
            bool enabled = GameIntegration.AudioIntegration.IsRunningUnderWine
                        && Environment.GetEnvironmentVariable("BLISHHUD_WINE_LAYERED") != "0";

            if (enabled) {
                Logger.Info("Running under Wine: presenting the overlay through UpdateLayeredWindow (per-pixel alpha).");
            }

            return enabled;
        });

        public static bool Enabled => _enabled.Value;

        private static System.Windows.Forms.Form _deviceForm;

        /// <summary>
        /// Handle of a hidden window used as the D3D device window under Wine.
        /// On macOS, Wine attaches the swap chain's Metal surface to the device window and draws it
        /// above the layered window content, hiding it. Rendering into a never-shown window avoids that:
        /// the visible overlay window only receives the frames pushed with UpdateLayeredWindow.
        /// </summary>
        public static IntPtr GetDeviceWindowHandle() {
            if (_deviceForm == null) {
                _deviceForm = new System.Windows.Forms.Form {
                    ShowInTaskbar   = false,
                    FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                    StartPosition   = System.Windows.Forms.FormStartPosition.Manual,
                    Location        = new System.Drawing.Point(-32000, -32000),
                    Text            = "Blish HUD (Wine render target)"
                };

                Logger.Info("Running under Wine: rendering into a hidden device window.");
            }

            return _deviceForm.Handle; // Creates the window handle without showing the form
        }

        private static IntPtr _memDc;
        private static IntPtr _bitmap;
        private static IntPtr _oldBitmap;
        private static IntPtr _bits;
        private static int    _width;
        private static int    _height;
        private static int[]  _buffer;

        private static bool _loggedFirstFrame;
        private static bool _loggedFailure;

        public static void Present(GraphicsDevice graphicsDevice, IntPtr hwnd) {
            var pp = graphicsDevice.PresentationParameters;
            int width  = pp.BackBufferWidth;
            int height = pp.BackBufferHeight;

            if (width <= 0 || height <= 0 || hwnd == IntPtr.Zero) return;

            EnsureSurface(width, height);

            graphicsDevice.GetBackBufferData(_buffer);

            // UpdateLayeredWindow expects premultiplied BGRA. SpriteBatch output is already premultiplied.
            if (pp.BackBufferFormat != SurfaceFormat.Bgra32) {
                SwapRedBlue(_buffer);
            }

            Marshal.Copy(_buffer, 0, _bits, width * height);

            var size  = new SIZE { cx = width, cy = height };
            var src   = new POINT();
            var blend = new BLENDFUNCTION {
                BlendOp             = AC_SRC_OVER,
                BlendFlags          = 0,
                SourceConstantAlpha = 255,
                AlphaFormat         = AC_SRC_ALPHA
            };

            bool ok = UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, _memDc, ref src, 0, ref blend, ULW_ALPHA);

            if (!ok && !_loggedFailure) {
                _loggedFailure = true;
                Logger.Warn($"UpdateLayeredWindow failed with error code {Marshal.GetLastWin32Error()} ({width}x{height}, format {pp.BackBufferFormat}).");
            } else if (ok && !_loggedFirstFrame) {
                _loggedFirstFrame = true;
                Logger.Info($"First layered frame presented ({width}x{height}, format {pp.BackBufferFormat}, multisample {pp.MultiSampleCount}).");
            }
        }

        private static void EnsureSurface(int width, int height) {
            if (_memDc != IntPtr.Zero && width == _width && height == _height) return;

            ReleaseSurface();

            _memDc = CreateCompatibleDC(IntPtr.Zero);

            var bmi = new BITMAPINFOHEADER {
                biSize        = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth       = width,
                biHeight      = -height, // top-down
                biPlanes      = 1,
                biBitCount    = 32,
                biCompression = BI_RGB
            };

            _bitmap    = CreateDIBSection(_memDc, ref bmi, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
            _oldBitmap = SelectObject(_memDc, _bitmap);
            _buffer    = new int[width * height];
            _width     = width;
            _height    = height;
        }

        private static void ReleaseSurface() {
            if (_memDc == IntPtr.Zero) return;

            SelectObject(_memDc, _oldBitmap);
            DeleteObject(_bitmap);
            DeleteDC(_memDc);

            _memDc  = IntPtr.Zero;
            _bitmap = IntPtr.Zero;
            _bits   = IntPtr.Zero;
        }

        private static void SwapRedBlue(int[] pixels) {
            for (int i = 0; i < pixels.Length; i++) {
                uint p = (uint)pixels[i];
                pixels[i] = (int)((p & 0xFF00FF00u) | ((p & 0x000000FFu) << 16) | ((p >> 16) & 0x000000FFu));
            }
        }

        #region Win32

        private const byte AC_SRC_OVER    = 0x00;
        private const byte AC_SRC_ALPHA   = 0x01;
        private const uint ULW_ALPHA      = 0x00000002;
        private const uint BI_RGB         = 0;
        private const uint DIB_RGB_COLORS = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER {
            public uint   biSize;
            public int    biWidth;
            public int    biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint   biCompression;
            public uint   biSizeImage;
            public int    biXPelsPerMeter;
            public int    biYPelsPerMeter;
            public uint   biClrUsed;
            public uint   biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr ho);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        #endregion

    }
}
