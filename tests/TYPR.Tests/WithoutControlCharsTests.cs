using Xunit;

namespace TYPR.Tests;

public sealed class WithoutControlCharsTests
{
    [Fact]
    public void RemovesControlCharacters_KeepsLineBreaksAndTabs() =>
        Assert.Equal("ab\r\nc\td", MainForm.WithoutControlChars("a\bb\r\nc\td\u0007"));

    [Fact]
    public void KeepsEmojiAndOtherText() =>
        Assert.Equal("Hi \U0001F600 é", MainForm.WithoutControlChars("Hi \U0001F600 é\u001b"));
}
