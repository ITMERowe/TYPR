using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TYPR
{
    // The installer's window. installer/TYPR.iss unpacks TYPR.exe to a temp folder and runs it with
    // "--setup <setup.exe>"; Install runs that same setup silently, so Inno Setup still does the file
    // copying, upgrades and uninstall. Nothing here saves TYPR's settings.
    internal sealed class SetupForm : Form
    {
        public const string Switch = "--setup";

        // AppId in installer/TYPR.iss.
        private const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{26BFC7A4-E90F-4C64-9D35-924A3EE6E5B0}_is1";
        private static readonly int Gutter = UiSpacing.Large;
        private static readonly int BarHeight = Dpi.S(35);

        private enum Step { Ready, Installing, Done, Failed }

        private readonly string setupExe;
        private readonly string logPath = Path.Combine(Path.GetTempPath(), "TYPR-Setup.log");
        private readonly bool updating;
        private readonly TitleIdentity identity = new TitleIdentity();
        private readonly TitleIconButton themeButton = new TitleIconButton();
        private readonly WindowCaptionButton closeButton = new WindowCaptionButton(WindowCaptionButtonKind.Close);
        private readonly Label heading = new Label { Font = UiFont.Heading };
        private readonly Label message = new Label { Font = UiFont.Small };
        private readonly RoundedPanel card = new RoundedPanel { CornerRadius = UiRadius.Large };
        private readonly Label folderLabel = new Label { Font = UiFont.Label, Text = "Install to" };
        private readonly TextField folder = new TextField();
        private readonly RoundedButton browse = new RoundedButton { Text = "Browse" };
        private readonly Label shortcutLabel = new Label { Font = UiFont.Small, Text = "Create a desktop shortcut" };
        private readonly ToggleCheckBox shortcut = new ToggleCheckBox { AccessibleName = "Create a desktop shortcut" };
        private readonly SetupProgressBar progress = new SetupProgressBar { Visible = false };
        private readonly LinkLabel license = new LinkLabel { Font = UiFont.Small, Text = "Free under the MIT license" };
        private readonly RoundedButton secondary = new RoundedButton { Text = "Cancel" };
        private readonly RoundedButton primary = new RoundedButton { Kind = ButtonKind.Primary };
        private ThemeReveal? reveal;
        private Step step = Step.Ready;
        private string installedTo = "";

        public SetupForm(string setupExe)
        {
            this.setupExe = setupExe;
            string? previousFolder = null, previousTasks = null;
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstallKey))
            {
                previousFolder = key?.GetValue("Inno Setup: App Path") as string;
                previousTasks = key?.GetValue("Inno Setup: Selected Tasks") as string;
            }
            updating = !string.IsNullOrEmpty(previousFolder);

            Text = "TYPR Setup";
            AccessibleName = "TYPR Setup";
            Icon = AppIcon.Value;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Dpi.S(560), Dpi.S(380));
            Font = UiFont.Small;
            KeyPreview = true;

            // 1 px in from each edge: the form showing there is the window border.
            int close = WindowCaptionButton.DefaultWidth;
            identity.SetBounds(1, 1, ClientSize.Width - 2 - close - BarHeight, BarHeight);
            themeButton.SetBounds(identity.Right, 1, BarHeight, BarHeight);
            closeButton.SetBounds(themeButton.Right, 1, close, BarHeight);

            int width = ClientSize.Width - Gutter * 2;
            heading.SetBounds(Gutter, Dpi.S(56), width, Dpi.S(28));
            message.SetBounds(Gutter, Dpi.S(86), width, Dpi.S(44));
            card.SetBounds(Gutter, Dpi.S(140), width, Dpi.S(140));
            int inner = width - UiSpacing.Medium * 2;
            int browseWidth = Dpi.S(96);
            folderLabel.SetBounds(UiSpacing.Medium, UiSpacing.Medium, inner, Dpi.S(20));
            browse.SetBounds(UiSpacing.Medium + inner - browseWidth, Dpi.S(38), browseWidth, UiSize.Button);
            folder.SetBounds(UiSpacing.Medium, Dpi.S(43), inner - browseWidth - UiSpacing.Small, UiSize.Field);
            shortcutLabel.SetBounds(UiSpacing.Medium, Dpi.S(98), inner - Dpi.S(60), Dpi.S(22));
            shortcut.Location = new Point(UiSpacing.Medium + inner - shortcut.Width, Dpi.S(94));
            progress.SetBounds(Gutter, Dpi.S(160), width, Dpi.S(6));
            license.SetBounds(Gutter, Dpi.S(330), Dpi.S(220), Dpi.S(22));
            int buttonTop = Dpi.S(320);
            primary.SetBounds(ClientSize.Width - Gutter - Dpi.S(120), buttonTop, Dpi.S(120), UiSize.Button);
            secondary.SetBounds(primary.Left - UiSpacing.Small - Dpi.S(100), buttonTop, Dpi.S(100), UiSize.Button);

            folder.Input.AccessibleName = "Install folder";
            folder.Input.Text = updating
                ? previousFolder!.TrimEnd('\\')
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TYPR");
            shortcut.SetInitial(previousTasks?.Contains("desktopicon") == true);

            card.Controls.AddRange(new Control[] { folderLabel, folder, browse, shortcutLabel, shortcut });
            // Added in tab order.
            Controls.AddRange(new Control[] { heading, message, card, progress, license, secondary, primary, identity, themeButton, closeButton });

            identity.MouseDown += (_, e) => Drag(e);
            MouseDown += (_, e) => Drag(e);
            themeButton.AccessibleRole = AccessibleRole.PushButton;
            themeButton.Click += (_, __) => ToggleTheme();
            closeButton.Click += (_, __) => Close();
            browse.Click += (_, __) => ChooseFolder();
            license.LinkClicked += (_, __) => ThemedDialog.Inform(this, "MIT license", LicenseText());
            secondary.Click += (_, __) => Close();
            primary.Click += async (_, __) => await PrimaryAsync();
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            folder.Input.KeyDown += async (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                await PrimaryAsync();
            };

            Show(Step.Ready);
            ApplyTheme();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetWindowAppearance(Handle, Theme.WindowBorder);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            primary.Focus();
        }

        // Stopping halfway would leave a broken install.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (step == Step.Installing && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            base.OnFormClosing(e);
        }

        private void Drag(MouseEventArgs e)
        {
            const int WM_NCLBUTTONDOWN = 0x00A1, HTCAPTION = 2;
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        private void ToggleTheme()
        {
            reveal?.Close();
            Point center = themeButton.Location + new Size(themeButton.Width / 2, themeButton.Height / 2);
            reveal = ThemeReveal.Cover(this, center, toDark: !Theme.Dark);
            Theme.SetMode(Theme.Dark ? "Light" : "Dark");
            ApplyTheme();
            Update();
            reveal?.Play();
        }

        private void ApplyTheme()
        {
            // The 1 px of form showing around the children is the window border.
            BackColor = Theme.Bg;
            foreach (Label label in new[] { heading, message, license })
                label.BackColor = Theme.Bg;
            heading.ForeColor = Theme.Text;
            message.ForeColor = Theme.Text2;
            card.BackColor = Theme.Surface1;
            card.BorderColor = Theme.Line;
            folderLabel.BackColor = shortcutLabel.BackColor = Theme.Surface1;
            folderLabel.ForeColor = Theme.Text3;
            shortcutLabel.ForeColor = Theme.Text;
            license.LinkColor = license.ActiveLinkColor = license.VisitedLinkColor = Theme.AccentText;
            folder.ApplyTheme();
            themeButton.AccessibleName = Theme.Dark ? "Switch to light theme" : "Switch to dark theme";
            if (IsHandleCreated) NativeMethods.SetWindowAppearance(Handle, Theme.WindowBorder);
            Invalidate(true);
        }

        private void Show(Step next)
        {
            step = next;
            card.Visible = next == Step.Ready;
            license.Visible = next == Step.Ready;
            progress.Visible = next == Step.Installing;
            progress.Running = progress.Visible;
            closeButton.Enabled = next != Step.Installing;
            primary.Visible = secondary.Visible = next != Step.Installing;
            secondary.Text = next == Step.Done ? "Close" : "Cancel";
            primary.Text = next switch
            {
                Step.Done => "Launch TYPR",
                Step.Failed => "Try again",
                _ => updating ? "Update" : "Install"
            };
            heading.Text = next switch
            {
                Step.Installing => updating ? "Updating TYPR\u2026" : "Installing TYPR\u2026",
                Step.Done => updating ? "TYPR is up to date" : "TYPR is installed",
                Step.Failed => "TYPR was not installed",
                _ => updating ? "Update TYPR" : "Install TYPR"
            };
            if (next != Step.Failed)
                message.Text = next switch
                {
                    Step.Installing => "This takes a few seconds.",
                    Step.Done => "Find it in the Start menu.",
                    _ => $"Version {Application.ProductVersion.Split('+')[0]}. Free, offline auto typer. No admin rights needed."
                };
        }

        private async Task PrimaryAsync()
        {
            if (step == Step.Installing) return;
            if (step == Step.Done)
            {
                try { Process.Start(new ProcessStartInfo(Path.Combine(installedTo, "TYPR.exe")) { UseShellExecute = true }); }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ThemedDialog.Inform(this, "TYPR could not start", ex.Message);
                    return;
                }
                Close();
                return;
            }
            if (step == Step.Failed)
            {
                Show(Step.Ready);
                return;
            }

            string? target = CleanFolder(folder.Input.Text);
            if (target == null)
            {
                ThemedDialog.Inform(this, "Choose another folder", "Enter a full folder path, like C:\\Apps\\TYPR. A drive on its own won't work.");
                return;
            }

            Show(Step.Installing);
            string? failure = null;
            try
            {
                using var setup = Process.Start(new ProcessStartInfo(setupExe, Arguments(target, shortcut.Checked, logPath))
                    { UseShellExecute = false })!;
                await setup.WaitForExitAsync();
                if (setup.ExitCode != 0) failure = $"Setup stopped with code {setup.ExitCode}.";
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                failure = ex.Message;
            }
            installedTo = target;
            if (failure != null) message.Text = $"{failure} Details: {logPath}";
            Show(failure == null ? Step.Done : Step.Failed);
            primary.Focus();
        }

        // The full folder path, or null when it can't be passed to Setup safely.
        internal static string? CleanFolder(string value)
        {
            string path = value.Trim();
            if (path.Contains('"') || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(path))
                return null;
            path = Path.GetFullPath(path).TrimEnd('\\');
            // "C:" alone, and a share's root, are not folders to install into.
            string root = (Path.GetPathRoot(path) ?? "").TrimEnd('\\');
            return path.Length > root.Length ? path : null;
        }

        internal static string Arguments(string folder, bool desktopShortcut, string log) =>
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER " +
            $"/DIR=\"{folder}\" /MERGETASKS=\"{(desktopShortcut ? "" : "!")}desktopicon\" /LOG=\"{log}\"";

        private void ChooseFolder()
        {
            using var dialog = new FolderBrowserDialog { Description = "Choose where to install TYPR", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            // As Inno's own Browse: TYPR gets its own folder inside the chosen one.
            string chosen = dialog.SelectedPath;
            folder.Input.Text = string.Equals(Path.GetFileName(chosen.TrimEnd('\\')), "TYPR", StringComparison.OrdinalIgnoreCase)
                ? chosen
                : Path.Combine(chosen, "TYPR");
        }

        private static string LicenseText()
        {
            using var stream = typeof(SetupForm).Assembly.GetManifestResourceStream("LICENSE")!;
            using var reader = new StreamReader(stream);
            // Hard-wrapped in the file; the dialog wraps it itself.
            string[] paragraphs = reader.ReadToEnd().Replace("\r", "").Split("\n\n");
            return string.Join("\n\n", Array.ConvertAll(paragraphs, p => p.Replace('\n', ' ').Trim()));
        }
    }

    // Setup gives no progress when silent, so an accent block sweeps the track until it exits.
    internal sealed class SetupProgressBar : Control
    {
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 15 };
        private readonly Stopwatch clock = new Stopwatch();

        public SetupProgressBar()
        {
            AccessibleRole = AccessibleRole.ProgressBar;
            AccessibleName = "Installing";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer.Tick += (_, __) => Invalidate();
        }

        public bool Running
        {
            set
            {
                if (value) clock.Restart();
                timer.Enabled = value && Motion.Enabled;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var track = new SolidBrush(Theme.Surface2))
            using (var path = RoundedPanel.CreatePath(ClientRectangle, Height / 2))
                g.FillPath(track, path);
            int block = Width / 3;
            double t = timer.Enabled ? clock.Elapsed.TotalSeconds % 1.2 / 1.2 : 0.5;
            var bounds = new Rectangle((int)(t * (Width + block)) - block, 0, block, Height);
            bounds.Intersect(ClientRectangle);
            using var accent = new SolidBrush(Theme.Accent);
            using var fill = RoundedPanel.CreatePath(bounds, Height / 2);
            g.FillPath(accent, fill);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
