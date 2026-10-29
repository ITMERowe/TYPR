using System.Windows.Forms;
using Xunit;

namespace TYPR.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "TYPR.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsFile => Path.Combine(folder, "settings.json");

    public SettingsStoreTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void Load_NoFile_ReturnsDefaultsWithoutWarning()
    {
        var settings = SettingsStore.Load(SettingsFile, out string? warning, out bool canSave);

        Assert.Null(warning);
        Assert.True(canSave);
        Assert.Equal((int)Keys.F8, settings.StartKey);
        Assert.Equal((int)Keys.F9, settings.StopKey);
    }

    [Fact]
    public void Load_NoFile_StartsWithOneEmptyTab()
    {
        // The window reads the active tab on startup, so a first run needs one.
        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal("Text 1", Assert.Single(settings.TextTemplates).Name);
        Assert.Equal(0, settings.ActiveTextTemplate);
    }

    [Fact]
    public void SaveThenLoad_KeepsTextsAndActiveTab()
    {
        var original = new UserSettings
        {
            TextTemplates = new List<TextTemplate>
            {
                new() { Name = "Server", Text = "ssh admin@10.0.0.1\n" },
                new() { Name = "Emoji", Text = "héllo 👋\tend" }
            },
            ActiveTextTemplate = 1
        };

        SettingsStore.Save(SettingsFile, original);
        var loaded = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal(new[] { "Server", "Emoji" }, loaded.TextTemplates.Select(t => t.Name));
        Assert.Equal("héllo 👋\tend", loaded.TextTemplates[1].Text);
        Assert.Equal(1, loaded.ActiveTextTemplate);
    }

    [Fact]
    public void Load_OldTemplateNames_RenamedToText()
    {
        File.WriteAllText(SettingsFile, """{"TextTemplates":[{"Name":"Template 3","Text":"a"},{"Name":"Template notes","Text":"b"}]}""");

        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal("Text 3", settings.TextTemplates[0].Name);
        Assert.Equal("Template notes", settings.TextTemplates[1].Name);
    }

    [Fact]
    public void Load_MissingNamesAndNullTexts_AreFilledIn()
    {
        File.WriteAllText(SettingsFile, """{"TextTemplates":[{"Name":"  ","Text":null},null]}""");

        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal(new[] { "Text 1", "Text 2" }, settings.TextTemplates.Select(t => t.Name));
        Assert.All(settings.TextTemplates, t => Assert.Equal(string.Empty, t.Text));
    }

    [Fact]
    public void Load_NoTemplates_CreatesOneEmptyTab()
    {
        File.WriteAllText(SettingsFile, """{"TextTemplates":[]}""");

        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Single(settings.TextTemplates);
        Assert.Equal("Text 1", settings.TextTemplates[0].Name);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(9, 1)]
    public void Load_ActiveTabOutOfRange_IsClamped(int stored, int expected)
    {
        File.WriteAllText(SettingsFile, $$"""{"TextTemplates":[{"Name":"A","Text":""},{"Name":"B","Text":""}],"ActiveTextTemplate":{{stored}}}""");

        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal(expected, settings.ActiveTextTemplate);
    }

    [Fact]
    public void Load_InvalidThemeAndKeys_FallBackToDefaults()
    {
        File.WriteAllText(SettingsFile, """{"Theme":"Purple","StartKey":0,"StopKey":17,"StartModifiers":65535}""");

        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal("System", settings.Theme);
        Assert.Equal((int)Keys.F8, settings.StartKey);
        Assert.Equal((int)Keys.F9, settings.StopKey);
        Assert.Equal(HotkeyModifiers.Supported, settings.StartModifiers);
    }

    [Fact]
    public void Load_SameStartAndStopShortcut_MovesStopToAnotherKey()
    {
        File.WriteAllText(SettingsFile, $$"""{"StartKey":{{(int)Keys.F9}},"StopKey":{{(int)Keys.F9}}}""");

        var settings = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal((int)Keys.F10, settings.StopKey);
    }

    [Fact]
    public void Load_CorruptFile_KeepsACopyOfIt()
    {
        const string damaged = """{"TextTemplates":[{"Name":"Precious","Text":"do not lose me" """;
        File.WriteAllText(SettingsFile, damaged);

        var settings = SettingsStore.Load(SettingsFile, out string? warning, out bool canSave);

        string backup = Assert.Single(Directory.GetFiles(folder, "settings.json.*.bad"));
        Assert.Equal(damaged, File.ReadAllText(backup));
        Assert.Contains(Path.GetFileName(backup), warning);
        Assert.True(canSave);
        Assert.Equal("Text 1", settings.TextTemplates.Single().Name);
    }

    [Fact]
    public void Load_UnreadableFile_ForbidsSavingOverIt()
    {
        File.WriteAllText(SettingsFile, """{"TextTemplates":[{"Name":"Precious","Text":"x"}]}""");
        using var lockedByAnotherProgram = new FileStream(SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None);

        SettingsStore.Load(SettingsFile, out string? warning, out bool canSave);

        Assert.False(canSave);
        Assert.NotNull(warning);
    }
}
