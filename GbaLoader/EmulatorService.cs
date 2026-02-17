using GbaLoader.Helpers;
using System.Diagnostics;

namespace GbaLoader;

internal static class EmulatorService
{
    public static void Launch(string romPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/usr/games/mgba",
            Arguments = $"-f -C volume={AppSettings.AudioVolume} " +
            $"-C audio.buffer=4096 -C audio.sync=1 -C video.sync=0 \"{romPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        using var proc = Process.Start(psi);
        proc!.WaitForExit();

        //Sync any save files emulator might have written to disk
        FileHelper.FlushAllToDisk();
    }

    public static void Stop()
    {
        Process.Start("pkill", "-TERM mgba");
    }
}