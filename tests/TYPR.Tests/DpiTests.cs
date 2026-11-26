using Xunit;

namespace TYPR.Tests;

public class DpiTests
{
    [Theory]
    [InlineData(12, 1f, 12)]
    [InlineData(12, 1.25f, 15)]
    [InlineData(3, 1.5f, 5)]     // 4.5 rounds up, not to even
    [InlineData(35, 1.75f, 61)]  // 61.25
    [InlineData(232, 2f, 464)]
    public void ScaleRoundsHalfAwayFromZero(int px, float factor, int expected) =>
        Assert.Equal(expected, Dpi.Scale(px, factor));

    // The window size is saved in 100% pixels; scaling it to the screen and back must not drift launch to launch.
    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(1.75f)]
    [InlineData(2f)]
    public void SavedWindowSizeSurvivesTheRoundTrip(float factor)
    {
        for (int saved = 560; saved <= 4000; saved++)
            Assert.Equal(saved, Dpi.Scale(Dpi.Scale(saved, factor), 1 / factor));
    }
}
