namespace GbaLoader.Helpers;

using System.Diagnostics;
using System.IO;

internal static class FileHelper
{
    //Writes to file and flushes it to the disk immediately
    //The console can power off at anytime so it's important to flush the data to the disk
    public static void SafeWrite(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);

        File.WriteAllText(fullPath, content);
        FlushFileToDisk(fullPath);
    }

    public static void FlushFileToDisk(string path)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sync",
            ArgumentList = { "-d", path },
            UseShellExecute = false
        });

        p?.WaitForExit();
    }

    public static void FlushAllToDisk()
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sync",
            UseShellExecute = false
        });

        p?.WaitForExit();
    }
}
