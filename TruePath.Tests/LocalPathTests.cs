// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;

using Xunit.Abstractions;

namespace TruePath.Tests;

public class LocalPathTests(ITestOutputHelper output)
{
    [Fact]
    public void ReadKind_NonExistent()
    {
        // Arrange
        var currentDirectory = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
        var localPath = new LocalPath(currentDirectory);

        // Act
        var kind = localPath.ReadKind();

        // Assert
        Assert.Null(kind);
    }

    [Fact]
    public void ReadKind_IsDirectory()
    {
        // Arrange
        var currentDirectory = Environment.CurrentDirectory;
        var localPath = new LocalPath(currentDirectory);

        // Act
        var kind = localPath.ReadKind();

        // Assert
        Assert.Equal(FileEntryKind.Directory, kind);
    }

    [Fact]
    public void ReadKind_IsFile()
    {
        // Arrange
        string tempFilePath = Path.GetTempFileName();
        var localPath = new LocalPath(tempFilePath);

        // Act
        var kind = localPath.ReadKind();

        // Assert
        Assert.Equal(FileEntryKind.File, kind);
    }

    [Fact]
    public void OnWindows_ReadKind_IsJunction()
    {
        // Arrange
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var currentDirectory = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
        var localPath = new LocalPath(currentDirectory);

        var tempDirectoryInfo = Path.GetTempPath();

        var created = CreateJunction(currentDirectory, tempDirectoryInfo);

        Assert.True(created);

        // Act
        var kind = localPath.ReadKind();

        // Assert
        Assert.Equal(FileEntryKind.Junction, kind);

        Directory.Delete(currentDirectory, true);
    }

    [Fact]
    public void ReadKind_IsSymlink()
    {
        // Arrange
        var currentDirectory = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());
        var localPath = new LocalPath(currentDirectory);

        var tempDirectoryInfo = Path.GetTempPath();

        Directory.CreateSymbolicLink(currentDirectory, tempDirectoryInfo);

        // Act
        var kind = localPath.ReadKind();

        // Assert
        Assert.Equal(FileEntryKind.Symlink , kind);

        Directory.Delete(currentDirectory, true);
    }

    [Theory]
    [InlineData("user", "user/documents")]
    [InlineData("usEr", "User/documents")]
    [InlineData("user/documents", "user/documents")]
    [InlineData("user/documents", "user")]
    public void IsPrefixOfShouldBeEquivalentToStartsWith(string pathA, string pathB)
    {
        // Arrange
        var a = new LocalPath(pathA);
        var b = new LocalPath(pathB);

        // Act
        var isPrefix = a.IsPrefixOf(b);
        var startsWith = b.Value.StartsWith(a.Value);

        // Assert
        Assert.Equal(isPrefix, startsWith);
    }

    [Fact]
    public void AbsolutePathIsNormalizedOnCreation()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string path = @"C:/Users/John Doe\Documents";
        var absolutePath = new LocalPath(path);
        Assert.Equal(@"C:\Users\John Doe\Documents", absolutePath.Value);
    }

    [Theory]
    [InlineData("/foo/bar", "/foo", false)]
    [InlineData("/foo/", "/foo/bar/", true)]
    [InlineData("/foo", "/foo1/bar/", false)]
    [InlineData("/foo", "/foo1", false)]
    [InlineData("/foo", "/foo", true)]
    public void IsPrefixOf(string prefix, string other, bool result)
    {
        Assert.Equal(result, new LocalPath(prefix).IsPrefixOf(new LocalPath(other)));
    }

    [Fact]
    public void RelativePathIsNormalizedOnCreation()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string path = @"Users/John Doe\Documents";
        var relativePath = new LocalPath(path);
        Assert.Equal(@"Users\John Doe\Documents", relativePath.Value);
    }

    [Fact]
    public void LocalPathConvertedFromAbsolute()
    {
        var absolutePath = new AbsolutePath("/foo/bar");
        LocalPath localPath1 = absolutePath;
        var localPath2 = new LocalPath(absolutePath);

        Assert.Equal(localPath1, localPath2);
    }

    [Fact]
    public void ResolveToCurrentDirectoryTests()
    {
        var localPath = new LocalPath("foo/bar");
        var currentDirectory = AbsolutePath.CurrentWorkingDirectory;
        var expected = currentDirectory / localPath;
        Assert.Equal(expected, localPath.ResolveToCurrentDirectory());

        try
        {
            var newCurrentDirectory = new AbsolutePath(Path.GetTempPath()).Canonicalize();
            output.WriteLine("New current directory: " + newCurrentDirectory);
            Environment.CurrentDirectory = newCurrentDirectory.Value;
            expected = newCurrentDirectory / localPath;
            Assert.Equal(expected, localPath.ResolveToCurrentDirectory());
        }
        finally
        {
            Environment.CurrentDirectory = currentDirectory.Value;
            output.WriteLine("Current directory reset back to: " + currentDirectory);
        }
    }

    private static bool CreateJunction(string path, string target)
    {
        return Mklink(path, target, "J");
    }

    private static bool Mklink(string path, string target, string type)
    {
        string cmdline = $"cmd /c mklink /{type} {path} {target}";

        ProcessStartInfo si = new ProcessStartInfo("cmd.exe", cmdline)
        {
            UseShellExecute = false
        };

        Process? p = Process.Start(si);
        if (p == null)
        {
            return false;
        }
        p.WaitForExit();

        return p.ExitCode == 0;
    }
}
