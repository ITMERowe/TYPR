using System.Text;
using Xunit;

namespace TYPR.Tests;

public sealed class LineNumberGutterTests
{
    private static int[] Numbers(string text, params int[] rowStarts)
    {
        (int, int) anchor = default;
        return LineNumberGutter.RowNumbers(text, rowStarts, ref anchor);
    }

    [Fact]
    public void OneLine_IsNumberOne() => Assert.Equal(new[] { 1 }, Numbers("hello", 0));

    [Fact]
    public void EmptyText_StillShowsLineOne() => Assert.Equal(new[] { 1 }, Numbers("", 0));

    [Fact]
    public void CrlfLines_AreNumberedInOrder() => Assert.Equal(new[] { 1, 2, 3 }, Numbers("a\r\nb\r\nc", 0, 3, 6));

    [Fact]
    public void WrappedLine_IsNumberedOnItsFirstRowOnly() =>
        // "aaaa bbbb" wraps after "aaaa ": the second row continues line 1.
        Assert.Equal(new[] { 1, 0, 2 }, Numbers("aaaa bbbb\r\ncc", 0, 5, 11));

    [Fact]
    public void ViewStartingOnAWrappedRow_LeavesItBlank() => Assert.Equal(new[] { 0, 2 }, Numbers("aaaa bbbb\r\ncc", 5, 11));

    [Fact]
    public void EmptyLines_GetNumbers() => Assert.Equal(new[] { 1, 2, 3, 4 }, Numbers("a\r\n\r\n\r\nb", 0, 3, 5, 7));

    [Fact]
    public void TrailingNewline_NumbersTheEmptyLastLine() => Assert.Equal(new[] { 1, 2 }, Numbers("a\r\n", 0, 3));

    [Fact]
    public void LoneLineFeed_DoesNotStartALine() =>
        // The edit control shows a lone \n inside its line, so a row after it is a wrap, not line 2.
        Assert.Equal(new[] { 1, 0 }, Numbers("aaaa\nbbbb", 0, 5));

    [Fact]
    public void HundredThousandLines_CountedFromTheAnchorBothWays()
    {
        var text = new StringBuilder();
        for (int i = 1; i <= 100_000; i++) text.Append("line ").Append(i).Append(i < 100_000 ? "\r\n" : "");
        string all = text.ToString();
        int StartOf(int line) => line == 1 ? 0 : all.IndexOf("\r\nline " + line + "\r", StringComparison.Ordinal) + 2;
        int[] Rows(int firstLine) => Enumerable.Range(firstLine, 3).Select(StartOf).ToArray();

        (int, int) anchor = default;
        // Down to the end, back up to the middle, then up to the top: the cached count must follow.
        Assert.Equal(new[] { 99_997, 99_998, 99_999 }, LineNumberGutter.RowNumbers(all, Rows(99_997), ref anchor));
        Assert.Equal(new[] { 50_000, 50_001, 50_002 }, LineNumberGutter.RowNumbers(all, Rows(50_000), ref anchor));
        Assert.Equal(new[] { 1, 2, 3 }, LineNumberGutter.RowNumbers(all, Rows(1), ref anchor));
        Assert.Equal(100_000, LineNumberGutter.LineBreaks(all) + 1);
    }

    [Fact]
    public void StaleAnchorPastTheText_StartsOver()
    {
        (int, int) anchor = (500, 40);
        Assert.Equal(new[] { 1, 2 }, LineNumberGutter.RowNumbers("a\r\nb", new[] { 0, 3 }, ref anchor));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(9, 2)]
    [InlineData(99, 2)]
    [InlineData(100, 3)]
    [InlineData(999, 3)]
    [InlineData(1000, 4)]
    [InlineData(100_000, 6)]
    public void Digits_FitTheLargestNumber_AtLeastTwo(int lines, int digits) =>
        Assert.Equal(digits, LineNumberGutter.Digits(lines));
}
