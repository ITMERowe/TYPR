using Xunit;

namespace TYPR.Tests;

public sealed class NextTextNameTests
{
    private static List<TextTemplate> Tabs(params string[] names) =>
        names.Select(name => new TextTemplate { Name = name }).ToList();

    [Fact]
    public void NoGaps_UsesCountPlusOne() =>
        Assert.Equal("Text 3", MainForm.NextTextName(Tabs("Text 1", "Text 2")));

    [Fact]
    public void AfterADelete_SkipsANameStillInUse() =>
        // "Text 2" was deleted: Count + 1 is "Text 3", which the remaining tab already has.
        Assert.Equal("Text 4", MainForm.NextTextName(Tabs("Text 1", "Text 3")));

    [Fact]
    public void RenamedTabs_KeepTheUsualNumber() =>
        Assert.Equal("Text 3", MainForm.NextTextName(Tabs("Server", "Emoji")));
}
