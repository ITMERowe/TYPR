using System.Diagnostics;
using Xunit;

namespace TYPR.Tests;

public sealed class TimerResolutionTests
{
    // Measures real time, so it can be noisy on a busy machine; CI skips it (see build.yml).
    [Fact]
    [Trait("Category", "Timing")]
    public async Task Begin_ShortDelaysLastAboutAsLongAsAsked_EvenWithAHiddenWindow()
    {
        const int askedMs = 10;
        const int rounds = 30;

        // While typing, TYPR's window sits behind the target. Windows 11 ignores timer
        // precision requests from processes whose windows are hidden or fully covered.
        using var hiddenWindow = new System.Windows.Forms.Form
        {
            ShowInTaskbar = false,
            WindowState = System.Windows.Forms.FormWindowState.Minimized
        };
        hiddenWindow.Show();

        using (TimerResolution.Begin())
        {
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < rounds; i++)
                await Task.Delay(askedMs);
            double averageMs = watch.Elapsed.TotalMilliseconds / rounds;

            // Windows' default ~15.6 ms timer tick makes a 10 ms wait take ~16 ms.
            Assert.InRange(averageMs, askedMs, askedMs + 2.5);
        }
    }
}
