using Xunit;

namespace TYPR.Tests;

public sealed class CharacterCountLabelTests
{
    [Theory]
    [InlineData(6700, false, "6,700 characters · ≈6 min")]
    [InlineData(6700, true, "6,700 · ≈6 min")]
    [InlineData(1, false, "1 character · ≈1 s")]
    [InlineData(30, true, "30 · ≈2 s")]
    // Nothing follows the number, so the short form keeps the unit.
    [InlineData(0, true, "0 characters")]
    public void FullAndCompact(int count, bool compact, string expected) =>
        Assert.Equal(expected, MainForm.CharacterCountLabel(count, 1200, compact));
}
