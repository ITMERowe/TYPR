using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TYPR
{
    internal static class NativeMethods
    {
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const int WM_HOTKEY = 0x0312;

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
        private readonly NumericUpDown cpm = new NumericUpDown();
        private readonly NumericUpDown startDelay = new NumericUpDown();
        private readonly NumericUpDown jitter = new NumericUpDown();
        private readonly CheckBox pressEnter = new CheckBox();
        private readonly Button start = new Button();
        private readonly Button stop = new Button();
        private readonly Label status = new Label();
        private readonly Label characterCount = new Label();
        private CancellationTokenSource? cts;
        private readonly Random rng = new Random();
        private bool hotkeysRegistered;

        private const int HOTKEY_START = 1;
        private const int HOTKEY_STOP = 2;
        private static readonly System.Drawing.Color PageColor = System.Drawing.Color.FromArgb(246, 248, 250);
        private static readonly System.Drawing.Color InkColor = System.Drawing.Color.FromArgb(34, 42, 53);
        private static readonly System.Drawing.Color MutedColor = System.Drawing.Color.FromArgb(112, 123, 136);
        private static readonly System.Drawing.Color AccentColor = System.Drawing.Color.FromArgb(238, 103, 94);
        private static readonly System.Drawing.Color BorderColor = System.Drawing.Color.FromArgb(226, 231, 236);

        public MainForm()
        {
            Text = "TYPR";
            ClientSize = new System.Drawing.Size(1060, 740);
            MinimumSize = new System.Drawing.Size(850, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageColor;
            Font = new System.Drawing.Font("Segoe UI", 9.5f);

            cpm.Minimum = 30;
            cpm.Maximum = 20000;
            cpm.Value = 1200;
            cpm.Width = 138;
            startDelay.Minimum = 0;
            startDelay.Maximum = 30;
            startDelay.Value = 3;
            startDelay.Width = 138;
            jitter.Minimum = 0;
            jitter.Maximum = 80;
            jitter.Value = 5;
            jitter.Width = 138;
            text.Multiline = true;
            text.AcceptsReturn = true;
            text.AcceptsTab = true;
            text.ScrollBars = ScrollBars.Both;
            text.WordWrap = false;
            text.Font = new System.Drawing.Font("Consolas", 11f);
            text.BackColor = System.Drawing.Color.White;
            text.ForeColor = InkColor;
            text.BorderStyle = BorderStyle.FixedSingle;
            text.Dock = DockStyle.Fill;
            text.PlaceholderText = "Type or paste your text here...";
            text.TextChanged += (_, __) => characterCount.Text = $"{text.TextLength:N0} characters";

            pressEnter.Text = "Press Enter after typing";
            pressEnter.AutoSize = true;
            pressEnter.ForeColor = InkColor;
            pressEnter.Margin = new Padding(0, 8, 0, 0);

            start.Text = "Start typing";
            start.AccessibleName = "Start typing (F8)";
            start.Width = 150;
            start.Height = 42;
            start.Enabled = true;
            start.Click += StartButton_Click;
            StyleButton(start, AccentColor, System.Drawing.Color.White);

            stop.Text = "Stop";
            stop.AccessibleName = "Stop typing (F9)";
            stop.Width = 92;
            stop.Height = 42;
            stop.Enabled = false;
            stop.Click += (_, __) => StopTyping();
            StyleButton(stop, System.Drawing.Color.FromArgb(237, 240, 243), InkColor);

            status.Text = "Ready — click Start, then focus the target during the countdown.";
            status.AutoEllipsis = true;
            status.Dock = DockStyle.Fill;
            status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            status.ForeColor = MutedColor;
            status.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Regular);

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageColor,
                Padding = new Padding(24, 18, 24, 18),
                ColumnCount = 1,
                RowCount = 3
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

            shell.Controls.Add(BuildHeader(), 0, 0);
            shell.Controls.Add(BuildMainContent(), 0, 1);
            shell.Controls.Add(BuildFooter(), 0, 2);
            Controls.Add(shell);
        }

        private Control BuildHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PageColor,
                Margin = new Padding(0, 0, 0, 12)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var mark = new Label
            {
                Text = "T",
                Dock = DockStyle.Fill,
                BackColor = AccentColor,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 17f, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 5, 12, 5)
            };

            var titles = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PageColor,
                Margin = Padding.Empty
            };
            titles.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            titles.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            titles.Controls.Add(new Label
            {
                Text = "TYPR",
                Dock = DockStyle.Fill,
                ForeColor = InkColor,
                Font = new System.Drawing.Font("Segoe UI", 17f, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.BottomLeft,
                Margin = Padding.Empty
            }, 0, 0);
            titles.Controls.Add(new Label
            {
                Text = "Careful, controlled keyboard input",
                Dock = DockStyle.Fill,
                ForeColor = MutedColor,
                Font = new System.Drawing.Font("Segoe UI", 9.5f),
                TextAlign = System.Drawing.ContentAlignment.TopLeft,
                Margin = Padding.Empty
            }, 0, 1);

            header.Controls.Add(mark, 0, 0);
            header.Controls.Add(titles, 1, 0);
            return header;
        }

        private Control BuildMainContent()
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PageColor,
                Margin = new Padding(0, 8, 0, 12)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

            var editorCard = CreateCard();
            editorCard.Margin = new Padding(0, 0, 12, 0);
            var editorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = System.Drawing.Color.White,
                Margin = Padding.Empty
            };
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            editorLayout.Controls.Add(new Label
            {
                Text = "Your text",
                Dock = DockStyle.Fill,
                ForeColor = InkColor,
                Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);
            editorLayout.Controls.Add(text, 0, 1);
            characterCount.Text = "0 characters";
            characterCount.Dock = DockStyle.Fill;
            characterCount.ForeColor = MutedColor;
            characterCount.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            characterCount.Margin = new Padding(0, 6, 0, 0);
            editorLayout.Controls.Add(characterCount, 0, 2);
            editorCard.Controls.Add(editorLayout);
            content.Controls.Add(editorCard, 0, 0);

            var settingsCard = CreateCard();
            settingsCard.Margin = new Padding(12, 0, 0, 0);
            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                BackColor = System.Drawing.Color.White,
                Margin = Padding.Empty
            };
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settings.Controls.Add(new Label
            {
                Text = "Typing settings",
                Dock = DockStyle.Fill,
                ForeColor = InkColor,
                Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            settings.Controls.Add(new Label
            {
                Text = "Tune the pace to your needs.",
                Dock = DockStyle.Fill,
                ForeColor = MutedColor,
                TextAlign = System.Drawing.ContentAlignment.TopLeft,
                Margin = Padding.Empty
            }, 0, 1);
            settings.Controls.Add(CreateSettingField("Typing speed", cpm, "CPM"), 0, 2);
            settings.Controls.Add(CreateSettingField("Start delay", startDelay, "sec"), 0, 3);
            settings.Controls.Add(CreateSettingField("Typing variation", jitter, "%"), 0, 4);
            settings.Controls.Add(pressEnter, 0, 5);
            settings.Controls.Add(new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BorderColor,
                Margin = new Padding(0, 4, 0, 4)
            }, 0, 6);
            var shortcuts = new Label
            {
                Text = "SHORTCUTS\nF8  Start typing\nF9  Stop",
                Dock = DockStyle.Fill,
                ForeColor = MutedColor,
                Font = new System.Drawing.Font("Segoe UI", 9f),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 0)
            };
            settings.Controls.Add(shortcuts, 0, 7);
            settingsCard.Controls.Add(settings);
            content.Controls.Add(settingsCard, 1, 0);
            return content;
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PageColor,
                Margin = Padding.Empty,
                Padding = new Padding(0, 10, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 262));

            var statusArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PageColor,
                Margin = Padding.Empty
            };
            statusArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            statusArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusArea.Controls.Add(new Label
            {
                Text = "●",
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.FromArgb(76, 167, 124),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);
            statusArea.Controls.Add(status, 1, 0);
            footer.Controls.Add(statusArea, 0, 0);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = PageColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.Controls.Add(start);
            actions.Controls.Add(stop);
            footer.Controls.Add(actions, 1, 0);
            return footer;
        }

        private Panel CreateCard()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.White,
                Padding = new Padding(18),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Panel CreateSettingField(string title, NumericUpDown input, string unit)
        {
            var field = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.White,
                Margin = new Padding(0, 5, 0, 0)
            };
            field.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Location = new System.Drawing.Point(0, 0),
                ForeColor = MutedColor
            });
            input.Location = new System.Drawing.Point(0, 25);
            input.BorderStyle = BorderStyle.FixedSingle;
            input.Font = new System.Drawing.Font("Segoe UI", 10f);
            field.Controls.Add(input);
            field.Controls.Add(new Label
            {
                Text = unit,
                AutoSize = true,
                Location = new System.Drawing.Point(input.Width + 8, 29),
                ForeColor = MutedColor
            });
            return field;
        }

        private static void StyleButton(Button button, System.Drawing.Color backColor, System.Drawing.Color foreColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold);
            button.Margin = new Padding(8, 0, 0, 0);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderSize = 0;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TryRegisterHotKeys();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
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

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_START) _ = StartTypingAsync();
                if (id == HOTKEY_STOP) StopTyping();
            }
            base.WndProc(ref m);
        }

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
            cts?.Cancel();
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
