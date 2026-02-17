namespace GbaLoader.Helpers;

using System.Diagnostics;

internal static class LinuxHelper
{
    public static void ForcePwmAudio()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/amixer",
                Arguments = "cset numid=3 1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            p?.WaitForExit(2000);
        }
        catch
        {
        }
    }

    public static void StartNM()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/sudo",
                Arguments = "/bin/systemctl start NetworkManager.service",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process.Start(psi);
        }
        catch
        {
        }
    }
}