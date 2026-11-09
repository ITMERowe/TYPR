using System.Windows.Forms;
using Xunit;

namespace TYPR.Tests;

public sealed class FormatHotkeyTests
{
    [Theory]
    [InlineData(0, Keys.F8, "F8")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.F12, "Ctrl+Shift+F12")]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control, Keys.A, "Ctrl+Alt+Shift+A")]
    [InlineData(HotkeyModifiers.Control, Keys.D5, "Ctrl+5")]
    [InlineData(HotkeyModifiers.Control, Keys.OemPeriod, "Ctrl+.")]
    [InlineData(HotkeyModifiers.Alt, Keys.Space, "Alt+Space")]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Alt, Keys.T, "Alt+Win+T")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, Keys.F5, "Ctrl+Alt+Shift+Win+F5")]
    [InlineData(HotkeyModifiers.Control, Keys.Return, "Ctrl+Enter")]
    [InlineData(HotkeyModifiers.Control, Keys.NumPad7, "Ctrl+Num 7")]
    [InlineData(HotkeyModifiers.Shift, Keys.Next, "Shift+PgDn")]
    [InlineData(0, Keys.Pause, "Pause")]
    public void FormatHotkey_ShowsModifiersInFixedOrder(int modifiers, Keys key, string expected)
    {
        Assert.Equal(expected, MainForm.FormatHotkey(modifiers, (int)key));
    }
}
