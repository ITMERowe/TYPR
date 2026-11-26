using Xunit;

namespace TYPR.Tests;

public class SetupFormTests
{
    [Theory]
    [InlineData(@"C:\Apps\TYPR\", @"C:\Apps\TYPR")]
    [InlineData(@"  D:\My Apps\TYPR ", @"D:\My Apps\TYPR")]
    [InlineData(@"C:\", null)]
    [InlineData(@"\\server\share", null)]
    [InlineData(@"TYPR", null)]
    [InlineData("C:\\a\" /DIR=\"C:\\b", null)]
    [InlineData("", null)]
    public void CleanFolderAcceptsOnlyFullFolderPaths(string input, string? expected) =>
        Assert.Equal(expected, SetupForm.CleanFolder(input));

    [Theory]
    [InlineData(true, "/MERGETASKS=\"desktopicon\"")]
    [InlineData(false, "/MERGETASKS=\"!desktopicon\"")]
    public void ArgumentsRunSetupSilentlyWithTheChosenOptions(bool shortcut, string task)
    {
        string arguments = SetupForm.Arguments(@"C:\My Apps\TYPR", shortcut, @"C:\t\log.txt");
        Assert.Contains("/VERYSILENT", arguments);
        Assert.Contains("/DIR=\"C:\\My Apps\\TYPR\"", arguments);
        Assert.Contains(task, arguments);
    }
}
