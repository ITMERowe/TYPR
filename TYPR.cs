using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows.Forms;

namespace TYPR
{
    // CNTR's design tokens (CNTR docs/DESIGN.md), shared by every ITMERowe project: 4/8 spacing, three radii,
    // one type scale, one motion vocabulary. Use the token, never the literal.
    internal static class UiSpacing
    {
        public const int XSmall = 4;   // --s1
        public const int Small = 8;    // --s2
        public const int Medium = 12;  // --s3
        public const int Large = 16;   // --s4
        public const int XLarge = 20;  // --s5
        public const int XXLarge = 24; // --s6
    }

    internal static class UiRadius
    {
        // CNTR's 10/14/20 scaled to TYPR's denser desktop parts (radius about a quarter of the height).
        public const int Small = 6;   // --r-sm: tabs, keycaps, small controls
        public const int Medium = 8;  // --r-md: buttons, inputs
        public const int Large = 12;  // --r-lg: cards
    }

    internal static class UiFont
    {
        // Windows 11's UI face (Segoe UI on Windows 10). The type is TYPR's own; only colour, corners and motion follow CNTR.
        private static readonly FontFamily Regular = new FontFamily(FirstInstalled("Segoe UI Variable Text", "Segoe UI"));
        private static readonly FontFamily Strong = Regular;
        public static readonly Font Meta = new Font(Regular, 9f);                          // --fs-xs: meta lines
        public static readonly Font Label = new Font(Strong, 9f, FontStyle.Bold);          // --fs-xs bold: chips, group titles
        public static readonly Font Small = new Font(Regular, 9.75f);                      // --fs-sm: rows, tabs, status
        public static readonly Font SmallBold = new Font(Strong, 9.75f, FontStyle.Bold);   // --fs-sm bold: values, keycaps
        public static readonly Font Body = new Font(Regular, 10.5f);                         // --fs-md
        public static readonly Font Button = new Font(Strong, 9.75f, FontStyle.Bold);        // --fs-md bold: buttons
        public static readonly Font Heading = new Font(Strong, 12f, FontStyle.Bold);       // --fs-lg bold: dialog titles
        public static readonly Font Editor = new Font(FirstInstalled("Consolas", "Courier New"), 10.5f); // VS Code's Windows default, 14px
        public static readonly Font Icon = new Font(FirstInstalled("Segoe Fluent Icons", "Segoe MDL2 Assets"), 10f); // Windows' own icon font

        private static string FirstInstalled(params string[] names)
        {
            using var installed = new System.Drawing.Text.InstalledFontCollection();
            foreach (string name in names)
                foreach (FontFamily family in installed.Families)
                    if (family.Name == name) return name;
            return names[^1];
        }
    }

    // CNTR's motion vocabulary for the owner-drawn controls.
    internal static class Motion
    {
        public const int Tap = 120;    // --dur-tap: presses
        public const int Short = 200;  // --dur-1: small state changes
        public const int Liquid = 340; // --dur-liquid: the switch knob

        // Windows' "Animation effects" setting stands in for prefers-reduced-motion.
        public static bool Enabled => SystemInformation.UIEffectsEnabled;

        // --ease-out, cubic-bezier(0.22, 1, 0.36, 1), approximated.
        public static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);

        // --ease-liquid: CNTR's spring as linear() stops, 5% apart (4.6% overshoot).
        private static readonly double[] LiquidStops =
        {
            0, 0.051, 0.173, 0.327, 0.485, 0.632, 0.756, 0.856, 0.931, 0.984, 1.018,
            1.037, 1.045, 1.046, 1.042, 1.035, 1.028, 1.021, 1.015, 1.009, 1
        };

        public static double EaseLiquid(double t)
        {
            t = Math.Clamp(t, 0, 1);
            double position = t * (LiquidStops.Length - 1);
            int index = Math.Min((int)position, LiquidStops.Length - 2);
            return LiquidStops[index] + (LiquidStops[index + 1] - LiquidStops[index]) * (position - index);
        }
    }

    // Runs one animation on the UI thread: calls step with linear progress 0..1 over the duration.
    internal sealed class Tween : IDisposable
    {
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 15 };
        private long startedAt;
        private int duration;
        private Action<double>? step;

        public Tween()
        {
            timer.Tick += (_, __) =>
            {
                double t = Math.Min(1, (Environment.TickCount64 - startedAt) / (double)duration);
                step?.Invoke(t);
                if (t >= 1) timer.Stop();
            };
        }

        public bool Running => timer.Enabled;

        public void Run(int durationMs, Action<double> onStep)
        {
            step = onStep;
            duration = Math.Max(1, durationMs);
            startedAt = Environment.TickCount64;
            if (!Motion.Enabled)
            {
                timer.Stop();
                onStep(1);
                return;
            }
            onStep(0);
            timer.Start();
        }

        public void Dispose() => timer.Dispose();
    }

    internal static class Theme
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TYPR Kimi",
            "settings.json");

        // Declared before Settings, with no initializer, so LoadSettings' value isn't overwritten.
        private static bool settingsWritable;
        public static readonly UserSettings Settings = LoadSettings();
        public static bool Dark { get; private set; } = Settings.Theme switch
        {
            "Dark" => true,
            "Light" => false,
            _ => DetectDark()
        };

        public static void SetMode(string mode)
        {
            Settings.Theme = mode;
            Dark = mode switch
            {
                "Dark" => true,
                "Light" => false,
                _ => DetectDark()
            };
        }

        private static bool saveWarned;

        public static void SaveSettings()
        {
            if (!settingsWritable) return;
            try
            {
                SettingsStore.Save(SettingsPath, Settings);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (saveWarned) return;
                saveWarned = true;
                MessageBox.Show(
                    $"TYPR could not save your preferences. Changes will apply for this session only.\n\n{ex.Message}",
                    "TYPR - settings could not be saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static UserSettings LoadSettings()
        {
            var settings = SettingsStore.Load(SettingsPath, out string? warning, out settingsWritable);
            if (warning != null)
            {
                MessageBox.Show(
                    warning,
                    "TYPR - settings could not be loaded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return settings;
        }

        private static bool DetectDark()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? v = key?.GetValue("AppsUseLightTheme");
                return v is int i && i == 0;
            }
            catch
            {
                return false;
            }
        }

        // ---- CNTR palette: VS Code Dark+ / Light+ (CNTR docs/DESIGN.md). Text tokens are >= 4.5:1 on every surface. ----

        public static Color Bg         => Dark ? Rgb(0x1e1e1e) : Rgb(0xffffff);
        public static Color Surface1   => Dark ? Rgb(0x252526) : Rgb(0xf3f3f3);
        public static Color Surface2   => Dark ? Rgb(0x2d2d2d) : Rgb(0xe8e8e8);
        public static Color Surface3   => Dark ? Rgb(0x37373d) : Rgb(0xe4e6f1);
        public static Color Line       => Dark ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(26, 0, 0, 0);
        public static Color LineStrong => Dark ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(46, 0, 0, 0);
        // Edges of things you type into or click (inputs, keycaps): 3:1 on --surface-1, unlike --line-strong.
        public static Color InputBorder => Dark ? Rgb(0x717171) : Rgb(0x8a8a8a);
        public static Color Text       => Dark ? Rgb(0xe7e7e7) : Rgb(0x1e1e1e);
        public static Color Text2      => Dark ? Rgb(0xcccccc) : Rgb(0x3b3b3b);
        public static Color Text3      => Dark ? Rgb(0x9d9d9d) : Rgb(0x616161);
        public static Color Accent     => Dark ? Rgb(0x8b93ff) : Rgb(0x4338ca);
        public static Color OnAccent   => Dark ? Rgb(0x12143a) : Rgb(0xffffff);
        public static Color Positive   => Dark ? Rgb(0x4ade80) : Rgb(0x116329);
        public static Color Negative   => Dark ? Rgb(0xff6b5e) : Rgb(0xc72c1c);
        public static Color OnNegative => Dark ? Rgb(0x1d0806) : Rgb(0xffffff);

        // TYPR's logo mark (a white T on a blue square, PRODUCT.md), outside the palette like CNTR's blue cross.
        public static Color Logo => Rgb(0x007acc);

        // The window's 1px DWM border: --line-strong over --bg, made opaque.
        public static Color WindowBorder => Blend(LineStrong, Bg);

        // Desktop hover (CNTR is touch-first and has none): a colour moved 12% toward --text.
        public static Color Hover(Color color) => Blend(Color.FromArgb(31, Text), color);

        public static Color Blend(Color top, Color bottom)
        {
            double a = top.A / 255.0;
            return Color.FromArgb(
                (int)Math.Round(top.R * a + bottom.R * (1 - a)),
                (int)Math.Round(top.G * a + bottom.G * (1 - a)),
                (int)Math.Round(top.B * a + bottom.B * (1 - a)));
        }

        private static Color Rgb(int hex) => Color.FromArgb((hex >> 16) & 255, (hex >> 8) & 255, hex & 255);
    }

    // Reads, repairs and writes settings.json; no UI, so it can be tested against a temp folder.
    internal static class SettingsStore
    {
        public static UserSettings Load(string path, out string? warning, out bool canSave)
        {
            warning = null;
            canSave = true;
            try
            {
                if (!File.Exists(path)) return new UserSettings();
                return Read(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                // The next save would write defaults over the unreadable file, so keep a copy
                // first; if even that fails, refuse to save over it at all this session.
                string? backup = TryKeepCopy(path);
                canSave = backup != null;
                string kept = backup != null
                    ? $"Your old file was kept as {Path.GetFileName(backup)} in {Path.GetDirectoryName(path)}."
                    : "To protect your saved texts, changes made now won't be saved.";
                try
                {
                    // The copy from before the last save is usually intact.
                    if (File.Exists(path + ".bak"))
                    {
                        var recovered = Read(path + ".bak");
                        warning = $"TYPR could not read its preferences and restored the copy from before the last save. {kept}\n\n{ex.Message}";
                        return recovered;
                    }
                }
                catch (Exception bakEx) when (bakEx is IOException || bakEx is UnauthorizedAccessException || bakEx is JsonException)
                {
                }
                warning = $"TYPR could not read its preferences and started with default settings. {kept}\n\n{ex.Message}";
                return new UserSettings();
            }
        }

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            // The file is never put in a web page, so non-ASCII text can stay readable (and 3x smaller).
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static UserSettings Read(string path)
        {
            // A settings file this big is not TYPR's; reading it would run out of memory every launch.
            if (new FileInfo(path).Length > 64L * 1024 * 1024)
                throw new IOException("The settings file is too large to be TYPR's.");
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path))
                ?? new UserSettings();
            {
                if (settings.Theme != "System" && settings.Theme != "Light" && settings.Theme != "Dark")
                    settings.Theme = "System";
                settings.StartModifiers &= HotkeyModifiers.Supported;
                settings.StopModifiers &= HotkeyModifiers.Supported;
                if (!IsValidKey(settings.StartKey, settings.StartModifiers)) { settings.StartKey = (int)Keys.F8; settings.StartModifiers = 0; }
                if (!IsValidKey(settings.StopKey, settings.StopModifiers)) { settings.StopKey = (int)Keys.F9; settings.StopModifiers = 0; }
                if (settings.StartKey == settings.StopKey &&
                    settings.StartModifiers == settings.StopModifiers)
                {
                    settings.StopKey = settings.StartKey == (int)Keys.F9
                        ? (int)Keys.F10
                        : (int)Keys.F9;
                    settings.StopModifiers = 0;
                }
                if (settings.TextTemplates == null || settings.TextTemplates.Count == 0)
                    settings.TextTemplates = new List<TextTemplate> { new TextTemplate { Name = "Text 1" } };
                for (int i = 0; i < settings.TextTemplates.Count; i++)
                {
                    settings.TextTemplates[i] ??= new TextTemplate { Name = $"Text {i + 1}" };
                    if (string.IsNullOrWhiteSpace(settings.TextTemplates[i].Name))
                        settings.TextTemplates[i].Name = $"Text {i + 1}";
                    else if (settings.TextTemplates[i].Name.StartsWith("Template ", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(settings.TextTemplates[i].Name.AsSpan("Template ".Length), out _))
                        settings.TextTemplates[i].Name =
                            $"Text {settings.TextTemplates[i].Name["Template ".Length..]}";
                    settings.TextTemplates[i].Text ??= string.Empty;
                }
                settings.ActiveTextTemplate = Math.Clamp(
                    settings.ActiveTextTemplate, 0, settings.TextTemplates.Count - 1);
            }
            return settings;
        }

        private static string? TryKeepCopy(string path)
        {
            string backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bad";
            try
            {
                File.Copy(path, backup, overwrite: false);
                return backup;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void Save(string path, UserSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Write a temp file, flush it to the disk, then swap it in and keep the previous file as
            // .bak: a crash or power loss can't leave settings.json empty, and Load can fall back.
            string tempPath = path + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, WriteOptions);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
            else File.Move(tempPath, path);
        }

        // Same rule as the shortcut picker: a real keyboard key, and only F1-F24, Pause or Scroll Lock without a modifier
        // (a bare letter would start a run on every press of it).
        private static bool IsValidKey(int value, int modifiers)
        {
            if (value < 0x08 || value > 0xFE) return false; // mouse buttons, modifier flags
            var key = (Keys)value;
            if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin ||
                !Enum.IsDefined(typeof(Keys), key)) return false;
            return modifiers != 0 || HotkeyModifiers.CanStandAlone(key);
        }
    }

    internal sealed class UserSettings
    {
        public UserSettings() { }

        public string Theme { get; set; } = "System";
        public int StartModifiers { get; set; }
        public int StartKey { get; set; } = (int)Keys.F8;
        public int StopModifiers { get; set; }
        public int StopKey { get; set; } = (int)Keys.F9;
        // Never empty: the window reads the active tab on startup.
        public List<TextTemplate> TextTemplates { get; set; } = new() { new TextTemplate { Name = "Text 1" } };
        public int ActiveTextTemplate { get; set; }
    }

    internal sealed class TextTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    internal static class HotkeyModifiers
    {
        public const int Alt = 0x0001;
        public const int Control = 0x0002;
        public const int Shift = 0x0004;
        public const int Win = 0x0008;
        public const int NoRepeat = 0x4000;
        public const int Supported = Alt | Control | Shift | Win;

        // Keys that can be a shortcut on their own: nothing types them, so they can't catch normal typing.
        public static bool CanStandAlone(System.Windows.Forms.Keys key) =>
            (key >= System.Windows.Forms.Keys.F1 && key <= System.Windows.Forms.Keys.F24) ||
            key == System.Windows.Forms.Keys.Pause || key == System.Windows.Forms.Keys.Scroll;
    }

    internal static class TypingSchedule
    {
        // Waits toward an absolute deadline, so a late wake-up is paid back on the next key instead
        // of piling up. Never repays more than one key slot and always waits at least 1 ms, so a
        // stall never turns into a burst of keys (remote consoles drop bursts).
        public static int NextWait(ref double dueMs, double nowMs, double slotMs)
        {
            if (dueMs < nowMs - slotMs) dueMs = nowMs - slotMs;
            return Math.Max(1, (int)Math.Round(dueMs - nowMs));
        }
    }

    // Windows' default timer tick is ~15.6 ms, so every short wait rounds up to it and typing
    // runs slower than the CPM setting. A 1 ms tick while typing keeps the pacing accurate and
    // even; catching up in bursts instead would drop keys on remote desktops.
    internal static class TimerResolution
    {
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint milliseconds);

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, uint size);

        private const int ProcessPowerThrottling = 4;
        private const uint IgnoreTimerResolution = 0x4;

        // Windows 11 drops a minimized or covered window's timer request (TYPR sits behind the
        // target while typing). Opting out of that throttling keeps the 1 ms tick. Best effort:
        // Windows 10 before 2004 doesn't know the flag and keeps the old behaviour.
        private static void HonorTimerRequest(bool honor)
        {
            var state = new PowerThrottlingState { Version = 1, ControlMask = honor ? IgnoreTimerResolution : 0, StateMask = 0 };
            SetProcessInformation((IntPtr)(-1), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>());
        }

        public static IDisposable Begin()
        {
            HonorTimerRequest(true);
            return new Scope(timeBeginPeriod(1) == 0);
        }

        private sealed class Scope : IDisposable
        {
            private bool active;
            private bool disposed;

            public Scope(bool active) => this.active = active;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                HonorTimerRequest(false);
                if (!active) return;
                active = false;
                timeEndPeriod(1);
            }
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        // Brings the TYPR window that is already running to the front (restoring it if minimized).
        public static void ShowRunningInstance()
        {
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var other in System.Diagnostics.Process.GetProcessesByName(self.ProcessName))
            {
                using (other)
                {
                    if (other.Id == self.Id || other.MainWindowHandle == IntPtr.Zero) continue;
                    ShowWindow(other.MainWindowHandle, 9); // SW_RESTORE
                    SetForegroundWindow(other.MainWindowHandle);
                    return;
                }
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll")]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll")]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int length, out int returned);

        // True when the process runs as administrator. A token that can't be read counts as
        // elevated: Windows hides it exactly from processes it also blocks typing into.
        public static bool IsElevated(uint processId)
        {
            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            const uint TOKEN_QUERY = 0x0008;
            const int TokenElevation = 20;
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero) return true;
            try
            {
                if (!OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) return true;
                try
                {
                    return !GetTokenInformation(token, TokenElevation, out int elevated, sizeof(int), out _) || elevated != 0;
                }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(process); }
        }

        public static bool WinDown() => GetAsyncKeyState(0x5B) < 0 || GetAsyncKeyState(0x5C) < 0;

        // The character a key types on the current keyboard layout ('\0' if none).
        public static char KeyChar(int vk) => (char)(MapVirtualKey((uint)vk, 2) & 0xFFFF); // MAPVK_VK_TO_CHAR

        // True while Shift, Ctrl, Alt or Win is held: typed Enter/Tab would combine with it.
        public static bool AnyModifierDown()
        {
            foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if (GetAsyncKeyState(key) < 0) return true;
            return false;
        }

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const int WM_HOTKEY = 0x0312;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT Reserved;
            public POINT MaxSize;
            public POINT MaxPosition;
            public POINT MinTrackSize;
            public POINT MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT Work;
            public uint Flags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public static void SetWindowAppearance(IntPtr handle, Color borderColor)
        {
            if (Environment.OSVersion.Version.Build < 22000) return;

            SetDwmWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
            SetDwmWindowAttribute(handle, DWMWA_BORDER_COLOR, ColorTranslator.ToWin32(borderColor));
        }

        // Dark caption for standard dialogs. Best effort: older builds ignore the attribute.
        public static void SetDarkTitleBar(IntPtr handle, bool dark)
        {
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, Marshal.SizeOf<int>());
        }

        public static MINMAXINFO GetMaximizedWorkArea(IntPtr handle, MINMAXINFO info)
        {
            const uint MONITOR_DEFAULTTONEAREST = 2;
            IntPtr monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo)) return info; // keep Windows' defaults

            info.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
            info.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
            info.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
            info.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
            return info;
        }

        private static void SetDwmWindowAttribute(IntPtr handle, int attribute, int value)
        {
            // Cosmetic and best effort: some RDP/VM display drivers reject it.
            DwmSetWindowAttribute(handle, attribute, ref value, Marshal.SizeOf<int>());
        }

        public static void SendUnicodeChar(char ch)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE };
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP };
            SendInputChecked(inputs);
        }

        public static void SendVirtualKey(ushort vk)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            ushort scan = (ushort)MapVirtualKey(vk, 0); // MAPVK_VK_TO_VSC
            inputs[0].U.ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = 0 };
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = KEYEVENTF_KEYUP };
            SendInputChecked(inputs);
        }

        private static void SendInputChecked(INPUT[] inputs)
        {
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent == (uint)inputs.Length) return;
            int error = Marshal.GetLastWin32Error();

            // A partial send (key-down accepted, key-up dropped) leaves a key stuck down in
            // whatever app has focus. Best-effort only: if SendInput is fully blocked (e.g. by
            // UIPI against an elevated window) this retry will fail too and we fall through to
            // the exception below; there's no further recovery available at this layer.
            if (sent > 0 && sent < (uint)inputs.Length)
            {
                for (int i = (int)sent; i < inputs.Length; i++)
                {
                    if ((inputs[i].U.ki.dwFlags & KEYEVENTF_KEYUP) != 0)
                        SendInput(1, new[] { inputs[i] }, Marshal.SizeOf(typeof(INPUT)));
                }
            }

            string detail = error == 0
                ? "Windows blocked or rejected the synthetic keyboard input."
                : new System.ComponentModel.Win32Exception(error).Message;
            throw new InvalidOperationException($"Windows accepted {sent} of {inputs.Length} keyboard events. {detail}");
        }
    }

    public class MainForm : Form, IMessageFilter
    {
        private const int TitleBarHeight = 35;
        private const int SidebarWidth = 232;
        private const int TabHeight = 35;
        private const int TabMinWidth = 80;
        private const int TabMaxWidth = 160;

        private readonly EditorBox text = new EditorBox();
        private readonly ThinScrollBar editorScroll = new ThinScrollBar();
        private readonly NumberInput cpm = new NumberInput();
        private readonly NumberInput startDelay = new NumberInput();
        private readonly Slider jitter = new Slider();
        private readonly Label jitterValue = new Label();
        private readonly ToggleCheckBox pressEnter = new ToggleCheckBox();
        private readonly RoundedButton start = new RoundedButton { Kind = ButtonKind.Primary };
        private readonly RoundedButton stop = new RoundedButton { Kind = ButtonKind.Secondary };
        private readonly RoundedButton startHotkey = new RoundedButton { Kind = ButtonKind.Keycap };
        private readonly RoundedButton stopHotkey = new RoundedButton { Kind = ButtonKind.Keycap };
        private readonly StatusDot statusDot = new StatusDot();
        private readonly Label status = new Label();
        private readonly ChipLabel characterCount = new ChipLabel();
        private readonly ToolTip statusTip = new ToolTip();
        private readonly ToolTip tips = new ToolTip();
        private const string EmptyHint = "Add text to start";
        // Dragging a tab to reorder it.
        private EditorTabPanel? dragTab;
        private Point dragStart;
        private bool dragMoved;
        private bool statusWarning;
        private readonly WindowCaptionButton maximizeButton = new WindowCaptionButton(WindowCaptionButtonKind.Maximize);
        private readonly TitleIconButton themeToggleButton = new TitleIconButton();
        // Below CompactWidth the sidebar folds away; this button (or Ctrl+B) opens it over the editor.
        private const int CompactWidth = 780;
        private readonly TitleIconButton sidebarToggle = new TitleIconButton { Glyph = Glyph.Sidebar };
        private Control? sidebar;
        private TableLayoutPanel? contentGrid;
        private SidebarSheet? sidebarSheet;
        private bool sidebarOpen;
        private readonly System.Windows.Forms.Timer templateSaveTimer = new System.Windows.Forms.Timer { Interval = 400 };
        private readonly AddTextButton addTemplateButton = new AddTextButton();
        private readonly LinkLabel resetHotkeysLink = new LinkLabel();
        private Control? windowContent;
        private TabStripPanel templateTabs = null!;
        private CancellationTokenSource? cts;
        private readonly Random rng = new Random();
        private bool hotkeysRegistered;
        private bool loadingTemplate;
        // Global hotkeys are registered on this top-level window and fire via WM_HOTKEY
        // regardless of which child window has focus, so a TYPR modal dialog (e.g. rename)
        // being open doesn't stop F8/F9 from starting/stopping typing underneath it.
        private bool modalDialogOpen;

        private const int HOTKEY_START = 1;
        private const int HOTKEY_STOP = 2;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 0x0002;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorderWidth = 6;

        public MainForm()
        {
            Text = "TYPR";
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(1020, 700);
            MinimumSize = new Size(560, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.WindowBorder;
            Font = UiFont.Small;
            maximizeButton.Click += (_, __) => ToggleWindowState();
            Resize += (_, __) =>
            {
                bool maximized = WindowState == FormWindowState.Maximized;
                maximizeButton.IsMaximized = maximized;
                tips.SetToolTip(maximizeButton, maximized ? "Restore down" : "Maximize");
                if (windowContent != null)
                    windowContent.Margin = maximized ? Padding.Empty : new Padding(1);
                ApplyResponsive();
            };

            themeToggleButton.AccessibleRole = AccessibleRole.PushButton;
            themeToggleButton.Click += (_, __) => ToggleTheme();
            sidebarToggle.AccessibleRole = AccessibleRole.PushButton;
            sidebarToggle.AccessibleName = "Show settings";
            sidebarToggle.Click += (_, __) => SetSidebarOpen(!sidebarOpen);
            tips.SetToolTip(sidebarToggle, "Settings (Ctrl+B)");

            cpm.Minimum = 30;
            cpm.Maximum = 20000;
            cpm.Value = 1200;
            cpm.FieldName = "Typing speed, characters per minute";
            startDelay.Minimum = 0;
            startDelay.Maximum = 30;
            startDelay.Value = 3;
            startDelay.FieldName = "Start delay, seconds";
            jitter.Minimum = 0;
            jitter.Maximum = 80;
            jitter.Value = 5;
            jitter.AccessibleName = "Humanize pacing, percent";
            jitter.ValueChanged += (_, __) => jitterValue.Text = "±" + jitter.Value + "%";
            jitterValue.Text = "±" + jitter.Value + "%";
            cpm.ValueChanged += (_, __) => UpdateCharacterCount();

            text.Multiline = true;
            text.AcceptsReturn = true;
            text.AcceptsTab = true;
            text.ScrollBars = ScrollBars.None;
            editorScroll.Target = text;
            text.WordWrap = true;
            text.Font = UiFont.Editor;
            text.BorderStyle = BorderStyle.None;
            text.Dock = DockStyle.Fill;
            text.PlaceholderText = "Type or paste the text TYPR should type for you. Esc leaves the editor.";
            text.AccessibleName = "Text to type";
            text.AccessibleDescription = "Tab inserts a tab. Esc moves on to the buttons. Ctrl+Tab switches text tabs.";
            text.MouseDown += (_, __) => SetSidebarOpen(false);
            text.TextChanged += (_, __) =>
            {
                UpdateCharacterCount();
                if (loadingTemplate) return;
                ActiveTemplate.Text = text.Text;
                templateSaveTimer.Stop();
                templateSaveTimer.Start();
            };
            templateSaveTimer.Tick += (_, __) =>
            {
                templateSaveTimer.Stop();
                SaveActiveTemplate();
                Theme.SaveSettings();
            };

            pressEnter.AccessibleName = "End with Enter";

            start.Text = "Start typing";
            start.AccessibleName = $"Start typing ({FormatHotkey(Theme.Settings.StartModifiers, Theme.Settings.StartKey)})";
            start.Click += StartButton_Click;

            stop.Text = "Stop";
            stop.AccessibleName = $"Stop typing ({FormatHotkey(Theme.Settings.StopModifiers, Theme.Settings.StopKey)})";
            stop.Enabled = false;
            stop.Click += (_, __) => StopTyping();

            UpdateHotkeyButton(startHotkey, "Start typing", Theme.Settings.StartModifiers, Theme.Settings.StartKey);
            startHotkey.Click += (_, __) => ChangeHotkey(true);
            UpdateHotkeyButton(stopHotkey, "Stop", Theme.Settings.StopModifiers, Theme.Settings.StopKey);
            stopHotkey.Click += (_, __) => ChangeHotkey(false);
            ConfigureKeycap(startHotkey);
            ConfigureKeycap(stopHotkey);
            tips.SetToolTip(addTemplateButton, "New text");
            tips.SetToolTip(maximizeButton, "Maximize");
            addTemplateButton.Click += (_, __) => AddTextTemplate();

            resetHotkeysLink.Text = "Reset to F8 / F9";
            resetHotkeysLink.AutoSize = true;
            resetHotkeysLink.Font = UiFont.Small;
            resetHotkeysLink.LinkBehavior = LinkBehavior.HoverUnderline;
            resetHotkeysLink.AccessibleName = "Reset shortcuts to F8 start, F9 stop";
            resetHotkeysLink.LinkClicked += (_, __) => ResetHotkeysToDefault();

            ShowStatus("Ready");
            status.AutoSize = false;
            status.AutoEllipsis = true;
            status.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            status.Height = UiFont.Small.Height + 4;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Font = UiFont.Small;
            status.Margin = Padding.Empty;
            status.LiveSetting = System.Windows.Forms.Automation.AutomationLiveSetting.Polite;

            characterCount.Anchor = AnchorStyles.None;
            characterCount.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            // Match the action buttons' visible face (40 px minus the focus-ring room on each side).
            characterCount.MinimumSize = new Size(0, 40 - FocusRing.Room * 2);
            loadingTemplate = true;
            text.Text = ActiveTemplate.Text;
            loadingTemplate = false;
            UpdateCharacterCount();

            windowContent = BuildWindowContent();
            Controls.Add(windowContent);
            ApplyResponsive();
            // Stop any in-flight typing before the window (and its controls) go away, so
            // SendInput doesn't keep firing into whatever window has focus after TYPR closes.
            FormClosing += (_, __) => cts?.Cancel();
            Application.AddMessageFilter(this);
            FormClosed += (_, __) => Application.RemoveMessageFilter(this);
            FormClosed += (_, __) =>
            {
                templateSaveTimer.Stop();
                SaveActiveTemplate();
                Theme.SaveSettings();
                templateSaveTimer.Dispose();
            };

            UpdateThemeToggleButton();
            ActiveControl = text;
        }

        // The footer's one line of feedback. Problems paint the dot and text --negative; the full
        // message is also the tooltip, since a long one is cut off at narrow widths.
        private void ShowStatus(string message, bool warning = false, bool announce = true)
        {
            statusWarning = warning;
            status.Text = message;
            status.ForeColor = warning ? Theme.Negative : Theme.Text2;
            statusDot.Warning = warning;
            statusDot.Idle = message == EmptyHint;
            statusTip.SetToolTip(status, message);
            if (announce && status.IsHandleCreated) status.AccessibilityObject.RaiseLiveRegionChanged();
        }

        private void UpdateCharacterCount()
        {
            int count = text.TextLength;
            string label = $"{count.ToString("N0", CultureInfo.InvariantCulture)} {(count == 1 ? "character" : "characters")}";
            if (count > 0 && cpm.Value > 0)
            {
                double minutes = count / (double)cpm.Value;
                label += minutes < 1 ? $" \u00b7 \u2248{Math.Max(1, (int)Math.Round(minutes * 60))} s" : $" \u00b7 \u2248{Math.Round(minutes):N0} min";
            }
            characterCount.Text = label;
            if (cts != null) return;
            bool blank = string.IsNullOrWhiteSpace(text.Text);
            start.Enabled = !blank;
            // U9: say why Start is off instead of a green "Ready".
            if (blank) ShowStatus(EmptyHint, announce: false);
            else if (status.Text == EmptyHint) ShowStatus("Ready", announce: false);
        }

        // ---------- Layout (rebuilt from the current theme on every theme switch) ----------

        private Control BuildWindowContent()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 1,
                RowCount = 2,
                Margin = WindowState == FormWindowState.Maximized ? Padding.Empty : new Padding(1),
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var titleBar = BuildTitleBar();
            var content = BuildContent();
            root.Controls.Add(titleBar, 0, 0);
            root.Controls.Add(content, 0, 1);
            // Tab goes through the work area first; the title bar's theme toggle comes last.
            content.TabIndex = 0;
            titleBar.TabIndex = 1;
            return root;
        }

        private Control BuildContent()
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = new Padding(UiSpacing.Medium, 0, 0, 0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            contentGrid = content;
            sidebar = BuildSidebar();
            content.Controls.Add(sidebar, 0, 0);
            content.Controls.Add(BuildEditorGroup(), 1, 0);
            return content;
        }

        // CNTR's Settings pattern: a --fs-sm bold --text-3 title over one --surface-1 card of rows.
        private Control BuildSidebar()
        {
            cpm.ApplyTheme();
            startDelay.ApplyTheme();
            jitterValue.AutoSize = true;
            jitterValue.Font = UiFont.SmallBold;
            jitterValue.ForeColor = Theme.Text;
            jitterValue.Anchor = AnchorStyles.Right;
            jitterValue.Margin = Padding.Empty;
            resetHotkeysLink.LinkColor = Theme.Text2;
            resetHotkeysLink.ActiveLinkColor = Theme.Text;
            resetHotkeysLink.VisitedLinkColor = Theme.Text2;
            resetHotkeysLink.Margin = new Padding(UiSpacing.Medium, UiSpacing.Small, 0, 0);

            var typing = CardRows(
                SettingRow(Glyph.Speed, Theme.Accent, "Speed", Fixed(cpm, 68), "Characters per minute"),
                SettingRow(Glyph.Stopwatch, Theme.Accent, "Start delay", Fixed(startDelay, 68), "Seconds to focus the target window"),
                SliderRow(Glyph.Pulse, "Humanize pacing", jitter, jitterValue),
                SwitchRow(Glyph.Enter, "End with Enter", pressEnter));
            var shortcuts = CardRows(
                SettingRow(Glyph.Play, Theme.Positive, "Start", startHotkey),
                SettingRow(Glyph.Stop, Theme.Negative, "Stop", stopHotkey));

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = new Padding(UiSpacing.XSmall, UiSpacing.XSmall, UiSpacing.Medium, UiSpacing.Medium)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddGroup(layout, "Typing", typing, first: true);
            AddGroup(layout, "Shortcuts", shortcuts);
            layout.Controls.Add(resetHotkeysLink);
            layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = Padding.Empty });
            for (int i = 0; i < layout.Controls.Count - 1; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return layout;
        }

        private static void AddGroup(TableLayoutPanel layout, string title, Control card, bool first = false)
        {
            layout.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = UiFont.Label,
                ForeColor = Theme.Text3,
                Margin = new Padding(UiSpacing.Medium, first ? UiSpacing.Small : UiSpacing.Medium, 0, UiSpacing.XSmall)
            });
            layout.Controls.Add(card);
        }

        private static Control Fixed(Control control, int width)
        {
            control.Width = width;
            control.Height = 26;
            control.Anchor = AnchorStyles.Right;
            control.Margin = Padding.Empty;
            return control;
        }

        // One --surface-1 card (--line border, --r-lg) holding rows split by --line dividers.
        private static Control CardRows(params Control[] rows)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Top,
                BackColor = Theme.Surface1,
                BorderColor = Theme.Line,
                CornerRadius = UiRadius.Large,
                Padding = new Padding(UiSpacing.Medium, 1, UiSpacing.Medium, 1),
                Margin = Padding.Empty
            };
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stack.RowCount = rows.Length * 2 - 1;
            int height = 2;
            int row = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0)
                {
                    stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    stack.Controls.Add(new LineDivider(), 0, row++);
                    height += 1;
                }
                // Rows carry their intended height in Tag: a docked row's Height can change once
                // children are added, before it is ever laid out.
                int rowHeight = rows[i].Tag is int intended ? intended : rows[i].Height;
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
                stack.Controls.Add(rows[i], 0, row++);
                height += rowHeight;
            }
            card.Height = height;
            // The sidebar rows size to preferred height; pin it so the card is exactly its rows.
            card.MinimumSize = new Size(0, height);
            card.MaximumSize = new Size(0, height);
            card.Controls.Add(stack);
            return card;
        }

        // A 40px row: tinted icon tile, title, the value or control right.
        private static Control SettingRow(string glyph, Color tint, string title, Control value, string? description = null)
        {
            const int height = 40;
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = height,
                Tag = height,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            // Icon and title keep their natural width; the value takes what is left, right-aligned,
            // so a long shortcut can never squeeze the title into a wrap.
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.Controls.Add(new RowIcon { Glyph = glyph, Tint = tint }, 0, 0);
            row.Controls.Add(RowLabel(title), 1, 0);
            if (description != null) value.AccessibleDescription = description;
            if (value.Anchor == AnchorStyles.None || value.Anchor == (AnchorStyles.Top | AnchorStyles.Left))
                value.Anchor = AnchorStyles.Right;
            row.Controls.Add(value, 2, 0);
            return row;
        }

        private static Control RowLabel(string title) => new Label
        {
            Text = title,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = UiFont.Small,
            ForeColor = Theme.Text,
            BackColor = Theme.Surface1,
            Margin = new Padding(0, 0, UiSpacing.Small, 0)
        };

        private static Control SliderRow(string glyph, string title, Slider slider, Label value)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 62,
                Tag = 62,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 2)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.Controls.Add(new RowIcon { Glyph = glyph, Tint = Theme.Accent }, 0, 0);
            row.Controls.Add(RowLabel(title), 1, 0);
            row.Controls.Add(value, 2, 0);
            // The thumb's 12px inset puts the rail's start under the icon's centre.
            slider.Dock = DockStyle.Fill;
            slider.Margin = Padding.Empty;
            row.Controls.Add(slider, 0, 1);
            row.SetColumnSpan(slider, 3);
            return row;
        }

        // CNTR: "the whole row is the label", so a click anywhere on the row flips the switch.
        private static Control SwitchRow(string glyph, string title, ToggleCheckBox toggle)
        {
            toggle.Size = new Size(44, 26);
            toggle.Anchor = AnchorStyles.Right;
            toggle.Margin = Padding.Empty;
            var row = SettingRow(glyph, Theme.Accent, title, toggle, "Press Enter after the last character");
            void Flip(object? sender, EventArgs e)
            {
                if (toggle.Enabled && !ReferenceEquals(sender, toggle)) toggle.Toggle();
            }
            row.Cursor = Cursors.Hand;
            row.Click += Flip;
            foreach (Control child in row.Controls)
                if (!ReferenceEquals(child, toggle)) child.Click += Flip;
            return row;
        }

        private Control BuildEditorGroup()
        {
            var group = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = new Padding(UiSpacing.XSmall, UiSpacing.Small, UiSpacing.Large, UiSpacing.Small)
            };
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, TabHeight));
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            group.Controls.Add(BuildTabStrip(), 0, 0);
            group.Controls.Add(BuildEditorCard(), 0, 1);
            group.Controls.Add(BuildEditorActions(), 0, 2);
            return group;
        }

        private Control BuildEditorCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface1,
                BorderColor = Theme.Line,
                CornerRadius = UiRadius.Large,
                SquareTop = true,
                Padding = new Padding(UiSpacing.Medium, UiSpacing.Small, UiSpacing.XSmall, UiSpacing.Small),
                Margin = Padding.Empty
            };
            text.BackColor = Theme.Surface1;
            text.ForeColor = Theme.Text;
            card.Controls.Add(text);
            // Added after the Fill editor, so it docks first and takes the right edge.
            editorScroll.Dock = DockStyle.Right;
            card.Controls.Add(editorScroll);
            return card;
        }

        private Control BuildEditorActions()
        {
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = new Padding(UiSpacing.XSmall, UiSpacing.Small, 0, 0)
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            statusDot.Dock = DockStyle.Fill;
            statusDot.Margin = Padding.Empty;
            status.ForeColor = statusWarning ? Theme.Negative : Theme.Text2;
            ConfigureActionButton(stop, 80);
            ConfigureActionButton(start, 120);
            start.Margin = new Padding(UiSpacing.Small, 0, 0, 0);

            actions.Controls.Add(statusDot, 0, 0);
            actions.Controls.Add(status, 1, 0);
            actions.Controls.Add(characterCount, 2, 0);
            actions.Controls.Add(stop, 3, 0);
            actions.Controls.Add(start, 4, 0);
            return actions;
        }

        private static void ConfigureActionButton(RoundedButton button, int minWidth)
        {
            button.MinimumSize = new Size(minWidth, 40);
            button.Padding = new Padding(UiSpacing.Large, 0, UiSpacing.Large, 0);
            button.FitText = true;
            button.Anchor = AnchorStyles.None;
            button.Margin = new Padding(UiSpacing.Small, 0, 0, 0);
        }

        private Control BuildTabStrip()
        {
            templateTabs = new TabStripPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            templateTabs.Resize += (_, __) => FitTemplateTabs();
            addTemplateButton.AccessibleName = "Add text";
            addTemplateButton.AccessibleRole = AccessibleRole.PushButton;
            addTemplateButton.Size = new Size(TabHeight, TabHeight);
            addTemplateButton.Margin = Padding.Empty;
            addTemplateButton.Cursor = Cursors.Hand;
            RefreshTemplateTabs();
            return templateTabs;
        }

        private void RefreshTemplateTabs()
        {
            if (templateTabs == null) return;
            templateTabs.SuspendLayout();
            templateTabs.Controls.Remove(addTemplateButton);
            while (templateTabs.Controls.Count > 0)
            {
                Control control = templateTabs.Controls[0];
                templateTabs.Controls.RemoveAt(0);
                tips.SetToolTip(control, null);
                control.Dispose();
            }

            for (int i = 0; i < Theme.Settings.TextTemplates.Count; i++)
            {
                int templateIndex = i;
                var tab = new EditorTabPanel
                {
                    Text = Theme.Settings.TextTemplates[i].Name,
                    Height = TabHeight,
                    IsActive = i == Theme.Settings.ActiveTextTemplate,
                    Font = UiFont.Small,
                    Margin = Padding.Empty,
                    Cursor = Cursors.Hand,
                    AccessibleRole = AccessibleRole.PageTab,
                    AccessibleName = Theme.Settings.TextTemplates[i].Name,
                    AccessibleDescription = "Enter selects. F2 or double-click renames. Delete closes. Drag to reorder.",
                    Tag = i
                };
                tips.SetToolTip(tab, Theme.Settings.TextTemplates[i].Name);
                tab.Click += (_, __) => { if (!dragMoved) SelectTextTemplate(templateIndex); };
                tab.MouseDown += (_, e) =>
                {
                    SetSidebarOpen(false);
                    if (e.Button != MouseButtons.Left || cts != null) return;
                    dragTab = tab;
                    dragStart = e.Location;
                    dragMoved = false;
                };
                tab.MouseMove += (_, e) => DragTab(tab, e);
                tab.MouseUp += (_, __) => EndTabDrag();
                tab.CloseRequested += (_, __) => DeleteTextTemplate(templateIndex);
                tab.RenameRequested += (_, __) => RenameTextTemplate(templateIndex);
                templateTabs.Controls.Add(tab);
                tab.Enabled = cts == null;
            }
            templateTabs.Controls.Add(addTemplateButton);
            addTemplateButton.Enabled = cts == null;
            FitTemplateTabs();
            templateTabs.ResumeLayout(true);
        }

        // Wide: sidebar in its column. Narrow (under CompactWidth): the editor keeps the room, the sidebar
        // hides, and the title-bar toggle lays it over the editor on a sheet.
        private void ApplyResponsive()
        {
            if (contentGrid == null || sidebar == null) return;
            bool compact = ClientSize.Width < CompactWidth;
            if (!compact) sidebarOpen = false;
            bool overlay = compact && sidebarOpen;
            sidebarToggle.Visible = compact;
            sidebarToggle.Active = overlay;
            sidebarToggle.AccessibleName = overlay ? "Hide settings" : "Show settings";

            if (overlay)
            {
                if (sidebarSheet == null)
                {
                    sidebarSheet = new SidebarSheet(SidebarWidth + UiSpacing.Medium);
                    sidebarSheet.Dismissed += (_, __) => SetSidebarOpen(false);
                    Controls.Add(sidebarSheet);
                }
                if (sidebar.Parent != sidebarSheet.Drawer)
                {
                    contentGrid.Controls.Remove(sidebar);
                    sidebarSheet.Drawer.Controls.Add(sidebar);
                }
                int edge = WindowState == FormWindowState.Maximized ? 0 : 1;
                int top = edge + TitleBarHeight;
                sidebarSheet.Bounds = new Rectangle(edge, top, Math.Max(0, ClientSize.Width - edge * 2), Math.Max(0, ClientSize.Height - top - edge));
                if (windowContent != null) sidebarSheet.CaptureBackdrop(windowContent, TitleBarHeight);
                sidebarSheet.BringToFront();
            }
            else
            {
                if (sidebarSheet != null)
                {
                    sidebarSheet.Drawer.Controls.Remove(sidebar);
                    Controls.Remove(sidebarSheet);
                    sidebarSheet.Dispose();
                    sidebarSheet = null;
                }
                if (sidebar.Parent != contentGrid) contentGrid.Controls.Add(sidebar, 0, 0);
            }
            // Hidden, not only zero-width, so Tab can't land in it.
            sidebar.Visible = !compact || overlay;
            contentGrid.ColumnStyles[0].Width = compact ? 0 : SidebarWidth;
        }

        private void SetSidebarOpen(bool open)
        {
            if (open == sidebarOpen || (open && ClientSize.Width >= CompactWidth)) return;
            bool focusInside = sidebar?.ContainsFocus == true;
            sidebarOpen = open;
            ApplyResponsive();
            if (open) sidebar?.SelectNextControl(null, true, true, true, false);
            else if (focusInside) text.Focus();
        }

        // Drag a tab along the strip: it swaps places live; the new order is saved on release.
        private void DragTab(EditorTabPanel tab, MouseEventArgs e)
        {
            if (dragTab != tab || (MouseButtons & MouseButtons.Left) == 0) return;
            if (!dragMoved)
            {
                var size = SystemInformation.DragSize;
                if (Math.Abs(e.X - dragStart.X) < size.Width && Math.Abs(e.Y - dragStart.Y) < size.Height) return;
                dragMoved = true;
                tab.Cursor = Cursors.SizeWE;
            }
            int count = templateTabs.Controls.Count - 1; // the + button is last
            Point inStrip = templateTabs.PointToClient(tab.PointToScreen(e.Location));
            int target = Math.Clamp((inStrip.X - templateTabs.Controls[0].Left) / Math.Max(1, tab.Width), 0, count - 1);
            if (templateTabs.Controls.GetChildIndex(tab) != target)
                templateTabs.Controls.SetChildIndex(tab, target);
        }

        private void EndTabDrag()
        {
            if (dragTab == null) return;
            dragTab = null;
            if (!dragMoved) return;
            // Click already ran (WinForms raises it before MouseUp), so the flag can go after this.
            BeginInvoke(new Action(() =>
            {
                dragMoved = false;
                templateSaveTimer.Stop();
                SaveActiveTemplate();
                var old = Theme.Settings.TextTemplates;
                var active = ActiveTemplate;
                var order = templateTabs.Controls.OfType<EditorTabPanel>().Select(t => old[(int)t.Tag!]).ToList();
                if (order.Count != old.Count) { RefreshTemplateTabs(); return; }
                Theme.Settings.TextTemplates = order;
                Theme.Settings.ActiveTextTemplate = order.IndexOf(active);
                RefreshTemplateTabs();
                Theme.SaveSettings();
            }));
        }

        // Ctrl+Tab / Ctrl+Shift+Tab and Ctrl+PageDown / PageUp step through the text tabs, Ctrl+1-8 jump
        // to one, Ctrl+9 to the last (browser keys). Esc leaves the editor, where Tab types a tab.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            int count = Theme.Settings.TextTemplates.Count;
            int current = Theme.Settings.ActiveTextTemplate;
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;
            int target = -1;
            if (mods == Keys.Control && (key == Keys.Tab || key == Keys.PageDown)) target = (current + 1) % count;
            else if ((mods == (Keys.Control | Keys.Shift) && key == Keys.Tab) || (mods == Keys.Control && key == Keys.PageUp))
                target = (current - 1 + count) % count;
            else if (mods == Keys.Control && (key == Keys.D9 || key == Keys.NumPad9)) target = count - 1;
            else if (mods == Keys.Control && key >= Keys.D1 && key <= Keys.D8) target = key - Keys.D1;
            else if (mods == Keys.Control && key >= Keys.NumPad1 && key <= Keys.NumPad8) target = key - Keys.NumPad1;
            if (target >= 0)
            {
                if (target < count) SelectTextTemplate(target);
                return true;
            }
            if (keyData == Keys.Escape && sidebarOpen)
            {
                SetSidebarOpen(false);
                return true;
            }
            if (keyData == (Keys.Control | Keys.B))
            {
                SetSidebarOpen(!sidebarOpen);
                return true;
            }
            if (keyData == Keys.Escape && text.Focused)
            {
                SelectNextControl(text, true, true, true, true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Tabs share the strip's width (TabMinWidth..TabMaxWidth) so many tabs stay on one row; only
        // past the minimum does the strip scroll.
        private void FitTemplateTabs()
        {
            int count = templateTabs.Controls.Count - 1;
            if (count <= 0) return;
            int available = templateTabs.ClientSize.Width - addTemplateButton.Width;
            int width = Math.Clamp(available / count, TabMinWidth, TabMaxWidth);
            foreach (Control control in templateTabs.Controls)
                if (control is EditorTabPanel tab && tab.Width != width)
                    tab.Width = width;
            templateTabs.AutoScroll = count * width > available;
        }

        private TextTemplate ActiveTemplate =>
            Theme.Settings.TextTemplates[Theme.Settings.ActiveTextTemplate];

        private void SaveActiveTemplate()
        {
            if (Theme.Settings.TextTemplates.Count == 0) return;
            Theme.Settings.ActiveTextTemplate = Math.Clamp(
                Theme.Settings.ActiveTextTemplate, 0, Theme.Settings.TextTemplates.Count - 1);
            ActiveTemplate.Text = text.Text;
        }

        private void SelectTextTemplate(int index)
        {
            if (cts != null)
            {
                ShowStatus("Stop typing before switching text tabs.", warning: true);
                return;
            }
            if (index < 0 || index >= Theme.Settings.TextTemplates.Count ||
                index == Theme.Settings.ActiveTextTemplate)
                return;

            templateSaveTimer.Stop();
            SaveActiveTemplate();
            Theme.Settings.ActiveTextTemplate = index;
            loadingTemplate = true;
            text.Text = ActiveTemplate.Text;
            loadingTemplate = false;
            UpdateCharacterCount();
            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        private void DeleteTextTemplate(int index)
        {
            if (cts != null)
            {
                ShowStatus("Stop typing before deleting text.", warning: true);
                return;
            }

            if (Theme.Settings.TextTemplates.Count <= 1)
            {
                ShowStatus("At least one text tab must remain.", warning: true);
                return;
            }

            templateSaveTimer.Stop();
            SaveActiveTemplate();

            if (!string.IsNullOrWhiteSpace(Theme.Settings.TextTemplates[index].Text))
            {
                modalDialogOpen = true;
                DialogResult confirm;
                try
                {
                    confirm = ThemedDialog.Confirm(this, "Delete text?",
                        $"\u201c{Theme.Settings.TextTemplates[index].Name}\u201d has saved text. Deleting it can't be undone.",
                        "Delete") ? DialogResult.Yes : DialogResult.No;
                }
                finally
                {
                    modalDialogOpen = false;
                }
                if (confirm != DialogResult.Yes) return;
            }

            int activeIndex = Theme.Settings.ActiveTextTemplate;
            Theme.Settings.TextTemplates.RemoveAt(index);

            if (index < activeIndex)
                Theme.Settings.ActiveTextTemplate = activeIndex - 1;
            else if (index == activeIndex)
                Theme.Settings.ActiveTextTemplate = Math.Min(index, Theme.Settings.TextTemplates.Count - 1);

            if (index == activeIndex)
            {
                loadingTemplate = true;
                text.Text = ActiveTemplate.Text;
                loadingTemplate = false;
                UpdateCharacterCount();
            }

            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        private void RenameTextTemplate(int index)
        {
            if (cts != null || index < 0 || index >= Theme.Settings.TextTemplates.Count)
                return;

            var nameField = new TextField { Width = 320, Margin = Padding.Empty };
            var nameInput = nameField.Input;
            nameInput.Text = Theme.Settings.TextTemplates[index].Name;
            nameInput.MaxLength = 80;
            nameInput.Font = UiFont.Small;
            nameInput.AccessibleName = "Text name";
            var nameError = new Label
            {
                AutoSize = true,
                Font = UiFont.Meta,
                ForeColor = Theme.Negative,
                Margin = new Padding(0, UiSpacing.XSmall, 0, 0),
                Visible = false
            };
            var body = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Surface1
            };
            body.Controls.Add(nameField);
            body.Controls.Add(nameError);

            using var dialog = new ThemedDialog("Rename text", null, body);
            var cancelButton = dialog.AddButton("Cancel", ButtonKind.Secondary, DialogResult.Cancel);
            var saveButton = dialog.AddButton("Save", ButtonKind.Primary, DialogResult.None);
            dialog.AcceptButton = saveButton;
            dialog.CancelButton = cancelButton;
            dialog.Shown += (_, __) => { nameInput.Focus(); nameInput.SelectAll(); };
            nameInput.TextChanged += (_, __) => nameError.Visible = false;
            saveButton.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(nameInput.Text))
                {
                    nameError.Text = "Give the text a name.";
                    nameError.Visible = true;
                    nameInput.Focus();
                    return;
                }
                dialog.DialogResult = DialogResult.OK;
            };

            modalDialogOpen = true;
            DialogResult renameResult;
            try
            {
                renameResult = dialog.ShowDialog(this);
            }
            finally
            {
                modalDialogOpen = false;
            }
            if (renameResult != DialogResult.OK)
                return;

            string name = nameInput.Text.Trim();
            Theme.Settings.TextTemplates[index].Name = name;
            RefreshTemplateTabs();
            Theme.SaveSettings();
        }

        private void AddTextTemplate()
        {
            if (cts != null)
            {
                ShowStatus("Stop typing before adding text.", warning: true);
                return;
            }

            templateSaveTimer.Stop();
            SaveActiveTemplate();
            var template = new TextTemplate
            {
                Name = $"Text {Theme.Settings.TextTemplates.Count + 1}"
            };
            Theme.Settings.TextTemplates.Add(template);
            Theme.Settings.ActiveTextTemplate = Theme.Settings.TextTemplates.Count - 1;
            loadingTemplate = true;
            text.Clear();
            loadingTemplate = false;
            UpdateCharacterCount();
            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        private void SetTemplateTabsEnabled(bool enabled)
        {
            addTemplateButton.Enabled = enabled;
            foreach (Control tab in templateTabs.Controls)
                tab.Enabled = enabled;
        }

        // ---------- Title bar ----------

        private Control BuildTitleBar()
        {
            var titleBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138 + TitleBarHeight * 2));
            titleBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var identity = new TitleIdentity { Dock = DockStyle.Fill, Margin = Padding.Empty };
            titleBar.Controls.Add(identity, 0, 0);

            var windowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var minimize = new WindowCaptionButton(WindowCaptionButtonKind.Minimize) { Height = TitleBarHeight };
            minimize.Click += (_, __) => WindowState = FormWindowState.Minimized;
            maximizeButton.Height = TitleBarHeight;
            var close = new WindowCaptionButton(WindowCaptionButtonKind.Close) { Height = TitleBarHeight };
            close.Click += (_, __) => Close();
            tips.SetToolTip(minimize, "Minimize");
            tips.SetToolTip(close, "Close");
            windowButtons.Controls.Add(close);
            windowButtons.Controls.Add(maximizeButton);
            windowButtons.Controls.Add(minimize);
            themeToggleButton.Size = new Size(TitleBarHeight, TitleBarHeight);
            windowButtons.Controls.Add(themeToggleButton);
            sidebarToggle.Size = new Size(TitleBarHeight, TitleBarHeight);
            windowButtons.Controls.Add(sidebarToggle);
            titleBar.Controls.Add(windowButtons, 1, 0);

            titleBar.MouseDown += TitleBar_MouseDown;
            identity.MouseDown += TitleBar_MouseDown;
            titleBar.DoubleClick += TitleBar_DoubleClick;
            identity.DoubleClick += TitleBar_DoubleClick;
            return titleBar;
        }


        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        private void TitleBar_DoubleClick(object? sender, EventArgs e)
        {
            ToggleWindowState();
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            maximizeButton.IsMaximized = WindowState == FormWindowState.Maximized;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x0083;
            const int WM_NCPAINT = 0x0085;
            const int WM_NCACTIVATE = 0x0086;
            const int WM_NCHITTEST = 0x0084;
            const int WM_GETMINMAXINFO = 0x0024;
            const int HTCLIENT = 1;
            const int HTNOWHERE = 0;

            if (m.Msg == WM_GETMINMAXINFO)
            {
                base.WndProc(ref m);
                var info = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(m.LParam);
                info = NativeMethods.GetMaximizedWorkArea(Handle, info);
                Marshal.StructureToPtr(info, m.LParam, false);
                return;
            }

            if (m.Msg == WM_NCCALCSIZE)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCPAINT)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCACTIVATE)
            {
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_STOP) StopTyping();
                else if (id == HOTKEY_START && !modalDialogOpen) _ = StartTypingAsync();
            }

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if (m.Result != (IntPtr)HTCLIENT && m.Result != (IntPtr)HTNOWHERE) return;

                int edge = ResizeEdgeAt(PointToClient(Cursor.Position));
                if (edge != 0) m.Result = (IntPtr)edge;
                return;
            }

            base.WndProc(ref m);
        }

        // Hit-test code for the resize edge under a client point, or 0 when not on an edge.
        private int ResizeEdgeAt(Point point)
        {
            if (WindowState != FormWindowState.Normal) return 0;
            bool left = point.X < ResizeBorderWidth;
            bool right = point.X >= ClientSize.Width - ResizeBorderWidth;
            bool top = point.Y < ResizeBorderWidth;
            bool bottom = point.Y >= ClientSize.Height - ResizeBorderWidth;

            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;
            return 0;
        }

        // Child controls fill the whole client area, so the form never receives WM_NCHITTEST
        // at its edges. Catch mouse messages headed for any child and start the resize here.
        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_LBUTTONDOWN = 0x0201;
            if (m.Msg != WM_MOUSEMOVE && m.Msg != WM_LBUTTONDOWN) return false;
            if (m.HWnd == Handle || !IsChildWindow(m.HWnd)) return false;

            int edge = ResizeEdgeAt(PointToClient(Cursor.Position));
            if (edge == 0) return false;

            if (m.Msg == WM_MOUSEMOVE)
            {
                Cursor.Current = ResizeCursor(edge);
                return false;
            }
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)edge, IntPtr.Zero);
            return true;
        }

        private bool IsChildWindow(IntPtr hwnd)
        {
            Control? control = Control.FromChildHandle(hwnd);
            return control != null && control.FindForm() == this;
        }

        private static Cursor ResizeCursor(int edge)
        {
            switch (edge)
            {
                case HTLEFT: case HTRIGHT: return Cursors.SizeWE;
                case HTTOP: case HTBOTTOM: return Cursors.SizeNS;
                case HTTOPLEFT: case HTBOTTOMRIGHT: return Cursors.SizeNWSE;
                default: return Cursors.SizeNESW;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_THICKFRAME = 0x00040000;
                const int WS_MINIMIZEBOX = 0x00020000;
                const int WS_MAXIMIZEBOX = 0x00010000;
                const int WS_CAPTION = 0x00C00000;
                const int WS_SYSMENU = 0x00080000;
                var parameters = base.CreateParams;
                // Keep WS_SYSMENU (just drop WS_CAPTION): there's no title bar to show a menu
                // button on, but the bit alone keeps Alt+Space/Alt+F4 and the taskbar icon's
                // right-click system menu working for keyboard-only window management.
                parameters.Style &= ~WS_CAPTION;
                parameters.Style |= WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
                // Paint the form and all children off-screen in one pass, so resizing doesn't flicker.
                const int WS_EX_COMPOSITED = 0x02000000;
                parameters.ExStyle |= WS_EX_COMPOSITED;
                return parameters;
            }
        }

        // ---------- Building blocks ----------

        private static void ConfigureKeycap(RoundedButton button)
        {
            button.MinimumSize = new Size(40, 28);
            // Capped so a long combo (Ctrl+Alt+Shift+Win+F12) can't push the row label out; the
            // tooltip shows it whole.
            button.MaximumSize = new Size(110, 28);
            button.Padding = new Padding(UiSpacing.Small, 0, UiSpacing.Small, 0);
            button.FitText = true;
            button.Margin = Padding.Empty;
        }

        private void UpdateHotkeyButton(RoundedButton button, string name, int modifiers, int key)
        {
            button.Text = FormatHotkey(modifiers, key);
            button.AccessibleName = $"{name} hotkey: {button.Text}. Click to change.";
            tips.SetToolTip(button, $"{button.Text} — click to change");
        }

        private static System.Collections.Generic.List<string> ModifierNames(int modifiers)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
            return parts;
        }

        internal static string FormatHotkey(int modifiers, int keyValue)
        {
            var parts = ModifierNames(modifiers);

            var key = (Keys)keyValue;
            string keyName = key switch
            {
                Keys.OemPeriod => ".",
                Keys.Oemcomma => ",",
                Keys.OemQuestion => "/",
                Keys.OemMinus => "-",
                Keys.Oemplus => "=",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.OemPipe => "\\",
                Keys.OemSemicolon => ";",
                Keys.OemQuotes => "'",
                Keys.Oemtilde => "`",
                Keys.Space => "Space",
                Keys.Return => "Enter",
                Keys.Back => "Backspace",
                Keys.Delete => "Del",
                Keys.Insert => "Ins",
                Keys.Prior => "PgUp",
                Keys.Next => "PgDn",
                Keys.Scroll => "ScrollLock",
                Keys.Snapshot => "PrtSc",
                Keys.Capital => "CapsLock",
                Keys.Multiply => "Num *",
                Keys.Add => "Num +",
                Keys.Subtract => "Num -",
                Keys.Divide => "Num /",
                Keys.Decimal => "Num .",
                _ when key >= Keys.NumPad0 && key <= Keys.NumPad9 => "Num " + (key - Keys.NumPad0),
                _ when key >= Keys.D0 && key <= Keys.D9 => key.ToString().Substring(1),
                // Other layout-dependent keys: the character they type here.
                _ when key.ToString().StartsWith("Oem") && NativeMethods.KeyChar(keyValue) is char c && c > ' ' => c.ToString(),
                _ => key.ToString()
            };
            parts.Add(keyName);
            return string.Join("+", parts);
        }

        // ---------- Hotkeys ----------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Focusing the editor at startup selects all of it; start with the caret at the top instead.
            text.Select(0, 0);
            TryRegisterHotKeys();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetWindowAppearance(Handle, Theme.WindowBorder);
            TryRegisterHotKeys();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            TryUnregisterHotKeys();
            base.OnHandleDestroyed(e);
        }

        private bool TryRegisterHotKeys()
        {
            if (hotkeysRegistered) return true;
            if (IsDisposed || Handle == IntPtr.Zero) return false;

            bool startOk = NativeMethods.RegisterHotKey(
                Handle,
                HOTKEY_START,
                (uint)(Theme.Settings.StartModifiers | HotkeyModifiers.NoRepeat),
                (uint)Theme.Settings.StartKey);
            bool stopOk = NativeMethods.RegisterHotKey(
                Handle,
                HOTKEY_STOP,
                (uint)(Theme.Settings.StopModifiers | HotkeyModifiers.NoRepeat),
                (uint)Theme.Settings.StopKey);
            hotkeysRegistered = startOk && stopOk;

            if (!hotkeysRegistered)
            {
                ShowStatus("Shortcuts unavailable — use the buttons.", warning: true);
                if (startOk) NativeMethods.UnregisterHotKey(Handle, HOTKEY_START);
                if (stopOk) NativeMethods.UnregisterHotKey(Handle, HOTKEY_STOP);
            }

            return hotkeysRegistered;
        }

        private void TryUnregisterHotKeys()
        {
            if (!hotkeysRegistered || IsDisposed || Handle == IntPtr.Zero) return;

            NativeMethods.UnregisterHotKey(Handle, HOTKEY_START);
            NativeMethods.UnregisterHotKey(Handle, HOTKEY_STOP);
            hotkeysRegistered = false;
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            await StartTypingAsync();
        }


        private void ChangeHotkey(bool isStart)
        {
            if (cts != null) return;
            TryUnregisterHotKeys();
            const string Hint = "Press the new shortcut: any key with Ctrl, Alt, Shift or Win, or F1\u2013F24 alone. Esc cancels.";
            var preview = new Label
            {
                Text = "\u2026",
                AutoSize = true,
                Font = UiFont.Heading,
                ForeColor = Theme.Text,
                BackColor = Theme.Surface1,
                Margin = new Padding(0, 0, 0, UiSpacing.Small)
            };
            var instruction = new Label
            {
                Text = Hint,
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                MinimumSize = new Size(320, 0),
                Font = UiFont.Small,
                ForeColor = Theme.Text2,
                BackColor = Theme.Surface1
            };
            var body = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Surface1
            };
            body.Controls.Add(preview);
            body.Controls.Add(instruction);
            using var dialog = new ThemedDialog(isStart ? "Change start shortcut" : "Change stop shortcut", null, body)
            {
                KeyPreview = true
            };
            dialog.AddButton("Cancel", ButtonKind.Secondary, DialogResult.Cancel).TabStop = false;

            void ShowHeld()
            {
                var held = ModifierNames(HeldModifiers());
                preview.Text = held.Count == 0 ? "\u2026" : string.Join("+", held) + "+\u2026";
            }
            void Refuse(string why)
            {
                instruction.Text = why;
                instruction.ForeColor = Theme.Negative;
            }

            int newModifiers = 0;
            int newKey = 0;
            int otherModifiers = isStart ? Theme.Settings.StopModifiers : Theme.Settings.StartModifiers;
            int otherKey = isStart ? Theme.Settings.StopKey : Theme.Settings.StartKey;
            // ProcessCmdKey sees every key, including Tab, Enter, arrows and Alt combos that KeyDown never gets.
            dialog.KeyCapture = keyData =>
            {
                Keys key = keyData & Keys.KeyCode;
                if (key == Keys.Escape)
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    return true;
                }
                if (IsModifierKey(key))
                {
                    ShowHeld();
                    return true;
                }
                int mods = HeldModifiers();
                string combo = FormatHotkey(mods, (int)key);
                preview.Text = combo;
                if (mods == 0 && !HotkeyModifiers.CanStandAlone(key))
                    Refuse($"{combo} alone would fire on normal typing. Hold Ctrl, Alt, Shift or Win with it.");
                else if (mods == HotkeyModifiers.Alt && key == Keys.F4)
                    Refuse("Alt+F4 closes windows everywhere. Pick another shortcut.");
                else if (mods == otherModifiers && (int)key == otherKey)
                    Refuse($"{combo} is already the {(isStart ? "stop" : "start")} shortcut. Start and stop need different shortcuts.");
                else
                {
                    newModifiers = mods;
                    newKey = (int)key;
                    dialog.DialogResult = DialogResult.OK;
                }
                return true;
            };
            dialog.KeyUp += (_, __) => { if (dialog.DialogResult == DialogResult.None && preview.Text.EndsWith("\u2026")) ShowHeld(); };

            DialogResult result;
            try
            {
                result = dialog.ShowDialog(this);
            }
            catch
            {
                TryRegisterHotKeys();
                throw;
            }

            if (result != DialogResult.OK)
            {
                TryRegisterHotKeys();
                return;
            }

            int oldKey = isStart ? Theme.Settings.StartKey : Theme.Settings.StopKey;
            int oldModifiers = isStart ? Theme.Settings.StartModifiers : Theme.Settings.StopModifiers;
            if (isStart)
            {
                Theme.Settings.StartKey = newKey;
                Theme.Settings.StartModifiers = newModifiers;
            }
            else
            {
                Theme.Settings.StopKey = newKey;
                Theme.Settings.StopModifiers = newModifiers;
            }

            if (!TryRegisterHotKeys())
            {
                if (isStart)
                {
                    Theme.Settings.StartKey = oldKey;
                    Theme.Settings.StartModifiers = oldModifiers;
                }
                else
                {
                    Theme.Settings.StopKey = oldKey;
                    Theme.Settings.StopModifiers = oldModifiers;
                }

                TryRegisterHotKeys();
                ThemedDialog.Inform(this, "Shortcut unavailable",
                    $"Windows could not register {FormatHotkey(newModifiers, newKey)}. Another app may already use it" +
                    ((newModifiers & HotkeyModifiers.Win) != 0 ? ", and Windows keeps most Win shortcuts for itself." : "."));
                return;
            }

            UpdateHotkeyButton(startHotkey, "Start typing", Theme.Settings.StartModifiers, Theme.Settings.StartKey);
            UpdateHotkeyButton(stopHotkey, "Stop", Theme.Settings.StopModifiers, Theme.Settings.StopKey);
            start.AccessibleName = $"Start typing ({startHotkey.Text})";
            stop.AccessibleName = $"Stop typing ({stopHotkey.Text})";
            Theme.SaveSettings();
            ShowStatus($"Shortcuts updated — {startHotkey.Text} starts, {stopHotkey.Text} stops.");
        }

        private void ResetHotkeysToDefault()
        {
            if (Theme.Settings.StartKey == (int)Keys.F8 && Theme.Settings.StartModifiers == 0 &&
                Theme.Settings.StopKey == (int)Keys.F9 && Theme.Settings.StopModifiers == 0)
            {
                ShowStatus("Shortcuts are already F8 / F9.");
                return;
            }

            TryUnregisterHotKeys();
            Theme.Settings.StartKey = (int)Keys.F8;
            Theme.Settings.StartModifiers = 0;
            Theme.Settings.StopKey = (int)Keys.F9;
            Theme.Settings.StopModifiers = 0;
            TryRegisterHotKeys();

            UpdateHotkeyButton(startHotkey, "Start typing", Theme.Settings.StartModifiers, Theme.Settings.StartKey);
            UpdateHotkeyButton(stopHotkey, "Stop", Theme.Settings.StopModifiers, Theme.Settings.StopKey);
            start.AccessibleName = $"Start typing ({startHotkey.Text})";
            stop.AccessibleName = $"Stop typing ({stopHotkey.Text})";
            Theme.SaveSettings();
            if (hotkeysRegistered) ShowStatus("Shortcuts reset — F8 starts, F9 stops.");
        }

        private static bool IsModifierKey(Keys key)
        {
            return key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin;
        }

        private static int HeldModifiers() =>
            ToHotkeyModifiers(Control.ModifierKeys) | (NativeMethods.WinDown() ? HotkeyModifiers.Win : 0);

        private static int ToHotkeyModifiers(Keys modifiers)
        {
            int result = 0;
            if ((modifiers & Keys.Control) != 0) result |= HotkeyModifiers.Control;
            if ((modifiers & Keys.Alt) != 0) result |= HotkeyModifiers.Alt;
            if ((modifiers & Keys.Shift) != 0) result |= HotkeyModifiers.Shift;
            return result;
        }

        // ---------- Theme ----------

        private void ToggleTheme()
        {
            Theme.SetMode(Theme.Dark ? "Light" : "Dark");
            RebuildWindowContent();
            Theme.SaveSettings();
            themeToggleButton.Focus();
        }

        // Rebuilds every surface from the current theme. The long-lived controls (editor, inputs,
        // buttons) move into the new layout, so text, values and focus state survive.
        private void RebuildWindowContent()
        {
            Control[] kept =
            {
                text, editorScroll, cpm, startDelay, jitter, jitterValue, pressEnter, start, stop, startHotkey, stopHotkey,
                statusDot, status, characterCount, maximizeButton,
                themeToggleButton, sidebarToggle, addTemplateButton, resetHotkeysLink
            };
            SuspendLayout();
            foreach (Control control in kept)
                control.Parent?.Controls.Remove(control);
            if (sidebarSheet != null)
            {
                Controls.Remove(sidebarSheet);
                sidebarSheet.Dispose();
                sidebarSheet = null;
            }
            Control? old = windowContent;
            if (old != null)
            {
                Controls.Remove(old);
                old.Dispose();
            }
            BackColor = Theme.WindowBorder;
            windowContent = BuildWindowContent();
            Controls.Add(windowContent);
            ApplyResponsive();
            ResumeLayout(true);

            if (IsHandleCreated) NativeMethods.SetWindowAppearance(Handle, Theme.WindowBorder);
            UpdateThemeToggleButton();
            Invalidate(true);
        }

        private void UpdateThemeToggleButton()
        {
            themeToggleButton.AccessibleName = Theme.Dark ? "Switch to light theme" : "Switch to dark theme";
            tips.SetToolTip(themeToggleButton, themeToggleButton.AccessibleName);
            themeToggleButton.Invalidate();
        }

        // ---------- Typing engine (unchanged) ----------

        private async Task StartTypingAsync()
        {
            if (cts != null) return;
            cpm.Commit();
            startDelay.Commit();
            templateSaveTimer.Stop();
            SaveActiveTemplate();
            Theme.SaveSettings();
            string payload = ActiveTemplate.Text;
            if (string.IsNullOrWhiteSpace(payload)) { ShowStatus("Tab is empty \u2014 add text first.", warning: true); return; }
            int controls = payload.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
            if (controls > 0)
            {
                // Backspace, form feed and the like would delete or change text in the target.
                ShowStatus($"This text has {controls:N0} hidden control character{(controls == 1 ? "" : "s")} (like backspace). Remove them first.", warning: true);
                return;
            }

            cts = new CancellationTokenSource();
            SetRunning(true);
            try
            {
                int delay = (int)startDelay.Value;
                for (int i = delay; i > 0; i--)
                {
                    ShowStatus($"Switch to the target window \u2014 {i}");
                    await Task.Delay(1000, cts.Token);
                }
                // A start shortcut such as Ctrl+F8 may still be held; typed Enter/Tab would combine with it.
                for (int waited = 0; NativeMethods.AnyModifierDown() && waited < 2000; waited += 20)
                {
                    ShowStatus("Release Ctrl, Alt, Shift and Win to start.");
                    await Task.Delay(20, cts.Token);
                }

                // The window in front now is the target. If focus moves (a toast, alt-tab, a click
                // on TYPR), the rest of the text would land in the wrong window, so the run stops.
                IntPtr target = NativeMethods.GetForegroundWindow();
                NativeMethods.GetWindowThreadProcessId(target, out uint targetProcess);
                if (target == IntPtr.Zero || targetProcess == (uint)Environment.ProcessId)
                {
                    ShowStatus("TYPR is still in front. Switch to the target window, then start again.", warning: true);
                    return;
                }

                if (NativeMethods.IsElevated(targetProcess) && !NativeMethods.IsElevated((uint)Environment.ProcessId))
                {
                    ShowStatus("That window runs as administrator. Windows blocks typing into it unless TYPR runs as administrator too.", warning: true);
                    return;
                }

                string stopKey = hotkeysRegistered ? FormatHotkey(Theme.Settings.StopModifiers, Theme.Settings.StopKey) : "Stop button";
                ShowStatus($"Typing\u2026 {stopKey} stops.");
                long nextProgress = 0;
                double baseDelayMs = 60000.0 / (double)cpm.Value;
                double jitterPct = (double)jitter.Value / 100.0;
                using var preciseTimer = TimerResolution.Begin();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                double dueMs = 0;

                for (int i = 0; i < payload.Length; i++)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    if (NativeMethods.GetForegroundWindow() != target)
                    {
                        ShowStatus($"Stopped at {i:N0} / {payload.Length:N0} \u2014 the target window lost focus.", warning: true);
                        return;
                    }
                    char ch = payload[i];
                    if (Environment.TickCount64 >= nextProgress)
                    {
                        // Throttled so label updates never slow the typing itself.
                        nextProgress = Environment.TickCount64 + 100;
                        ShowStatus($"Typing {i + 1:N0} / {payload.Length:N0} \u00b7 {stopKey} stops", announce: false);
                    }

                    if (ch == '\r' && i + 1 < payload.Length && payload[i + 1] == '\n') continue;
                    if (ch == '\n' || ch == '\r') NativeMethods.SendVirtualKey((ushort)Keys.Enter);
                    else if (ch == '\t') NativeMethods.SendVirtualKey((ushort)Keys.Tab);
                    else
                    {
                        NativeMethods.SendUnicodeChar(ch);
                        // Keep a surrogate pair's two halves together — a jitter delay between
                        // them risks the receiving app/IME failing to recombine them into one
                        // character (e.g. emoji outside the Basic Multilingual Plane).
                        if (char.IsHighSurrogate(ch) && i + 1 < payload.Length && char.IsLowSurrogate(payload[i + 1]))
                        {
                            i++;
                            NativeMethods.SendUnicodeChar(payload[i]);
                        }
                    }

                    double factor = 1.0 + ((rng.NextDouble() * 2.0 - 1.0) * jitterPct);
                    dueMs += baseDelayMs * factor;
                    await Task.Delay(TypingSchedule.NextWait(ref dueMs, clock.Elapsed.TotalMilliseconds, baseDelayMs), cts.Token);
                }

                if (pressEnter.Checked)
                {
                    if (NativeMethods.GetForegroundWindow() != target)
                    {
                        ShowStatus("Typed everything; Enter skipped \u2014 the target window lost focus.", warning: true);
                        return;
                    }
                    NativeMethods.SendVirtualKey((ushort)Keys.Enter);
                }
                if (!IsDisposed) ShowStatus("Completed.");
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) ShowStatus("Stopped.");
            }
            catch (InvalidOperationException ex)
            {
                if (!IsDisposed)
                {
                    ShowStatus("Typing failed.", warning: true);
                    ThemedDialog.Inform(this, "Typing failed", ex.Message);
                }
            }
            finally
            {
                cts.Dispose();
                cts = null;
                if (!IsDisposed)
                {
                    SetRunning(false);
                }
            }
        }

        // While typing, only Stop stays live: settings are read once at the start, and changing a
        // shortcut mid-run would unregister F9 (Stop must always work).
        private void SetRunning(bool running)
        {
            bool focusOnAction = start.Focused || stop.Focused;
            stop.Enabled = running;
            stop.Kind = running ? ButtonKind.Primary : ButtonKind.Secondary;
            start.Kind = running ? ButtonKind.Secondary : ButtonKind.Primary;
            statusDot.Busy = running;
            SetTemplateTabsEnabled(!running);
            foreach (Control control in new Control[] { cpm, startDelay, jitter, pressEnter, startHotkey, stopHotkey, resetHotkeysLink, themeToggleButton })
                control.Enabled = !running;
            text.ReadOnly = running;
            start.Enabled = !running && !string.IsNullOrWhiteSpace(text.Text);
            // Disabling the focused button would push focus somewhere random; keep it on the live one.
            if (focusOnAction) (running ? stop : start.Enabled ? start : (Control)text).Focus();
        }

        private void StopTyping()
        {
            if (cts == null) return;

            ShowStatus("Stopping\u2026");
            cts.Cancel();
        }
    }

    // ---------- Controls ----------
    // Every control paints from Theme at paint time, so a theme switch only has to repaint them.
    // Focusable controls keep a 3px margin inside their bounds for CNTR's 2px --text focus ring.

    internal static class FocusRing
    {
        public const int Room = 3;

        public static void Draw(Graphics g, Rectangle bounds, int radius)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var ring = bounds;
            ring.Inflate(-1, -1);
            ring.Width--;
            ring.Height--;
            using var path = RoundedPanel.CreatePath(ring, radius);
            using var pen = new Pen(Theme.Text, 2f);
            g.DrawPath(pen, path);
        }
    }

    internal sealed class TabStripPanel : FlowLayoutPanel
    {
        public TabStripPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new SolidBrush(Theme.Line);
            e.Graphics.FillRectangle(brush, 0, ClientSize.Height - 1, ClientSize.Width, 1);
        }
    }

    // A 1px --line divider between card rows.
    internal sealed class LineDivider : Control
    {
        public LineDivider()
        {
            Dock = DockStyle.Fill;
            Height = 1;
            Margin = Padding.Empty;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Surface1);
            using var brush = new SolidBrush(Theme.Line);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    // The footer's state dot: --positive when ready, --accent while counting down or typing.
    // The narrow-window sidebar as a drawer: the sidebar on --bg at the left, the editor behind it dimmed,
    // and a soft shadow off the drawer's edge instead of a hard line. WinForms children can't be
    // translucent, so the sheet covers the work area and paints a snapshot of it; a click on the
    // dimmed part closes the drawer.
    internal sealed class SidebarSheet : Control
    {
        private const int ShadowWidth = 20;
        private Bitmap? backdrop;
        private int backdropOffset;

        public Panel Drawer { get; }
        public event EventHandler? Dismissed;

        public SidebarSheet(int drawerWidth)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Drawer = new Panel
            {
                Dock = DockStyle.Left,
                Width = drawerWidth,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = new Padding(UiSpacing.Medium, 0, 0, 0)
            };
            Controls.Add(Drawer);
        }

        // content is drawn at -offsetY: the sheet starts below the title bar, the snapshot above it.
        public void CaptureBackdrop(Control content, int offsetY)
        {
            backdrop?.Dispose();
            backdrop = null;
            if (content.Width > 0 && content.Height > 0)
            {
                backdrop = new Bitmap(content.Width, content.Height);
                content.DrawToBitmap(backdrop, new Rectangle(0, 0, content.Width, content.Height));
            }
            backdropOffset = offsetY;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            if (backdrop != null) g.DrawImageUnscaled(backdrop, 0, -backdropOffset);
            int edge = Drawer.Right;
            using (var dim = new SolidBrush(Color.FromArgb(Theme.Dark ? 120 : 64, 0, 0, 0)))
                g.FillRectangle(dim, edge, 0, Width - edge, Height);
            var shadow = new Rectangle(edge, 0, ShadowWidth, Height);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(shadow,
                Color.FromArgb(Theme.Dark ? 110 : 48, 0, 0, 0), Color.FromArgb(0, 0, 0, 0),
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
            g.FillRectangle(brush, shadow);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X >= Drawer.Right) Dismissed?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) backdrop?.Dispose();
            base.Dispose(disposing);
        }
    }

    // Segoe Fluent Icons / MDL2 Assets code points used by the sidebar rows.
    internal static class Glyph
    {
        public const string Speed = "\uEC4A";
        public const string Stopwatch = "\uE916";
        public const string Pulse = "\uE9D9";
        public const string Enter = "\uE751";
        public const string Play = "\uE768";
        public const string Stop = "\uE71A";
        public const string Sidebar = "\uE90C";
    }

    // A 26px rounded tile tinted with its colour at low strength, the glyph in full colour.
    internal sealed class RowIcon : Control
    {
        public string Glyph { get; set; } = "";
        public Color Tint { get; set; } = Theme.Accent;

        public RowIcon()
        {
            Size = new Size(26, 26);
            Margin = new Padding(0, 0, UiSpacing.Small + 2, 0);
            Anchor = AnchorStyles.Left;
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color ground = Parent?.BackColor ?? Theme.Surface1;
            g.Clear(ground);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var tile = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedPanel.CreatePath(tile, UiRadius.Small))
            using (var brush = new SolidBrush(Theme.Blend(Color.FromArgb(Theme.Dark ? 46 : 30, Tint), ground)))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, Glyph, UiFont.Icon, ClientRectangle, Tint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class StatusDot : Control
    {
        private bool busy;
        private bool warning;

        public bool Busy
        {
            get => busy;
            set { busy = value; Invalidate(); }
        }

        public bool Warning
        {
            get => warning;
            set { warning = value; Invalidate(); }
        }

        // Nothing to do yet (empty tab): --text-3, not the green "ready".
        private bool idle;
        public bool Idle
        {
            get => idle;
            set { idle = value; Invalidate(); }
        }

        public StatusDot()
        {
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(warning ? Theme.Negative : busy ? Theme.Accent : idle ? Theme.Text3 : Theme.Positive);
            e.Graphics.FillEllipse(brush, Width / 2 - 4, Height / 2 - 4, 8, 8);
        }
    }

    // CNTR's chip: --surface-2 pill, --fs-xs bold --text-2. Sizes itself to its text.
    internal sealed class ChipLabel : Label
    {
        public ChipLabel()
        {
            AutoSize = true;
            Font = UiFont.Label;
            Padding = new Padding(UiSpacing.Small + 2, 4, UiSpacing.Small + 2, 4);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var pill = ClientRectangle;
            pill.Width--;
            pill.Height--;
            using (var path = RoundedPanel.CreatePath(pill, pill.Height / 2))
            using (var brush = new SolidBrush(Theme.Surface2))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Theme.Text2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // Logo mark, the spaced TYPR wordmark (CNTR's wordmark is spaced 0.32em) and the subtitle.
    internal sealed class TitleIdentity : Control
    {
        public TitleIdentity()
        {
            TabStop = false;
            AccessibleRole = AccessibleRole.TitleBar;
            AccessibleName = "TYPR, auto typer";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

            int x = UiSpacing.Large;
            var logo = new Rectangle(x, (Height - 20) / 2, 20, 20);
            using (var path = RoundedPanel.CreatePath(logo, 5))
            using (var brush = new SolidBrush(Theme.Logo))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, "T", UiFont.Label, logo, Color.White,
                TextFormatFlags.HorizontalCenter | flags);
            x = logo.Right + UiSpacing.Medium;

            Font word = UiFont.SmallBold;
            int tracking = (int)Math.Round(word.SizeInPoints * 96 / 72 * 0.32);
            foreach (char letter in "TYPR")
            {
                string glyph = letter.ToString();
                int width = TextRenderer.MeasureText(g, glyph, word, Size, flags).Width;
                TextRenderer.DrawText(g, glyph, word, new Rectangle(x, 0, width + 2, Height), Theme.Text, flags);
                x += width + tracking;
            }
            x += UiSpacing.XSmall;
            TextRenderer.DrawText(g, "auto typer", UiFont.Small, new Rectangle(x, 0, Math.Max(0, Width - x), Height),
                Theme.Text3, flags);
        }
    }

    // A VS Code editor tab: square, flush with its neighbours. The active tab takes the editor's
    // --surface-1 and opens into it; the others sit on --bg behind a --line edge.
    internal sealed class EditorTabPanel : Panel
    {
        private const int CloseSize = 20;
        private bool hovered;
        private bool closeHovered;

        public bool IsActive { get; set; }
        public event EventHandler? CloseRequested;
        public event EventHandler? RenameRequested;

        public EditorTabPanel()
        {
            // Panel disables ControlStyles.Selectable by default, which blocks keyboard focus
            // entirely regardless of TabStop — re-enable it so tabs are keyboard-operable.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                RenameRequested?.Invoke(this, EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            if (!IsActive)
                using (var line = new SolidBrush(Theme.Line))
                {
                    g.FillRectangle(line, 0, Height - 1, Width, 1);                  // the editor's top edge
                    g.FillRectangle(line, Width - 1, 10, 1, Height - 20);            // short divider to the next tab
                }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // Active (and hovered) tabs have rounded top corners and no bottom edge, so the active one
            // runs straight into the editor below.
            if (IsActive || (hovered && Enabled))
            {
                Color fill = IsActive ? Theme.Surface1 : Theme.Blend(Color.FromArgb(128, Theme.Surface1), Theme.Bg);
                using (var shape = TabOutline(closed: true))
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, shape);
                if (IsActive)
                    using (var outline = TabOutline(closed: false))
                    using (var pen = new Pen(Theme.Line))
                        g.DrawPath(pen, outline);
            }

            Color ink = !Enabled ? Theme.Text3 : IsActive ? Theme.Text : hovered ? Theme.Text2 : Theme.Text3;
            DrawDocumentIcon(g, IsActive && Enabled ? Theme.Accent : ink, UiSpacing.Medium, Height / 2);
            TextRenderer.DrawText(g, Text, Font, GetTextBounds(), ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Like VS Code, the close button shows on the active tab and on hover only.
            if (IsActive || hovered || Focused)
            {
                Rectangle close = GetCloseButtonBounds();
                if (closeHovered && Enabled)
                    using (var brush = new SolidBrush(Theme.Surface3))
                    using (var path = RoundedPanel.CreatePath(close, 4))
                        g.FillPath(brush, path);
                int inset = 6;
                using var pen = new Pen(ink, 1.4f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawLine(pen, close.Left + inset, close.Top + inset, close.Right - inset, close.Bottom - inset);
                g.DrawLine(pen, close.Right - inset, close.Top + inset, close.Left + inset, close.Bottom - inset);
            }

            if (Focused && ShowFocusCues)
            {
                var ring = ClientRectangle;
                ring.Inflate(-2, -2);
                FocusRing.Draw(g, ring, UiRadius.Small);
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath TabOutline(bool closed)
        {
            const int r = UiRadius.Small;
            int w = Width - 1;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddLine(0, Height, 0, r);
            path.AddArc(0, 0, r * 2, r * 2, 180, 90);
            path.AddArc(w - r * 2, 0, r * 2, r * 2, 270, 90);
            path.AddLine(w, r, w, Height);
            if (closed) path.CloseFigure();
            return path;
        }

        // A small page with two text lines: the "file" this tab holds.
        private static void DrawDocumentIcon(Graphics g, Color color, int left, int cy)
        {
            var page = new Rectangle(left, cy - 7, 11, 14);
            using var pen = new Pen(color, 1.3f);
            using (var path = RoundedPanel.CreatePath(page, 2))
                g.DrawPath(pen, path);
            g.DrawLine(pen, page.Left + 3, cy - 1, page.Right - 3, cy - 1);
            g.DrawLine(pen, page.Left + 3, cy + 3, page.Right - 3, cy + 3);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && GetCloseButtonBounds().Contains(e.Location))
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && GetTextBounds().Contains(e.Location))
            {
                RenameRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overClose = GetCloseButtonBounds().Contains(e.Location);
            if (!hovered || overClose != closeHovered)
            {
                hovered = true;
                closeHovered = overClose;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            closeHovered = false;
            Invalidate();
        }

        private Rectangle GetCloseButtonBounds() =>
            new Rectangle(Width - CloseSize - 8, (Height - CloseSize) / 2, CloseSize, CloseSize);

        private Rectangle GetTextBounds()
        {
            int left = UiSpacing.Medium + 11 + 6;
            return new Rectangle(left, 0, Math.Max(0, GetCloseButtonBounds().Left - 4 - left), Height);
        }
    }

    // The strip's + : a round icon button that fills --surface-2 on hover.
    internal sealed class AddTextButton : Control
    {
        private bool hovered;

        public AddTextButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var line = new SolidBrush(Theme.Line))
                g.FillRectangle(line, 0, Height - 1, Width, 1);
            var circle = new Rectangle(Width / 2 - 13, Height / 2 - 13, 26, 26);
            if (hovered && Enabled)
                using (var brush = new SolidBrush(Theme.Surface2))
                using (var path = RoundedPanel.CreatePath(circle, 6))
                    g.FillPath(brush, path);

            int cx = Width / 2;
            int cy = Height / 2;
            using (var pen = new Pen(Enabled ? Theme.Text2 : Theme.Text3, 1.6f))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                g.DrawLine(pen, cx, cy - 6, cx, cy + 6);
            }

            if (Focused && ShowFocusCues)
            {
                var ring = circle;
                ring.Inflate(3, 3);
                FocusRing.Draw(g, ring, 9);
            }
        }
    }

    internal class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 12;
        public Color BorderColor { get; set; } = Color.Transparent;
        public bool SquareTop { get; set; }

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var path = CreatePath(ClientRectangle, CornerRadius, SquareTop))
            using (var fill = new SolidBrush(BackColor))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.Clear(Parent?.BackColor ?? BackColor);
                e.Graphics.FillPath(fill, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor.A == 0) return;

            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            using (var path = CreatePath(bounds, CornerRadius, SquareTop))
            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
            // Square-topped panels open into the tab strip above, which draws that edge itself.
            if (SquareTop)
                using (var fill = new SolidBrush(BackColor))
                    e.Graphics.FillRectangle(fill, 1, 0, Width - 2, 1);
        }

        public static System.Drawing.Drawing2D.GraphicsPath CreatePath(Rectangle bounds, int radius, bool squareTop = false)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            if (squareTop)
            {
                path.AddLine(bounds.X, bounds.Y, bounds.Right, bounds.Y);
            }
            else
            {
                path.AddArc(arc, 180, 90);
                arc.X = bounds.Right - diameter;
                path.AddArc(arc, 270, 90);
            }
            arc.X = bounds.Right - diameter;
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal enum ButtonKind
    {
        Primary,   // --accent / --on-accent
        Secondary, // --surface-2 / --text
        Keycap,    // a shortcut key: --surface-2, --r-sm, --fs-sm bold
        Danger     // --negative / --on-negative, destructive confirms only
    }

    // CNTR's .btn: --r-md, --fs-md bold; a press shrinks it to 0.97 over --dur-tap.
    // A plain Control, not Button: Button's own native painting showed through the owner-draw.
    // IButtonControl keeps AcceptButton, CancelButton and DialogResult working in dialogs.
    internal sealed class RoundedButton : Control, IButtonControl
    {
        private readonly Tween pressTween = new Tween();
        private ButtonKind kind = ButtonKind.Secondary;
        private bool hovered;
        private double press;

        public ButtonKind Kind
        {
            get => kind;
            set
            {
                kind = value;
                Font = value == ButtonKind.Keycap ? UiFont.Label : UiFont.Button;
                Invalidate();
            }
        }

        public DialogResult DialogResult { get; set; }

        public RoundedButton()
        {
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = UiFont.Button;
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable |
                ControlStyles.StandardClick, true);
            SetStyle(ControlStyles.StandardDoubleClick, false);
        }

        public void NotifyDefault(bool value) { }

        public void PerformClick()
        {
            if (Enabled) OnClick(EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (DialogResult != DialogResult.None && FindForm() is Form form && form.Modal)
                form.DialogResult = DialogResult;
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                PerformClick();
            }
            base.OnKeyDown(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
            if (fitText) Size = GetPreferredSize(Size.Empty);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return new Size(
                Math.Max(MinimumSize.Width, textSize.Width + Padding.Horizontal + FocusRing.Room * 2),
                Math.Max(MinimumSize.Height, textSize.Height + FocusRing.Room * 2));
        }

        // Sizes the button to its text (and MinimumSize) now and on every text change. Used instead of
        // AutoSize, whose late resize after the first paint left stale pixels on screen.
        private bool fitText;
        public bool FitText
        {
            get => fitText;
            set
            {
                fitText = value;
                if (value) Size = GetPreferredSize(Size.Empty);
            }
        }



        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Animate(0); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled) Animate(1);
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Animate(0);
            base.OnMouseUp(e);
        }

        private void Animate(double target)
        {
            double from = press;
            if (from == target) { Invalidate(); return; }
            pressTween.Run(Motion.Tap, t =>
            {
                press = from + (target - from) * Motion.EaseOut(t);
                Invalidate();
            });
        }

        protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var face = ClientRectangle;
            face.Inflate(-FocusRing.Room, -FocusRing.Room);
            int shrinkX = (int)Math.Round(face.Width * 0.03 * press / 2);
            int shrinkY = (int)Math.Round(face.Height * 0.03 * press / 2);
            face.Inflate(-shrinkX, -shrinkY);
            int radius = kind == ButtonKind.Keycap ? UiRadius.Small : UiRadius.Medium;

            Color ground = Parent?.BackColor ?? Theme.Bg;
            // Disabled: no fill, a --line outline, so it can't pass for the live grey button.
            Color fill = !Enabled ? ground : kind switch
            {
                ButtonKind.Primary => Theme.Accent,
                ButtonKind.Danger => Theme.Negative,
                _ => Theme.Surface2
            };
            if (Enabled && hovered) fill = Theme.Hover(fill);
            Color ink = !Enabled ? Theme.Text3 : kind switch
            {
                ButtonKind.Primary => Theme.OnAccent,
                ButtonKind.Danger => Theme.OnNegative,
                _ => Theme.Text
            };

            if (kind == ButtonKind.Keycap)
            {
                // A key: --input-border edge with a 2px lip at the bottom; the lip goes on press.
                int lip = press > 0.5 ? 1 : 2;
                using (var path = RoundedPanel.CreatePath(face, radius))
                using (var brush = new SolidBrush(Enabled ? Theme.InputBorder : Theme.Line))
                    g.FillPath(brush, path);
                face = new Rectangle(face.X + 1, face.Y + 1 + (2 - lip), face.Width - 2, face.Height - 2 - lip);
                radius--;
            }
            else if (!Enabled)
            {
                using var outline = RoundedPanel.CreatePath(new Rectangle(face.X, face.Y, face.Width - 1, face.Height - 1), radius);
                using var pen = new Pen(Theme.Line);
                g.DrawPath(pen, outline);
            }
            if (Enabled || kind == ButtonKind.Keycap)
                using (var path = RoundedPanel.CreatePath(face, radius))
                using (var brush = new SolidBrush(Enabled ? fill : Theme.Surface1))
                    g.FillPath(brush, path);
            TextRenderer.DrawText(g, Text, Font, face, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
                FocusRing.Draw(g, ClientRectangle, radius + 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) pressTween.Dispose();
            base.Dispose(disposing);
        }
    }

    // CNTR's switch: a 44x26 pill (--surface-3 off, --accent on). The knob slides like a liquid drop
    // over --dur-liquid / --ease-liquid: it stretches on the way, squishes at the edge and settles,
    // and the accent fills in behind it.
    internal sealed class ToggleCheckBox : Control
    {
        private readonly Tween tween = new Tween();
        private double position;
        private double progress = 1;

        public bool Checked { get; private set; }

        public ToggleCheckBox()
        {
            Size = new Size(48, 30);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleDescription = "Off";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        public void Toggle()
        {
            Checked = !Checked;
            AccessibleDescription = Checked ? "On" : "Off";
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            double from = position;
            double to = Checked ? 1 : 0;
            tween.Run(Motion.Liquid, t =>
            {
                progress = t;
                position = t >= 1 ? to : from + (to - from) * Motion.EaseLiquid(t);
                Invalidate();
            });
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Toggle();
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface1);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var track = new Rectangle((Width - 44) / 2, (Height - 26) / 2, 44, 26);
            double on = Math.Clamp(position, 0, 1);

            using (var path = RoundedPanel.CreatePath(track, track.Height / 2))
            {
                using (var brush = new SolidBrush(Enabled ? Theme.Surface3 : Theme.Surface2))
                    g.FillPath(brush, path);
                if (on > 0)
                {
                    var saved = g.Save();
                    g.SetClip(path);
                    using (var brush = new SolidBrush(Enabled ? Theme.Accent : Theme.Blend(Color.FromArgb(110, Theme.Text3), Theme.Surface2)))
                        g.FillRectangle(brush, track.X, track.Y, (float)(track.Width * on), track.Height);
                    g.Restore(saved);
                }
            }

            // Stretch to 1.55 x 0.8 on the way, squish to 0.82 x 1.14 at the edge, then settle.
            double sx = 1, sy = 1;
            if (progress < 0.55)
            {
                double s = Math.Sin(Math.PI * progress / 0.55);
                sx = 1 + 0.55 * s;
                sy = 1 - 0.2 * s;
            }
            else if (progress < 0.85)
            {
                double s = Math.Sin(Math.PI * (progress - 0.55) / 0.3);
                sx = 1 - 0.18 * s;
                sy = 1 + 0.14 * s;
            }
            double centerX = track.X + 13 + 18 * position;
            double centerY = track.Y + 13;
            float knobW = (float)(20 * sx);
            float knobH = (float)(20 * sy);
            // Off knob: --text-3 in dark; in light a white knob with an --input-border ring (U20).
            Color offKnob = Theme.Dark || !Enabled ? Theme.Text3 : Theme.Bg;
            Color onKnob = Enabled ? Theme.OnAccent : Theme.Surface1;
            Color knob = Theme.Blend(Color.FromArgb((int)(255 * on), onKnob), offKnob);
            var knobRect = new RectangleF((float)(centerX - knobW / 2), (float)(centerY - knobH / 2), knobW, knobH);
            using (var brush = new SolidBrush(knob))
                g.FillEllipse(brush, knobRect);
            if (!Theme.Dark && Enabled && on < 1)
                using (var pen = new Pen(Color.FromArgb((int)(255 * (1 - on)), Theme.InputBorder)))
                    g.DrawEllipse(pen, knobRect);

            if (Focused && ShowFocusCues)
            {
                var ring = track;
                ring.Inflate(2, 2);
                FocusRing.Draw(g, ring, ring.Height / 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tween.Dispose();
            base.Dispose(disposing);
        }
    }

    // CNTR's input: --surface-1, 1px --line-strong border, --r-md; focus turns the border --text-2.
    internal sealed class TextField : UserControl
    {
        private readonly RoundedPanel frame;
        private readonly Panel host;
        public TextBox Input { get; } = new TextBox();

        public TextField()
        {
            Height = 26;
            frame = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = UiRadius.Medium,
                Padding = new Padding(UiSpacing.Medium, 2, UiSpacing.Medium, 2),
                Margin = Padding.Empty
            };
            host = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            Input.BorderStyle = BorderStyle.None;
            Input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Input.Height = Input.PreferredHeight;
            host.Resize += (_, __) =>
            {
                Input.Width = host.ClientSize.Width;
                Input.Top = Math.Max(0, (host.ClientSize.Height - Input.Height) / 2);
            };
            Input.GotFocus += (_, __) => { frame.BorderColor = Theme.Text2; frame.Invalidate(); };
            Input.LostFocus += (_, __) => { frame.BorderColor = Theme.InputBorder; frame.Invalidate(); };
            host.Controls.Add(Input);
            frame.Controls.Add(host);
            Controls.Add(frame);
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            frame.BackColor = Theme.Surface1;
            frame.BorderColor = Input.Focused ? Theme.Text2 : Theme.InputBorder;
            host.BackColor = Theme.Surface1;
            Input.BackColor = Theme.Surface1;
            Input.ForeColor = Theme.Text;
            Invalidate(true);
        }
    }

    internal sealed class NumberInput : UserControl
    {
        private readonly TextField field = new TextField { Dock = DockStyle.Fill };
        private TextBox input => field.Input;

        public event EventHandler? ValueChanged;
        private decimal value;
        private decimal minimum;
        private decimal maximum = 100;

        public decimal Minimum
        {
            get => minimum;
            set
            {
                minimum = value;
                if (this.value < minimum) Value = minimum;
            }
        }

        public decimal Maximum
        {
            get => maximum;
            set
            {
                maximum = value;
                if (this.value > maximum) Value = maximum;
            }
        }

        public decimal Value
        {
            get => value;
            set => SetValue(value);
        }

        // The name a screen reader reads for the text box inside.
        public string FieldName
        {
            set => input.AccessibleName = value;
        }

        public void ApplyTheme() => field.ApplyTheme();

        public NumberInput()
        {
            Height = 26;
            MinimumSize = new Size(64, 26);
            input.Font = UiFont.SmallBold;
            input.TextAlign = HorizontalAlignment.Right;
            input.Text = "0";
            input.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            input.KeyDown += (_, e) =>
            {
                // Commit a typed value first, so Up/Down step from it instead of discarding it.
                if (e.KeyCode == Keys.Up) { CommitInput(); Value++; e.Handled = true; }
                if (e.KeyCode == Keys.Down) { CommitInput(); Value--; e.Handled = true; }
                if (e.KeyCode == Keys.Enter) { CommitInput(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            input.Leave += (_, __) => CommitInput();
            Controls.Add(field);
        }

        // F8 starts a run without the box losing focus, so the run commits the typed value itself.
        public void Commit() => CommitInput();

        private void CommitInput()
        {
            if (int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) SetValue(parsed);
            else input.Text = value.ToString();
        }

        private void SetValue(decimal next)
        {
            value = Math.Max(minimum, Math.Min(maximum, next));
            input.Text = value.ToString();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // The editor: a multiline TextBox that says when its view may have scrolled, so the thin
    // scroll bar can follow. Line numbers are display lines (word wrap included).
    internal sealed class EditorBox : TextBox
    {
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_LINESCROLL = 0x00B6;

        public event EventHandler? ViewChanged;

        public int FirstVisibleLine => IsHandleCreated ? (int)NativeMethods.SendMessage(Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero) : 0;
        public int LineCount => IsHandleCreated ? (int)NativeMethods.SendMessage(Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero) : 1;
        public int VisibleLines => Math.Max(1, ClientSize.Height / Math.Max(1, Font.Height));

        public void ScrollToLine(int line)
        {
            int delta = line - FirstVisibleLine;
            if (delta != 0) NativeMethods.SendMessage(Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Messages after which the first visible line or the line count can change:
            // size, set text, key down, char, mouse move (drag-select), wheel, scroll, paste.
            switch (m.Msg)
            {
                case 0x0005: case 0x000C: case 0x0100: case 0x0102: case 0x0200: case 0x020A: case 0x0115: case 0x0302:
                    ViewChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    // VS Code-style overlay scroll bar: no track, a thin --text-3 pill that widens on hover or drag.
    // Hidden while everything fits.
    internal sealed class ThinScrollBar : Control
    {
        private const int MinThumb = 24;
        private EditorBox? target;
        private bool hovered;
        private int dragOffset = -1;
        private (int first, int lines, int visible) shown;

        public EditorBox? Target
        {
            get => target;
            set
            {
                target = value;
                if (target != null)
                {
                    target.ViewChanged += (_, __) => Sync();
                    target.TextChanged += (_, __) => Sync();
                }
            }
        }

        public ThinScrollBar()
        {
            Width = 12;
            TabStop = false;
            Margin = Padding.Empty;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private void Sync()
        {
            if (target == null) return;
            var now = (target.FirstVisibleLine, target.LineCount, target.VisibleLines);
            if (now == shown) return;
            shown = now;
            Invalidate();
        }

        private Rectangle Thumb()
        {
            var (first, lines, visible) = shown;
            if (lines <= visible || Height <= 0) return Rectangle.Empty;
            int height = Math.Max(MinThumb, Height * visible / lines);
            int top = (int)((long)(Height - height) * first / Math.Max(1, lines - visible));
            return new Rectangle(0, Math.Clamp(top, 0, Height - height), Width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface1);
            var thumb = Thumb();
            if (thumb.IsEmpty) return;
            bool active = hovered || dragOffset >= 0;
            int width = active ? 8 : 4;
            var pill = new Rectangle(Width - width - 2, thumb.Y, width, thumb.Height);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = RoundedPanel.CreatePath(pill, width / 2);
            using var brush = new SolidBrush(Color.FromArgb(active ? 220 : 150, Theme.Text3));
            g.FillPath(brush, path);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || target == null) return;
            var thumb = Thumb();
            if (thumb.IsEmpty) return;
            // A click off the thumb centres it there, then drags from the middle.
            dragOffset = thumb.Contains(e.Location) ? e.Y - thumb.Y : thumb.Height / 2;
            DragTo(e.Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragOffset >= 0) DragTo(e.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragOffset = -1;
            Invalidate();
        }

        private void DragTo(int y)
        {
            var (_, lines, visible) = shown;
            int travel = Height - Thumb().Height;
            if (target == null || travel <= 0) return;
            double fraction = Math.Clamp((y - dragOffset) / (double)travel, 0, 1);
            target.ScrollToLine((int)Math.Round(fraction * (lines - visible)));
            Sync();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Wheel over the bar scrolls the editor, 3 lines a notch like Windows.
            if (target != null) target.ScrollToLine(target.FirstVisibleLine - Math.Sign(e.Delta) * 3);
            Sync();
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Sync(); }
    }

    internal sealed class Slider : Control
    {
        private const int Pad = 12;
        private int minimum;
        private int maximum = 50;
        private int val = 5;
        private bool dragging;
        private bool hovered;

        public event EventHandler? ValueChanged;

        public int Minimum
        {
            get => minimum;
            set { minimum = value; Invalidate(); }
        }

        public int Maximum
        {
            get => maximum;
            set { maximum = value; Invalidate(); }
        }

        public int Value
        {
            get => val;
            set
            {
                int clamped = Math.Max(minimum, Math.Min(maximum, value));
                if (clamped == val) return;
                val = clamped;
                AccessibleDescription = val + "%";
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Slider()
        {
            Height = 32;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        private double Fraction => maximum <= minimum ? 0 : (double)(val - minimum) / (maximum - minimum);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color ground = Parent?.BackColor ?? Theme.Surface1;
            g.Clear(ground);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var track = new Rectangle(Pad, Height / 2 - 2, Math.Max(1, Width - Pad * 2), 4);
            using (var path = RoundedPanel.CreatePath(track, 2))
            using (var brush = new SolidBrush(Theme.Blend(Color.FromArgb(140, Theme.Text3), ground)))
                g.FillPath(brush, path);

            Color active = Enabled ? Theme.Accent : Theme.Text3;
            int fillWidth = (int)(track.Width * Fraction);
            if (fillWidth > 8)
                using (var path = RoundedPanel.CreatePath(new Rectangle(track.X, track.Y, fillWidth, track.Height), 2))
                using (var brush = new SolidBrush(active))
                    g.FillPath(brush, path);

            int radius = hovered || dragging ? 9 : 8;
            int cx = track.X + fillWidth;
            int cy = Height / 2;
            using (var halo = new SolidBrush(ground))
                g.FillEllipse(halo, cx - radius - 2, cy - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
            using (var brush = new SolidBrush(active))
                g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);

            if (Focused && ShowFocusCues)
                FocusRing.Draw(g, new Rectangle(cx - radius - 4, cy - radius - 4, (radius + 4) * 2, (radius + 4) * 2), radius + 4);
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End ||
            base.IsInputKey(keyData);

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            dragging = true;
            Focus();
            UpdateFromX(e.X);
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) UpdateFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Value--; e.Handled = true; }
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Value++; e.Handled = true; }
            if (e.KeyCode == Keys.PageDown) { Value -= 10; e.Handled = true; }
            if (e.KeyCode == Keys.PageUp) { Value += 10; e.Handled = true; }
            if (e.KeyCode == Keys.Home) { Value = minimum; e.Handled = true; }
            if (e.KeyCode == Keys.End) { Value = maximum; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        private void UpdateFromX(int x)
        {
            int usable = Math.Max(1, Width - Pad * 2);
            double frac = Math.Max(0, Math.Min(1, (x - Pad) / (double)usable));
            Value = minimum + (int)Math.Round(frac * (maximum - minimum));
        }
    }

    internal enum WindowCaptionButtonKind
    {
        Minimize,
        Maximize,
        Close
    }

    internal sealed class WindowCaptionButton : Control
    {
        private bool hovered;

        public WindowCaptionButtonKind Kind { get; }
        private bool isMaximized;
        public bool IsMaximized
        {
            get => isMaximized;
            set
            {
                isMaximized = value;
                if (Kind == WindowCaptionButtonKind.Maximize) AccessibleName = value ? "Restore down" : "Maximize";
                Invalidate();
            }
        }

        public WindowCaptionButton(WindowCaptionButtonKind kind)
        {
            Kind = kind;
            Width = 46;
            Height = 40;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            TabStop = false;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = kind.ToString();
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool closeHover = hovered && Kind == WindowCaptionButtonKind.Close;
            Color background = !hovered ? Theme.Bg : closeHover ? Color.FromArgb(196, 43, 28) : Theme.Surface2;
            e.Graphics.Clear(background);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var pen = new Pen(closeHover ? Color.White : hovered ? Theme.Text : Theme.Text2, 1.2f);
            int cx = Width / 2;
            int cy = Height / 2;
            if (Kind == WindowCaptionButtonKind.Minimize)
            {
                e.Graphics.DrawLine(pen, cx - 5, cy, cx + 5, cy);
            }
            else if (Kind == WindowCaptionButtonKind.Maximize && IsMaximized)
            {
                e.Graphics.DrawRectangle(pen, cx - 3, cy - 5, 8, 8);
                e.Graphics.DrawLine(pen, cx - 5, cy - 2, cx - 5, cy + 5);
                e.Graphics.DrawLine(pen, cx - 5, cy + 5, cx + 2, cy + 5);
            }
            else if (Kind == WindowCaptionButtonKind.Maximize)
            {
                e.Graphics.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
            }
            else
            {
                e.Graphics.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                e.Graphics.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
            }
        }
    }

    // The title bar's light/dark switch: a glyph that gets a --surface-2 square on hover.
    internal sealed class TitleIconButton : Control
    {
        private bool hovered;

        // An icon-font glyph; null draws the light/dark glyph.
        public string? Glyph { get; set; }

        // Pressed-in look while the thing it opens is showing.
        private bool active;
        public bool Active
        {
            get => active;
            set { active = value; Invalidate(); }
        }

        public TitleIconButton()
        {
            Width = 40;
            Height = 40;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int cx = Width / 2;
            int cy = Height / 2;
            var square = new Rectangle(cx - 13, cy - 13, 26, 26);
            Color ground = hovered || active ? Theme.Surface2 : Theme.Bg;
            if (hovered || active)
                using (var path = RoundedPanel.CreatePath(square, UiRadius.Small))
                using (var brush = new SolidBrush(ground))
                    g.FillPath(brush, path);

            Color ink = hovered || active ? Theme.Text : Theme.Text2;
            if (Glyph != null)
                TextRenderer.DrawText(g, Glyph, UiFont.Icon, ClientRectangle, ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            else
                DrawThemeGlyph(g, ink, ground, cx, cy);

            if (Focused && ShowFocusCues)
            {
                var ring = square;
                ring.Inflate(3, 3);
                FocusRing.Draw(g, ring, UiRadius.Small);
            }
        }

        private static void DrawThemeGlyph(Graphics g, Color color, Color background, int cx, int cy)
        {
            using var pen = new Pen(color, 1.6f);
            using var brush = new SolidBrush(color);

            if (Theme.Dark)
            {
                g.FillEllipse(brush, cx - 7, cy - 7, 14, 14);
                using var cutout = new SolidBrush(background);
                g.FillEllipse(cutout, cx - 2, cy - 10, 13, 13);
                return;
            }

            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            for (int angle = 0; angle < 360; angle += 45)
            {
                double radians = angle * Math.PI / 180.0;
                float x1 = cx + (float)(Math.Cos(radians) * 7.5);
                float y1 = cy + (float)(Math.Sin(radians) * 7.5);
                float x2 = cx + (float)(Math.Cos(radians) * 10);
                float y2 = cy + (float)(Math.Sin(radians) * 10);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
            g.FillEllipse(brush, cx - 4, cy - 4, 8, 8);
        }
    }

    // TYPR's dialogs in CNTR's sheet style: --surface-1, title --fs-lg bold, text --fs-sm --text-2,
    // a button row with Cancel first. Buttons say what they do (CNTR copy rules).
    internal sealed class ThemedDialog : Form
    {
        private readonly TableLayoutPanel layout;
        private readonly FlowLayoutPanel buttons;

        public ThemedDialog(string title, string? message, Control? body = null)
        {
            // The heading below already says the title; the caption only names the app.
            Text = "TYPR";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.Surface1;
            ForeColor = Theme.Text;
            Font = UiFont.Small;
            Padding = new Padding(UiSpacing.XLarge, UiSpacing.Large, UiSpacing.XLarge, UiSpacing.Large);

            layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = UiFont.Heading,
                ForeColor = Theme.Text,
                Margin = new Padding(0, 0, 0, UiSpacing.Small)
            });
            if (message != null)
                layout.Controls.Add(new Label
                {
                    Text = message,
                    AutoSize = true,
                    MaximumSize = new Size(360, 0),
                    MinimumSize = new Size(320, 0),
                    Font = UiFont.Small,
                    ForeColor = Theme.Text2,
                    Margin = new Padding(0, 0, 0, UiSpacing.Large)
                });
            if (body != null)
            {
                body.Margin = new Padding(0, 0, 0, UiSpacing.Large);
                layout.Controls.Add(body);
            }
            buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Right,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty
            };
            layout.Controls.Add(buttons);
            Controls.Add(layout);
        }

        // Gets every key press before the dialog acts on it (Tab, Enter, Esc included); true = handled.
        public Func<Keys, bool>? KeyCapture { get; set; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
            (KeyCapture != null && KeyCapture(keyData)) || base.ProcessCmdKey(ref msg, keyData);

        public RoundedButton AddButton(string text, ButtonKind kind, DialogResult result)
        {
            var button = new RoundedButton
            {
                Text = text,
                Kind = kind,
                DialogResult = result,
                MinimumSize = new Size(88, 38),
                Padding = new Padding(UiSpacing.XLarge, 0, UiSpacing.XLarge, 0),
                Margin = new Padding(UiSpacing.Small - FocusRing.Room, 0, 0, 0),
                FitText = true
            };
            buttons.Controls.Add(button);
            return button;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetDarkTitleBar(Handle, Theme.Dark);
        }

        public static bool Confirm(IWin32Window owner, string title, string message, string action)
        {
            using var dialog = new ThemedDialog(title, message);
            var cancel = dialog.AddButton("Cancel", ButtonKind.Secondary, DialogResult.Cancel);
            dialog.AddButton(action, ButtonKind.Danger, DialogResult.OK);
            dialog.CancelButton = cancel;
            dialog.ActiveControl = cancel;
            return dialog.ShowDialog(owner) == DialogResult.OK;
        }

        public static void Inform(IWin32Window owner, string title, string message)
        {
            using var dialog = new ThemedDialog(title, message);
            var close = dialog.AddButton("Close", ButtonKind.Primary, DialogResult.OK);
            dialog.CancelButton = close;
            dialog.AcceptButton = close;
            dialog.ActiveControl = close;
            dialog.ShowDialog(owner);
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Two windows would each save their own copy of the texts on close, and the last one wins.
            using var single = new Mutex(true, @"Local\TYPR-single-instance", out bool first);
            if (!first)
            {
                NativeMethods.ShowRunningInstance();
                return;
            }
            // An unexpected error saves the texts and keeps TYPR open instead of losing the last edits.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                Theme.SaveSettings();
                MessageBox.Show($"Something went wrong, but your texts were saved.\n\n{e.Exception.Message}",
                    "TYPR - unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
