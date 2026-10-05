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
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendVirtualKey(ushort vk)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = 0 };
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = KEYEVENTF_KEYUP };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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
        private CancellationTokenSource? cts;
        private readonly Random rng = new Random();
        private bool hotkeysRegistered;

        private const int HOTKEY_START = 1;
        private const int HOTKEY_STOP = 2;

        public MainForm()
        {
            Text = "TYPR";
            Width = 850;
            Height = 650;
            StartPosition = FormStartPosition.CenterScreen;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(8), AutoSize = false };
            top.Controls.Add(new Label { Text = "CPM:", AutoSize = true, Padding = new Padding(0,8,0,0) });
            cpm.Minimum = 30; cpm.Maximum = 20000; cpm.Value = 1200; cpm.Width = 80;
            top.Controls.Add(cpm);
            top.Controls.Add(new Label { Text = "Start delay (sec):", AutoSize = true, Padding = new Padding(10,8,0,0) });
            startDelay.Minimum = 0; startDelay.Maximum = 30; startDelay.Value = 3; startDelay.Width = 60;
            top.Controls.Add(startDelay);
            top.Controls.Add(new Label { Text = "Jitter %:", AutoSize = true, Padding = new Padding(10,8,0,0) });
            jitter.Minimum = 0; jitter.Maximum = 80; jitter.Value = 5; jitter.Width = 60;
            top.Controls.Add(jitter);
            pressEnter.Text = "Press Enter after text"; pressEnter.AutoSize = true; pressEnter.Padding = new Padding(10,5,0,0);
            top.Controls.Add(pressEnter);

            start.Text = "Start (F8)"; start.Width = 110; start.Click += async (_,__) => await StartTypingAsync();
            stop.Text = "Stop (F9)"; stop.Width = 110; stop.Click += (_,__) => StopTyping();
            top.Controls.Add(start); top.Controls.Add(stop);

            status.Dock = DockStyle.Bottom; status.Height = 30; status.Text = "Ready — click Start, then focus the RDP target window during the countdown. F8=start, F9=stop.";
            status.Padding = new Padding(8,6,0,0);

            text.Multiline = true;
            text.AcceptsReturn = true;
            text.AcceptsTab = true;
            text.ScrollBars = ScrollBars.Both;
            text.WordWrap = false;
            text.Dock = DockStyle.Fill;
            text.Font = new System.Drawing.Font("Consolas", 10.5f);

            Controls.Add(text);
            Controls.Add(top);
            Controls.Add(status);
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
                status.Text = "F8/F9 hotkeys are unavailable; another app may already be using them.";
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
            try
            {
                int delay = (int)startDelay.Value;
                for (int i = delay; i > 0; i--)
                {
                    status.Text = $"Starting in {i}s — focus the target inside RDP...";
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
            finally
            {
                cts.Dispose();
                cts = null;
                start.Enabled = true;
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
