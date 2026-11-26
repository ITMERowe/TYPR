using System.Drawing;
using Xunit;

namespace TYPR.Tests;

public class ThemeRevealTests
{
    [Theory]
    [InlineData(0, 0.5, 0.3, 1)]
    [InlineData(0.7, 0, 1, 0.5)]
    public void BezierRunsFromZeroToOneWithoutGoingBack(double x1, double y1, double x2, double y2)
    {
        double last = 0;
        for (int i = 0; i <= 100; i++)
        {
            double y = ThemeReveal.Bezier(i / 100.0, x1, y1, x2, y2);
            Assert.True(y >= last - 1e-6);
            last = y;
        }
        Assert.Equal(0, ThemeReveal.Bezier(0, x1, y1, x2, y2), 3);
        Assert.Equal(1, last, 3);
    }

    [Fact]
    public void CircleReachesTheFarthestCorner() =>
        Assert.Equal(500, ThemeReveal.FarthestCorner(new Point(300, 0), new Size(300, 400)));
}
