using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows.Forms;

namespace TYPR
{
    internal static class UiSpacing
    {
        public const int XSmall = 4;
        public const int Small = 8;
        public const int Medium = 12;
        public const int Large = 16;
        public const int XLarge = 20;
    }

    internal static class UiTypography
    {
        public const float Caption = 9f;
        public const float Body = 9.5f;
        public const float Control = 10f;
        public const float Editor = 10.5f;
        public const float Section = 11f;
        public const float Header = 12f;
    }

    internal static class Theme
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TYPR Kimi",
            "settings.json");

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

        public static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(
                    $"TYPR could not save your preferences. Changes will apply for this session only.\n\n{ex.Message}",
                    "TYPR - settings could not be saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static UserSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new UserSettings();

                var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath))
                    ?? new UserSettings();
                if (settings.Theme != "System" && settings.Theme != "Light" && settings.Theme != "Dark")
                    settings.Theme = "System";
                if (!IsValidKey(settings.StartKey)) settings.StartKey = (int)Keys.F8;
                if (!IsValidKey(settings.StopKey)) settings.StopKey = (int)Keys.F9;
                settings.StartModifiers &= HotkeyModifiers.Supported;
                settings.StopModifiers &= HotkeyModifiers.Supported;
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
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                MessageBox.Show(
                    $"TYPR could not read its preferences. Default settings will be used.\n\n{ex.Message}",
                    "TYPR - settings could not be loaded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return new UserSettings();
            }
        }

        private static bool IsValidKey(int value)
        {
            var key = (Keys)value;
            return key != Keys.None &&
                key != Keys.ControlKey &&
                key != Keys.ShiftKey &&
                key != Keys.Menu &&
                key != Keys.LWin &&
                key != Keys.RWin &&
                Enum.IsDefined(typeof(Keys), key);
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

        // ---- VS Code inspired palette ----

        public static Color TitleBar      => Dark ? Color.FromArgb(25, 26, 27)     : Color.FromArgb(221, 221, 221);
        public static Color ActivityBar   => Dark ? Color.FromArgb(25, 26, 27)     : Color.FromArgb(44, 44, 44);
        public static Color SideBar       => Dark ? Color.FromArgb(25, 26, 27)     : Color.FromArgb(243, 243, 243);
        public static Color Editor        => Dark ? Color.FromArgb(17, 17, 17)     : Color.FromArgb(255, 255, 255);
        public static Color TabStrip      => Dark ? Color.FromArgb(24, 24, 24)     : Color.FromArgb(243, 243, 243);
        public static Color ActiveTab     => Editor;
        public static Color TabContainer  => Color.FromArgb(25, 26, 27);
        public static Color StatusBar     => Color.FromArgb(0, 122, 204);
        public static Color StatusBarHover => Color.FromArgb(0, 95, 158);
        public static Color Input         => Dark ? Color.FromArgb(49, 49, 49)     : Color.White;
        public static Color Border        => Dark ? Color.FromArgb(60, 60, 60)     : Color.FromArgb(206, 206, 206);
        public static Color PaneBorder    => Dark ? Color.FromArgb(43, 43, 43)     : Color.FromArgb(224, 224, 224);
        public static Color Surface       => Dark ? Color.FromArgb(25, 26, 27)     : Color.White;
        public static Color SurfaceMuted  => Dark ? Color.FromArgb(58, 58, 58)     : Color.FromArgb(232, 232, 232);
        public static Color FocusBorder   => Color.FromArgb(0, 127, 212);
        public static Color Text          => Dark ? Color.FromArgb(204, 204, 204)  : Color.FromArgb(51, 51, 51);
        public static Color Text2         => Dark ? Color.FromArgb(157, 157, 157)  : Color.FromArgb(97, 97, 97);
        public static Color Text3         => Dark ? Color.FromArgb(128, 128, 128)  : Color.FromArgb(120, 120, 120);
        public static Color Accent        => Color.FromArgb(0, 122, 204);
        public static Color AccentHover   => Color.FromArgb(0, 90, 158);
        public static Color Positive      => Color.FromArgb(137, 209, 133);
        public static Color OnAccent      => Color.White;
        public static Color ActivityGlyph => Color.FromArgb(197, 197, 197);
        public static Color StatusBarText => Color.White;

        public static Color MapBackColor(Color color, bool fromDark)
        {
            if (color == TitleBarFor(fromDark)) return TitleBar;
            if (color == ActivityBarFor(fromDark)) return ActivityBar;
            if (color == SideBarFor(fromDark)) return SideBar;
            if (color == EditorFor(fromDark)) return Editor;
            if (color == TabStripFor(fromDark)) return TabStrip;
            if (color == ActiveTabFor(fromDark)) return ActiveTab;
            if (color == StatusBarFor(fromDark)) return StatusBar;
            if (color == InputFor(fromDark)) return Input;
            if (color == BorderFor(fromDark)) return Border;
            if (color == PaneBorderFor(fromDark)) return PaneBorder;
            if (color == SurfaceFor(fromDark)) return Surface;
            if (color == SurfaceMutedFor(fromDark)) return SurfaceMuted;
            if (color == Accent) return Accent;
            if (color == StatusBarHover) return StatusBarHover;
            return color;
        }

        public static Color MapForeColor(Color color, bool fromDark)
        {
            if (color == Color.White) return OnAccent;
            if (color == StatusBarText) return StatusBarText;
            if (color == TextFor(fromDark)) return Text;
            if (color == Text2For(fromDark)) return Text2;
            if (color == Text3For(fromDark)) return Text3;
            if (color == Positive) return Positive;
            if (color == ActivityGlyph) return ActivityGlyph;
            return color;
        }

        public static Color MapBorderColor(Color color, bool fromDark)
        {
            if (color == BorderFor(fromDark)) return Border;
            if (color == PaneBorderFor(fromDark)) return PaneBorder;
            if (color == FocusBorder) return FocusBorder;
            return color;
        }

        private static Color TitleBarFor(bool dark) => dark ? Color.FromArgb(25, 26, 27) : Color.FromArgb(221, 221, 221);
        private static Color ActivityBarFor(bool dark) => dark ? Color.FromArgb(25, 26, 27) : Color.FromArgb(44, 44, 44);
        private static Color SideBarFor(bool dark) => dark ? Color.FromArgb(25, 26, 27) : Color.FromArgb(243, 243, 243);
        private static Color EditorFor(bool dark) => dark ? Color.FromArgb(17, 17, 17) : Color.FromArgb(255, 255, 255);
        private static Color TabStripFor(bool dark) => dark ? Color.FromArgb(24, 24, 24) : Color.FromArgb(243, 243, 243);
        private static Color ActiveTabFor(bool dark) => dark ? Color.FromArgb(30, 30, 30) : Color.White;
        private static Color StatusBarFor(bool dark) => Color.FromArgb(0, 122, 204);
        private static Color InputFor(bool dark) => dark ? Color.FromArgb(49, 49, 49) : Color.White;
        private static Color BorderFor(bool dark) => dark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(206, 206, 206);
        private static Color PaneBorderFor(bool dark) => dark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(224, 224, 224);
        private static Color SurfaceFor(bool dark) => dark ? Color.FromArgb(25, 26, 27) : Color.White;
        private static Color SurfaceMutedFor(bool dark) => dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(232, 232, 232);
        private static Color TextFor(bool dark) => dark ? Color.FromArgb(204, 204, 204) : Color.FromArgb(51, 51, 51);
        private static Color Text2For(bool dark) => dark ? Color.FromArgb(157, 157, 157) : Color.FromArgb(97, 97, 97);
        private static Color Text3For(bool dark) => dark ? Color.FromArgb(128, 128, 128) : Color.FromArgb(120, 120, 120);
    }

    internal sealed class UserSettings
    {
        public UserSettings() { }

        public string Theme { get; set; } = "System";
        public int StartModifiers { get; set; }
        public int StartKey { get; set; } = (int)Keys.F8;
        public int StopModifiers { get; set; }
        public int StopKey { get; set; } = (int)Keys.F9;
        public List<TextTemplate> TextTemplates { get; set; } = new();
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
        public const int NoRepeat = 0x4000;
        public const int Supported = Alt | Control | Shift;
    }

    internal static class NativeMethods
    {
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

        public static MINMAXINFO GetMaximizedWorkArea(IntPtr handle, MINMAXINFO info)
        {
            const uint MONITOR_DEFAULTTONEAREST = 2;
            IntPtr monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            info.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
            info.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
            info.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
            info.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
            return info;
        }

        private static void SetDwmWindowAttribute(IntPtr handle, int attribute, int value)
        {
            int result = DwmSetWindowAttribute(handle, attribute, ref value, Marshal.SizeOf<int>());
            if (result != 0) Marshal.ThrowExceptionForHR(result);
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
            inputs[0].U.ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = 0 };
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = KEYEVENTF_KEYUP };
            SendInputChecked(inputs);
        }

        private static void SendInputChecked(INPUT[] inputs)
        {
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent == (uint)inputs.Length) return;

            int error = Marshal.GetLastWin32Error();
            string detail = error == 0
                ? "Windows blocked or rejected the synthetic keyboard input."
                : new System.ComponentModel.Win32Exception(error).Message;
            throw new InvalidOperationException($"Windows accepted {sent} of {inputs.Length} keyboard events. {detail}");
        }
    }

    public class MainForm : Form
    {
        private const int SidebarWidth = 272;
        private const int ActivityBarWidth = 48;

        private readonly TextBox text = new TextBox();
        private readonly NumberInput cpm = new NumberInput();
        private readonly NumberInput startDelay = new NumberInput();
        private readonly Slider jitter = new Slider();
        private readonly Label jitterValue = new Label();
        private readonly ToggleCheckBox pressEnter = new ToggleCheckBox();
        private readonly RoundedButton start = new RoundedButton { CornerRadius = 6 };
        private readonly RoundedButton stop = new RoundedButton { CornerRadius = 6 };
        private readonly RoundedButton startHotkey = new RoundedButton { CornerRadius = 5 };
        private readonly RoundedButton stopHotkey = new RoundedButton { CornerRadius = 5 };
        private readonly Label status = new Label();
        private readonly Label characterCount = new Label();
        private readonly Label cpmIndicator = new Label();
        private readonly WindowCaptionButton maximizeButton = new WindowCaptionButton(WindowCaptionButtonKind.Maximize);
        private readonly ActivityBarButton settingsButton = new ActivityBarButton(ActivityBarButtonKind.Settings);
        private readonly ActivityBarButton themeToggleButton = new ActivityBarButton(ActivityBarButtonKind.ThemeToggle);
        private readonly System.Windows.Forms.Timer templateSaveTimer = new System.Windows.Forms.Timer { Interval = 400 };
        private readonly AddTextButton addTemplateButton = new AddTextButton();
        private Control? windowContent;
        private TemplateFlowLayoutPanel templateTabs = null!;
        private EditorFramePanel? editorFrame;
        private Panel settingsView = null!;
        private CancellationTokenSource? cts;
        private readonly Random rng = new Random();
        private bool hotkeysRegistered;
        private bool loadingTemplate;

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
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.PaneBorder;
            Font = new Font("Segoe UI", UiTypography.Body);
            maximizeButton.Click += (_, __) => ToggleWindowState();
            Resize += (_, __) =>
            {
                bool maximized = WindowState == FormWindowState.Maximized;
                maximizeButton.IsMaximized = maximized;
                if (windowContent != null)
                    windowContent.Margin = maximized ? Padding.Empty : new Padding(1);
            };

            themeToggleButton.IsDarkTheme = Theme.Dark;
            themeToggleButton.Click += (_, __) => ToggleTheme();
            settingsButton.IsActive = true;

            cpm.Minimum = 30;
            cpm.Maximum = 20000;
            cpm.Value = 1200;
            startDelay.Minimum = 0;
            startDelay.Maximum = 30;
            startDelay.Value = 3;
            jitter.Minimum = 0;
            jitter.Maximum = 80;
            jitter.Value = 5;
            jitter.ValueChanged += (_, __) => jitterValue.Text = jitter.Value + "%";
            jitterValue.Text = jitter.Value + "%";
            cpm.ValueChanged += (_, __) => cpmIndicator.Text = $"{cpm.Value:N0} CPM";

            text.Multiline = true;
            text.AcceptsReturn = true;
            text.AcceptsTab = true;
            text.ScrollBars = ScrollBars.None;
            text.WordWrap = true;
            text.Font = new Font("Segoe UI", UiTypography.Editor);
            text.BackColor = Theme.Editor;
            text.ForeColor = Theme.Text;
            text.BorderStyle = BorderStyle.None;
            text.Dock = DockStyle.Fill;
            text.PlaceholderText = "Paste or type the text you want TYPR to type for you...";
            text.TextChanged += (_, __) =>
            {
                characterCount.Text = $"{text.TextLength:N0} chars";
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

            pressEnter.TabStop = false;
            pressEnter.Cursor = Cursors.Hand;

            start.Text = "Start typing";
            start.AccessibleName = $"Start typing ({FormatHotkey(Theme.Settings.StartModifiers, Theme.Settings.StartKey)})";
            start.Click += StartButton_Click;

            stop.Text = "Stop";
            stop.AccessibleName = $"Stop typing ({FormatHotkey(Theme.Settings.StopModifiers, Theme.Settings.StopKey)})";
            stop.Enabled = false;
            stop.MouseUp += StopButton_MouseUp;

            ConfigureHotkeyButton(startHotkey, "Start typing", Theme.Settings.StartModifiers, Theme.Settings.StartKey);
            startHotkey.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && startHotkey.ClientRectangle.Contains(e.Location))
                    ChangeHotkey(true);
            };
            ConfigureHotkeyButton(stopHotkey, "Stop", Theme.Settings.StopModifiers, Theme.Settings.StopKey);
            stopHotkey.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && stopHotkey.ClientRectangle.Contains(e.Location))
                    ChangeHotkey(false);
            };

            status.Text = "Ready — focus the target window during the countdown";
            status.AutoSize = false;
            status.AutoEllipsis = true;
            status.Dock = DockStyle.Fill;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = Theme.Text2;
            status.Padding = new Padding(0, 8, 0, 0);
            status.Margin = Padding.Empty;

            characterCount.Text = "0 chars";
            characterCount.Dock = DockStyle.Fill;
            characterCount.ForeColor = Theme.Text3;
            characterCount.TextAlign = ContentAlignment.MiddleRight;
            characterCount.Margin = Padding.Empty;
            characterCount.Padding = Padding.Empty;
            loadingTemplate = true;
            text.Text = ActiveTemplate.Text;
            loadingTemplate = false;
            characterCount.Text = $"{text.TextLength:N0} chars";

            cpmIndicator.Text = $"{cpm.Value:N0} CPM";
            cpmIndicator.Dock = DockStyle.Fill;
            cpmIndicator.ForeColor = Theme.Text3;
            cpmIndicator.TextAlign = ContentAlignment.MiddleRight;
            cpmIndicator.Margin = Padding.Empty;
            cpmIndicator.Padding = Padding.Empty;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.TitleBar,
                ColumnCount = 1,
                RowCount = 2,
                Margin = WindowState == FormWindowState.Maximized ? Padding.Empty : new Padding(1),
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildTitleBar(), 0, 0);
            root.Controls.Add(BuildContent(), 0, 1);
            Controls.Add(root);
            windowContent = root;
            FormClosed += (_, __) =>
            {
                templateSaveTimer.Stop();
                SaveActiveTemplate();
                Theme.SaveSettings();
                templateSaveTimer.Dispose();
            };

            UpdateThemeToggleButton();
        }

        // ---------- Content layout ----------

        private Control BuildContent()
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Editor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ActivityBarWidth));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            content.Controls.Add(BuildActivityBar(), 0, 0);
            content.Controls.Add(BuildSidebar(), 1, 0);
            content.Controls.Add(BuildEditorGroup(), 2, 0);
            return content;
        }

        private Control BuildActivityBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.ActivityBar,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            settingsButton.Margin = Padding.Empty;
            bar.Controls.Add(settingsButton, 0, 0);

            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.ActivityBar,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            themeToggleButton.Margin = Padding.Empty;
            bottom.Controls.Add(themeToggleButton, 0, 0);
            bottom.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.ActivityBar, Margin = Padding.Empty }, 0, 1);
            bar.Controls.Add(bottom, 0, 1);
            return bar;
        }

        private Control BuildSidebar()
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Editor,
                Padding = new Padding(UiSpacing.Small, UiSpacing.Small, UiSpacing.XSmall, UiSpacing.Small),
                Margin = Padding.Empty
            };

            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.SideBar,
                BorderColor = Theme.PaneBorder,
                CornerRadius = 10,
                Padding = new Padding(10),
                Margin = Padding.Empty
            };

            var sidebarContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.SideBar,
                Margin = Padding.Empty,
                Padding = new Padding(UiSpacing.Large, 10, UiSpacing.Large, 0)
            };
            settingsView = BuildSettingsView();
            settingsView.Dock = DockStyle.Fill;
            sidebarContent.Controls.Add(settingsView);
            card.Controls.Add(sidebarContent);
            sidebar.Controls.Add(card);
            return sidebar;
        }

        private static Label SectionHeader(string title)
        {
            return new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Caption, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(UiSpacing.XSmall, 0, 0, UiSpacing.XSmall)
            };
        }

        private Panel BuildSettingsView()
        {
            var view = new Panel { BackColor = Theme.SideBar, Margin = Padding.Empty };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 13,
                BackColor = Theme.SideBar,
                Margin = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // TYPING header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // speed
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // delay
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // jitter
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));   // divider
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));   // OPTIONS header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // toggle row
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));   // divider
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));   // SHORTCUTS header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // start row
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // stop row
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // hint
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(SectionHeader("TYPING"), 0, 0);
            layout.Controls.Add(CreateSettingField("Typing speed (characters per minute)", cpm), 0, 1);
            layout.Controls.Add(CreateSettingField("Start delay (seconds)", startDelay), 0, 2);
            layout.Controls.Add(CreateSliderField("Typing variation — humanize pacing", jitter, jitterValue), 0, 3);
            layout.Controls.Add(Divider(), 0, 4);

            layout.Controls.Add(SectionHeader("OPTIONS"), 0, 5);
            layout.Controls.Add(CreateToggleRow("Press Enter when finished"), 0, 6);
            layout.Controls.Add(Divider(), 0, 7);

            layout.Controls.Add(SectionHeader("SHORTCUTS"), 0, 8);
            layout.Controls.Add(ShortcutRow("Start typing", startHotkey), 0, 9);
            layout.Controls.Add(ShortcutRow("Stop typing", stopHotkey), 0, 10);

            var hint = new Label
            {
                Text = "Click a shortcut to rebind it.",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(UiSpacing.XSmall, UiSpacing.Small, 0, 0)
            };
            layout.Controls.Add(hint, 0, 11);

            view.Controls.Add(layout);
            return view;
        }

        private Control BuildEditorGroup()
        {
            var group = new EditorGroupPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Editor,
                BorderColor = Color.Transparent,
                CornerRadius = 10,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
            group.Controls.Add(BuildTabStrip(), 0, 0);
            group.Controls.Add(BuildEditorSurface(), 0, 1);
            group.Controls.Add(BuildEditorActions(), 0, 2);
            return group;
        }

        private Control BuildEditorActions()
        {
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Theme.Editor,
                Margin = new Padding(UiSpacing.XSmall, UiSpacing.XSmall, UiSpacing.XSmall, 10),
                Padding = new Padding(2, 4, 2, 4)
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));

            var statusItem = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Editor,
                Margin = new Padding(0, 0, UiSpacing.Small, 0),
                Padding = Padding.Empty
            };
            statusItem.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            statusItem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusItem.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            statusItem.Controls.Add(new Label
            {
                Text = "●",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Positive,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            }, 0, 0);
            statusItem.Controls.Add(status, 1, 0);
            actions.Controls.Add(statusItem, 0, 0);

            characterCount.Margin = new Padding(UiSpacing.Small, 0, UiSpacing.Small, 0);
            actions.Controls.Add(characterCount, 1, 0);
            cpmIndicator.Margin = new Padding(UiSpacing.Small, 0, UiSpacing.Small, 0);
            actions.Controls.Add(cpmIndicator, 2, 0);

            stop.Text = "Stop";
            stop.Dock = DockStyle.Fill;
            stop.Margin = new Padding(UiSpacing.Small, 0, UiSpacing.Small, 0);
            StyleEditorStopButton(stop);
            actions.Controls.Add(stop, 3, 0);

            start.Text = "Start typing";
            start.Dock = DockStyle.Fill;
            start.Margin = new Padding(UiSpacing.Small, 0, UiSpacing.Small, 0);
            StyleEditorStartButton(start);
            actions.Controls.Add(start, 4, 0);
            return actions;
        }

        private Control BuildTabStrip()
        {
            var strip = new TabContainerPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.TabContainer,
                BorderColor = Theme.PaneBorder,
                Margin = new Padding(4, 8, 8, 0),
                Padding = Padding.Empty
            };

            templateTabs = new TemplateFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Theme.TabContainer,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BorderColor = Theme.PaneBorder
            };
            templateTabs.Dock = DockStyle.Fill;
            addTemplateButton.Text = "+";
            addTemplateButton.AccessibleName = "Add text";
            addTemplateButton.AccessibleRole = AccessibleRole.PushButton;
            addTemplateButton.Size = new Size(28, 28);
            addTemplateButton.Margin = new Padding(2, 1, 2, 1);
            addTemplateButton.BackColor = Color.Transparent;
            addTemplateButton.ForeColor = Theme.Text2;
            addTemplateButton.Font = new Font("Segoe UI", 16f);
            addTemplateButton.TextAlign = ContentAlignment.MiddleCenter;
            addTemplateButton.Cursor = Cursors.Hand;
            addTemplateButton.TabStop = false;
            addTemplateButton.Click += (_, __) => AddTextTemplate();

            strip.Controls.Add(templateTabs);
            RefreshTemplateTabs();
            return strip;
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
                control.Dispose();
            }

            for (int i = 0; i < Theme.Settings.TextTemplates.Count; i++)
            {
                int templateIndex = i;
                bool isActive = i == Theme.Settings.ActiveTextTemplate;
                var tab = new EditorTabPanel
                {
                    Text = Theme.Settings.TextTemplates[i].Name,
                    Width = 152,
                    Height = 36,
                    BackColor = isActive ? Theme.ActiveTab : Theme.TabContainer,
                    IsActive = isActive,
                    BorderColor = Theme.PaneBorder,
                    CornerRadius = 6,
                    ForeColor = isActive ? Theme.Text : Theme.Text2,
                    Font = new Font("Segoe UI", UiTypography.Body),
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    Cursor = Cursors.Hand,
                    AccessibleRole = AccessibleRole.PageTab,
                    AccessibleName = $"{Theme.Settings.TextTemplates[i].Name}, text tab. Double-click to rename; close button deletes it."
                };
                tab.Click += (_, __) => SelectTextTemplate(templateIndex);
                tab.CloseRequested += (_, __) => DeleteTextTemplate(templateIndex);
                tab.RenameRequested += (_, __) => RenameTextTemplate(templateIndex);
                templateTabs.Controls.Add(tab);
                tab.Enabled = cts == null;
            }
            templateTabs.Controls.Add(addTemplateButton);
            addTemplateButton.Enabled = cts == null;
            templateTabs.ResumeLayout(true);
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
                status.Text = "Stop typing before switching text tabs.";
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
            characterCount.Text = $"{text.TextLength:N0} chars";
            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        private void DeleteTextTemplate(int index)
        {
            if (cts != null)
            {
                status.Text = "Stop typing before deleting text.";
                return;
            }

            if (Theme.Settings.TextTemplates.Count <= 1)
            {
                status.Text = "At least one text tab must remain.";
                return;
            }

            templateSaveTimer.Stop();
            SaveActiveTemplate();
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
                characterCount.Text = $"{text.TextLength:N0} chars";
            }

            RefreshTemplateTabs();
            Theme.SaveSettings();
            text.Focus();
        }

        private void RenameTextTemplate(int index)
        {
            if (cts != null || index < 0 || index >= Theme.Settings.TextTemplates.Count)
                return;

            using var dialog = new Form
            {
                Text = "Rename text",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(340, 112),
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                BackColor = Theme.SideBar,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body)
            };
            var nameInput = new TextBox
            {
                Text = Theme.Settings.TextTemplates[index].Name,
                Location = new Point(12, 12),
                Width = 316,
                MaxLength = 80,
                BackColor = Theme.Input,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            var saveButton = new Button
            {
                Text = "Save",
                Location = new Point(172, 64),
                Size = new Size(75, 30)
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(253, 64),
                Size = new Size(75, 30)
            };
            dialog.Controls.Add(nameInput);
            dialog.Controls.Add(saveButton);
            dialog.Controls.Add(cancelButton);
            dialog.AcceptButton = saveButton;
            dialog.CancelButton = cancelButton;
            saveButton.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(nameInput.Text))
                {
                    status.Text = "Text name cannot be empty.";
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
                status.Text = "Stop typing before adding text.";
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
            characterCount.Text = "0 chars";
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

        private Control BuildEditorSurface()
        {
            var surfaceHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Editor,
                Padding = new Padding(4, 0, 8, 4),
                Margin = Padding.Empty
            };
            editorFrame = new EditorFramePanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Editor,
                BorderColor = Theme.PaneBorder,
                CornerRadius = 10,
                Padding = new Padding(UiSpacing.Large, UiSpacing.Medium, UiSpacing.Large, UiSpacing.Small),
                Margin = Padding.Empty
            };
            editorFrame.Controls.Add(text);
            surfaceHost.Controls.Add(editorFrame);
            return surfaceHost;
        }

        // ---------- Title bar ----------

        private Control BuildTitleBar()
        {
            var titleBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.TitleBar,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));

            var identity = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Theme.TitleBar,
                Padding = new Padding(UiSpacing.Medium, 0, 0, 0),
                Margin = Padding.Empty
            };
            var icon = new Panel
            {
                BackColor = Theme.Accent,
                Width = 16,
                Height = 16,
                Margin = new Padding(0, 10, UiSpacing.Small, 0)
            };
            icon.Controls.Add(new Label
            {
                Text = "T",
                Dock = DockStyle.Fill,
                BackColor = Theme.Accent,
                ForeColor = Theme.OnAccent,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            var appName = new Label
            {
                Text = "TYPR",
                AutoSize = false,
                Width = 40,
                Height = 36,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            };
            var subtitle = new Label
            {
                Text = "— auto typer",
                AutoSize = false,
                Width = 100,
                Height = 36,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            };
            identity.Controls.Add(icon);
            identity.Controls.Add(appName);
            identity.Controls.Add(subtitle);
            titleBar.Controls.Add(identity, 0, 0);

            var windowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 138,
                Height = 36,
                BackColor = Theme.TitleBar,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var minimize = new WindowCaptionButton(WindowCaptionButtonKind.Minimize);
            minimize.Click += (_, __) => WindowState = FormWindowState.Minimized;
            maximizeButton.Dock = DockStyle.None;
            var close = new WindowCaptionButton(WindowCaptionButtonKind.Close);
            close.Click += (_, __) => Close();
            windowButtons.Controls.Add(close);
            windowButtons.Controls.Add(maximizeButton);
            windowButtons.Controls.Add(minimize);
            titleBar.Controls.Add(windowButtons, 2, 0);

            titleBar.MouseDown += TitleBar_MouseDown;
            identity.MouseDown += TitleBar_MouseDown;
            icon.MouseDown += TitleBar_MouseDown;
            appName.MouseDown += TitleBar_MouseDown;
            subtitle.MouseDown += TitleBar_MouseDown;
            titleBar.DoubleClick += TitleBar_DoubleClick;
            identity.DoubleClick += TitleBar_DoubleClick;
            appName.DoubleClick += TitleBar_DoubleClick;
            subtitle.DoubleClick += TitleBar_DoubleClick;
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
                if (id == HOTKEY_START) _ = StartTypingAsync();
                if (id == HOTKEY_STOP) StopTyping();
            }

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if (m.Result != (IntPtr)HTCLIENT && m.Result != (IntPtr)HTNOWHERE) return;

                Point point = PointToClient(Cursor.Position);
                bool left = point.X < ResizeBorderWidth;
                bool right = point.X >= ClientSize.Width - ResizeBorderWidth;
                bool top = point.Y < ResizeBorderWidth;
                bool bottom = point.Y >= ClientSize.Height - ResizeBorderWidth;

                if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                return;
            }

            base.WndProc(ref m);
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
                parameters.Style &= ~(WS_CAPTION | WS_SYSMENU);
                parameters.Style |= WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                return parameters;
            }
        }

        // ---------- Building blocks ----------

        private static Control Divider()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.PaneBorder,
                Margin = new Padding(UiSpacing.XSmall, UiSpacing.XSmall, UiSpacing.XSmall, UiSpacing.XSmall)
            };
        }

        private Control CreateSettingField(string title, NumberInput input)
        {
            var field = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.SideBar,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(UiSpacing.XSmall, 0, UiSpacing.XSmall, UiSpacing.XSmall)
            };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, UiSpacing.Small));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            field.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text2,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = Padding.Empty
            }, 0, 0);
            input.Dock = DockStyle.Fill;
            input.Margin = Padding.Empty;
            field.Controls.Add(input, 0, 2);
            return field;
        }

        private Control CreateSliderField(string title, Slider slider, Label valueLabel)
        {
            var field = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.SideBar,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(UiSpacing.XSmall, 0, UiSpacing.XSmall, UiSpacing.XSmall)
            };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            field.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text2,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = Padding.Empty
            }, 0, 0);
            var line = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.SideBar,
                Margin = new Padding(0, UiSpacing.XSmall, 0, 0)
            };
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            slider.Dock = DockStyle.Fill;
            slider.Margin = Padding.Empty;
            line.Controls.Add(slider, 0, 0);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.ForeColor = Theme.Text;
            valueLabel.Font = new Font("Segoe UI", UiTypography.Body);
            valueLabel.TextAlign = ContentAlignment.MiddleRight;
            valueLabel.Margin = new Padding(UiSpacing.Small, 0, UiSpacing.XSmall, 0);
            line.Controls.Add(valueLabel, 1, 0);
            field.Controls.Add(line, 0, 1);
            return field;
        }

        private Control CreateToggleRow(string caption)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.SideBar,
                Margin = new Padding(UiSpacing.XSmall, 0, UiSpacing.XSmall, 0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            row.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            pressEnter.Width = 40;
            pressEnter.Height = 22;
            pressEnter.Margin = new Padding(0, UiSpacing.Small, UiSpacing.XSmall, 0);
            row.Controls.Add(pressEnter, 1, 0);
            return row;
        }

        private static Control ShortcutRow(string caption, RoundedButton key)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.SideBar,
                Margin = new Padding(UiSpacing.XSmall, UiSpacing.XSmall, UiSpacing.XSmall, 0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            key.Dock = DockStyle.Fill;
            key.Margin = new Padding(0, 2, 0, 2);
            row.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            row.Controls.Add(key, 1, 0);
            return row;
        }

        private static void ConfigureHotkeyButton(RoundedButton button, string name, int modifiers, int key)
        {
            button.CornerRadius = 5;
            StyleKeycapButton(button);
            UpdateHotkeyButton(button, name, modifiers, key);
        }

        private static void UpdateHotkeyButton(RoundedButton button, string name, int modifiers, int key)
        {
            button.Text = FormatHotkey(modifiers, key);
            button.AccessibleName = $"{name} hotkey: {button.Text}. Click to change.";
        }

        private static string FormatHotkey(int modifiers, int keyValue)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");

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
                _ when key >= Keys.D0 && key <= Keys.D9 => key.ToString().Substring(1),
                _ => key.ToString()
            };
            parts.Add(keyName);
            return string.Join("+", parts);
        }

        private static void StyleEditorStartButton(RoundedButton button)
        {
            button.CornerRadius = 6;
            button.BackColor = Theme.Accent;
            button.ForeColor = Theme.OnAccent;
            button.BorderColor = Color.Transparent;
            button.Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.HoverTint = Theme.StatusBarHover;
            button.DownTint = Color.FromArgb(0, 80, 134);
        }

        private static void StyleEditorStopButton(RoundedButton button)
        {
            button.CornerRadius = 6;
            button.BackColor = Theme.Input;
            button.ForeColor = Theme.Text;
            button.BorderColor = Theme.Border;
            button.Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.HoverTint = Theme.SurfaceMuted;
            button.DownTint = Theme.Border;
        }

        private static void StyleKeycapButton(RoundedButton button)
        {
            button.CornerRadius = 5;
            button.BackColor = Theme.Input;
            button.ForeColor = Theme.Text;
            button.BorderColor = Theme.Border;
            button.Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.HoverTint = Theme.SurfaceMuted;
            button.DownTint = Theme.Border;
        }

        // ---------- Hotkeys ----------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TryRegisterHotKeys();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetWindowAppearance(Handle, Theme.PaneBorder);
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
                status.Text = "Shortcuts unavailable — use the buttons.";
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

        private void StopButton_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && stop.ClientRectangle.Contains(e.Location))
                StopTyping();
        }

        private void ChangeHotkey(bool isStart)
        {
            TryUnregisterHotKeys();
            using var dialog = new Form
            {
                Text = isStart ? "Set start hotkey" : "Set stop hotkey",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(360, 116),
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                KeyPreview = true,
                BackColor = Theme.SideBar,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body)
            };
            var instruction = new Label
            {
                Text = "Press a key combination (for example Ctrl + .). Esc cancels.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Text,
                Padding = new Padding(UiSpacing.Medium)
            };
            dialog.Controls.Add(instruction);

            int newModifiers = 0;
            int newKey = 0;
            dialog.KeyDown += (_, e) =>
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.Escape)
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    dialog.Close();
                    return;
                }

                if (IsModifierKey(e.KeyCode))
                {
                    instruction.Text = "Hold Ctrl, Alt, or Shift, then press a key. Esc cancels.";
                    return;
                }

                newModifiers = ToHotkeyModifiers(e.Modifiers);
                newKey = (int)e.KeyCode;
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };

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
            if (newModifiers == 0 && ((Keys)newKey < Keys.F1 || (Keys)newKey > Keys.F24))
            {
                TryRegisterHotKeys();
                MessageBox.Show(this, "Use a modifier with regular keys so the shortcut does not intercept normal typing.",
                    "TYPR - modifier required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (isStart
                ? newKey == Theme.Settings.StopKey && newModifiers == Theme.Settings.StopModifiers
                : newKey == Theme.Settings.StartKey && newModifiers == Theme.Settings.StartModifiers)
            {
                TryRegisterHotKeys();
                MessageBox.Show(this, "Start and stop shortcuts must be different.", "TYPR - shortcut already in use",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, "Windows could not register that shortcut. It may already be in use.",
                    "TYPR - shortcut unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UpdateHotkeyButton(startHotkey, "Start typing", Theme.Settings.StartModifiers, Theme.Settings.StartKey);
            UpdateHotkeyButton(stopHotkey, "Stop", Theme.Settings.StopModifiers, Theme.Settings.StopKey);
            start.AccessibleName = $"Start typing ({startHotkey.Text})";
            stop.AccessibleName = $"Stop typing ({stopHotkey.Text})";
            Theme.SaveSettings();
            status.Text = $"Shortcuts updated — {startHotkey.Text} starts, {stopHotkey.Text} stops.";
        }

        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu;
        }

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
            bool previousDark = Theme.Dark;
            Theme.SetMode(previousDark ? "Light" : "Dark");
            ApplyTheme(this, previousDark);
            if (IsHandleCreated) NativeMethods.SetWindowAppearance(Handle, Theme.PaneBorder);
            Theme.SaveSettings();
            UpdateThemeToggleButton();
        }

        private void UpdateThemeToggleButton()
        {
            themeToggleButton.IsDarkTheme = Theme.Dark;
            themeToggleButton.AccessibleName = Theme.Dark
                ? "Dark mode. Click to switch to light mode."
                : "Light mode. Click to switch to dark mode.";
            themeToggleButton.Invalidate();
        }

        private void ApplyTheme(Control root, bool previousDark)
        {
            ApplyThemeToControl(root, previousDark);
            cpm.ApplyTheme();
            startDelay.ApplyTheme();
            themeToggleButton.IsDarkTheme = Theme.Dark;
            Invalidate(true);
        }

        private void ApplyThemeToControl(Control control, bool previousDark)
        {
            bool isEditorSurface = ReferenceEquals(control, text);
            if (!isEditorSurface)
                control.BackColor = Theme.MapBackColor(control.BackColor, previousDark);
            control.ForeColor = Theme.MapForeColor(control.ForeColor, previousDark);

            if (control is RoundedPanel panel)
                panel.BorderColor = Theme.MapBorderColor(panel.BorderColor, previousDark);

            if (control is TabContainerPanel tabContainer)
            {
                tabContainer.BackColor = Theme.TabContainer;
                tabContainer.BorderColor = Theme.PaneBorder;
            }

            if (control is TemplateFlowLayoutPanel templateFlow)
                templateFlow.BorderColor = Theme.MapBorderColor(templateFlow.BorderColor, previousDark);

            if (control is EditorTabPanel editorTab)
            {
                editorTab.BorderColor = Theme.MapBorderColor(editorTab.BorderColor, previousDark);
                editorTab.BackColor = editorTab.IsActive ? Theme.ActiveTab : Theme.TabContainer;
            }

            if (control is EditorFramePanel editorFrame)
                editorFrame.BorderColor = Theme.MapBorderColor(editorFrame.BorderColor, previousDark);

            if (control is RoundedButton button)
            {
                button.BorderColor = Theme.MapBorderColor(button.BorderColor, previousDark);
                button.HoverTint = Theme.MapBackColor(button.HoverTint, previousDark);
                button.DownTint = Theme.MapBackColor(button.DownTint, previousDark);
            }

            if (control is TextBox input)
            {
                input.BackColor = ReferenceEquals(input, text) ? Theme.Editor : Theme.Input;
                input.ForeColor = Theme.Text;
            }

            foreach (Control child in control.Controls)
                ApplyThemeToControl(child, previousDark);

            if (isEditorSurface) control.BackColor = Theme.Editor;
            control.Invalidate();
        }

        // ---------- Typing engine (unchanged) ----------

        private async Task StartTypingAsync()
        {
            if (cts != null) return;
            templateSaveTimer.Stop();
            SaveActiveTemplate();
            Theme.SaveSettings();
            string payload = ActiveTemplate.Text;
            if (string.IsNullOrEmpty(payload)) { status.Text = "Nothing to type."; return; }

            cts = new CancellationTokenSource();
            start.Enabled = false;
            stop.Enabled = true;
            SetTemplateTabsEnabled(false);
            try
            {
                int delay = (int)startDelay.Value;
                for (int i = delay; i > 0; i--)
                {
                    status.Text = $"Starting in {i}s — focus the target window...";
                    await Task.Delay(1000, cts.Token);
                }

                status.Text = $"Typing... {FormatHotkey(Theme.Settings.StopModifiers, Theme.Settings.StopKey)} to stop.";
                double baseDelayMs = 60000.0 / (double)cpm.Value;
                double jitterPct = (double)jitter.Value / 100.0;

                foreach (char ch in payload)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    if (ch == '\r') continue;
                    if (ch == '\n') NativeMethods.SendVirtualKey((ushort)Keys.Enter);
                    else if (ch == '\t') NativeMethods.SendVirtualKey((ushort)Keys.Tab);
                    else NativeMethods.SendUnicodeChar(ch);

                    double factor = 1.0 + ((rng.NextDouble() * 2.0 - 1.0) * jitterPct);
                    int wait = Math.Max(0, (int)(baseDelayMs * factor));
                    if (wait > 0) await Task.Delay(wait, cts.Token);
                }

                if (pressEnter.Checked) NativeMethods.SendVirtualKey((ushort)Keys.Enter);
                status.Text = "Completed.";
            }
            catch (OperationCanceledException)
            {
                status.Text = "Stopped.";
            }
            catch (InvalidOperationException ex)
            {
                status.Text = "Typing failed.";
                MessageBox.Show(this, ex.Message, "TYPR - input failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cts.Dispose();
                cts = null;
                start.Enabled = true;
                stop.Enabled = false;
                SetTemplateTabsEnabled(true);
            }
        }

        private void StopTyping()
        {
            if (cts == null) return;

            status.Text = "Stopping...";
            cts.Cancel();
        }
    }

    // ---------- Controls ----------

    internal sealed class EditorGroupPanel : TableLayoutPanel
    {
        public int CornerRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = Color.Transparent;

        public EditorGroupPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            using var path = RoundedPanel.CreatePath(ClientRectangle, CornerRadius);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor.A == 0 || Width < 2 || Height < 2) return;

            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            using var path = RoundedPanel.CreatePath(bounds, CornerRadius);
            using var pen = new Pen(BorderColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
    }

    internal sealed class TemplateFlowLayoutPanel : FlowLayoutPanel
    {
        public Color BorderColor { get; set; } = Color.Transparent;
        private const int CornerRadius = 6;

        public TemplateFlowLayoutPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var parentFill = new SolidBrush(Parent?.Parent?.BackColor ?? Parent?.BackColor ?? BackColor);
            e.Graphics.FillRectangle(parentFill, ClientRectangle);
            using var path = TabContainerPanel.CreateTopRoundedPath(ClientRectangle);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TabContainerPanel.TabBorderRenderer.Draw(
                e.Graphics, ClientRectangle, CornerRadius, BorderColor, drawBottom: true);
        }
    }

    internal sealed class EditorTabPanel : Panel
    {
        private const int CloseButtonSize = 22;
        private bool closeButtonHovered;

        public bool IsActive { get; set; }
        public Color BorderColor { get; set; } = Color.Transparent;
        public int CornerRadius { get; set; } = 6;
        public event EventHandler? CloseRequested;
        public event EventHandler? RenameRequested;

        public EditorTabPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var parentFill = new SolidBrush(Parent?.Parent?.BackColor ?? Parent?.BackColor ?? BackColor);
            e.Graphics.FillRectangle(parentFill, ClientRectangle);
            using var path = TabContainerPanel.CreateTopRoundedPath(ClientRectangle);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TabContainerPanel.TabBorderRenderer.Draw(
                e.Graphics, ClientRectangle, CornerRadius, BorderColor, drawBottom: !IsActive);
            var textBounds = ClientRectangle;
            textBounds.X += UiSpacing.Medium;
            textBounds.Width -= UiSpacing.Medium + CloseButtonSize + 4;
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            Rectangle closeBounds = GetCloseButtonBounds();
            if (closeButtonHovered)
            {
                using var hoverFill = new SolidBrush(Theme.SurfaceMuted);
                e.Graphics.FillEllipse(hoverFill, closeBounds);
            }

            int inset = 7;
            using var closePen = new Pen(ForeColor, 1.4f);
            e.Graphics.DrawLine(closePen,
                closeBounds.Left + inset, closeBounds.Top + inset,
                closeBounds.Right - inset, closeBounds.Bottom - inset);
            e.Graphics.DrawLine(closePen,
                closeBounds.Right - inset, closeBounds.Top + inset,
                closeBounds.Left + inset, closeBounds.Bottom - inset);
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
            bool isHovered = GetCloseButtonBounds().Contains(e.Location);
            if (isHovered != closeButtonHovered)
            {
                closeButtonHovered = isHovered;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (closeButtonHovered)
            {
                closeButtonHovered = false;
                Invalidate();
            }
        }

        private Rectangle GetCloseButtonBounds()
        {
            return new Rectangle(
                Width - CloseButtonSize - 4,
                Math.Max(0, (Height - CloseButtonSize) / 2),
                CloseButtonSize,
                CloseButtonSize);
        }

        private Rectangle GetTextBounds()
        {
            return new Rectangle(
                UiSpacing.Medium,
                0,
                Math.Max(0, Width - UiSpacing.Medium - CloseButtonSize - 4),
                Height);
        }
    }

    internal sealed class AddTextButton : Label
    {
        private readonly System.Windows.Forms.Timer hoverAnimation = new System.Windows.Forms.Timer
        {
            Interval = 15
        };
        private int hoverOpacity;
        private int hoverTarget;

        public AddTextButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            hoverAnimation.Tick += (_, __) =>
            {
                hoverOpacity = Math.Clamp(hoverOpacity + hoverTarget, 0, 180);
                Invalidate();
                if ((hoverTarget > 0 && hoverOpacity == 180) ||
                    (hoverTarget < 0 && hoverOpacity == 0))
                    hoverAnimation.Stop();
            };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Draw hover circle centered on the control center to match the centered '+' glyph
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (hoverOpacity > 0)
            {
                const int diameter = 24;
                int centerX = Width / 2;
                int centerY = Height / 2;
                var bounds = new Rectangle(centerX - diameter / 2, centerY - diameter / 2, diameter, diameter);
                using var hoverFill = new SolidBrush(Color.FromArgb(hoverOpacity, Theme.SurfaceMuted));
                e.Graphics.FillEllipse(hoverFill, bounds);
            }

            // Draw the '+' glyph tightly centered in the control to ensure alignment with the hover circle.
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            AnimateHover(1);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            AnimateHover(-1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                hoverAnimation.Dispose();
            base.Dispose(disposing);
        }

        private void AnimateHover(int direction)
        {
            hoverTarget = direction * 30;
            hoverAnimation.Start();
        }
    }

    internal sealed class TabContainerPanel : Panel
    {
        public Color BorderColor { get; set; } = Color.Transparent;
        private const int CornerRadius = 6;

        public TabContainerPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var createParams = base.CreateParams;
                createParams.ExStyle |= 0x02000000;
                return createParams;
            }
        }

        internal static class TabBorderRenderer
        {
            public static void Draw(
                Graphics graphics, Rectangle bounds, int cornerRadius, Color color, bool drawBottom = false)
            {
                if (color.A == 0 || bounds.Width < 2 || bounds.Height < 2) return;

                using var pen = new Pen(color);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int radius = Math.Max(0, Math.Min(cornerRadius, Math.Min(bounds.Width / 2, bounds.Height)));
                int right = bounds.Right - 1;
                int bottom = bounds.Bottom - 1;
                if (radius == 0)
                {
                    graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bottom);
                    graphics.DrawLine(pen, bounds.Left, bounds.Top, right, bounds.Top);
                    graphics.DrawLine(pen, right, bounds.Top, right, bottom);
                    if (drawBottom)
                        graphics.DrawLine(pen, bounds.Left, bottom, right, bottom);
                    return;
                }

                int diameter = radius * 2;
                graphics.DrawArc(pen, bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                graphics.DrawLine(pen, bounds.Left + radius, bounds.Top, right - radius, bounds.Top);
                graphics.DrawArc(pen, right - diameter, bounds.Top, diameter, diameter, 270, 90);
                graphics.DrawLine(pen, bounds.Left, bounds.Top + radius, bounds.Left, bottom);
                graphics.DrawLine(pen, right, bounds.Top + radius, right, bottom);
                if (drawBottom)
                    graphics.DrawLine(pen, bounds.Left, bottom, right, bottom);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var parentFill = new SolidBrush(Parent?.BackColor ?? BackColor);
            e.Graphics.FillRectangle(parentFill, ClientRectangle);
            using var path = CreateTopRoundedPath(ClientRectangle);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor.A == 0 || Width < 2 || Height < 2) return;

            using var pen = new Pen(BorderColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int radius = Math.Min(CornerRadius, Math.Min(Width / 2, Height));
            if (radius == 0)
            {
                e.Graphics.DrawLine(pen, 0, 0, 0, Height - 1);
                e.Graphics.DrawLine(pen, 0, 0, Width - 1, 0);
                e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height - 1);
                e.Graphics.DrawLine(pen, 0, Height - 1, Width - 1, Height - 1);
                return;
            }

            int diameter = radius * 2;
            e.Graphics.DrawArc(pen, 0, 0, diameter, diameter, 180, 90);
            e.Graphics.DrawLine(pen, radius, 0, Width - radius - 1, 0);
            e.Graphics.DrawArc(pen, Width - diameter - 1, 0, diameter, diameter, 270, 90);
            e.Graphics.DrawLine(pen, 0, radius, 0, Height - 1);
            e.Graphics.DrawLine(pen, Width - 1, radius, Width - 1, Height - 1);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width - 1, Height - 1);
        }

        internal static System.Drawing.Drawing2D.GraphicsPath CreateTopRoundedPath(Rectangle bounds)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;

            int radius = Math.Min(CornerRadius, Math.Min(bounds.Width / 2, bounds.Height));
            path.StartFigure();
            path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Top + radius);
            path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
            path.AddLine(bounds.Left + radius, bounds.Top, bounds.Right - radius, bounds.Top);
            path.AddArc(bounds.Right - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
            path.AddLine(bounds.Right, bounds.Top + radius, bounds.Right, bounds.Bottom);
            path.CloseFigure();
            return path;
        }

    }

    internal sealed class EditorFramePanel : Panel
    {
        public int CornerRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = Color.Transparent;

        public EditorFramePanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var parentFill = new SolidBrush(Parent?.BackColor ?? BackColor);
            e.Graphics.FillRectangle(parentFill, ClientRectangle);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(fill, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor.A == 0 || Width < 2 || Height < 2) return;

            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            using var path = CreateOpenTopPath(bounds, CornerRadius);
            using var pen = new Pen(BorderColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateOpenTopPath(
            Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;

            radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width / 2, bounds.Height / 2)));
            path.StartFigure();
            if (radius == 0)
            {
                path.AddLine(bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
                path.AddLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
                path.AddLine(bounds.Right, bounds.Bottom, bounds.Right, bounds.Top);
                return path;
            }

            int diameter = radius * 2;
            path.AddLine(bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - radius);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 180, -90);
            path.AddLine(bounds.Left + radius, bounds.Bottom, bounds.Right - radius, bounds.Bottom);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 90, -90);
            path.AddLine(bounds.Right, bounds.Bottom - radius, bounds.Right, bounds.Top);
            return path;
        }
    }

    internal class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 12;
        public Color BorderColor { get; set; } = Color.Transparent;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var path = CreatePath(ClientRectangle, CornerRadius))
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
            using (var path = CreatePath(bounds, CornerRadius))
            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        public static System.Drawing.Drawing2D.GraphicsPath CreatePath(Rectangle bounds, int radius)
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
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

    }

    internal sealed class RoundedButton : Button
    {
        private bool hovered;

        public int CornerRadius { get; set; } = 2;
        public Color BorderColor { get; set; } = Color.Transparent;
        public Color HoverTint { get; set; }
        public Color DownTint { get; set; }

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override bool ShowFocusCues => false;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
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
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            using (var path = RoundedPanel.CreatePath(bounds, CornerRadius))
            {
                Color fill;
                if (!Enabled) fill = Theme.SurfaceMuted;
                else if (hovered && MouseButtons == MouseButtons.Left) fill = DownTint.A > 0 ? DownTint : ControlPaint.Dark(BackColor, 0.08f);
                else if (hovered) fill = HoverTint.A > 0 ? HoverTint : ControlPaint.Light(BackColor, 0.08f);
                else fill = BackColor;

                using (var brush = new SolidBrush(fill))
                    e.Graphics.FillPath(brush, path);

                if (BorderColor.A > 0)
                    using (var pen = new Pen(BorderColor))
                        e.Graphics.DrawPath(pen, path);

                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    Font,
                    bounds,
                    Enabled ? ForeColor : Theme.Text3,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
            }
        }
    }

    internal sealed class ToggleCheckBox : Control
    {
        public bool Checked { get; set; }

        public ToggleCheckBox()
        {
            BackColor = Theme.SideBar;
            TabStop = false;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleName = "Press Enter when finished";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var track = new Rectangle(0, Math.Max(0, (Height - 20) / 2), 38, 20);
            using (var path = RoundedPanel.CreatePath(track, 10))
            using (var fill = new SolidBrush(Checked ? Theme.Accent : Theme.Input))
            {
                e.Graphics.FillPath(fill, path);
            }
            if (!Checked)
                using (var path = RoundedPanel.CreatePath(track, 10))
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawPath(pen, path);

            int thumbX = Checked ? 21 : 3;
            using (var thumb = new SolidBrush(Checked ? Theme.OnAccent : Theme.Text3))
            {
                e.Graphics.FillEllipse(thumb, thumbX, track.Y + 3, 14, 14);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            Invalidate();
            base.OnClick(e);
        }
    }

    internal sealed class NumberInput : UserControl
    {
        private readonly TextBox input = new TextBox();

        public event EventHandler? ValueChanged;
        private readonly RoundedPanel frame;
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

        public void ApplyTheme()
        {
            BackColor = Theme.SideBar;
            frame.BackColor = Theme.Input;
            frame.BorderColor = Theme.Border;
            input.BackColor = Theme.Input;
            input.ForeColor = Theme.Text;
            foreach (Control child in frame.Controls)
            {
                child.BackColor = Theme.Input;
                foreach (Control nested in child.Controls)
                    nested.BackColor = Theme.Input;
            }
            Invalidate(true);
        }

        public NumberInput()
        {
            BackColor = Theme.SideBar;
            Height = 28;
            MinimumSize = new Size(90, 28);

            frame = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Input,
                BorderColor = Theme.Border,
                CornerRadius = 6,
                Padding = new Padding(UiSpacing.Medium, 2, UiSpacing.Small, 2),
                Margin = Padding.Empty
            };
            var inputHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = frame.BackColor,
                Margin = Padding.Empty
            };
            input.BorderStyle = BorderStyle.None;
            input.BackColor = frame.BackColor;
            input.ForeColor = Theme.Text;
            input.Font = new Font("Segoe UI", UiTypography.Control);
            input.TextAlign = HorizontalAlignment.Left;
            input.Text = "0";
            input.Margin = Padding.Empty;
            input.Dock = DockStyle.None;
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.Height = input.PreferredHeight;
            inputHost.Resize += (_, __) =>
            {
                input.Width = inputHost.ClientSize.Width;
                input.Top = Math.Max(0, (inputHost.ClientSize.Height - input.Height) / 2);
            };
            input.GotFocus += (_, __) => frame.BorderColor = Theme.FocusBorder;
            input.LostFocus += (_, __) => frame.BorderColor = Theme.Border;
            input.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Up) { Value++; e.Handled = true; }
                if (e.KeyCode == Keys.Down) { Value--; e.Handled = true; }
                if (e.KeyCode == Keys.Enter) { CommitInput(); e.Handled = true; }
            };
            input.Leave += (_, __) => CommitInput();
            inputHost.Controls.Add(input);
            frame.Controls.Add(inputHost);
            Controls.Add(frame);
        }

        private void CommitInput()
        {
            if (decimal.TryParse(input.Text, out decimal parsed)) SetValue(parsed);
            else input.Text = value.ToString();
        }

        private void SetValue(decimal next)
        {
            value = Math.Max(minimum, Math.Min(maximum, next));
            input.Text = value.ToString();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal sealed class Slider : UserControl
    {
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
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Slider()
        {
            Height = 32;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        private double Fraction => maximum <= minimum ? 0 : (double)(val - minimum) / (maximum - minimum);

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.SideBar);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int pad = 8;
            int trackY = Height / 2 - 2;
            var track = new Rectangle(pad, trackY, Math.Max(1, Width - pad * 2), 4);
            using (var path = RoundedPanel.CreatePath(track, 2))
            using (var brush = new SolidBrush(Theme.Input))
                g.FillPath(brush, path);

            int fillWidth = (int)(track.Width * Fraction);
            if (fillWidth > 2)
            {
                var fill = new Rectangle(track.X, track.Y, fillWidth, track.Height);
                using (var path = RoundedPanel.CreatePath(fill, 2))
                using (var brush = new SolidBrush(Theme.Accent))
                    g.FillPath(brush, path);
            }

            int thumbX = track.X + fillWidth;
            int thumbR = 7;
            var thumbRect = new Rectangle(thumbX - thumbR, Height / 2 - thumbR, thumbR * 2, thumbR * 2);
            using (var brush = new SolidBrush(hovered || dragging || Focused ? Theme.Text2 : Theme.Text3))
                g.FillEllipse(brush, thumbRect);
            using (var pen = new Pen(Theme.Border))
                g.DrawEllipse(pen, thumbRect);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            dragging = true;
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
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Value--; e.Handled = true; }
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Value++; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        private void UpdateFromX(int x)
        {
            int pad = 8;
            int usable = Math.Max(1, Width - pad * 2);
            double frac = Math.Max(0, Math.Min(1, (x - pad) / (double)usable));
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
        public bool IsMaximized { get; set; }

        public WindowCaptionButton(WindowCaptionButtonKind kind)
        {
            Kind = kind;
            Dock = DockStyle.None;
            Width = 46;
            Height = 36;
            BackColor = Theme.TitleBar;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
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
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color hoverBg = Theme.Dark ? Color.FromArgb(74, 74, 74) : Color.FromArgb(202, 202, 202);
            Color background = hovered
                ? Kind == WindowCaptionButtonKind.Close
                    ? Color.FromArgb(196, 43, 28)
                    : hoverBg
                : BackColor;
            Color glyph = hovered && Kind == WindowCaptionButtonKind.Close ? Color.White : ForeColor;
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, ClientRectangle);

            using (var pen = new Pen(glyph, 1.6f))
            {
                int cx = Width / 2;
                int cy = Height / 2;
                if (Kind == WindowCaptionButtonKind.Minimize)
                {
                    e.Graphics.DrawLine(pen, cx - 5, cy + 3, cx + 5, cy + 3);
                }
                else if (Kind == WindowCaptionButtonKind.Maximize && IsMaximized)
                {
                    e.Graphics.DrawRectangle(pen, cx - 3, cy - 5, 7, 7);
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
            base.OnPaint(e);
        }
    }

    internal enum ActivityBarButtonKind
    {
        Settings,
        ThemeToggle
    }

    internal sealed class ActivityBarButton : Control
    {
        private bool hovered;

        public ActivityBarButtonKind Kind { get; }
        public bool IsActive { get; set; }
        public bool IsDarkTheme { get; set; }

        public ActivityBarButton(ActivityBarButtonKind kind)
        {
            Kind = kind;
            Dock = DockStyle.None;
            Width = 48;
            Height = 48;
            BackColor = Theme.ActivityBar;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color bg = hovered ? Color.FromArgb(62, 62, 62) : BackColor;
            using (var brush = new SolidBrush(bg))
                g.FillRectangle(brush, ClientRectangle);

            if (IsActive)
                using (var brush = new SolidBrush(Color.White))
                    g.FillRectangle(brush, new Rectangle(0, 0, 2, Height));

            Color glyph = IsActive ? Color.White : Theme.ActivityGlyph;
            int cx = Width / 2;
            int cy = Height / 2;

            if (Kind == ActivityBarButtonKind.Settings) DrawGear(g, glyph, cx, cy);
            else DrawThemeGlyph(g, glyph, bg, cx, cy);

            base.OnPaint(e);
        }

        private static void DrawGear(Graphics g, Color color, int cx, int cy)
        {
            using var pen = new Pen(color, 1.6f);
            using var brush = new SolidBrush(color);
            g.DrawEllipse(pen, cx - 8, cy - 8, 16, 16);
            g.FillEllipse(brush, cx - 2, cy - 2, 4, 4);
            for (int angle = 0; angle < 360; angle += 45)
            {
                double radians = angle * Math.PI / 180.0;
                int x1 = cx + (int)Math.Round(Math.Cos(radians) * 8);
                int y1 = cy + (int)Math.Round(Math.Sin(radians) * 8);
                int x2 = cx + (int)Math.Round(Math.Cos(radians) * 12);
                int y2 = cy + (int)Math.Round(Math.Sin(radians) * 12);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        private void DrawThemeGlyph(Graphics g, Color color, Color background, int cx, int cy)
        {
            using var pen = new Pen(color, 1.5f);
            using var brush = new SolidBrush(color);

            if (IsDarkTheme)
            {
                g.FillEllipse(brush, cx - 8, cy - 8, 16, 16);
                using var cutout = new SolidBrush(background);
                g.FillEllipse(cutout, cx - 2, cy - 11, 15, 15);
                return;
            }

            for (int angle = 0; angle < 360; angle += 45)
            {
                double radians = angle * Math.PI / 180.0;
                int x1 = cx + (int)Math.Round(Math.Cos(radians) * 9);
                int y1 = cy + (int)Math.Round(Math.Sin(radians) * 9);
                int x2 = cx + (int)Math.Round(Math.Cos(radians) * 12);
                int y2 = cy + (int)Math.Round(Math.Sin(radians) * 12);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
            g.FillEllipse(brush, cx - 5, cy - 5, 10, 10);
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
