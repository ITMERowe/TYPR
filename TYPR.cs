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
    // Display scaling. Every size in TYPR is written in 100% (96 DPI) pixels and goes through S, except 1 px
    // hairlines (edges, dividers), which stay one device pixel like Windows' own. The process is system-DPI
    // aware, so on a monitor with another scale Windows stretches the window instead.
    // Program.Main sets Factor before any window exists; tests and the off-screen harness set it by reflection.
    internal static class Dpi
    {
        public static float Factor = 1f;

        public static int S(int px) => Scale(px, Factor);
        public static float S(float px) => px * Factor;

        internal static int Scale(int px, float factor) => (int)Math.Round(px * factor, MidpointRounding.AwayFromZero);
    }

    // CNTR's design tokens (CNTR docs/DESIGN.md), shared by every ITMERowe project: 4/8 spacing, three radii,
    // one type scale, one motion vocabulary. Use the token, never the literal.
    internal static class UiSpacing
    {
        public static readonly int XSmall = Dpi.S(4);   // --s1
        public static readonly int Small = Dpi.S(8);    // --s2
        public static readonly int Medium = Dpi.S(12);  // --s3
        public static readonly int Large = Dpi.S(16);   // --s4
        public static readonly int XLarge = Dpi.S(20);  // --s5
        // Not a step: a Label draws its text this far in from its left edge, so text that must line up
        // with a box edge (an icon tile, a field) sits this much less far in. Visible gaps stay on the scale.
        public static readonly int LabelInset = Dpi.S(3);
    }

    // Minimum heights, so every control reads as a rectangle with rounded corners (radius 6), never a pill.
    internal static class UiSize
    {
        public static readonly int Button = Dpi.S(40);  // outer height of a button, focus-ring room included
        public static readonly int Keycap = Dpi.S(32);  // a shortcut key in the sidebar
        public static readonly int Field = Dpi.S(30);   // a text or number box
        public static readonly int Tile = Dpi.S(26);    // icon tiles and icon-button hover squares: a keycap's face height
    }

    internal static class UiRadius
    {
        // CNTR's 10/14/20 scaled to TYPR's denser desktop parts (radius about a quarter of the height).
        public static readonly int Small = Dpi.S(6);   // --r-sm: buttons, inputs, tabs, keycaps, chips, icon tiles
        public static readonly int Large = Dpi.S(12);  // --r-lg: cards
        // Glyph-sized parts below --r-sm.
        public static readonly int XSmall = Dpi.S(4);  // a tab's 20px close button
    }

    internal static class UiFont
    {
        // Windows 11's UI face (Segoe UI on Windows 10). The type is TYPR's own; only colour, corners and motion follow CNTR.
        private static readonly FontFamily Regular = new FontFamily(FirstInstalled("Segoe UI Variable Text", "Segoe UI"));
        // Semibold, not Bold: bold Segoe is heavy next to the 400-weight text. Falls back to Bold when the face is missing.
        private static readonly bool HasSemibold = FirstInstalled("Segoe UI Semibold", "") == "Segoe UI Semibold";
        private static readonly FontFamily Strong = HasSemibold ? new FontFamily("Segoe UI Semibold") : Regular;
        private static readonly FontStyle StrongStyle = HasSemibold ? FontStyle.Regular : FontStyle.Bold;
        public static readonly Font Label = new Font(Strong, Px(9f), StrongStyle, GraphicsUnit.Pixel);        // --fs-xs semibold: chips, group titles, logo
        public static readonly Font Small = new Font(Regular, Px(9.75f), GraphicsUnit.Pixel);                 // --fs-sm: rows, tabs, status, dialog text, errors
        public static readonly Font SmallBold = new Font(Strong, Px(9.75f), StrongStyle, GraphicsUnit.Pixel); // --fs-sm semibold: values
        public static readonly Font Button = new Font(Strong, Px(9.75f), StrongStyle, GraphicsUnit.Pixel);    // --fs-sm semibold: buttons, keycaps
        public static readonly Font Heading = new Font(Strong, Px(12f), StrongStyle, GraphicsUnit.Pixel);     // --fs-lg semibold: dialog titles
        public static readonly Font Editor = new Font(FirstInstalled("Consolas", "Courier New"), Px(10.5f), GraphicsUnit.Pixel); // VS Code's Windows default, 14px
        public static readonly Font Icon = new Font(FirstInstalled("Segoe Fluent Icons", "Segoe MDL2 Assets"), Px(10f), GraphicsUnit.Pixel); // Windows' own icon font

        // Point sizes as pixels at 96 DPI times Dpi.Factor: the same size a point font gets on a scaled
        // screen, but set by Factor alone, so an off-screen render at any factor matches the screen.
        private static float Px(float points) => Dpi.S(points * 96f / 72f);

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
        public static bool Dark { get; private set; } = IsDark(Settings.Theme);

        public static void SetMode(string mode)
        {
            Settings.Theme = mode;
            Dark = IsDark(mode);
        }

        private static bool IsDark(string mode) => mode switch
        {
            "Dark" => true,
            "Light" => false,
            _ => DetectDark()
        };

        private static bool saveWarned;

        // False when nothing was written: saving is off this session, or the disk refused.
        public static bool SaveSettings()
        {
            if (!settingsWritable) return false;
            try
            {
                SettingsStore.Save(SettingsPath, Settings);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (!saveWarned)
                {
                    saveWarned = true;
                    ThemedDialog.Inform(Form.ActiveForm, "Settings could not be saved",
                        $"TYPR could not save your preferences. Changes will apply for this session only.\n\n{ex.Message}");
                }
                return false;
            }
        }

        private static UserSettings LoadSettings()
        {
            var settings = SettingsStore.Load(SettingsPath, out string? warning, out settingsWritable);
            // Runs while Theme is still initialising, so the themed dialog waits until the window is shown.
            startupWarning = warning;
            return settings;
        }

        private static string? startupWarning;

        public static void ShowStartupWarning(IWin32Window owner)
        {
            if (startupWarning == null) return;
            string message = startupWarning;
            startupWarning = null;
            ThemedDialog.Inform(owner, "Settings could not be loaded", message);
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
        // Watermelon neon #ff2e63 in both themes. Dark ink on it is 5.3:1 (white would be 3.6:1); as a shape it is 3.6:1 on white.
        public static Color Accent     => Rgb(0xff2e63);
        public static Color OnAccent   => Rgb(0x1f0710);
        // The accent as text: the same hue, darkened on light surfaces to 5.3:1.
        public static Color AccentText => Dark ? Rgb(0xff2e63) : Rgb(0xd4144a);
        public static Color Positive   => Dark ? Rgb(0x4ade80) : Rgb(0x116329);
        public static Color Negative   => Dark ? Rgb(0xff6b5e) : Rgb(0xc72c1c);
        public static Color OnNegative => Dark ? Rgb(0x1d0806) : Rgb(0xffffff);

        // The window's 1px DWM border: --line-strong over --bg, made opaque.
        public static Color WindowBorder => Blend(LineStrong, Bg);

        // Desktop hover (CNTR is touch-first and has none): a colour moved 12% toward --text.
        public static Color Hover(Color color) => Blend(Color.FromArgb(31, Text), color);

        // Windows' own close-button red, for the title bar's close on hover.
        public static Color CloseHover => Rgb(0xc42b1c);

        // Layered effects, each strength set in one place.
        public static Color Scrim => Color.FromArgb(Dark ? 120 : 64, 0, 0, 0);   // dims the editor behind the drawer
        public static Color Shadow => Color.FromArgb(Dark ? 110 : 48, 0, 0, 0);  // the drawer's soft edge
        public static Color Tile(Color tint, Color ground) => Blend(Color.FromArgb(Dark ? 46 : 30, tint), ground); // icon tiles
        public static Color LockedOn => Blend(Color.FromArgb(110, Text3), Surface2); // a locked switch's on fill

        // --text-3 at 190 is >= 3:1 on --surface-1 and --bg in both themes (3.28:1 at the lowest, light
        // --surface-1): the slider rail and the resting scroll thumb. 220 while the thumb is hovered or dragged.
        public const int RailAlpha = 190;
        public const int RailActiveAlpha = 220;

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
                if (File.Exists(path)) return Read(path);
                // A save cut off between its two renames leaves only the .bak behind.
                if (!File.Exists(path + ".bak")) return new UserSettings();
                var restored = Read(path + ".bak");
                warning = "TYPR could not find its preferences and restored the copy from before the last save.";
                return restored;
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
            if (settings.Theme != "System" && settings.Theme != "Light" && settings.Theme != "Dark")
                settings.Theme = "System";
            if (settings.WindowWidth < 560 || settings.WindowHeight < 640 ||
                settings.WindowWidth > 16384 || settings.WindowHeight > 16384)
                settings.WindowWidth = settings.WindowHeight = 0;
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
        // Window size when last closed; 0 = never saved, use the default size.
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
        // Files from before the setting existed load it as false (off).
        public bool ShowLineNumbers { get; set; }
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, ref RECT lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        public const uint PW_RENDERFULLCONTENT = 2;

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

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
                ? "Windows blocked the keys TYPR sent."
                : new System.ComponentModel.Win32Exception(error).Message;
            throw new InvalidOperationException($"Windows accepted {sent} of {inputs.Length} keyboard events. {detail}");
        }
    }

    // The theme switch as a circle around the toggle: the old look, laid over the rebuilt window, shrinks
    // into the button (to dark) or opens from it (to light). Ported from spc-dashboard's view transition.
    internal sealed class ThemeReveal : Form
    {
        private const int Duration = 520;
        private readonly Bitmap snapshot;
        private readonly Point origin;
        private readonly bool toDark;
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 10 };

        private ThemeReveal(Bitmap snapshot, Rectangle bounds, Point origin, bool toDark)
        {
            this.snapshot = snapshot;
            this.origin = origin;
            this.toDark = toDark;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            Bounds = bounds;
            DoubleBuffered = true;
            timer.Tick += (_, __) => Step();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        // Covers the window with its current look; call Play once the new theme is in place underneath.
        // Null means switch without animating.
        public static ThemeReveal? Cover(Form owner, Point origin, bool toDark)
        {
            if (!Motion.Enabled || !owner.Visible || owner.WindowState == FormWindowState.Minimized) return null;
            var bitmap = new Bitmap(owner.Width, owner.Height);
            bool captured;
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = g.GetHdc();
                captured = NativeMethods.PrintWindow(owner.Handle, hdc, NativeMethods.PW_RENDERFULLCONTENT);
                g.ReleaseHdc(hdc);
            }
            if (!captured)
            {
                bitmap.Dispose();
                return null;
            }

            var cover = new ThemeReveal(bitmap, owner.Bounds, origin, toDark);
            cover.Show(owner);
            NativeMethods.SetWindowAppearance(cover.Handle, Theme.WindowBorder);
            cover.Update();
            return cover;
        }

        public void Play()
        {
            // A moved or resized window no longer matches the snapshot.
            if (Owner != null)
            {
                Owner.LocationChanged += OwnerChanged;
                Owner.SizeChanged += OwnerChanged;
            }
            clock.Start();
            timer.Start();
        }

        private void OwnerChanged(object? sender, EventArgs e) => Close();

        private void Step()
        {
            double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / Duration);
            if (t >= 1)
            {
                Close();
                return;
            }

            // The CSS curves: cubic-bezier(0, 0.5, 0.3, 1) to dark, cubic-bezier(0.7, 0, 1, 0.5) to light.
            double eased = toDark ? Bezier(t, 0, 0.5, 0.3, 1) : Bezier(t, 0.7, 0, 1, 0.5);
            float radius = (float)(FarthestCorner(origin, ClientSize) * (toDark ? 1 - eased : eased));
            using var circle = new System.Drawing.Drawing2D.GraphicsPath();
            circle.AddEllipse(origin.X - radius, origin.Y - radius, radius * 2, radius * 2);
            var region = new Region(circle);
            // To light, the circle is the hole the new theme shows through.
            if (!toDark) region.Complement(ClientRectangle);
            Region? previous = Region;
            Region = region;
            previous?.Dispose();
        }

        internal static double FarthestCorner(Point p, Size size) =>
            Math.Sqrt(Math.Pow(Math.Max(p.X, size.Width - p.X), 2) + Math.Pow(Math.Max(p.Y, size.Height - p.Y), 2));

        // CSS cubic-bezier: bisect for the curve parameter at x = t, then return its y.
        internal static double Bezier(double t, double x1, double y1, double x2, double y2)
        {
            static double At(double u, double a, double b) =>
                3 * a * u * (1 - u) * (1 - u) + 3 * b * u * u * (1 - u) + u * u * u;
            double low = 0, high = 1;
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (At(mid, x1, x2) < t) low = mid;
                else high = mid;
            }
            return At((low + high) / 2, y1, y2);
        }

        protected override void OnPaint(PaintEventArgs e) => e.Graphics.DrawImageUnscaled(snapshot, 0, 0);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (Owner != null)
            {
                Owner.LocationChanged -= OwnerChanged;
                Owner.SizeChanged -= OwnerChanged;
            }
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                snapshot.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class AppIcon
    {
        public static readonly Icon Value = new Icon(typeof(AppIcon).Assembly.GetManifestResourceStream("TYPR.ico")!);
    }

    public class MainForm : Form, IMessageFilter
    {
        private static readonly int TitleBarHeight = Dpi.S(35);
        private static readonly int SidebarWidth = Dpi.S(232);
        private static readonly int TabHeight = Dpi.S(35);
        private static readonly int TabMinWidth = Dpi.S(80);
        private static readonly int TabMaxWidth = Dpi.S(160);

        private readonly EditorBox text = new EditorBox();
        private readonly ThinScrollBar editorScroll = new ThinScrollBar();
        private readonly NumberInput cpm = new NumberInput();
        private readonly NumberInput startDelay = new NumberInput();
        private readonly Slider jitter = new Slider();
        private readonly Label jitterValue = new Label();
        private readonly ToggleCheckBox pressEnter = new ToggleCheckBox();
        private readonly ToggleCheckBox lineNumbers = new ToggleCheckBox();
        private readonly LineNumberGutter lineGutter = new LineNumberGutter();
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
        private static readonly int CompactWidth = Dpi.S(780);
        private readonly TitleIconButton sidebarToggle = new TitleIconButton { Glyph = Glyph.Sidebar };
        private Control? sidebar;
        private TableLayoutPanel? contentGrid;
        private SidebarSheet? sidebarSheet;
        private bool sidebarOpen;
        private readonly System.Windows.Forms.Timer templateSaveTimer = new System.Windows.Forms.Timer { Interval = 400 };
        private readonly AddTextButton addTemplateButton = new AddTextButton();
        private readonly LinkLabel resetHotkeysLink = new LinkLabel();
        private Control? windowContent;
        private RoundedPanel? editorCard;
        private TabStripPanel templateTabs = null!;
        private CancellationTokenSource? cts;
        private readonly Random rng = new Random();
        private bool hotkeysRegistered;
        private bool loadingTemplate;
        private ThemeReveal? themeReveal;
        // Last non-minimized state, so maximize, minimize, close still reopens maximized.
        private bool lastMaximized;

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
        private static readonly int ResizeBorderWidth = Dpi.S(6);

        public MainForm()
        {
            Text = "TYPR";
            Icon = AppIcon.Value;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            RestoreWindowSize();
            BackColor = Theme.WindowBorder;
            Font = UiFont.Small;
            maximizeButton.Click += (_, __) => ToggleWindowState();
            Resize += (_, __) =>
            {
                bool maximized = WindowState == FormWindowState.Maximized;
                if (WindowState != FormWindowState.Minimized) lastMaximized = maximized;
                maximizeButton.IsMaximized = maximized;
                tips.SetToolTip(maximizeButton, maximized ? "Restore down" : "Maximize");
                ApplyResponsive();
            };

            themeToggleButton.AccessibleRole = AccessibleRole.PushButton;
            themeToggleButton.Click += (_, __) => ToggleTheme();
            sidebarToggle.AccessibleRole = AccessibleRole.PushButton;
            sidebarToggle.AccessibleName = "Show settings";
            sidebarToggle.Click += (_, __) => SetSidebarOpen(!sidebarOpen);
            sidebarToggle.Leave += (_, __) => CloseSidebarIfFocusLeft();
            tips.SetToolTip(sidebarToggle, "Settings (Ctrl+B)");

            cpm.Minimum = 30;
            cpm.Maximum = 20000;
            cpm.Value = 1200;
            cpm.PageStep = 100;
            cpm.Unit = "cpm";
            cpm.FieldName = "Typing speed, characters per minute";
            cpm.Clamped += (_, __) => ShowStatus("Speed is 30–20,000 cpm.", warning: true);
            startDelay.Minimum = 0;
            startDelay.Maximum = 30;
            startDelay.Value = 3;
            startDelay.PageStep = 5;
            startDelay.Unit = "s";
            startDelay.FieldName = "Start delay, seconds";
            startDelay.Clamped += (_, __) => ShowStatus("Start delay is 0–30 s.", warning: true);
            jitter.Minimum = 0;
            jitter.Maximum = 80;
            jitter.Value = 5;
            jitter.AccessibleName = "Humanize pacing, percent";
            jitter.ValueChanged += (_, __) => jitterValue.Text = "±" + jitter.Value + "%";
            jitterValue.Text = "±" + jitter.Value + "%";
            cpm.ValueChanged += (_, __) => UpdateCharacterCount(text.Text);

            text.Multiline = true;
            text.MaxLength = int.MaxValue; // WinForms stops typing and cuts pastes at 32,767 characters otherwise
            text.AcceptsReturn = true;
            text.AcceptsTab = true;
            text.ScrollBars = ScrollBars.None;
            editorScroll.Target = text;
            lineGutter.Target = text;
            lineGutter.Visible = Theme.Settings.ShowLineNumbers;
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
                // Copied once per keystroke; ActiveTemplate holds it at once, so the crash handler can save it.
                string current = text.Text;
                UpdateCharacterCount(current);
                if (loadingTemplate) return;
                ActiveTemplate.Text = current;
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
            // Purely visual, so it stays live during a run; it never touches the text or the selection.
            lineNumbers.AccessibleName = "Line numbers";
            lineNumbers.SetInitial(Theme.Settings.ShowLineNumbers);
            lineNumbers.CheckedChanged += (_, __) =>
            {
                Theme.Settings.ShowLineNumbers = lineNumbers.Checked;
                lineGutter.Visible = lineNumbers.Checked;
                Theme.SaveSettings();
            };
            tips.SetToolTip(lineNumbers, "Line numbers (Ctrl+Shift+L)");

            start.Text = "Start typing";
            start.Click += StartButton_Click;

            stop.Text = "Stop";
            stop.Enabled = false;
            stop.Click += (_, __) => StopTyping();

            RefreshHotkeyLabels();
            startHotkey.Click += (_, __) => ChangeHotkey(true);
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
            // Two lines (the footer's 40px), so a long message wraps at narrow widths instead of being cut;
            // a short one stays one line, centred.
            status.Height = UiFont.Small.Height * 2 + Dpi.S(4);
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Font = UiFont.Small;
            // 8px of clear space after the status dot.
            status.Margin = new Padding(UiSpacing.Small - UiSpacing.LabelInset, 0, 0, 0);
            status.LiveSetting = System.Windows.Forms.Automation.AutomationLiveSetting.Polite;

            characterCount.Anchor = AnchorStyles.None;
            characterCount.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            // Match the action buttons' visible face (their height minus the focus-ring room on each side).
            characterCount.MinimumSize = new Size(0, UiSize.Button - FocusRing.Room * 2);
            ShowTemplateText(ActiveTemplate.Text);

            windowContent = BuildWindowContent();
            Controls.Add(windowContent);
            ApplyResponsive();
            // Stop any in-flight typing before the window (and its controls) go away, so
            // SendInput doesn't keep firing into whatever window has focus after TYPR closes.
            FormClosing += (_, __) =>
            {
                cts?.Cancel();
                RememberWindowSize();
            };
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

        // current is text.Text, passed in so a keystroke copies the editor's text only once.
        private void UpdateCharacterCount(string current)
        {
            UpdateCharacterChip();
            if (cts != null) return;
            bool blank = string.IsNullOrWhiteSpace(current);
            start.Enabled = !blank;
            // U9: say why Start is off instead of a green "Ready".
            if (blank) ShowStatus(EmptyHint, announce: false);
            else if (status.Text == EmptyHint) ShowStatus("Ready", announce: false);
        }

        // Narrow windows and runs drop the word "characters" so the status keeps the room; the tooltip has it all.
        private void UpdateCharacterChip()
        {
            int count = text.TextLength;
            characterCount.Text = CharacterCountLabel(count, cpm.Value, compact: cts != null || ClientSize.Width < CompactWidth);
            tips.SetToolTip(characterCount, CharacterCountLabel(count, cpm.Value, compact: false));
        }

        // "6,700 characters · ≈6 min"; compact "6,700 · ≈6 min" (the unit stays when no estimate follows).
        internal static string CharacterCountLabel(int count, decimal cpm, bool compact)
        {
            string label = count.ToString("N0", CultureInfo.InvariantCulture);
            if (count == 0 || cpm <= 0) compact = false;
            if (!compact) label += count == 1 ? " character" : " characters";
            if (count > 0 && cpm > 0)
            {
                double minutes = count / (double)cpm;
                label += minutes < 1 ? $" · ≈{Math.Max(1, (int)Math.Round(minutes * 60))} s" : $" · ≈{Math.Round(minutes):N0} min";
            }
            return label;
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
                Margin = Padding.Empty,
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
            sidebar.Leave += (_, __) => CloseSidebarIfFocusLeft();
            content.Controls.Add(sidebar, 0, 0);
            content.Controls.Add(BuildEditorGroup(), 1, 0);
            return content;
        }

        // CNTR's Settings pattern: a --fs-xs bold --text-2 title over one --surface-1 card of rows.
        private Control BuildSidebar()
        {
            cpm.ApplyTheme();
            startDelay.ApplyTheme();
            jitterValue.AutoSize = true;
            jitterValue.Font = UiFont.SmallBold;
            jitterValue.ForeColor = Theme.Text;
            jitterValue.Anchor = AnchorStyles.Right;
            jitterValue.Margin = Padding.Empty;
            // Reads as a link: --accent (6.1:1 on dark --bg, 7.9:1 on light), underlined on hover.
            resetHotkeysLink.LinkColor = Theme.AccentText;
            resetHotkeysLink.ActiveLinkColor = Theme.AccentText;
            resetHotkeysLink.VisitedLinkColor = Theme.AccentText;
            resetHotkeysLink.DisabledLinkColor = Theme.Text3;
            // Its text starts at the card's inner edge, over the icon tiles.
            resetHotkeysLink.Margin = new Padding(UiSpacing.Medium - UiSpacing.LabelInset, UiSpacing.Small, 0, 0);

            var typing = CardRows(
                // Wider than the delay box: room for "20000" and the "cpm" unit inside it.
                SettingRow(Glyph.Speed, Theme.Text, "Speed", Fixed(cpm, Dpi.S(96)), "Characters per minute"),
                SettingRow(Glyph.Stopwatch, Theme.Text, "Start delay", Fixed(startDelay, Dpi.S(68)), "Seconds to focus the target window"),
                SliderRow(Glyph.Pulse, "Humanize pacing", jitter, jitterValue),
                SwitchRow(Glyph.Enter, "End with Enter", pressEnter, "Press Enter after the last character"));
            // Its own group: Typing is locked during a run, this switch is not.
            var editor = CardRows(
                SwitchRow(Glyph.LineNumbers, "Line numbers", lineNumbers, "Show line numbers left of the text. Ctrl+Shift+L"));
            var shortcuts = CardRows(
                SettingRow(Glyph.Play, Theme.Positive, "Start", startHotkey),
                SettingRow(Glyph.Stop, Theme.Negative, "Stop", stopHotkey));

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                // Top as the editor column's, so the first group title shares the tab labels' baseline.
                Padding = new Padding(UiSpacing.XSmall, UiSpacing.Small, UiSpacing.Medium, UiSpacing.Medium)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddGroup(layout, "Typing", typing, first: true);
            AddGroup(layout, "Editor", editor);
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
                ForeColor = Theme.Text2,
                // Text at the card's inner edge, in line with the icon tiles below.
                Margin = new Padding(UiSpacing.Medium - UiSpacing.LabelInset, first ? UiSpacing.Small : UiSpacing.Medium, 0, UiSpacing.XSmall)
            });
            layout.Controls.Add(card);
        }

        private static Control Fixed(Control control, int width)
        {
            control.Width = width;
            control.Height = UiSize.Field;
            control.Anchor = AnchorStyles.Right;
            // A field has no focus-ring room, so it stops Room short, where the keycap and switch faces end.
            control.Margin = new Padding(0, 0, FocusRing.Room, 0);
            return control;
        }

        // One --surface-1 card (--line border, --r-lg) holding rows split by --line dividers.
        // The right padding leaves out FocusRing.Room: keycaps and the switch keep that room around their
        // face, so every visible right edge lands --s3 inside the card; the rest stops Room short.
        private static Control CardRows(params Control[] rows)
        {
            // Rows are 40 high around a 26 px tile (7 px of air), so 5 px of card padding puts the tile 12 px
            // from the top and bottom edges, the same as from the left and right edges.
            int vertical = Dpi.S(5);
            var card = new RoundedPanel
            {
                Dock = DockStyle.Top,
                BackColor = Theme.Surface1,
                BorderColor = Theme.Line,
                CornerRadius = UiRadius.Large,
                Padding = new Padding(UiSpacing.Medium, vertical, UiSpacing.Medium - FocusRing.Room, vertical),
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
            int height = vertical * 2;
            int row = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0)
                {
                    stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    stack.Controls.Add(new LineDivider { Margin = new Padding(0, 0, FocusRing.Room, 0) }, 0, row++);
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
            int height = Dpi.S(40);
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
                // The slider gets 26px: room for the hovered thumb's 2px focus ring (r 9 + 4 each way).
                Dock = DockStyle.Fill,
                Height = Dpi.S(64),
                Tag = Dpi.S(64),
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(38)));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.Controls.Add(new RowIcon { Glyph = glyph, Tint = Theme.Text }, 0, 0);
            row.Controls.Add(RowLabel(title), 1, 0);
            row.Controls.Add(value, 2, 0);
            // Slider.Pad (12px) puts the rail's start under the icon's centre; the margin keeps its end as far in.
            slider.Dock = DockStyle.Fill;
            slider.Margin = new Padding(0, 0, FocusRing.Room, 0);
            row.Controls.Add(slider, 0, 1);
            row.SetColumnSpan(slider, 3);
            return row;
        }

        // CNTR: "the whole row is the label", so a click anywhere on the row flips the switch.
        private static Control SwitchRow(string glyph, string title, ToggleCheckBox toggle, string description)
        {
            // The 44x26 track plus 3px each way, so its 2px focus ring isn't clipped.
            toggle.Size = new Size(Dpi.S(50), Dpi.S(32));
            toggle.Anchor = AnchorStyles.Right;
            toggle.Margin = Padding.Empty;
            var row = SettingRow(glyph, Theme.Text, title, toggle, description);
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
                // The footer buttons keep FocusRing.Room around their face, so the right and bottom edges
                // give it back: faces end --s4 from the window's right edge (with the cards) and --s3 from
                // its bottom; the tab strip and editor card take the Room back as a right margin.
                Padding = new Padding(UiSpacing.XSmall, UiSpacing.Small, UiSpacing.Large - FocusRing.Room, UiSpacing.Medium - FocusRing.Room)
            };
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, TabHeight));
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, UiSpacing.Medium - FocusRing.Room + UiSize.Button));
            group.Controls.Add(BuildTabStrip(), 0, 0);
            group.Controls.Add(BuildEditorCard(), 0, 1);
            group.Controls.Add(BuildEditorActions(), 0, 2);
            return group;
        }

        // One card for the window's life, recoloured on a theme switch: the edit control sends its change
        // notices to the window it was created in, so moving it to a new card silenced TextChanged (and with
        // it the tab's autosave, the character count and the line numbers).
        private Control BuildEditorCard()
        {
            if (editorCard == null)
            {
                editorCard = new RoundedPanel
                {
                    Dock = DockStyle.Fill,
                    CornerRadius = UiRadius.Large,
                    SquareTop = true,
                    Padding = new Padding(UiSpacing.Medium, UiSpacing.Small, UiSpacing.XSmall, UiSpacing.Small),
                    Margin = new Padding(0, 0, FocusRing.Room, 0)
                };
                editorCard.Controls.Add(text);
                // Added after the Fill editor, so they dock first and take the right and left edges.
                editorScroll.Dock = DockStyle.Right;
                editorCard.Controls.Add(editorScroll);
                lineGutter.Dock = DockStyle.Left;
                editorCard.Controls.Add(lineGutter);
            }
            editorCard.BackColor = Theme.Surface1;
            editorCard.BorderColor = Theme.Line;
            text.BackColor = Theme.Surface1;
            text.ForeColor = Theme.Text;
            return editorCard;
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
                // The dot starts at the editor card's inner edge, with the tab icons; the faces start --s3 below the card.
                Padding = new Padding(UiSpacing.Medium, UiSpacing.Medium - FocusRing.Room, 0, 0)
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, StatusDot.Diameter));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            statusDot.Dock = DockStyle.Fill;
            statusDot.Margin = Padding.Empty;
            status.ForeColor = statusWarning ? Theme.Negative : Theme.Text2;
            ConfigureActionButton(stop, Dpi.S(80));
            ConfigureActionButton(start, Dpi.S(120));
            // --s2 between the visible faces: the chip has no focus-ring room, the buttons Room each.
            stop.Margin = new Padding(UiSpacing.Small - FocusRing.Room, 0, 0, 0);
            start.Margin = new Padding(UiSpacing.Small - FocusRing.Room * 2, 0, 0, 0);

            actions.Controls.Add(statusDot, 0, 0);
            actions.Controls.Add(status, 1, 0);
            actions.Controls.Add(characterCount, 2, 0);
            actions.Controls.Add(stop, 3, 0);
            actions.Controls.Add(start, 4, 0);
            return actions;
        }

        private static void ConfigureActionButton(RoundedButton button, int minWidth)
        {
            button.MinimumSize = new Size(minWidth, UiSize.Button);
            button.Padding = new Padding(UiSpacing.Large, 0, UiSpacing.Large, 0);
            button.FitText = true;
            button.Anchor = AnchorStyles.None;
        }

        // The tabs scroll sideways inside their own strip (no native scroll bar over them); the + sits
        // right after them, outside the strip, so it stays in view when they overflow.
        private Control BuildTabStrip()
        {
            var row = new TabStripPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Theme.Bg,
                // Ends with the editor card (see BuildEditorGroup).
                Margin = new Padding(0, 0, FocusRing.Room, 0),
                Padding = Padding.Empty
            };
            templateTabs = new TabStripPanel
            {
                Height = TabHeight,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.Controls.Add(templateTabs);
            row.Resize += (_, __) => FitTemplateTabs();
            addTemplateButton.AccessibleName = "Add text";
            addTemplateButton.AccessibleRole = AccessibleRole.PushButton;
            addTemplateButton.Size = new Size(TabHeight, TabHeight);
            addTemplateButton.Margin = Padding.Empty;
            addTemplateButton.Cursor = Cursors.Hand;
            row.Controls.Add(addTemplateButton);
            RefreshTemplateTabs();
            return row;
        }

        private void RefreshTemplateTabs()
        {
            if (templateTabs == null) return;
            templateTabs.SuspendLayout();
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
                    // The last tab can't be deleted, so it shows no x.
                    CanClose = Theme.Settings.TextTemplates.Count > 1,
                    // Roving focus: Tab lands on the active tab only; the arrow keys move along the strip.
                    TabStop = i == Theme.Settings.ActiveTextTemplate,
                    Font = UiFont.Small,
                    Margin = Padding.Empty,
                    Cursor = Cursors.Hand,
                    AccessibleRole = AccessibleRole.PageTab,
                    AccessibleName = Theme.Settings.TextTemplates[i].Name,
                    AccessibleDescription = "Left and Right arrows switch tabs. F2 or double-click renames. Delete closes. Drag to reorder.",
                    Tag = i
                };
                tips.SetToolTip(tab, Theme.Settings.TextTemplates[i].Name);
                tab.Click += (_, __) => { if (!dragMoved) SelectTextTemplate(templateIndex); };
                tab.GotFocus += (_, __) => templateTabs.ScrollIntoView(tab);
                tab.KeyDown += (_, e) =>
                {
                    int count = Theme.Settings.TextTemplates.Count;
                    int target = e.Modifiers != Keys.None ? -1 : e.KeyCode switch
                    {
                        Keys.Left => (templateIndex - 1 + count) % count,
                        Keys.Right => (templateIndex + 1) % count,
                        Keys.Home => 0,
                        Keys.End => count - 1,
                        _ => -1
                    };
                    if (target < 0) return;
                    e.Handled = true;
                    // Deferred: selecting rebuilds the tabs, this one included.
                    BeginInvoke(new Action(() => SelectTextTemplate(target, focusEditor: false)));
                };
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
                // The window content fills the client area, so the sheet starts right under the title bar.
                sidebarSheet.Bounds = new Rectangle(0, TitleBarHeight, ClientSize.Width, Math.Max(0, ClientSize.Height - TitleBarHeight));
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
            UpdateCharacterChip();
        }

        private void SetSidebarOpen(bool open)
        {
            if (open == sidebarOpen || (open && ClientSize.Width >= CompactWidth)) return;
            bool focusInside = sidebar?.ContainsFocus == true;
            sidebarOpen = open;
            ApplyResponsive();
            if (open) sidebar?.SelectNextControl(null, true, true, true, false);
            else if (focusInside) sidebarToggle.Focus();
        }

        // The drawer covers the editor, so focus leaving it (Tab past its end, Shift+Tab out of the title
        // bar) closes it: nothing under the sheet may hold focus. Focus on the toggle keeps it open, so the
        // toggle's own click can close it.
        private void CloseSidebarIfFocusLeft()
        {
            if (!sidebarOpen || !IsHandleCreated) return;
            // Deferred: Leave runs before the next control takes focus.
            BeginInvoke(new Action(() =>
            {
                if (sidebarOpen && ActiveControl != sidebarToggle && sidebar?.Contains(ActiveControl) != true)
                    SetSidebarOpen(false);
            }));
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
            int count = templateTabs.Controls.Count;
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
                // The drawer covers the tabs and the editor that this moves focus to.
                SetSidebarOpen(false);
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
            if (keyData == (Keys.Control | Keys.Shift | Keys.L))
            {
                lineNumbers.Toggle();
                return true;
            }
            if (keyData == Keys.Escape && text.Focused)
            {
                SelectNextControl(text, true, true, true, true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Tabs share the row's width (TabMinWidth..TabMaxWidth) so many tabs stay on one row; only
        // past the minimum does the strip scroll, and the active tab is kept in view.
        private void FitTemplateTabs()
        {
            int count = templateTabs.Controls.Count;
            if (count == 0 || templateTabs.Parent == null) return;
            int available = Math.Max(0, templateTabs.Parent.ClientSize.Width - addTemplateButton.Width);
            int width = Math.Clamp(available / count, TabMinWidth, TabMaxWidth);
            foreach (Control control in templateTabs.Controls)
                if (control.Width != width)
                    control.Width = width;
            templateTabs.Width = Math.Min(count * width, available);
            templateTabs.ScrollIntoView(Theme.Settings.ActiveTextTemplate);
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

        // focusEditor false keeps focus on the strip (arrow keys), on the new active tab.
        private void SelectTextTemplate(int index, bool focusEditor = true)
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
            ShowTemplateText(ActiveTemplate.Text);
            RefreshTemplateTabs();
            Theme.SaveSettings();
            if (focusEditor) text.Focus();
            else templateTabs.Controls.OfType<EditorTabPanel>().FirstOrDefault(t => t.IsActive)?.Focus();
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

            if (!string.IsNullOrWhiteSpace(Theme.Settings.TextTemplates[index].Text) &&
                !ThemedDialog.Confirm(this, "Delete text?",
                    $"\u201c{Theme.Settings.TextTemplates[index].Name}\u201d has saved text. Deleting it can't be undone.",
                    "Delete"))
                return;

            int activeIndex = Theme.Settings.ActiveTextTemplate;
            Theme.Settings.TextTemplates.RemoveAt(index);

            if (index < activeIndex)
                Theme.Settings.ActiveTextTemplate = activeIndex - 1;
            else if (index == activeIndex)
                Theme.Settings.ActiveTextTemplate = Math.Min(index, Theme.Settings.TextTemplates.Count - 1);

            if (index == activeIndex) ShowTemplateText(ActiveTemplate.Text);

            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        private void RenameTextTemplate(int index)
        {
            if (cts != null || index < 0 || index >= Theme.Settings.TextTemplates.Count)
                return;

            // Its box starts where the dialog's text does.
            var nameField = new TextField { Width = Dpi.S(320), Margin = new Padding(UiSpacing.LabelInset, 0, 0, 0) };
            var nameInput = nameField.Input;
            nameInput.Text = Theme.Settings.TextTemplates[index].Name;
            nameInput.MaxLength = 80;
            nameInput.Font = UiFont.Small;
            nameInput.AccessibleName = "Text name";
            var nameError = new Label
            {
                AutoSize = true,
                Font = UiFont.Small,
                ForeColor = Theme.Negative,
                Margin = new Padding(0, UiSpacing.XSmall, 0, 0),
                Visible = false,
                LiveSetting = System.Windows.Forms.Automation.AutomationLiveSetting.Polite
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
                    if (nameError.IsHandleCreated) nameError.AccessibilityObject.RaiseLiveRegionChanged();
                    nameInput.Focus();
                    return;
                }
                dialog.DialogResult = DialogResult.OK;
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
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
                Name = NextTextName(Theme.Settings.TextTemplates)
            };
            Theme.Settings.TextTemplates.Add(template);
            Theme.Settings.ActiveTextTemplate = Theme.Settings.TextTemplates.Count - 1;
            ShowTemplateText(string.Empty);
            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        // "Text N" from Count + 1 up, skipping names in use, so deleting a tab can't lead to two with one name.
        internal static string NextTextName(List<TextTemplate> templates)
        {
            for (int n = templates.Count + 1; ; n++)
            {
                string name = $"Text {n}";
                if (!templates.Exists(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))) return name;
            }
        }

        // Puts a tab's text in the editor without counting it as an edit.
        private void ShowTemplateText(string value)
        {
            loadingTemplate = true;
            text.Text = value;
            loadingTemplate = false;
            UpdateCharacterCount(text.Text);
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
            // The three caption buttons, then the theme and sidebar toggles.
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, WindowCaptionButton.DefaultWidth * 3 + TitleBarHeight * 2));
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

        // The size TYPR was closed at comes back next launch; a size that no longer fits the screen is shrunk,
        // the minimum too (a large display scale on a small screen).
        private void RestoreWindowSize()
        {
            var saved = Theme.Settings;
            // CenterScreen opens on the monitor under the cursor, so fit that one.
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            MinimumSize = new Size(Math.Min(Dpi.S(560), area.Width), Math.Min(Dpi.S(640), area.Height));
            // Saved in 100% pixels (see RememberWindowSize).
            var size = saved.WindowWidth > 0 ? new Size(saved.WindowWidth, saved.WindowHeight) : new Size(1020, 700);
            ClientSize = new Size(Math.Min(Dpi.S(size.Width), area.Width), Math.Min(Dpi.S(size.Height), area.Height));
            // Maximizing before the handle exists makes RestoreBounds the frame-padded size, which grew 14px a launch.
            if (saved.WindowMaximized) Load += (_, __) => WindowState = FormWindowState.Maximized;
        }

        // FormClosed saves the settings right after this.
        private void RememberWindowSize()
        {
            var normal = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            // In 100% pixels, so a new display scale doesn't shrink or grow the window against its content.
            Theme.Settings.WindowWidth = Dpi.Scale(normal.Width, 1 / Dpi.Factor);
            Theme.Settings.WindowHeight = Dpi.Scale(normal.Height, 1 / Dpi.Factor);
            Theme.Settings.WindowMaximized = lastMaximized;
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
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
                // Hotkeys reach this window even while a TYPR dialog is up: Stop always works, Start waits.
                // StartButton_Click is async void, so a failure reaches ThreadException instead of a dropped Task.
                if (id == HOTKEY_STOP) StopTyping();
                else if (id == HOTKEY_START && !ThemedDialog.AnyOpen) StartButton_Click(this, EventArgs.Empty);
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
            button.MinimumSize = new Size(Dpi.S(40), UiSize.Keycap);
            // Capped so a long combo (Ctrl+Alt+Shift+Win+F12) can't push the row label out; the
            // tooltip shows it whole.
            button.MaximumSize = new Size(Dpi.S(110), UiSize.Keycap);
            button.Padding = new Padding(UiSpacing.Small, 0, UiSpacing.Small, 0);
            button.FitText = true;
            button.Margin = Padding.Empty;
        }

        private void UpdateHotkeyButton(RoundedButton button, string name, int modifiers, int key)
        {
            button.Text = FormatHotkey(modifiers, key);
            button.AccessibleName = $"{name} shortcut: {button.Text}. Click to change.";
            tips.SetToolTip(button, $"{button.Text} — click to change");
        }

        // The keycaps and the Start/Stop buttons' names follow the saved shortcuts.
        private void RefreshHotkeyLabels()
        {
            UpdateHotkeyButton(startHotkey, "Start typing", Theme.Settings.StartModifiers, Theme.Settings.StartKey);
            UpdateHotkeyButton(stopHotkey, "Stop", Theme.Settings.StopModifiers, Theme.Settings.StopKey);
            start.AccessibleName = $"Start typing ({startHotkey.Text})";
            stop.AccessibleName = $"Stop typing ({stopHotkey.Text})";
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
            Theme.ShowStartupWarning(this);
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
            if (IsDisposed || !IsHandleCreated) return false;

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
            if (!hotkeysRegistered || IsDisposed || !IsHandleCreated) return;

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
            // Names exactly the keys HotkeyModifiers.CanStandAlone lets stand alone.
            const string Hint = "Press the new shortcut: any key with Ctrl, Alt, Shift or Win, or F1\u2013F24, Pause or Scroll Lock alone. Esc cancels.";
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
                MaximumSize = new Size(Dpi.S(360), 0),
                MinimumSize = new Size(Dpi.S(320), 0),
                Font = UiFont.Small,
                ForeColor = Theme.Text2,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                LiveSetting = System.Windows.Forms.Automation.AutomationLiveSetting.Polite
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
                if (instruction.IsHandleCreated) instruction.AccessibilityObject.RaiseLiveRegionChanged();
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

            void SetShortcut(int key, int modifiers)
            {
                if (isStart)
                {
                    Theme.Settings.StartKey = key;
                    Theme.Settings.StartModifiers = modifiers;
                }
                else
                {
                    Theme.Settings.StopKey = key;
                    Theme.Settings.StopModifiers = modifiers;
                }
            }
            int oldKey = isStart ? Theme.Settings.StartKey : Theme.Settings.StopKey;
            int oldModifiers = isStart ? Theme.Settings.StartModifiers : Theme.Settings.StopModifiers;
            SetShortcut(newKey, newModifiers);

            if (!TryRegisterHotKeys())
            {
                SetShortcut(oldKey, oldModifiers);
                TryRegisterHotKeys();
                ThemedDialog.Inform(this, "Shortcut unavailable",
                    $"Windows could not register {FormatHotkey(newModifiers, newKey)}. Another app may already use it" +
                    ((newModifiers & HotkeyModifiers.Win) != 0 ? ", and Windows keeps most Win shortcuts for itself." : "."));
                return;
            }

            RefreshHotkeyLabels();
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

            RefreshHotkeyLabels();
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
            themeReveal?.Close();
            // The circle centres on the pointer when it is on the button, else (keyboard) on the button.
            Rectangle button = themeToggleButton.RectangleToScreen(themeToggleButton.ClientRectangle);
            Point origin = button.Contains(Cursor.Position)
                ? Cursor.Position
                : new Point(button.X + button.Width / 2, button.Y + button.Height / 2);
            themeReveal = ThemeReveal.Cover(this, PointToClient(origin), toDark: !Theme.Dark);
            Theme.SetMode(Theme.Dark ? "Light" : "Dark");
            RebuildWindowContent();
            Update();
            themeReveal?.Play();
            Theme.SaveSettings();
            themeToggleButton.Focus();
        }

        // Rebuilds every surface from the current theme. The long-lived controls (editor, inputs,
        // buttons) move into the new layout, so text, values and focus state survive.
        private void RebuildWindowContent()
        {
            Control[] kept =
            {
                editorCard!, cpm, startDelay, jitter, jitterValue, pressEnter, lineNumbers, start, stop, startHotkey, stopHotkey,
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

        // ---------- Typing engine ----------

        private async Task StartTypingAsync()
        {
            if (cts != null) return;
            cpm.Commit();
            startDelay.Commit();
            templateSaveTimer.Stop();
            SaveActiveTemplate();
            Theme.SaveSettings();
            string payload = ActiveTemplate.Text;
            if (string.IsNullOrWhiteSpace(payload)) { ShowStatus(EmptyHint, warning: true); return; }
            string cleaned = WithoutControlChars(payload);
            int controls = payload.Length - cleaned.Length;
            if (controls > 0)
            {
                // Backspace, form feed and the like would delete or change text in the target.
                if (!ThemedDialog.Confirm(this, $"Remove {controls:N0} control character{(controls == 1 ? "" : "s")}?",
                        "This text has hidden characters (like backspace) that would delete or change text in the target window. " +
                        "TYPR removes them from this tab, keeps line breaks and tabs, then starts typing.",
                        "Remove and start"))
                    return;
                payload = cleaned;
                text.Text = cleaned;
                templateSaveTimer.Stop();
                SaveActiveTemplate();
                Theme.SaveSettings();
                if (string.IsNullOrWhiteSpace(payload)) { ShowStatus(EmptyHint, warning: true); return; }
            }

            cts = new CancellationTokenSource();
            SetRunning(true);
            try
            {
                int delay = (int)startDelay.Value;
                for (int i = delay; i > 0; i--)
                {
                    // The changing part first, so a narrow footer still shows it.
                    ShowStatus($"{i} \u00b7 switch to the target window");
                    await Task.Delay(1000, cts.Token);
                }
                // A start shortcut such as Ctrl+F8 may still be held; typed Enter/Tab would combine with it.
                for (int waited = 0; NativeMethods.AnyModifierDown() && waited < 2000; waited += 20)
                {
                    ShowStatus("Release Ctrl, Alt, Shift and Win to start.");
                    await Task.Delay(20, cts.Token);
                }
                if (NativeMethods.AnyModifierDown())
                {
                    ShowStatus("Ctrl, Alt, Shift or Win is still held — release it and start again.", warning: true);
                    return;
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
                        ShowStatus($"{i + 1:N0} / {payload.Length:N0} \u00b7 {stopKey} stops", announce: false);
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

        // The text minus control characters, keeping line breaks and tabs (typed as Enter and Tab).
        internal static string WithoutControlChars(string value) =>
            string.Concat(value.Where(c => !char.IsControl(c) || c is '\r' or '\n' or '\t'));

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
            UpdateCharacterChip();
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
        public static readonly int Room = Dpi.S(3);

        public static void Draw(Graphics g, Rectangle bounds, int radius)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var ring = bounds;
            // Half the pen in, so the whole ring stays inside bounds.
            ring.Inflate(-Dpi.S(1), -Dpi.S(1));
            ring.Width--;
            ring.Height--;
            using var path = RoundedPanel.CreatePath(ring, radius);
            using var pen = new Pen(Theme.Text, Dpi.S(2f));
            g.DrawPath(pen, path);
        }

        // A ring drawn Room px outside a shape rounds its corners by the same amount.
        public static int Radius(int shapeRadius) => shapeRadius + Room;
    }

    // Screen-reader face of an owner-drawn control: Invoke (DoDefaultAction) presses it like its native
    // counterpart, and its checked/selected state and value are read out.
    internal sealed class OwnerDrawnAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly string? action;
        private readonly Action? press;
        private readonly Func<AccessibleStates>? state;
        private readonly Func<string>? read;

        public OwnerDrawnAccessibleObject(Control owner, string? action, Action? press,
            Func<AccessibleStates>? state = null, Func<string>? read = null) : base(owner)
        {
            this.action = action;
            this.press = press;
            this.state = state;
            this.read = read;
        }

        public override string? DefaultAction => action ?? base.DefaultAction;
        public override AccessibleStates State => base.State | (state?.Invoke() ?? AccessibleStates.None);
        public override string? Value => read != null ? read() : base.Value;

        public override void DoDefaultAction()
        {
            if (press != null) press();
            else base.DoDefaultAction();
        }
    }

    // A row that never shows a native scroll bar: children past the right edge scroll sideways with the
    // mouse wheel or ScrollIntoView. Children are laid out at -ScrollX through DisplayRectangle.
    internal sealed class TabStripPanel : FlowLayoutPanel
    {
        private int scrollX;

        public TabStripPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        public int ScrollX
        {
            get => scrollX;
            set
            {
                int content = 0;
                foreach (Control child in Controls) content += child.Width;
                int next = Math.Clamp(value, 0, Math.Max(0, content - ClientSize.Width));
                if (next == scrollX) return;
                scrollX = next;
                PerformLayout();
            }
        }

        public override Rectangle DisplayRectangle
        {
            get
            {
                var display = base.DisplayRectangle;
                display.X -= scrollX;
                display.Width += scrollX;
                return display;
            }
        }

        // Children are equal-width tabs with no margins, so child n starts at n widths. Also re-clamps
        // ScrollX after a resize or a closed tab.
        public void ScrollIntoView(int index)
        {
            if (Controls.Count == 0) return;
            int width = Controls[0].Width;
            int left = Math.Clamp(index, 0, Controls.Count - 1) * width;
            ScrollX = Math.Min(left, Math.Max(scrollX, left + width - ClientSize.Width));
        }

        public void ScrollIntoView(Control child) => ScrollIntoView(Controls.GetChildIndex(child, false));

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // One tab per notch; wheel down moves toward the later tabs.
            if (Controls.Count > 0) ScrollX -= Math.Sign(e.Delta) * Controls[0].Width;
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
            base.OnMouseWheel(e);
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

    // The narrow-window sidebar as a drawer: the sidebar on --bg at the left, the editor behind it dimmed,
    // and a soft shadow off the drawer's edge instead of a hard line. WinForms children can't be
    // translucent, so the sheet covers the work area and paints a snapshot of it; a click on the
    // dimmed part closes the drawer.
    internal sealed class SidebarSheet : Control
    {
        private static readonly int ShadowWidth = Dpi.S(20);
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
            using (var dim = new SolidBrush(Theme.Scrim))
                g.FillRectangle(dim, edge, 0, Width - edge, Height);
            var shadow = new Rectangle(edge, 0, ShadowWidth, Height);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(shadow,
                Theme.Shadow, Color.FromArgb(0, 0, 0, 0),
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
        public const string LineNumbers = "\uE8FD"; // BulletedList: markers down the left of lines
        public const string Play = "\uE768";
        public const string Stop = "\uE71A";
        public const string Sidebar = "\uE90C";
    }

    // A UiSize.Tile rounded tile tinted with its colour at low strength, the glyph in full colour.
    internal sealed class RowIcon : Control
    {
        public string Glyph { get; set; } = "";
        public Color Tint { get; set; } = Theme.Text;

        public RowIcon()
        {
            Size = new Size(UiSize.Tile, UiSize.Tile);
            // --s3 of clear space to the row's title.
            Margin = new Padding(0, 0, UiSpacing.Medium - UiSpacing.LabelInset, 0);
            Anchor = AnchorStyles.Left;
            TabStop = false;
            // Decoration: the row's title already says what it is.
            AccessibleRole = AccessibleRole.None;
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
            using (var brush = new SolidBrush(Theme.Tile(Tint, ground)))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, Glyph, UiFont.Icon, ClientRectangle, Tint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    // The footer's state dot: --positive when ready, --accent while counting down or typing,
    // --negative after a problem.
    internal sealed class StatusDot : Control
    {
        public static readonly int Diameter = Dpi.S(8);
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
            e.Graphics.FillEllipse(brush, (Width - Diameter) / 2, (Height - Diameter) / 2, Diameter, Diameter);
        }
    }

    // CNTR's chip: --surface-2 pill, --fs-xs bold --text-2. Sizes itself to its text.
    internal sealed class ChipLabel : Label
    {
        public ChipLabel()
        {
            AutoSize = true;
            Font = UiFont.Label;
            // --s3 at the sides like a field; the height comes from MinimumSize (the button face).
            Padding = new Padding(UiSpacing.Medium, UiSpacing.XSmall, UiSpacing.Medium, UiSpacing.XSmall);
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
            using (var path = RoundedPanel.CreatePath(pill, UiRadius.Small))
            using (var brush = new SolidBrush(Theme.Surface2))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Theme.Text2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // The app icon and the subtitle.
    internal sealed class TitleIdentity : Control
    {
        private static readonly int MarkSize = Dpi.S(20);
        private readonly Icon mark = new Icon(AppIcon.Value, MarkSize, MarkSize);

        public TitleIdentity()
        {
            TabStop = false;
            AccessibleRole = AccessibleRole.TitleBar;
            AccessibleName = "TYPR, auto typer";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) mark.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

            int x = UiSpacing.Large;
            var logo = new Rectangle(x, (Height - MarkSize) / 2, MarkSize, MarkSize);
            g.DrawIcon(mark, logo);
            x = logo.Right + UiSpacing.Small;

            TextRenderer.DrawText(g, "auto typer", UiFont.Small, new Rectangle(x, 0, Math.Max(0, Width - x), Height),
                Theme.Text3, flags);
        }
    }

    // A VS Code editor tab: square, flush with its neighbours. The active tab takes the editor's
    // --surface-1 and opens into it; the others sit on --bg behind a --line edge.
    internal sealed class EditorTabPanel : Panel
    {
        private static readonly int CloseSize = Dpi.S(20);
        private bool hovered;
        private bool closeHovered;

        public bool IsActive { get; set; }
        // False hides the x (and its click), e.g. on the only tab left.
        public bool CanClose { get; set; } = true;
        public event EventHandler? CloseRequested;
        public event EventHandler? RenameRequested;

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibleObject(this, "Select", () => { if (Enabled) OnClick(EventArgs.Empty); },
                () => AccessibleStates.Selectable | (IsActive ? AccessibleStates.Selected : AccessibleStates.None));

        public EditorTabPanel()
        {
            // Panel disables ControlStyles.Selectable by default, which blocks keyboard focus
            // entirely regardless of TabStop — re-enable it so tabs are keyboard-operable.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            TabStop = true;
        }

        // Left/Right move along the strip (roving focus) instead of to the next control.
        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Space or Keys.Left or Keys.Right || base.IsInputKey(keyData);

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
        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            if (!IsActive)
                using (var line = new SolidBrush(Theme.Line))
                {
                    g.FillRectangle(line, 0, Height - 1, Width, 1);                  // the editor's top edge
                    g.FillRectangle(line, Width - 1, 0, 1, Height);                 // full-height divider to the next tab, like VS Code
                }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // Disabled (during a run): --text-3 ink, --line edges, no hover, no x.
            bool lit = hovered && Enabled;
            Color fill = IsActive ? Theme.Surface1 : lit ? Theme.Hover(Theme.Bg) : Theme.Bg;
            // Active (and hovered) tabs have rounded top corners and no bottom edge, so the active one
            // runs straight into the editor below.
            if (IsActive || lit)
            {
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
            if (ShowsClose && (IsActive || hovered || Focused))
            {
                Rectangle close = GetCloseButtonBounds();
                if (closeHovered)
                    using (var brush = new SolidBrush(Theme.Hover(fill)))
                    using (var path = RoundedPanel.CreatePath(close, UiRadius.XSmall))
                        g.FillPath(brush, path);
                int inset = Dpi.S(6);
                using var pen = new Pen(ink, Dpi.S(1.4f))
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
                ring.Inflate(-Dpi.S(2), -Dpi.S(2));
                FocusRing.Draw(g, ring, UiRadius.Small);
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath TabOutline(bool closed)
        {
            int r = UiRadius.Small;
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
            var page = new Rectangle(left, cy - Dpi.S(7), DocumentIconWidth, Dpi.S(14));
            using var pen = new Pen(color, Dpi.S(1.3f));
            using (var path = RoundedPanel.CreatePath(page, Dpi.S(2)))
                g.DrawPath(pen, path);
            int margin = Dpi.S(3);
            g.DrawLine(pen, page.Left + margin, cy - Dpi.S(1), page.Right - margin, cy - Dpi.S(1));
            g.DrawLine(pen, page.Left + margin, cy + Dpi.S(3), page.Right - margin, cy + Dpi.S(3));
        }

        private bool ShowsClose => CanClose && Enabled;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && ShowsClose && GetCloseButtonBounds().Contains(e.Location))
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
            bool overClose = ShowsClose && GetCloseButtonBounds().Contains(e.Location);
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
            new Rectangle(Width - CloseSize - Dpi.S(8), (Height - CloseSize) / 2, CloseSize, CloseSize);

        private static readonly int DocumentIconWidth = Dpi.S(11);

        private Rectangle GetTextBounds()
        {
            int left = UiSpacing.Medium + DocumentIconWidth + Dpi.S(6);
            int right = CanClose ? GetCloseButtonBounds().Left - Dpi.S(4) : Width - UiSpacing.Small;
            return new Rectangle(left, 0, Math.Max(0, right - left), Height);
        }
    }

    // The strip's + : a rounded icon button that fills with the hover colour.
    internal sealed class AddTextButton : Control
    {
        private bool hovered;

        public AddTextButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibleObject(this, "Press", () => { if (Enabled) OnClick(EventArgs.Empty); });

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
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
            var circle = new Rectangle((Width - UiSize.Tile) / 2, (Height - UiSize.Tile) / 2, UiSize.Tile, UiSize.Tile);
            Color ground = Parent?.BackColor ?? Theme.Bg;
            if (hovered && Enabled)
                using (var brush = new SolidBrush(Theme.Hover(ground)))
                using (var path = RoundedPanel.CreatePath(circle, UiRadius.Small))
                    g.FillPath(brush, path);
            else if (!Enabled)
                // Disabled: a --line edge and --text-3 ink, like the other locked controls.
                using (var pen = new Pen(Theme.Line))
                using (var path = RoundedPanel.CreatePath(new Rectangle(circle.X, circle.Y, circle.Width - 1, circle.Height - 1), UiRadius.Small))
                    g.DrawPath(pen, path);

            int cx = Width / 2;
            int cy = Height / 2;
            using (var pen = new Pen(Enabled ? Theme.Text2 : Theme.Text3, Dpi.S(1.6f)))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                int arm = Dpi.S(6);
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }

            if (Focused && ShowFocusCues)
            {
                var ring = circle;
                ring.Inflate(FocusRing.Room, FocusRing.Room);
                FocusRing.Draw(g, ring, FocusRing.Radius(UiRadius.Small));
            }
        }
    }

    internal class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = UiRadius.Large;
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
        Keycap,    // a shortcut key: looks the same as Secondary
        Danger     // --negative / --on-negative, destructive confirms only
    }

    // CNTR's .btn: --r-md, --fs-sm bold; a press shrinks it to 0.97 over --dur-tap.
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
                Font = UiFont.Button;
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

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibleObject(this, "Press", PerformClick);

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
            int radius = UiRadius.Small; // one radius for every button
            int ringRadius = FocusRing.Radius(radius);

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

            if (Enabled)
            {
                // Every button is drawn as a key: a darker edge with a 2px lip at the bottom; the lip goes on press.
                Color edge = kind switch
                {
                    ButtonKind.Primary or ButtonKind.Danger => Theme.Blend(Color.FromArgb(64, 0, 0, 0), fill),
                    _ => Theme.InputBorder
                };
                int fullLip = Dpi.S(2);
                int lip = press > 0.5 ? Dpi.S(1) : fullLip;
                using (var path = RoundedPanel.CreatePath(face, radius))
                using (var brush = new SolidBrush(edge))
                    g.FillPath(brush, path);
                face = new Rectangle(face.X + 1, face.Y + 1 + (fullLip - lip), face.Width - 2, face.Height - 2 - lip);
                using (var path = RoundedPanel.CreatePath(face, radius - 1))
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
            }
            else
            {
                using var outline = RoundedPanel.CreatePath(new Rectangle(face.X, face.Y, face.Width - 1, face.Height - 1), radius);
                using var pen = new Pen(Theme.Line);
                g.DrawPath(pen, outline);
            }
            TextRenderer.DrawText(g, Text, Font, face, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
                FocusRing.Draw(g, ClientRectangle, ringRadius);
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
        public event EventHandler? CheckedChanged;

        // The state the switch opens with: no slide and no CheckedChanged.
        public void SetInitial(bool on)
        {
            Checked = on;
            position = on ? 1 : 0;
            AccessibleDescription = on ? "On" : "Off";
            Invalidate();
        }

        public ToggleCheckBox()
        {
            Size = new Size(Dpi.S(48), Dpi.S(30));
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleDescription = "Off";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibleObject(this, "Toggle", () => { if (Enabled) OnClick(EventArgs.Empty); },
                () => Checked ? AccessibleStates.Checked : AccessibleStates.None);

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
            CheckedChanged?.Invoke(this, EventArgs.Empty);
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
            int trackWidth = Dpi.S(44), trackHeight = Dpi.S(26);
            var track = new Rectangle((Width - trackWidth) / 2, (Height - trackHeight) / 2, trackWidth, trackHeight);
            double on = Math.Clamp(position, 0, 1);

            using (var path = RoundedPanel.CreatePath(track, track.Height / 2))
            {
                using (var brush = new SolidBrush(Enabled ? Theme.Surface3 : Theme.Surface2))
                    g.FillPath(brush, path);
                if (on > 0)
                {
                    // A growing pill, not a clipped rectangle: a clip region has no anti-aliasing and gave jagged edges.
                    int fill = Math.Max(track.Height, (int)Math.Round(track.Width * on));
                    using var grown = RoundedPanel.CreatePath(new Rectangle(track.X, track.Y, fill, track.Height), track.Height / 2);
                    using (var brush = new SolidBrush(Enabled ? Theme.Accent : Theme.LockedOn))
                        g.FillPath(brush, grown);
                }
            }
            // The off track (--surface-3) is 1.3:1 on the row; an --input-border edge (3.1:1) shows it,
            // fading out as the accent fills in.
            if (Enabled && on < 1)
                using (var edge = RoundedPanel.CreatePath(new Rectangle(track.X, track.Y, track.Width - 1, track.Height - 1), (track.Height - 1) / 2))
                using (var pen = new Pen(Color.FromArgb((int)(255 * (1 - on)), Theme.InputBorder)))
                    g.DrawPath(pen, edge);

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
            // The knob's centre runs from the track's left end-cap centre to its right one.
            double centerX = track.X + track.Height / 2.0 + (track.Width - track.Height) * position;
            double centerY = track.Y + track.Height / 2.0;
            int knobSize = Dpi.S(20);
            float knobW = (float)(knobSize * sx);
            float knobH = (float)(knobSize * sy);
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
                ring.Inflate(Dpi.S(2), Dpi.S(2));
                FocusRing.Draw(g, ring, ring.Height / 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tween.Dispose();
            base.Dispose(disposing);
        }
    }

    // CNTR's input: --surface-1, 1px --input-border edge, --r-md; focus draws the 2px focus ring on that
    // edge. Disabled: --line edge, --text-3 ink. An optional unit ("cpm") sits inside, right, in --text-3.
    internal sealed class TextField : UserControl
    {
        private readonly RoundedPanel frame;
        private readonly Panel host;
        private string? unit;
        public TextBox Input { get; } = new TextBox();

        public string? Unit
        {
            get => unit;
            set
            {
                unit = value;
                int room = string.IsNullOrEmpty(value) ? 0 : TextRenderer.MeasureText(value, UiFont.Small).Width + Dpi.S(2);
                // A unit takes the right padding's place, and the box gives the number the left padding's room.
                frame.Padding = room == 0
                    ? new Padding(UiSpacing.Medium, Dpi.S(2), UiSpacing.Medium, Dpi.S(2))
                    : new Padding(UiSpacing.Small, Dpi.S(2), UiSpacing.Small + room, Dpi.S(2));
                frame.Invalidate();
            }
        }

        public TextField()
        {
            Height = UiSize.Field;
            frame = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = UiRadius.Small,
                Padding = new Padding(UiSpacing.Medium, Dpi.S(2), UiSpacing.Medium, Dpi.S(2)),
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
            Input.GotFocus += (_, __) => ApplyTheme();
            Input.LostFocus += (_, __) => ApplyTheme();
            frame.Paint += (_, e) =>
            {
                if (!string.IsNullOrEmpty(unit))
                {
                    var room = new Rectangle(frame.ClientSize.Width - frame.Padding.Right, 0, frame.Padding.Right - UiSpacing.Small, frame.ClientSize.Height);
                    TextRenderer.DrawText(e.Graphics, unit, UiFont.Small, room, Theme.Text3,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                }
            };
            host.Controls.Add(Input);
            frame.Controls.Add(host);
            Controls.Add(frame);
            ApplyTheme();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            ApplyTheme();
            base.OnEnabledChanged(e);
        }

        public void ApplyTheme()
        {
            frame.BackColor = Theme.Surface1;
            // Focus is the same 1px edge as at rest, in the accent colour (6:1 on the box), not a second, thicker ring.
            frame.BorderColor = !Enabled ? Theme.Line : Input.Focused ? Theme.Accent : Theme.InputBorder;
            host.BackColor = Theme.Surface1;
            Input.BackColor = Theme.Surface1;
            Input.ForeColor = Enabled ? Theme.Text : Theme.Text3;
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

        // Shown inside the box, right of the number.
        public string Unit
        {
            set => field.Unit = value;
        }

        // PageUp / PageDown step.
        public int PageStep { get; set; } = 10;

        // A typed value outside Minimum..Maximum was pulled into range.
        public event EventHandler? Clamped;

        public void ApplyTheme() => field.ApplyTheme();

        public NumberInput()
        {
            Height = UiSize.Field;
            MinimumSize = new Size(Dpi.S(64), UiSize.Field);
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
                if (e.KeyCode == Keys.PageUp) { CommitInput(); Value += PageStep; e.Handled = true; }
                if (e.KeyCode == Keys.PageDown) { CommitInput(); Value -= PageStep; e.Handled = true; }
                if (e.KeyCode == Keys.Enter) { CommitInput(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            input.Leave += (_, __) => CommitInput();
            Controls.Add(field);
        }

        // F8 starts a run without the box losing focus, so the run commits the typed value itself.
        public void Commit() => CommitInput();

        private void CommitInput()
        {
            // long, so a pasted 3000000000 is clamped (and said) instead of silently dropped.
            if (long.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                SetValue(parsed);
                if (parsed != value) Clamped?.Invoke(this, EventArgs.Empty);
            }
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
        private const int EM_GETRECT = 0x00B2;
        private const int EM_LINEINDEX = 0x00BB;
        private const int EM_LINEFROMCHAR = 0x00C9;
        private const int EM_POSFROMCHAR = 0x00D6;

        public event EventHandler? ViewChanged;

        public int FirstVisibleLine => IsHandleCreated ? (int)NativeMethods.SendMessage(Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero) : 0;
        public int LineCount => IsHandleCreated ? (int)NativeMethods.SendMessage(Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero) : 1;
        public int VisibleLines => Math.Max(1, ClientSize.Height / Math.Max(1, Font.Height));
        // The display line holding the caret; with a selection, the line where the selection starts.
        public int CaretLine => IsHandleCreated ? (int)NativeMethods.SendMessage(Handle, EM_LINEFROMCHAR, (IntPtr)(-1), IntPtr.Zero) : 0;

        // The first character and the client y of each display line from the top of the view down to its
        // bottom edge, as the edit control itself places them.
        public void VisibleRows(List<int> starts, List<int> tops)
        {
            starts.Clear();
            tops.Clear();
            if (!IsHandleCreated) return;
            int length = TextLength;
            int rowHeight = TextRenderer.MeasureText("0", Font, Size.Empty, TextFormatFlags.NoPadding).Height;
            for (int row = FirstVisibleLine, count = LineCount; row < count; row++)
            {
                int start = (int)NativeMethods.SendMessage(Handle, EM_LINEINDEX, (IntPtr)row, IntPtr.Zero);
                if (start < 0) break;
                int top;
                // EM_POSFROMCHAR has no answer past the last character: the empty line after a final newline, or no text.
                if (start < length) top = (short)((long)NativeMethods.SendMessage(Handle, EM_POSFROMCHAR, (IntPtr)start, IntPtr.Zero) >> 16);
                else if (tops.Count > 0) top = tops[^1] + rowHeight;
                else
                {
                    var format = new NativeMethods.RECT();
                    NativeMethods.SendMessage(Handle, EM_GETRECT, IntPtr.Zero, ref format);
                    top = format.Top;
                }
                if (top >= ClientSize.Height) break;
                starts.Add(start);
                tops.Add(top);
            }
        }

        public void ScrollToLine(int line)
        {
            int delta = line - FirstVisibleLine;
            if (delta != 0) NativeMethods.SendMessage(Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
        }

        // The placeholder shows only while empty and unfocused, so focus changes repaint it.
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (TextLength == 0) Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); if (TextLength == 0) Invalidate(); }

        protected override void WndProc(ref Message m)
        {
            const int WM_PAINT = 0x000F;
            if (m.Msg == WM_PAINT)
            {
                // TextBox would add PlaceholderText in SystemColors.GrayText (2.96:1 on the dark editor), so
                // paint natively and add it in --text-3 (5.6:1) instead, with TextBox's visibility.
                DefWndProc(ref m);
                if (TextLength == 0 && !Focused && !string.IsNullOrEmpty(PlaceholderText))
                {
                    using var g = CreateGraphics();
                    // The formatting rectangle: where typed text starts, so the hint doesn't jump when typing begins.
                    var format = new NativeMethods.RECT();
                    NativeMethods.SendMessage(Handle, EM_GETRECT, IntPtr.Zero, ref format);
                    var bounds = Rectangle.FromLTRB(format.Left, format.Top, format.Right, format.Bottom);
                    TextRenderer.DrawText(g, PlaceholderText, Font, bounds, Theme.Text3,
                        TextFormatFlags.NoPadding | TextFormatFlags.Top | TextFormatFlags.Left |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
                }
                return;
            }
            base.WndProc(ref m);
            // Messages after which the first visible line, the line count or the caret's line can change:
            // size, set text, key down, char, mouse move (drag-select), click, wheel, scroll, paste,
            // select (EM_SETSEL), line scroll (EM_LINESCROLL).
            switch (m.Msg)
            {
                case 0x0005: case 0x000C: case 0x0100: case 0x0102: case 0x0200: case 0x0201: case 0x020A: case 0x0115: case 0x0302:
                case 0x00B1: case 0x00B6:
                    ViewChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    // VS Code-style overlay scroll bar: no track, a thin --text-3 pill that widens on hover or drag.
    // Hidden while everything fits.
    internal sealed class ThinScrollBar : Control
    {
        private static readonly int MinThumb = Dpi.S(24);
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
            Width = Dpi.S(12);
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
            int width = Dpi.S(active ? 8 : 4);
            var pill = new Rectangle(Width - width - Dpi.S(2), thumb.Y, width, thumb.Height);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = RoundedPanel.CreatePath(pill, width / 2);
            using var brush = new SolidBrush(Color.FromArgb(active ? Theme.RailActiveAlpha : Theme.RailAlpha, Theme.Text3));
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

    // VS Code-style line numbers left of the editor text, on the card's own surface: a wrapped line is
    // numbered on its first display line only, and the caret's line is brighter. Rows come from the edit
    // control (EditorBox.VisibleRows), so they line up with the text at any font size or DPI.
    internal sealed class LineNumberGutter : Control
    {
        private EditorBox? target;
        private string text = "";
        // Line breaks before the last first-visible row, so scrolling counts only the rows it moved over.
        private (int Index, int Breaks) anchor;
        private (int first, int lines, int caret, int width) shown;
        private readonly List<int> starts = new();
        private readonly List<int> tops = new();

        public EditorBox? Target
        {
            get => target;
            set
            {
                target = value;
                if (target == null) return;
                target.ViewChanged += (_, __) => Sync();
                target.TextChanged += (_, __) => { if (Visible) Reload(); };
            }
        }

        public LineNumberGutter()
        {
            TabStop = false;
            Margin = Padding.Empty;
            // Decoration: the editor already tells a screen reader which line the caret is on.
            AccessibleRole = AccessibleRole.None;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        // At least two digits, so the text doesn't shift until line 100.
        internal static int Digits(int lineCount) => Math.Max(2, lineCount.ToString(CultureInfo.InvariantCulture).Length);

        // Only CRLF ends a line: the Win32 edit control shows a lone \n or \r inside its line.
        internal static int LineBreaks(ReadOnlySpan<char> text) => text.Count("\r\n".AsSpan());

        // The 1-based line number of each display row starting at rowStarts (top to bottom), or 0 for a row
        // that continues a wrapped line. anchor carries the count between calls; reset it when the text changes.
        internal static int[] RowNumbers(string text, IReadOnlyList<int> rowStarts, ref (int Index, int Breaks) anchor)
        {
            var numbers = new int[rowStarts.Count];
            var (at, breaks) = anchor.Index <= text.Length ? anchor : (0, 0);
            for (int i = 0; i < rowStarts.Count; i++)
            {
                int start = Math.Clamp(rowStarts[i], 0, text.Length);
                breaks += start >= at
                    ? LineBreaks(text.AsSpan(at, start - at))
                    : -LineBreaks(text.AsSpan(start, at - start));
                at = start;
                if (i == 0) anchor = (at, breaks);
                bool firstRow = start == 0 || (start >= 2 && text[start - 2] == '\r' && text[start - 1] == '\n');
                numbers[i] = firstRow ? breaks + 1 : 0;
            }
            return numbers;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Reload();
        }

        // shortcut: copies the whole text once per edit while shown (about 1 ms at 1M characters); share
        // MainForm's TextChanged copy if huge texts lag.
        private void Reload()
        {
            if (target == null) return;
            text = target.Text;
            anchor = default;
            int digit = TextRenderer.MeasureText("0", target.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            int width = Digits(LineBreaks(text) + 1) * digit + UiSpacing.Medium;
            if (Width != width) Width = width;
            Invalidate();
        }

        private void Sync()
        {
            if (target == null || !Visible) return;
            var now = (target.FirstVisibleLine, target.LineCount, target.CaretLine, target.ClientSize.Width);
            if (now == shown) return;
            shown = now;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface1);
            if (target == null) return;
            int first = target.FirstVisibleLine;
            target.VisibleRows(starts, tops);
            var numbers = RowNumbers(text, starts, ref anchor);
            // The caret's line: the numbered row at or above the caret's display row.
            int current = target.CaretLine - first;
            while (current >= 0 && current < numbers.Length && numbers[current] == 0) current--;
            int offset = target.Top - Top;
            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] == 0) continue;
                int y = tops[i] + offset;
                TextRenderer.DrawText(g, numbers[i].ToString(CultureInfo.InvariantCulture), target.Font,
                    new Rectangle(0, y, Width - UiSpacing.Medium, Math.Max(1, Height - y)),
                    i == current ? Theme.Text2 : Theme.Text3,
                    TextFormatFlags.NoPadding | TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.SingleLine);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Wheel over the numbers scrolls the editor, 3 lines a notch like the scroll bar.
            target?.ScrollToLine(target.FirstVisibleLine - Math.Sign(e.Delta) * 3);
            base.OnMouseWheel(e);
        }
    }

    internal sealed class Slider : Control
    {
        private static readonly int Pad = Dpi.S(12);
        private static readonly int Inset = Dpi.S(9);
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
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Slider()
        {
            Height = Dpi.S(32);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
            // Value's setter skips an unchanged value, so the starting one is described here.
            AccessibleDescription = val + "%";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibleObject(this, null, null, read: () => val + "%");

        private double Fraction => maximum <= minimum ? 0 : (double)(val - minimum) / (maximum - minimum);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color ground = Parent?.BackColor ?? Theme.Surface1;
            g.Clear(ground);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int rail = Dpi.S(4);
            var track = new Rectangle(Pad, Height / 2 - rail / 2, Math.Max(1, Width - Pad * 2), rail);
            using (var path = RoundedPanel.CreatePath(track, rail / 2))
            using (var brush = new SolidBrush(Theme.Blend(Color.FromArgb(Theme.RailAlpha, Theme.Text3), ground)))
                g.FillPath(brush, path);

            Color active = Enabled ? Theme.Accent : Theme.Text3;
            // Thumb centre travels Inset..Width-Inset so the thumb stays on the rail at both ends.
            int cx = track.X + Inset + (int)((track.Width - Inset * 2) * Fraction);
            using (var path = RoundedPanel.CreatePath(new Rectangle(track.X, track.Y, cx - track.X, track.Height), rail / 2))
            using (var brush = new SolidBrush(active))
                g.FillPath(brush, path);

            int radius = Dpi.S(hovered || dragging ? 9 : 8);
            int haloRadius = radius + Dpi.S(2);
            int ringRadius = radius + Dpi.S(4);
            int cy = Height / 2;
            using (var halo = new SolidBrush(ground))
                g.FillEllipse(halo, cx - haloRadius, cy - haloRadius, haloRadius * 2, haloRadius * 2);
            using (var brush = new SolidBrush(active))
                g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);

            if (Focused && ShowFocusCues)
                FocusRing.Draw(g, new Rectangle(cx - ringRadius, cy - ringRadius, ringRadius * 2, ringRadius * 2), ringRadius);
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
            int usable = Math.Max(1, Width - (Pad + Inset) * 2);
            double frac = Math.Max(0, Math.Min(1, (x - Pad - Inset) / (double)usable));
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

        public static readonly int DefaultWidth = Dpi.S(46);

        public WindowCaptionButton(WindowCaptionButtonKind kind)
        {
            Kind = kind;
            Width = DefaultWidth;
            Height = Dpi.S(40);
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
            Color background = !hovered ? Theme.Bg : closeHover ? Theme.CloseHover : Theme.Hover(Theme.Bg);
            e.Graphics.Clear(background);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var pen = new Pen(closeHover ? Color.White : hovered ? Theme.Text : Theme.Text2, Dpi.S(1.2f));
            int cx = Width / 2;
            int cy = Height / 2;
            int r = Dpi.S(5); // half the glyph
            if (Kind == WindowCaptionButtonKind.Minimize)
            {
                e.Graphics.DrawLine(pen, cx - r, cy, cx + r, cy);
            }
            else if (Kind == WindowCaptionButtonKind.Maximize && IsMaximized)
            {
                e.Graphics.DrawRectangle(pen, cx - Dpi.S(3), cy - r, Dpi.S(8), Dpi.S(8));
                e.Graphics.DrawLine(pen, cx - r, cy - Dpi.S(2), cx - r, cy + r);
                e.Graphics.DrawLine(pen, cx - r, cy + r, cx + Dpi.S(2), cy + r);
            }
            else if (Kind == WindowCaptionButtonKind.Maximize)
            {
                e.Graphics.DrawRectangle(pen, cx - r, cy - r, r * 2, r * 2);
            }
            else
            {
                e.Graphics.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                e.Graphics.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
            }
        }
    }

    // A title-bar icon button (the light/dark switch, the narrow-window sidebar toggle): a glyph that gets
    // a --surface-2 square on hover or while Active.
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
            Width = Dpi.S(40);
            Height = Dpi.S(40);
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibleObject(this, "Press", () => { if (Enabled) OnClick(EventArgs.Empty); });

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
            var square = new Rectangle((Width - UiSize.Tile) / 2, (Height - UiSize.Tile) / 2, UiSize.Tile, UiSize.Tile);
            // Disabled (the theme switch during a run): no hover, a --line edge, --text-3 ink.
            bool lit = Enabled && (hovered || active);
            Color ground = lit ? Theme.Hover(Theme.Bg) : Theme.Bg;
            if (lit)
                using (var path = RoundedPanel.CreatePath(square, UiRadius.Small))
                using (var brush = new SolidBrush(ground))
                    g.FillPath(brush, path);
            else if (!Enabled)
                using (var path = RoundedPanel.CreatePath(new Rectangle(square.X, square.Y, square.Width - 1, square.Height - 1), UiRadius.Small))
                using (var pen = new Pen(Theme.Line))
                    g.DrawPath(pen, path);

            Color ink = !Enabled ? Theme.Text3 : lit ? Theme.Text : Theme.Text2;
            if (Glyph != null)
                TextRenderer.DrawText(g, Glyph, UiFont.Icon, ClientRectangle, ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            else
                DrawThemeGlyph(g, ink, ground, cx, cy);

            if (Focused && ShowFocusCues)
            {
                var ring = square;
                ring.Inflate(FocusRing.Room, FocusRing.Room);
                FocusRing.Draw(g, ring, FocusRing.Radius(UiRadius.Small));
            }
        }

        private static void DrawThemeGlyph(Graphics g, Color color, Color background, int cx, int cy)
        {
            using var pen = new Pen(color, Dpi.S(1.6f));
            using var brush = new SolidBrush(color);

            if (Theme.Dark)
            {
                int moon = Dpi.S(7), bite = Dpi.S(13);
                g.FillEllipse(brush, cx - moon, cy - moon, moon * 2, moon * 2);
                using var cutout = new SolidBrush(background);
                g.FillEllipse(cutout, cx - Dpi.S(2), cy - Dpi.S(10), bite, bite);
                return;
            }

            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            float inner = Dpi.S(7.5f), outer = Dpi.S(10f);
            for (int angle = 0; angle < 360; angle += 45)
            {
                double radians = angle * Math.PI / 180.0;
                float x1 = cx + (float)(Math.Cos(radians) * inner);
                float y1 = cy + (float)(Math.Sin(radians) * inner);
                float x2 = cx + (float)(Math.Cos(radians) * outer);
                float y2 = cy + (float)(Math.Sin(radians) * outer);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
            int sun = Dpi.S(4);
            g.FillEllipse(brush, cx - sun, cy - sun, sun * 2, sun * 2);
        }
    }

    // TYPR's dialogs in CNTR's sheet style: --surface-1, title --fs-lg bold, text --fs-sm --text-2,
    // a button row with Cancel first. Buttons say what they do (CNTR copy rules).
    internal sealed class ThemedDialog : Form
    {
        // 24 visible from the text or field to the button faces, which sit FocusRing.Room inside their control.
        private static readonly int ActionsGap = UiSpacing.XLarge + UiSpacing.XSmall - FocusRing.Room;
        private readonly TableLayoutPanel layout;
        private readonly FlowLayoutPanel buttons;

        public ThemedDialog(string title, string? message, Control? body = null)
        {
            // The heading below already says the title; the caption only names the app.
            Text = "TYPR";
            Icon = AppIcon.Value;
            // A screen reader names the dialog by what it asks, not the caption.
            AccessibleName = title;
            AccessibleDescription = message;
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
            // 10 visible on every side (owner): the text's own LabelInset makes up the left edge, and the
            // buttons' focus-ring room the right and bottom ones.
            int edge = UiSpacing.Small + Dpi.S(2);
            Padding = new Padding(edge - UiSpacing.LabelInset, edge, edge - FocusRing.Room, edge - FocusRing.Room);

            layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = Theme.Surface1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                // Docked, so the form's own padding applies on every side.
                Dock = DockStyle.Fill
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
                    MaximumSize = new Size(Dpi.S(360), 0),
                    MinimumSize = new Size(Dpi.S(320), 0),
                    Font = UiFont.Small,
                    ForeColor = Theme.Text2,
                    Margin = new Padding(0, 0, FocusRing.Room, ActionsGap)
                });
            if (body != null)
            {
                // The buttons' face sits Room px inside their control; the same gap on the right lines the edges up.
                body.Margin = new Padding(0, 0, FocusRing.Room, ActionsGap);
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
                MinimumSize = new Size(Dpi.S(88), UiSize.Button),
                // As the footer buttons: --s4 inside, --s2 between the visible faces.
                Padding = new Padding(UiSpacing.Large, 0, UiSpacing.Large, 0),
                Margin = new Padding(UiSpacing.Small - FocusRing.Room * 2, 0, 0, 0),
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

        // Dialogs on screen now. WM_HOTKEY still reaches the main window under a modal dialog,
        // so the start shortcut checks this; Stop keeps working.
        private static int openCount;
        private bool counted;
        public static bool AnyOpen => openCount > 0;

        protected override void OnShown(EventArgs e)
        {
            counted = true;
            openCount++;
            base.OnShown(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (counted) openCount--;
            counted = false;
            base.OnFormClosed(e);
        }

        public static bool Confirm(IWin32Window? owner, string title, string message, string action)
        {
            using var dialog = new ThemedDialog(title, message);
            var cancel = dialog.AddButton("Cancel", ButtonKind.Secondary, DialogResult.Cancel);
            dialog.AddButton(action, ButtonKind.Danger, DialogResult.OK);
            dialog.CancelButton = cancel;
            dialog.ActiveControl = cancel;
            return dialog.ShowOver(owner) == DialogResult.OK;
        }

        public static void Inform(IWin32Window? owner, string title, string message)
        {
            using var dialog = new ThemedDialog(title, message);
            var close = dialog.AddButton("Close", ButtonKind.Primary, DialogResult.OK);
            dialog.CancelButton = close;
            dialog.AcceptButton = close;
            dialog.ActiveControl = close;
            dialog.ShowOver(owner);
        }

        // Form.ActiveForm is null while another app is in front, so fall back to TYPR's window.
        private DialogResult ShowOver(IWin32Window? owner)
        {
            owner ??= Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            if (owner == null) StartPosition = FormStartPosition.CenterScreen;
            return ShowDialog(owner);
        }
    }

    internal static class Program
    {
        // True while the error dialog is up, so an error inside it can't stack dialogs without end.
        private static bool reporting;

        [STAThread]
        static void Main(string[] args)
        {
            // Setup's window, run from a temp folder. Before the mutex: TYPR may be open during an update.
            if (args.Length == 2 && args[0] == SetupForm.Switch)
            {
                UseSystemDpi();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SetupForm(args[1]));
                return;
            }
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
                bool saved = Theme.SaveSettings();
                if (reporting) return;
                reporting = true;
                try
                {
                    string message = (saved
                        ? "Something went wrong, but your texts were saved."
                        : "Something went wrong, and your texts could not be saved.") + $"\n\n{e.Exception.Message}";
                    try { ThemedDialog.Inform(Form.ActiveForm, "Unexpected error", message); }
                    catch { MessageBox.Show(message, "TYPR - unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
                finally
                {
                    reporting = false;
                }
            };
            UseSystemDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        // Sizes scale once, by the system DPI; on a monitor with another scale Windows stretches the window.
        // Before anything reads a UI token: the tokens capture Dpi.Factor when first used.
        private static void UseSystemDpi()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            using var screen = Graphics.FromHwnd(IntPtr.Zero);
            Dpi.Factor = screen.DpiX / 96f;
        }
    }
}
