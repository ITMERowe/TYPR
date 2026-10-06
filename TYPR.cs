using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        public static readonly bool Dark = DetectDark();

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

        public static Color Page        => Dark ? Color.FromArgb(30, 30, 30)   : Color.FromArgb(243, 243, 243);
        public static Color Surface     => Dark ? Color.FromArgb(37, 37, 38)   : Color.White;
        public static Color SurfaceMuted=> Dark ? Color.FromArgb(51, 51, 51)   : Color.FromArgb(232, 232, 232);
        public static Color Editor      => Dark ? Color.FromArgb(30, 30, 30)   : Color.White;
        public static Color Input       => Dark ? Color.FromArgb(60, 60, 60)   : Color.White;
        public static Color Border      => Dark ? Color.FromArgb(60, 60, 60)   : Color.FromArgb(206, 206, 206);
        public static Color PaneBorder  => Dark ? Color.FromArgb(43, 43, 43)   : Color.FromArgb(224, 224, 224);
        public static Color FocusBorder => Color.FromArgb(0, 127, 212);
        public static Color Text        => Dark ? Color.FromArgb(204, 204, 204): Color.FromArgb(51, 51, 51);
        public static Color Text2       => Dark ? Color.FromArgb(156, 156, 156): Color.FromArgb(97, 97, 97);
        public static Color Text3       => Dark ? Color.FromArgb(133, 133, 133): Color.FromArgb(120, 120, 120);
        public static Color Accent      => Color.FromArgb(14, 99, 156);
        public static Color AccentHover => Color.FromArgb(17, 119, 187);
        public static Color Positive    => Color.FromArgb(34, 197, 94);
        public static Color OnAccent    => Color.White;
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

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public static void SetWindowAppearance(IntPtr handle, Color borderColor)
        {
            if (Environment.OSVersion.Version.Build < 22000) return;

            SetDwmWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
            SetDwmWindowAttribute(handle, DWMWA_BORDER_COLOR, ColorTranslator.ToWin32(borderColor));
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
        private readonly TextBox text = new TextBox();
        private readonly NumberInput cpm = new NumberInput();
        private readonly NumberInput startDelay = new NumberInput();
        private readonly Slider jitter = new Slider();
        private readonly Label jitterValue = new Label();
        private readonly ToggleCheckBox pressEnter = new ToggleCheckBox();
        private readonly RoundedButton start = new RoundedButton();
        private readonly RoundedButton stop = new RoundedButton();
        private readonly Label status = new Label();
        private readonly Label characterCount = new Label();
        private readonly WindowCaptionButton maximizeButton = new WindowCaptionButton(WindowCaptionButtonKind.Maximize);
        private CancellationTokenSource? cts;
        private readonly Random rng = new Random();
        private bool hotkeysRegistered;

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
        private const int SettingsPanelWidth = 250;

        public MainForm()
        {
            Text = "TYPR";
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(980, 700);
            MinimumSize = new Size(860, 686);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Page;
            Font = new Font("Segoe UI", UiTypography.Body);
            maximizeButton.Click += (_, __) => ToggleWindowState();
            Resize += (_, __) => maximizeButton.IsMaximized = WindowState == FormWindowState.Maximized;

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
            text.TextChanged += (_, __) => characterCount.Text = $"{text.TextLength:N0} characters";

            pressEnter.TabStop = false;
            pressEnter.Cursor = Cursors.Hand;

            start.Text = "Start typing";
            start.AccessibleName = "Start typing (F8)";
            start.Width = 132;
            start.Height = 38;
            start.Click += StartButton_Click;
            StylePrimaryButton(start);

            stop.Text = "Stop";
            stop.AccessibleName = "Stop typing (F9)";
            stop.Width = 86;
            stop.Height = 38;
            stop.Enabled = false;
            stop.MouseUp += StopButton_MouseUp;
            StyleSecondaryButton(stop);

            status.Text = "Ready · focus target during countdown";
            status.AutoEllipsis = true;
            status.Dock = DockStyle.Fill;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = Theme.Text2;

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                Padding = new Padding(UiSpacing.Large, UiSpacing.Large, UiSpacing.Large, 0),
                Margin = Padding.Empty,
                ColumnCount = 1,
                RowCount = 2
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

            shell.Controls.Add(BuildMainContent(), 0, 0);
            shell.Controls.Add(BuildFooter(), 0, 1);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildTitleBar(), 0, 0);
            root.Controls.Add(shell, 0, 1);
            Controls.Add(root);
        }

        private Control BuildTitleBar()
        {
            var titleBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));

            var identity = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Theme.Surface,
                Padding = new Padding(UiSpacing.Medium, 0, 0, 0),
                Margin = Padding.Empty
            };
            var icon = new RoundedPanel
            {
                BackColor = Theme.Accent,
                CornerRadius = 2,
                Width = 18,
                Height = 18,
                Margin = new Padding(0, 9, UiSpacing.Small, 0)
            };
            icon.Controls.Add(new Label
            {
                Text = "T",
                Dock = DockStyle.Fill,
                BackColor = Theme.Accent,
                ForeColor = Theme.OnAccent,
                Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            var appName = new Label
            {
                Text = "TYPR",
                AutoSize = false,
                Width = 54,
                Height = 36,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            };
            identity.Controls.Add(icon);
            identity.Controls.Add(appName);
            titleBar.Controls.Add(identity, 0, 0);

            var title = new Label
            {
                Text = "Text input",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            };
            titleBar.Controls.Add(title, 1, 0);

            var windowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 138,
                Height = 36,
                BackColor = Theme.Surface,
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
            title.MouseDown += TitleBar_MouseDown;
            titleBar.DoubleClick += TitleBar_DoubleClick;
            identity.DoubleClick += TitleBar_DoubleClick;
            appName.DoubleClick += TitleBar_DoubleClick;
            title.DoubleClick += TitleBar_DoubleClick;
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
            const int HTCLIENT = 1;
            const int HTNOWHERE = 0;

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

        // ---------- Main ----------

        private Control BuildMainContent()
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Page,
                Margin = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SettingsPanelWidth));

            content.Controls.Add(BuildEditorCard(), 0, 0);
            content.Controls.Add(BuildSettingsCard(), 1, 0);
            return content;
        }

        private Control BuildEditorCard()
        {
            var card = CreateCard();
            card.Margin = new Padding(UiSpacing.XSmall);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Theme.Surface,
                Margin = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var head = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Surface,
                Margin = new Padding(0, 0, 0, UiSpacing.Small)
            };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            head.Controls.Add(new Label
            {
                Text = "Your text",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Section, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            characterCount.Text = "0 characters";
            characterCount.Dock = DockStyle.Fill;
            characterCount.ForeColor = Theme.Text3;
            characterCount.TextAlign = ContentAlignment.MiddleRight;
            characterCount.Margin = Padding.Empty;
            head.Controls.Add(characterCount, 1, 0);
            layout.Controls.Add(head, 0, 0);

            var editorSurface = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Editor,
                BorderColor = Theme.Border,
                CornerRadius = UiSpacing.Small,
                Padding = new Padding(UiSpacing.Medium),
                Margin = Padding.Empty
            };
            editorSurface.Controls.Add(text);
            layout.Controls.Add(editorSurface, 0, 1);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildSettingsCard()
        {
            var card = CreateCard();
            card.Margin = new Padding(UiSpacing.XSmall);

            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 12,
                BackColor = Theme.Surface,
                Margin = Padding.Empty
            };
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 9));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 9));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            settings.Controls.Add(new Label
            {
                Text = "Typing settings",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Header, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            settings.Controls.Add(new Label
            {
                Text = "Tune the pace to your needs.",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.TopLeft,
                Margin = Padding.Empty
            }, 0, 1);

            settings.Controls.Add(CreateSettingField("Typing speed", cpm, "CPM"), 0, 2);
            settings.Controls.Add(CreateSettingField("Start delay", startDelay, "sec"), 0, 3);
            settings.Controls.Add(CreateSliderField("Typing variation", jitter, jitterValue, "humanize"), 0, 4);
            settings.Controls.Add(Divider(), 0, 5);

            var switchRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Theme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var switchLabel = new Label
            {
                Text = "Press Enter when finished",
                AutoSize = true,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, UiSpacing.Small, UiSpacing.Small, 0)
            };
            switchRow.Controls.Add(switchLabel);
            pressEnter.Width = 42;
            pressEnter.Height = 34;
            pressEnter.Margin = Padding.Empty;
            switchRow.Controls.Add(pressEnter);
            settings.Controls.Add(switchRow, 0, 6);
            settings.Controls.Add(Divider(), 0, 7);

            settings.Controls.Add(new Label
            {
                Text = "SHORTCUTS",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Caption, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = Padding.Empty
            }, 0, 8);
            settings.Controls.Add(ShortcutRow("Start typing", "F8"), 0, 9);
            settings.Controls.Add(ShortcutRow("Stop", "F9"), 0, 10);

            card.Controls.Add(settings);
            return card;
        }

        // ---------- Footer ----------

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Page,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

            var statusLine = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Page,
                Margin = Padding.Empty
            };
            statusLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            statusLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusLine.Controls.Add(new Label
            {
                Text = "●",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Positive,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            statusLine.Controls.Add(status, 1, 0);
            footer.Controls.Add(statusLine, 0, 0);

            stop.Anchor = AnchorStyles.Top;
            stop.Margin = new Padding(0, UiSpacing.Medium, 0, 0);
            footer.Controls.Add(stop, 1, 0);

            start.Anchor = AnchorStyles.Top;
            start.Margin = new Padding(0, UiSpacing.Medium, 0, 0);
            footer.Controls.Add(start, 2, 0);
            return footer;
        }

        // ---------- Building blocks ----------

        private static RoundedPanel CreateCard()
        {
            return new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                BorderColor = Theme.PaneBorder,
                CornerRadius = 10,
                Padding = new Padding(
                    UiSpacing.Large, UiSpacing.Medium, UiSpacing.Large, UiSpacing.Large),
                Margin = Padding.Empty
            };
        }

        private static Control Divider()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.PaneBorder,
                Margin = new Padding(0, UiSpacing.XSmall, 0, UiSpacing.XSmall)
            };
        }

        private Control CreateSettingField(string title, NumberInput input, string unit)
        {
            var field = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
                BackColor = Theme.Surface,
                Margin = Padding.Empty
            };
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            input.Dock = DockStyle.Fill;
            line.Controls.Add(input, 0, 0);
            line.Controls.Add(new Label
            {
                Text = unit,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI", UiTypography.Caption),
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(UiSpacing.Small, 0, 0, 0)
            }, 1, 0);
            field.Controls.Add(line, 0, 1);
            return field;
        }

        private Control CreateSliderField(string title, Slider slider, Label valueLabel, string hint)
        {
            var field = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
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
                BackColor = Theme.Surface,
                Margin = new Padding(0, UiSpacing.XSmall, 0, 0)
            };
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            slider.Dock = DockStyle.Fill;
            slider.Margin = Padding.Empty;
            line.Controls.Add(slider, 0, 0);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.ForeColor = Theme.Text;
            valueLabel.Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Regular);
            valueLabel.TextAlign = ContentAlignment.MiddleRight;
            valueLabel.Margin = new Padding(UiSpacing.Small, 0, UiSpacing.XSmall, 0);
            line.Controls.Add(valueLabel, 1, 0);
            field.Controls.Add(line, 0, 1);
            return field;
        }

        private static Control ShortcutRow(string caption, string key)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Surface,
                Margin = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            row.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text2,
                Font = new Font("Segoe UI", UiTypography.Body),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);

            var kbd = new RoundedPanel
            {
                BackColor = Theme.SurfaceMuted,
                BorderColor = Theme.Border,
                CornerRadius = 2,
                Dock = DockStyle.None,
                Width = 32,
                Height = 21,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, UiSpacing.XSmall, UiSpacing.XSmall, 0)
            };
            kbd.Controls.Add(new Label
            {
                Text = key,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text2,
                BackColor = Theme.SurfaceMuted,
                Font = new Font("Segoe UI", UiTypography.Caption),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            });
            row.Controls.Add(kbd, 1, 0);
            return row;
        }

        private static void StylePrimaryButton(RoundedButton button)
        {
            button.BackColor = Theme.Accent;
            button.ForeColor = Theme.OnAccent;
            button.Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold);
            button.Margin = Padding.Empty;
            button.Cursor = Cursors.Hand;
            button.HoverTint = Theme.AccentHover;
            button.DownTint = Theme.AccentHover;
        }

        private static void StyleSecondaryButton(RoundedButton button)
        {
            button.BackColor = Theme.Surface;
            button.ForeColor = Theme.Text2;
            button.BorderColor = Theme.Border;
            button.Font = new Font("Segoe UI", UiTypography.Body, FontStyle.Bold);
            button.Margin = Padding.Empty;
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

        private void TryRegisterHotKeys()
        {
            if (hotkeysRegistered || IsDisposed || Handle == IntPtr.Zero) return;

            bool startOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_START, 0, (uint)Keys.F8);
            bool stopOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_STOP, 0, (uint)Keys.F9);
            hotkeysRegistered = startOk && stopOk;

            if (!hotkeysRegistered)
            {
                status.Text = "Shortcuts unavailable — use the buttons.";
                if (!startOk) NativeMethods.UnregisterHotKey(Handle, HOTKEY_START);
                if (!stopOk) NativeMethods.UnregisterHotKey(Handle, HOTKEY_STOP);
            }
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

        // ---------- Typing engine (unchanged) ----------

        private async Task StartTypingAsync()
        {
            if (cts != null) return;
            string payload = text.Text;
            if (string.IsNullOrEmpty(payload)) { status.Text = "Nothing to type."; return; }

            cts = new CancellationTokenSource();
            start.Enabled = false;
            stop.Enabled = true;
            try
            {
                int delay = (int)startDelay.Value;
                for (int i = delay; i > 0; i--)
                {
                    status.Text = $"Starting in {i}s — focus the target window...";
                    await Task.Delay(1000, cts.Token);
                }

                status.Text = "Typing... F9 to stop.";
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region = CreateRegion(ClientRectangle, CornerRadius);
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

        public static Region CreateRegion(Rectangle bounds, int radius)
        {
            using (var path = CreatePath(bounds, radius))
            {
                return new Region(path);
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private bool hovered;

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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region = RoundedPanel.CreateRegion(ClientRectangle, 2);
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
            using (var path = RoundedPanel.CreatePath(bounds, 2))
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

                TextRenderer.DrawText(e.Graphics, Text, Font, bounds,
                    Enabled ? ForeColor : Theme.Text3,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    internal sealed class ToggleCheckBox : Control
    {
        public bool Checked { get; set; }

        public ToggleCheckBox()
        {
            BackColor = Theme.Surface;
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
            var track = new Rectangle(2, Math.Max(2, (Height - 20) / 2), 36, 20);
            using (var path = RoundedPanel.CreatePath(track, 10))
            using (var fill = new SolidBrush(Checked ? Theme.Accent : Theme.SurfaceMuted))
            {
                e.Graphics.FillPath(fill, path);
            }
            if (!Checked)
                using (var path = RoundedPanel.CreatePath(track, 10))
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawPath(pen, path);

            int thumbX = Checked ? 20 : 4;
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

        public NumberInput()
        {
            BackColor = Theme.Surface;
            Height = 38;
            MinimumSize = new Size(90, 38);

            frame = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Input,
                BorderColor = Theme.Border,
                CornerRadius = 2,
                Padding = new Padding(UiSpacing.Small, UiSpacing.XSmall, UiSpacing.XSmall, UiSpacing.XSmall),
                Margin = Padding.Empty
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = frame.BackColor,
                Margin = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            input.Dock = DockStyle.Fill;
            input.BorderStyle = BorderStyle.None;
            input.BackColor = frame.BackColor;
            input.ForeColor = Theme.Text;
            input.Font = new Font("Segoe UI", UiTypography.Control);
            input.TextAlign = HorizontalAlignment.Left;
            input.Text = "0";
            input.Margin = new Padding(0, UiSpacing.XSmall, 0, 0);
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
            layout.Controls.Add(input, 0, 0);
            frame.Controls.Add(layout);
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
            Height = 34;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        private double Fraction => maximum <= minimum ? 0 : (double)(val - minimum) / (maximum - minimum);

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int pad = 8;
            int trackY = Height / 2 - 2;
            var track = new Rectangle(pad, trackY, Math.Max(1, Width - pad * 2), 4);
            using (var path = RoundedPanel.CreatePath(track, 2))
            using (var brush = new SolidBrush(Theme.SurfaceMuted))
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
            using (var brush = new SolidBrush(hovered || dragging || Focused ? Theme.Text : Theme.Surface))
                g.FillEllipse(brush, thumbRect);
            using (var pen = new Pen(Theme.Border))
                g.DrawEllipse(pen, thumbRect);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region = RoundedPanel.CreateRegion(ClientRectangle, 9);
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
            BackColor = Theme.Surface;
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
            Color background = hovered
                ? Kind == WindowCaptionButtonKind.Close
                    ? Color.FromArgb(196, 43, 28)
                    : Theme.SurfaceMuted
                : BackColor;
            Color glyph = hovered && Kind == WindowCaptionButtonKind.Close ? Color.White : Theme.Text;
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