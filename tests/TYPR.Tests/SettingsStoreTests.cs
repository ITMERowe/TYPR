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
    public void WindowSize_IsKept_AndNonsenseIsDropped()
    {
        SettingsStore.Save(SettingsFile, new UserSettings { WindowWidth = 1200, WindowHeight = 800, WindowMaximized = true });
        var kept = SettingsStore.Load(SettingsFile, out _, out _);
        Assert.Equal((1200, 800, true), (kept.WindowWidth, kept.WindowHeight, kept.WindowMaximized));

        SettingsStore.Save(SettingsFile, new UserSettings { WindowWidth = 10, WindowHeight = 999999 });
        var dropped = SettingsStore.Load(SettingsFile, out _, out _);
        Assert.Equal((0, 0), (dropped.WindowWidth, dropped.WindowHeight));
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
        // An invalid key resets the whole shortcut, so its stored modifiers go too.
        Assert.Equal(0, settings.StartModifiers);
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

    [Fact]
    public void Load_CorruptFile_RecoversFromPreviousSave()
    {
        SettingsStore.Save(SettingsFile, new UserSettings { TextTemplates = new() { new() { Name = "Kept", Text = "a" } } });
        SettingsStore.Save(SettingsFile, new UserSettings { TextTemplates = new() { new() { Name = "Latest", Text = "b" } } });
        File.WriteAllText(SettingsFile, "{ not json");

        var loaded = SettingsStore.Load(SettingsFile, out string? warning, out bool canSave);

        Assert.Equal("Kept", Assert.Single(loaded.TextTemplates).Name);
        Assert.Contains("restored", warning);
        Assert.True(canSave);
    }

    [Fact]
    public void Load_MainFileMissing_RestoresTheBak()
    {
        // A save cut off between File.Replace's two renames leaves only settings.json.bak.
        SettingsStore.Save(SettingsFile, new UserSettings { TextTemplates = new() { new() { Name = "Kept", Text = "a" } } });
        SettingsStore.Save(SettingsFile, new UserSettings { TextTemplates = new() { new() { Name = "Latest", Text = "b" } } });
        File.Delete(SettingsFile);

        var loaded = SettingsStore.Load(SettingsFile, out string? warning, out bool canSave);

        Assert.Equal("Kept", Assert.Single(loaded.TextTemplates).Name);
        Assert.Contains("restored", warning);
        Assert.True(canSave);
    }

    [Fact]
    public void Save_KeepsThePreviousFileAsBak()
    {
        SettingsStore.Save(SettingsFile, new UserSettings { TextTemplates = new() { new() { Name = "First", Text = "a" } } });
        SettingsStore.Save(SettingsFile, new UserSettings { TextTemplates = new() { new() { Name = "Second", Text = "b" } } });

        Assert.Contains("First", File.ReadAllText(SettingsFile + ".bak"));
        Assert.Contains("Second", File.ReadAllText(SettingsFile));
        Assert.False(File.Exists(SettingsFile + ".tmp"));
    }

    [Fact]
    public void Load_BareLetterShortcut_ResetToF8()
    {
        // A bare "A" as the start shortcut would start a run on every A typed anywhere.
        File.WriteAllText(SettingsFile, """{"StartKey":65,"StartModifiers":0}""");

        var loaded = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal((int)Keys.F8, loaded.StartKey);
    }

    [Fact]
    public void Load_WinComboAndPauseAlone_AreKept()
    {
        // Win+Alt+T (8|1, 0x54) and a bare Pause are valid shortcuts.
        File.WriteAllText(SettingsFile, """{"StartKey":84,"StartModifiers":9,"StopKey":19,"StopModifiers":0}""");

        var loaded = SettingsStore.Load(SettingsFile, out _, out _);

        Assert.Equal(84, loaded.StartKey);
        Assert.Equal(HotkeyModifiers.Win | HotkeyModifiers.Alt, loaded.StartModifiers);
        Assert.Equal((int)Keys.Pause, loaded.StopKey);
    }

    [Fact]
    public void Load_FileFromBeforeLineNumbers_HasThemOff()
    {
        File.WriteAllText(SettingsFile, """{"Theme":"Dark","TextTemplates":[{"Name":"A","Text":"x"}]}""");

        var loaded = SettingsStore.Load(SettingsFile, out string? warning, out _);

        Assert.Null(warning);
        Assert.False(loaded.ShowLineNumbers);
        Assert.Equal("x", loaded.TextTemplates[0].Text);
    }

    [Fact]
    public void ShowLineNumbers_On_IsKept()
    {
        SettingsStore.Save(SettingsFile, new UserSettings { ShowLineNumbers = true });

        Assert.True(SettingsStore.Load(SettingsFile, out _, out _).ShowLineNumbers);
    }
}
