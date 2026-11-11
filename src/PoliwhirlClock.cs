// Poliwhirl Clock - the Pokemon Stadium 2 Mystery Gift clock as a live 4K desktop clock for Windows.
//
// Art (assets\, compiled into the exe; an assets\ folder next to the exe overrides single files): 7 weekday
// bodies (1728x1824 = 12x the in-game 144x152 sprite), hour/minute/second hands (192x372, pivot =
// bottom-centre), the centre pin (192x120), layout.ini, tick.wav and the icon. The clock follows the PC time,
// ticks once per second like the game, and switches the weekday body at midnight.
// Hand angles are the game's (f40 func_88B01FE0): hour = (h + m/60)*30, minute = (m + s/60)*6, second = s*6.
// Default size/place = Pokemon Stadium 2 (body 144x152 at x=451,y=45 of 640x480 -> top-right corner).
//
// Build: build.cmd (uses the C# 5 compiler that ships with .NET Framework 4.8 - nothing to install).
// Command line:  PoliwhirlClock.exe                 run the clock (single instance)
//                PoliwhirlClock.exe --exit          close a running clock
//                PoliwhirlClock.exe --snapshot out.png [yyyy-MM-ddTHH:mm:ss] [heightPx]   render one frame

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Poliwhirl Clock")]
[assembly: System.Reflection.AssemblyDescription("Pokemon Stadium 2 Poliwhirl clock for the Windows desktop")]
[assembly: System.Reflection.AssemblyProduct("Poliwhirl Clock")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace PoliwhirlClock
{
    static class Native
    {
        public const int WS_EX_LAYERED = 0x00080000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        public const int WM_NCHITTEST = 0x0084, WM_MOUSEACTIVATE = 0x0021, WM_NCLBUTTONDBLCLK = 0x00A3,
            WM_NCRBUTTONUP = 0x00A5, WM_RBUTTONUP = 0x0205, WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232,
            WM_DPICHANGED = 0x02E0, WM_SYSCOMMAND = 0x0112;
        public const int HTCLIENT = 1, HTCAPTION = 2, MA_NOACTIVATE = 3, ULW_ALPHA = 2;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1), HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int CX, CY; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
        public static extern bool UpdateLayeredWindowMove(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
        public static extern bool UpdateLayeredWindowStay(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage,
            out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] public static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong32(IntPtr hWnd, int index, int value);
        public const int GWLP_HWNDPARENT = -8;
        public const uint GW_OWNER = 4;

        /// <summary>Sets a top-level window's owner (GWLP_HWNDPARENT) on 32- and 64-bit Windows.</summary>
        public static void SetOwner(IntPtr hWnd, IntPtr owner)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(hWnd, GWLP_HWNDPARENT, owner.ToInt32());
        }
    }

    /// <summary>
    /// The art, layout, tick and icon. build.cmd compiles everything in assets\ into the exe (resources named
    /// "assets.&lt;file&gt;"), so PoliwhirlClock.exe runs on its own. A file with the same name in an assets\ folder
    /// next to the exe overrides the built-in copy, so the art can still be swapped without rebuilding.
    /// </summary>
    static class Assets
    {
        public static readonly string Dir = Path.Combine(Program.BaseDir, "assets");

        static string FilePath(string name) { return Path.Combine(Dir, name); }

        public static bool Exists(string name)
        {
            return File.Exists(FilePath(name))
                || typeof(Assets).Assembly.GetManifestResourceInfo("assets." + name) != null;
        }

        /// <summary>The asset's bytes (folder copy first, then the built-in one), or null if there is none.</summary>
        public static byte[] Read(string name)
        {
            string file = FilePath(name);
            if (File.Exists(file)) return File.ReadAllBytes(file);
            using (Stream s = typeof(Assets).Assembly.GetManifestResourceStream("assets." + name))
            {
                if (s == null) return null;
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        public static byte[] ReadRequired(string name)
        {
            byte[] b = Read(name);
            if (b == null) throw new FileNotFoundException("Missing artwork: " + name + " (not built in and not in " + Dir + ")");
            return b;
        }
    }

    /// <summary>Art geometry + default placement (layout.ini, all optional).</summary>
    class Layout
    {
        public float PivotX = 860.6f, PivotY = 622.0f;          // clock centre in the 1728x1824 body art
        public float HandPivotX = 96f, HandPivotY = 372f;       // pivot inside each 192x372 hand
        public float PinCenterX = 96f, PinCenterY = 60f;        // centre of the 192x120 pin
        public double BodyHeight = 152.0 / 480.0;               // clock height as a fraction of the screen
        public double MarginRight = 45.0 / 640.0, MarginTop = 45.0 / 480.0;

        public static Layout Load()
        {
            Layout l = new Layout();
            byte[] bytes = Assets.Read("layout.ini");
            if (bytes == null) return l;
            string text = System.Text.Encoding.UTF8.GetString(bytes);
            foreach (string raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                int i = line.IndexOf('=');
                if (i < 0) continue;
                string k = line.Substring(0, i).Trim().ToLowerInvariant();
                double d;
                if (!double.TryParse(line.Substring(i + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) continue;
                switch (k)
                {
                    case "pivotx": l.PivotX = (float)d; break;
                    case "pivoty": l.PivotY = (float)d; break;
                    case "handpivotx": l.HandPivotX = (float)d; break;
                    case "handpivoty": l.HandPivotY = (float)d; break;
                    case "pincenterx": l.PinCenterX = (float)d; break;
                    case "pincentery": l.PinCenterY = (float)d; break;
                    case "bodyheight": if (d > 0.02 && d < 1.5) l.BodyHeight = d; break;
                    case "marginright": l.MarginRight = d; break;
                    case "margintop": l.MarginTop = d; break;
                }
            }
            return l;
        }
    }

    /// <summary>User settings (settings.ini in Program.DataDir: next to the exe, or %APPDATA%\PoliwhirlClock).</summary>
    class Settings
    {
        public double Scale = 1.0;
        public bool Custom, Locked, TopMost;
        public bool Tick = true;          // play the Stadium 2 tick every second
        public double TickVolume = 0.5;   // 0.5 ~ the game's note velocity (0.50)
        public int Right, Top;          // custom position = the clock window's top-right corner (screen px)
        readonly string path;

        public Settings(string path)
        {
            this.path = path;
            try
            {
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    int i = raw.IndexOf('=');
                    if (i < 0 || raw.TrimStart().StartsWith(";")) continue;
                    string k = raw.Substring(0, i).Trim().ToLowerInvariant();
                    double d;
                    if (!double.TryParse(raw.Substring(i + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) continue;
                    switch (k)
                    {
                        case "scale": if (d >= 0.2 && d <= 3.0) Scale = d; break;
                        case "custom": Custom = d != 0; break;
                        case "locked": Locked = d != 0; break;
                        case "topmost": TopMost = d != 0; break;
                        case "right": Right = (int)d; break;
                        case "top": Top = (int)d; break;
                        case "tick": Tick = d != 0; break;
                        case "tickvolume": if (d >= 0 && d <= 1) TickVolume = d; break;
                    }
                }
            }
            catch (Exception ex) { Program.Log("settings load: " + ex.Message); }
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(path, new string[] {
                    "; Poliwhirl Clock settings (written by the app)",
                    "Scale=" + Scale.ToString(CultureInfo.InvariantCulture),
                    "Custom=" + (Custom ? "1" : "0"),
                    "Right=" + Right.ToString(CultureInfo.InvariantCulture),
                    "Top=" + Top.ToString(CultureInfo.InvariantCulture),
                    "Locked=" + (Locked ? "1" : "0"),
                    "TopMost=" + (TopMost ? "1" : "0"),
                    "Tick=" + (Tick ? "1" : "0"),
                    "TickVolume=" + TickVolume.ToString(CultureInfo.InvariantCulture) });
            }
            catch (Exception ex) { Program.Log("settings save: " + ex.Message); }
        }
    }

    /// <summary>Loads the art and composes clock frames (premultiplied ARGB).</summary>
    class Renderer : IDisposable
    {
        public static readonly string[] DayNames = { "SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" };
        public Layout Layout;
        Bitmap hourSrc, minuteSrc, secondSrc, pinSrc;   // full-size art
        Bitmap body, hour, minute, second, pin;         // scaled for the current size
        int cachedDay = -1, cachedHeight = -1;
        float s = 1f;                                   // art px -> screen px

        public Renderer()
        {
            Reload();
        }

        public void Reload()
        {
            Layout = Layout.Load();
            Bitmap h = LoadPArgb("hand_hour.png");
            Bitmap m = LoadPArgb("hand_minute.png");
            Bitmap sec = LoadPArgb("hand_second.png");
            Bitmap p = LoadPArgb("center_pin.png");
            Free(ref hourSrc); Free(ref minuteSrc); Free(ref secondSrc); Free(ref pinSrc);
            hourSrc = h; minuteSrc = m; secondSrc = sec; pinSrc = p;
            cachedDay = -1; cachedHeight = -1;
        }

        /// <summary>Loads a PNG asset into memory (no file lock, so art can be edited while the clock runs).</summary>
        public static Bitmap LoadPArgb(string name)
        {
            byte[] bytes = Assets.ReadRequired(name);
            using (MemoryStream ms = new MemoryStream(bytes))
            using (Image img = Image.FromStream(ms))
            {
                Bitmap b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb);
                b.SetResolution(96, 96);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
                }
                return b;
            }
        }

        /// <summary>Makes sure the body for today's weekday and all parts are scaled for targetHeight.</summary>
        public Size Prepare(DateTime now, int targetHeight)
        {
            int day = (int)now.DayOfWeek;
            if (day != cachedDay || targetHeight != cachedHeight || body == null)
            {
                string name = "body_" + DayNames[day] + ".png";
                if (!Assets.Exists(name)) name = "body_base.png";
                using (Bitmap src = LoadPArgb(name))
                {
                    s = (float)targetHeight / src.Height;
                    Replace(ref body, ScaleArt(src, s));
                }
                if (targetHeight != cachedHeight || hour == null)
                {
                    Replace(ref hour, ScaleArt(hourSrc, s));
                    Replace(ref minute, ScaleArt(minuteSrc, s));
                    Replace(ref second, ScaleArt(secondSrc, s));
                    Replace(ref pin, ScaleArt(pinSrc, s));
                }
                cachedDay = day;
                cachedHeight = targetHeight;
            }
            return body.Size;
        }

        public Bitmap Compose(DateTime now)
        {
            Bitmap frame = new Bitmap(body.Width, body.Height, PixelFormat.Format32bppPArgb);
            frame.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(frame))
            {
                Quality(g);
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(body, new Rectangle(0, 0, body.Width, body.Height));
                g.CompositingMode = CompositingMode.SourceOver;
                float px = Layout.PivotX * s, py = Layout.PivotY * s;
                double h = now.Hour % 12, m = now.Minute, sec = now.Second;
                DrawHand(g, hour, px, py, (float)((h + m / 60.0) * 30.0));
                DrawHand(g, minute, px, py, (float)((m + sec / 60.0) * 6.0));
                DrawHand(g, second, px, py, (float)(sec * 6.0));
                g.DrawImage(pin, new RectangleF(px - Layout.PinCenterX * s, py - Layout.PinCenterY * s, pin.Width, pin.Height));
            }
            return frame;
        }

        void DrawHand(Graphics g, Bitmap hand, float px, float py, float angle)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(px, py);
            g.RotateTransform(angle);
            g.DrawImage(hand, new RectangleF(-Layout.HandPivotX * s, -Layout.HandPivotY * s, hand.Width, hand.Height));
            g.Restore(st);
        }

        static Bitmap ScaleArt(Bitmap src, float s)
        {
            float fw = src.Width * s, fh = src.Height * s;
            Bitmap b = new Bitmap(Math.Max(1, (int)Math.Ceiling(fw)), Math.Max(1, (int)Math.Ceiling(fh)), PixelFormat.Format32bppPArgb);
            b.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(b))
            using (ImageAttributes ia = new ImageAttributes())
            {
                Quality(g);
                g.Clear(Color.Transparent);
                ia.SetWrapMode(WrapMode.TileFlipXY);   // no dark/half-transparent seams at the image edges
                PointF[] dest = { new PointF(0, 0), new PointF(fw, 0), new PointF(0, fh) };
                g.DrawImage(src, dest, new RectangleF(0, 0, src.Width, src.Height), GraphicsUnit.Pixel, ia);
            }
            return b;
        }

        static void Quality(Graphics g)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.CompositingQuality = CompositingQuality.AssumeLinear;
        }

        static void Free(ref Bitmap b) { if (b != null) { b.Dispose(); b = null; } }
        static void Replace(ref Bitmap b, Bitmap nb) { Free(ref b); b = nb; }

        public void Dispose()
        {
            Free(ref hourSrc); Free(ref minuteSrc); Free(ref secondSrc); Free(ref pinSrc);
            Free(ref body); Free(ref hour); Free(ref minute); Free(ref second); Free(ref pin);
        }
    }

    /// <summary>
    /// The Stadium 2 clock tick (assets\tick.wav, extracted from the ROM by tools\extract_tick.py):
    /// in Mystery Gift, f40 func_88B003B0 plays sound 0x155 every time the second advances.
    ///
    /// Plays through one continuous winmm waveOut stream (silence between ticks). Opening a short sound
    /// once a second (SoundPlayer/PlaySound) lets USB headsets/speakers power down between ticks, and a
    /// waking device swallows an 8 ms click - that is why the tick could suddenly go silent.
    /// </summary>
    class Ticker : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr hwo, int uDeviceID, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr hwo, IntPtr pwh, int cbwh);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr hwo);
        const int WAVE_MAPPER = -1, CALLBACK_EVENT = 0x50000, WHDR_INQUEUE = 0x10;
        const int BUFFERS = 3, BUFFER_MS = 20;          // ~60 ms queue: tick lands within 60 ms of the second
        static readonly int HdrSize = Marshal.SizeOf(typeof(WAVEHDR));
        static readonly int FlagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");

        short[] pcm;
        int rate, chunk;
        volatile float volume;
        int tickPos = int.MaxValue;                     // next tick sample to output; MaxValue = idle
        readonly AutoResetEvent bufferDone = new AutoResetEvent(false);
        Thread thread;
        volatile bool running;

        public Ticker(byte[] wav, double volume, bool enabled)
        {
            Load(wav);
            this.volume = (float)volume;
            chunk = Math.Max(64, rate * BUFFER_MS / 1000);
            SetEnabled(enabled);
        }

        void Load(byte[] b)
        {
            int channels = 0, bits = 0, p = 12;
            byte[] data = null;
            while (p + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                int len = BitConverter.ToInt32(b, p + 4);
                if (id == "fmt ") { channels = BitConverter.ToInt16(b, p + 10); rate = BitConverter.ToInt32(b, p + 12); bits = BitConverter.ToInt16(b, p + 22); }
                else if (id == "data") { data = new byte[Math.Min(len, b.Length - p - 8)]; Buffer.BlockCopy(b, p + 8, data, 0, data.Length); }
                p += 8 + len + (len & 1);
            }
            if (data == null || channels != 1 || bits != 16) throw new InvalidDataException("tick.wav must be 16-bit mono PCM");
            pcm = new short[data.Length / 2];
            Buffer.BlockCopy(data, 0, pcm, 0, pcm.Length * 2);
        }

        public void SetVolume(double v) { volume = (float)v; }

        /// <summary>Starts/stops the audio stream (stopped = the device is released and may sleep).</summary>
        public void SetEnabled(bool on)
        {
            if (on && !running)
            {
                running = true;
                thread = new Thread(StreamLoop);
                thread.IsBackground = true;
                thread.Name = "tick audio";
                thread.Priority = ThreadPriority.AboveNormal;
                thread.Start();
            }
            else if (!on && running)
            {
                running = false;
                bufferDone.Set();
                if (thread != null) thread.Join(1000);
                thread = null;
            }
        }

        public void Play() { Interlocked.Exchange(ref tickPos, 0); }

        void StreamLoop()
        {
            IntPtr hwo = IntPtr.Zero;
            IntPtr[] hdrs = new IntPtr[BUFFERS], bufs = new IntPtr[BUFFERS];
            short[] tmp = new short[chunk];
            try
            {
                while (running)
                {
                    // (re)open the device
                    if (hwo == IntPtr.Zero)
                    {
                        WAVEFORMATEX f = new WAVEFORMATEX();
                        f.wFormatTag = 1; f.nChannels = 1; f.nSamplesPerSec = (uint)rate; f.wBitsPerSample = 16;
                        f.nBlockAlign = 2; f.nAvgBytesPerSec = (uint)rate * 2;
                        int r = waveOutOpen(out hwo, WAVE_MAPPER, ref f, bufferDone.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
                        if (r != 0) { hwo = IntPtr.Zero; Program.Log("tick: waveOutOpen error " + r); WaitOrStop(3000); continue; }
                        for (int i = 0; i < BUFFERS; i++)
                        {
                            bufs[i] = Marshal.AllocHGlobal(chunk * 2);
                            hdrs[i] = Marshal.AllocHGlobal(HdrSize);
                            WAVEHDR h = new WAVEHDR();
                            h.lpData = bufs[i]; h.dwBufferLength = (uint)(chunk * 2);
                            Marshal.StructureToPtr(h, hdrs[i], false);
                            waveOutPrepareHeader(hwo, hdrs[i], HdrSize);
                        }
                    }
                    // refill every finished buffer: tick samples if a tick is pending, else silence
                    bool failed = false;
                    for (int i = 0; i < BUFFERS && !failed; i++)
                    {
                        if ((Marshal.ReadInt32(hdrs[i], FlagsOffset) & WHDR_INQUEUE) != 0) continue;
                        float v = volume;
                        for (int k = 0; k < chunk; k++)
                        {
                            int pos = tickPos;
                            if (pos < pcm.Length)
                            {
                                tmp[k] = (short)Math.Max(-32768, Math.Min(32767, (int)(pcm[pos] * v)));
                                Interlocked.CompareExchange(ref tickPos, pos + 1, pos);
                            }
                            else tmp[k] = 0;
                        }
                        Marshal.Copy(tmp, 0, bufs[i], chunk);
                        int w = waveOutWrite(hwo, hdrs[i], HdrSize);
                        if (w != 0) { Program.Log("tick: waveOutWrite error " + w + " - reopening"); failed = true; }
                    }
                    if (failed) { Close(ref hwo, hdrs, bufs); WaitOrStop(1000); continue; }
                    bufferDone.WaitOne(100);
                }
            }
            catch (Exception ex) { Program.Log("tick stream: " + ex); }
            finally { Close(ref hwo, hdrs, bufs); }
        }

        void WaitOrStop(int ms) { bufferDone.WaitOne(ms); }

        static void Close(ref IntPtr hwo, IntPtr[] hdrs, IntPtr[] bufs)
        {
            if (hwo != IntPtr.Zero) waveOutReset(hwo);
            for (int i = 0; i < hdrs.Length; i++)
            {
                if (hdrs[i] != IntPtr.Zero)
                {
                    if (hwo != IntPtr.Zero) waveOutUnprepareHeader(hwo, hdrs[i], HdrSize);
                    Marshal.FreeHGlobal(hdrs[i]); hdrs[i] = IntPtr.Zero;
                }
                if (bufs[i] != IntPtr.Zero) { Marshal.FreeHGlobal(bufs[i]); bufs[i] = IntPtr.Zero; }
            }
            if (hwo != IntPtr.Zero) { waveOutClose(hwo); hwo = IntPtr.Zero; }
        }

        public void Dispose() { SetEnabled(false); }
    }

    /// <summary>The transparent clock window (per-pixel alpha layered window, no taskbar button).</summary>
    class ClockForm : Form
    {
        readonly ClockContext ctx;
        bool moving;

        public ClockForm(ClockContext ctx)
        {
            this.ctx = ctx;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Poliwhirl Clock";
            Bounds = new Rectangle(-32000, -32000, 1, 1);   // positioned by the first frame
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyOwner();
        }

        /// <summary>
        /// WinForms re-owns windows with ShowInTaskbar=false to its own hidden window, so the desktop
        /// owner from CreateParams is lost; set it again (owner = desktop keeps the clock on the
        /// desktop behind apps and visible on Win+D). Always-on-top mode has no owner.
        /// </summary>
        public void ApplyOwner()
        {
            if (!IsHandleCreated) return;
            IntPtr owner = IntPtr.Zero;
            if (!ctx.Settings.TopMost) owner = Native.FindWindow("Progman", null);
            if (Native.GetWindow(Handle, Native.GW_OWNER) != owner) Native.SetOwner(Handle, owner);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                if (ctx != null && !ctx.Settings.TopMost)
                {
                    // owned by the desktop window: sits on the desktop (behind apps) and survives Win+D
                    IntPtr progman = Native.FindWindow("Progman", null);
                    if (progman != IntPtr.Zero) cp.Parent = progman;
                }
                return cp;
            }
        }

        public void Present(Bitmap frame, bool move, Point pos)
        {
            if (!IsHandleCreated) return;
            int w = frame.Width, h = frame.Height;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = w;
            bi.biHeight = -h;            // top-down
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            IntPtr bits;
            IntPtr dib = Native.CreateDIBSection(memDc, ref bi, 0, out bits, IntPtr.Zero, 0);
            IntPtr old = Native.SelectObject(memDc, dib);
            try
            {
                BitmapData bd = frame.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try { Native.CopyMemory(bits, bd.Scan0, new UIntPtr((uint)(w * h * 4))); }
                finally { frame.UnlockBits(bd); }

                Native.SIZE size; size.CX = w; size.CY = h;
                Native.POINT src; src.X = 0; src.Y = 0;
                Native.BLENDFUNCTION bf; bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
                if (move && !moving)
                {
                    Native.POINT dst; dst.X = pos.X; dst.Y = pos.Y;
                    Native.UpdateLayeredWindowMove(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, Native.ULW_ALPHA);
                }
                else
                {
                    Native.UpdateLayeredWindowStay(Handle, screenDc, IntPtr.Zero, ref size, memDc, ref src, 0, ref bf, Native.ULW_ALPHA);
                }
            }
            finally
            {
                Native.SelectObject(memDc, old);
                Native.DeleteObject(dib);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Native.WM_NCHITTEST:            // drag anywhere on the clock (unless locked)
                    m.Result = (IntPtr)(ctx.Settings.Locked ? Native.HTCLIENT : Native.HTCAPTION);
                    return;
                case Native.WM_MOUSEACTIVATE:        // never steal focus
                    m.Result = (IntPtr)Native.MA_NOACTIVATE;
                    return;
                case Native.WM_NCLBUTTONDBLCLK:      // no maximise on double-click
                    return;
                case Native.WM_NCRBUTTONUP:
                case Native.WM_RBUTTONUP:
                    // WinForms captures the mouse on button-down and releases it in the default button-up
                    // handling, which this case skips. A capture left on the clock steals the mouse moves
                    // over the menu (same thread), so hovering "Size"/"Position"/"Tick volume" did nothing
                    // until a click released it.
                    Capture = false;
                    ctx.ShowMenu(Cursor.Position);
                    return;
                case Native.WM_ENTERSIZEMOVE:
                    moving = true;
                    break;
                case Native.WM_EXITSIZEMOVE:
                    moving = false;
                    base.WndProc(ref m);
                    ctx.OnUserMoved(this);
                    return;
                case Native.WM_DPICHANGED:           // size is driven by the screen height, not by DPI
                    m.Result = IntPtr.Zero;
                    return;
                case Native.WM_SYSCOMMAND:
                    int cmd = m.WParam.ToInt32() & 0xFFF0;
                    if (cmd == 0xF030 || cmd == 0xF020) return;   // SC_MAXIMIZE / SC_MINIMIZE
                    break;
            }
            base.WndProc(ref m);
        }
    }

    /// <summary>Owns the clock window, tray icon, menu, timer and settings.</summary>
    class ClockContext : ApplicationContext
    {
        public readonly Settings Settings;
        public readonly Renderer Renderer;
        readonly string baseDir;
        readonly EventWaitHandle exitEvent;
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer peekTimer = new System.Windows.Forms.Timer();
        ClockForm form;
        ContextMenuStrip menu;
        ToolStripMenuItem miLock, miTop, miStartup;
        ToolStripMenuItem[] miSizes;
        static readonly double[] SizeSteps = { 0.5, 0.75, 1.0, 1.25, 1.5 };
        static readonly string[] SizeNames = { "Small (50%)", "Medium (75%)", "Stadium 2 size (100%)", "Large (125%)", "Extra large (150%)" };
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "PoliwhirlClock";
        volatile bool relayout;
        bool peeking, exiting, keepMenuOpen;
        Ticker ticker;
        int lastSecond = -1;
        ToolStripMenuItem miTick;
        ToolStripMenuItem[] miTickVol;
        static readonly double[] TickVolumes = { 0.25, 0.5, 0.75, 1.0 };
        static readonly string[] TickVolumeNames = { "Quiet (25%)", "Stadium 2 level (50%)", "Loud (75%)", "Full (100%)" };

        public ClockContext(string baseDir, EventWaitHandle exitEvent)
        {
            this.baseDir = baseDir;
            this.exitEvent = exitEvent;
            Settings = new Settings(Path.Combine(Program.DataDir, "settings.ini"));
            Renderer = new Renderer();
            try { ticker = new Ticker(Assets.ReadRequired("tick.wav"), Settings.TickVolume, Settings.Tick); }
            catch (Exception ex) { Program.Log("tick sound unavailable: " + ex.Message); }
            BuildMenu();
            tray = new NotifyIcon();
            tray.Text = "Poliwhirl Clock";
            tray.Icon = LoadTrayIcon();
            tray.ContextMenuStrip = menu;
            tray.MouseDoubleClick += delegate { Peek(); };
            tray.Visible = true;
            SystemEvents.DisplaySettingsChanged += OnSystemChange;
            SystemEvents.TimeChanged += OnSystemChange;
            SystemEvents.PowerModeChanged += OnPowerChange;
            timer.Tick += OnTick;
            peekTimer.Interval = 6000;
            peekTimer.Tick += delegate { peekTimer.Stop(); peeking = false; ApplyZOrder(); };
            Render(true);
            ScheduleNext();
        }

        void OnSystemChange(object sender, EventArgs e) { relayout = true; }
        void OnPowerChange(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) relayout = true; }

        void OnTick(object sender, EventArgs e)
        {
            timer.Stop();
            if (exitEvent.WaitOne(0)) { ExitClock(); return; }
            try
            {
                bool move = relayout;
                relayout = false;
                Render(move);
            }
            catch (Exception ex) { Program.Log("render: " + ex); }
            ScheduleNext();
        }

        void ScheduleNext()
        {
            if (exiting) return;
            int ms = 1000 - DateTime.Now.Millisecond + 20;     // tick just after each new second
            if (ms < 100) ms += 1000;
            timer.Interval = ms;
            timer.Start();
        }

        void Render(bool move)
        {
            if (exiting) return;
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
            {
                if (form != null) { try { form.Dispose(); } catch { } }
                form = new ClockForm(this);
                form.Show();
                move = true;
            }
            if (move) form.ApplyOwner();
            DateTime now = DateTime.Now;
            if (now.Second != lastSecond)
            {
                // like the game: the tick plays when the second hand advances (not on the first frame)
                if (lastSecond >= 0 && Settings.Tick && ticker != null) ticker.Play();
                lastSecond = now.Second;
            }
            Rectangle scr = Screen.PrimaryScreen.Bounds;
            int targetH = Math.Max(64, (int)Math.Round(scr.Height * Renderer.Layout.BodyHeight * Settings.Scale));
            Size size = Renderer.Prepare(now, targetH);
            Point pos = Position(scr, size);
            using (Bitmap frame = Renderer.Compose(now)) form.Present(frame, move, pos);
            if (move) ApplyZOrder();
        }

        Point Position(Rectangle scr, Size size)
        {
            if (Settings.Custom)
            {
                // anchored at its top-right corner, so changing the size keeps it in the corner;
                // clamped onto the screen it is (mostly) on
                Rectangle r = new Rectangle(Settings.Right - size.Width, Settings.Top, size.Width, size.Height);
                Rectangle b = Screen.FromRectangle(r).Bounds;
                return new Point(Math.Max(b.Left, Math.Min(b.Right - size.Width, r.Left)),
                                 Math.Max(b.Top, Math.Min(b.Bottom - size.Height, r.Top)));
            }
            return new Point(scr.Right - (int)Math.Round(scr.Width * Renderer.Layout.MarginRight) - size.Width,
                             scr.Top + (int)Math.Round(scr.Height * Renderer.Layout.MarginTop));
        }

        void ApplyZOrder()
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated) return;
            const uint f = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
            if (Settings.TopMost || peeking)
                Native.SetWindowPos(form.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, f);
            else
            {
                Native.SetWindowPos(form.Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, f);
                Native.SetWindowPos(form.Handle, Native.HWND_BOTTOM, 0, 0, 0, 0, f);
            }
        }

        public void OnUserMoved(ClockForm f)
        {
            Native.RECT r;
            if (Native.GetWindowRect(f.Handle, out r))
            {
                Settings.Custom = true;
                Settings.Right = r.Right;
                Settings.Top = r.Top;
                Settings.Save();
            }
            ApplyZOrder();
        }

        /// <summary>Moves the clock by (dx, dy) screen pixels, keeping it fully on its screen.</summary>
        void MoveBy(int dx, int dy)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated) return;
            Native.RECT r;
            if (!Native.GetWindowRect(form.Handle, out r)) return;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            Rectangle b = Screen.FromRectangle(new Rectangle(r.Left + dx, r.Top + dy, w, h)).Bounds;
            int x = Math.Max(b.Left, Math.Min(b.Right - w, r.Left + dx));
            int y = Math.Max(b.Top, Math.Min(b.Bottom - h, r.Top + dy));
            Settings.Custom = true;
            Settings.Right = x + w;
            Settings.Top = y;
            Settings.Save();
            SafeRender();
        }

        ToolStripMenuItem Nudge(string text, int dx, int dy)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Tag = "nudge";
            it.Click += delegate { MoveBy(dx, dy); };
            return it;
        }

        // nudge items keep the menu open so you can click them several times in a row
        void OnMenuItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            keepMenuOpen = e.ClickedItem != null && "nudge".Equals(e.ClickedItem.Tag);
        }

        void OnMenuClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (keepMenuOpen && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        }

        void Peek()
        {
            peeking = true;
            ApplyZOrder();
            peekTimer.Stop();
            peekTimer.Start();
        }

        /// <summary>
        /// The clock window never activates (it must stay on the desktop), so the menu used to open while
        /// another app was in the foreground. Clicks in other apps then never reach this app and the menu
        /// stayed open. Activating a never-shown helper window first makes this app the foreground app
        /// (the same trick NotifyIcon uses for tray menus), so clicking anywhere else deactivates it and
        /// WinForms closes the menu (AppFocusChange). The clock itself is not raised.
        /// </summary>
        NativeWindow menuHost;

        public void ShowMenu(Point screenPos)
        {
            if (menuHost == null)
            {
                menuHost = new NativeWindow();
                menuHost.CreateHandle(new CreateParams());     // invisible, never shown
            }
            Native.SetForegroundWindow(menuHost.Handle);
            menu.Show(screenPos);
        }

        void BuildMenu()
        {
            menu = new ContextMenuStrip();
            ToolStripLabel title = new ToolStripLabel("Poliwhirl Clock");
            title.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem size = new ToolStripMenuItem("Size");
            miSizes = new ToolStripMenuItem[SizeSteps.Length];
            for (int i = 0; i < SizeSteps.Length; i++)
            {
                double v = SizeSteps[i];
                ToolStripMenuItem it = new ToolStripMenuItem(SizeNames[i]);
                it.Click += delegate { Settings.Scale = v; Settings.Save(); SafeRender(); };
                miSizes[i] = it;
                size.DropDownItems.Add(it);
            }
            menu.Items.Add(size);

            // Position: step = 1% of the screen height (22 px on a 3840x2160 screen)
            ToolStripMenuItem position = new ToolStripMenuItem("Position");
            int step = Math.Max(4, (int)Math.Round(Screen.PrimaryScreen.Bounds.Height * 0.01));
            position.DropDownItems.Add(Nudge("↗   Up and right (towards the corner)", step, -step));
            position.DropDownItems.Add(Nudge("↙   Down and left (away from the corner)", -step, step));
            position.DropDownItems.Add(new ToolStripSeparator());
            position.DropDownItems.Add(Nudge("↑   Up", 0, -step));
            position.DropDownItems.Add(Nudge("↓   Down", 0, step));
            position.DropDownItems.Add(Nudge("←   Left", -step, 0));
            position.DropDownItems.Add(Nudge("→   Right", step, 0));
            position.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem home = new ToolStripMenuItem("Back to the Stadium 2 spot");
            home.Click += delegate { Settings.Custom = false; Settings.Save(); SafeRender(); };
            position.DropDownItems.Add(home);
            ToolStripLabel tip = new ToolStripLabel("Tip: you can also drag the clock with the left mouse button");
            tip.ForeColor = SystemColors.GrayText;
            position.DropDownItems.Add(tip);
            position.DropDown.ItemClicked += OnMenuItemClicked;
            position.DropDown.Closing += OnMenuClosing;
            menu.Items.Add(position);

            miLock = new ToolStripMenuItem("Lock position");
            miLock.Click += delegate { Settings.Locked = !Settings.Locked; Settings.Save(); };
            menu.Items.Add(miLock);

            miTop = new ToolStripMenuItem("Always on top");
            miTop.Click += delegate
            {
                Settings.TopMost = !Settings.TopMost;
                Settings.Save();
                if (form != null) { form.Dispose(); form = null; }   // recreated with the new owner/z-order
                SafeRender();
            };
            menu.Items.Add(miTop);
            menu.Items.Add(new ToolStripSeparator());

            miTick = new ToolStripMenuItem("Tick sound");
            miTick.Click += delegate { Settings.Tick = !Settings.Tick; Settings.Save(); if (ticker != null) ticker.SetEnabled(Settings.Tick); };
            menu.Items.Add(miTick);
            ToolStripMenuItem tickVol = new ToolStripMenuItem("Tick volume");
            miTickVol = new ToolStripMenuItem[TickVolumes.Length];
            for (int i = 0; i < TickVolumes.Length; i++)
            {
                double v = TickVolumes[i];
                ToolStripMenuItem it = new ToolStripMenuItem(TickVolumeNames[i]);
                it.Click += delegate
                {
                    Settings.TickVolume = v;
                    Settings.Tick = true;
                    Settings.Save();
                    if (ticker != null) { ticker.SetEnabled(true); ticker.SetVolume(v); ticker.Play(); }   // preview
                };
                miTickVol[i] = it;
                tickVol.DropDownItems.Add(it);
            }
            menu.Items.Add(tickVol);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem reload = new ToolStripMenuItem("Reload artwork");
            reload.Click += delegate
            {
                try { Renderer.Reload(); SafeRender(); }
                catch (Exception ex) { MessageBox.Show("Could not reload the artwork:\n\n" + ex.Message, "Poliwhirl Clock"); }
            };
            menu.Items.Add(reload);

            miStartup = new ToolStripMenuItem("Start with Windows");
            miStartup.Click += delegate { SetStartup(!StartupEnabled()); };
            menu.Items.Add(miStartup);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { ExitClock(); };
            menu.Items.Add(exit);

            menu.ItemClicked += OnMenuItemClicked;
            menu.Closing += OnMenuClosing;
            menu.Opening += delegate
            {
                keepMenuOpen = false;
                miTick.Checked = Settings.Tick;
                miTick.Enabled = ticker != null;
                for (int i = 0; i < TickVolumes.Length; i++) miTickVol[i].Checked = Math.Abs(Settings.TickVolume - TickVolumes[i]) < 0.001;
                miLock.Checked = Settings.Locked;
                miTop.Checked = Settings.TopMost;
                miStartup.Checked = StartupEnabled();
                for (int i = 0; i < SizeSteps.Length; i++) miSizes[i].Checked = Math.Abs(Settings.Scale - SizeSteps[i]) < 0.001;
            };
        }

        void SafeRender()
        {
            try { Render(true); }
            catch (Exception ex) { Program.Log("render: " + ex); }
        }

        static bool StartupEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        static void SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch (Exception ex) { MessageBox.Show("Could not change the Windows start-up setting:\n\n" + ex.Message, "Poliwhirl Clock"); }
        }

        Icon LoadTrayIcon()
        {
            try
            {
                byte[] ico = Assets.Read("poliwhirl.ico");
                if (ico != null) using (MemoryStream ms = new MemoryStream(ico)) return new Icon(ms, SystemInformation.SmallIconSize);
            }
            catch { }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            return SystemIcons.Application;
        }

        void ExitClock()
        {
            if (exiting) return;
            exiting = true;
            timer.Stop();
            peekTimer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnSystemChange;
            SystemEvents.TimeChanged -= OnSystemChange;
            SystemEvents.PowerModeChanged -= OnPowerChange;
            tray.Visible = false;
            tray.Dispose();
            if (menuHost != null) { menuHost.DestroyHandle(); menuHost = null; }
            if (form != null) { form.Dispose(); form = null; }
            Renderer.Dispose();
            if (ticker != null) ticker.Dispose();
            ExitThread();
        }
    }

    static class Program
    {
        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Where settings.ini and log.txt live. Portable by default: next to the exe. If that folder is not
        /// writable (e.g. the clock was put under Program Files), fall back to %APPDATA%\PoliwhirlClock.
        /// </summary>
        public static readonly string DataDir = PickDataDir();

        static string PickDataDir()
        {
            if (CanWrite(BaseDir)) return BaseDir;
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PoliwhirlClock");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".write_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(DataDir, "log.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ", CultureInfo.InvariantCulture) + msg + Environment.NewLine);
            }
            catch { }
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--snapshot") return Snapshot(args);

            bool createdNew;
            using (EventWaitHandle exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\PoliwhirlClock_Exit"))
            {
                if (args.Length >= 1 && args[0] == "--exit") { exitEvent.Set(); return 0; }
                using (Mutex single = new Mutex(true, @"Local\PoliwhirlClock_Single", out createdNew))
                {
                    if (!createdNew) return 0;      // already running
                    exitEvent.Reset();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Log("UI exception: " + e.Exception); };
                    AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Log("fatal: " + e.ExceptionObject); };
                    try
                    {
                        Application.Run(new ClockContext(BaseDir, exitEvent));
                    }
                    catch (Exception ex)
                    {
                        Log("startup failed: " + ex);
                        MessageBox.Show("Poliwhirl Clock could not start:\n\n" + ex.Message, "Poliwhirl Clock",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 1;
                    }
                }
            }
            return 0;
        }

        static int Snapshot(string[] args)
        {
            try
            {
                DateTime t = args.Length >= 3
                    ? DateTime.ParseExact(args[2], "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : DateTime.Now;
                int h = args.Length >= 4 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 684;
                using (Renderer r = new Renderer())
                {
                    r.Prepare(t, h);
                    using (Bitmap f = r.Compose(t)) f.Save(args[1], ImageFormat.Png);
                }
                return 0;
            }
            catch (Exception ex) { Log("snapshot: " + ex); return 2; }
        }
    }
}
