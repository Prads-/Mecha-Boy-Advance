namespace GbaLoader;

using GbaLoader.Helpers;
using GbaLoader.UI;
using GbaLoader.UI.Contracts;
using System.Diagnostics;
using Terminal.Gui;

class Program
{
    private static CancellationTokenSource _cts;
    private static Window? _win;

    private static readonly IUi _romListUi;
    private static readonly IUi _settingsUi;
    private static IUi? _currentUi;

    static Program()
    {
        _cts = new CancellationTokenSource();
        _romListUi = new RomListUi(() => ChangeUi(_settingsUi!));
        _settingsUi = new SettingsUi(() => ChangeUi(_romListUi));
    }

    static async Task Main()
    {
        WaitForTerminal();
        Application.Init();
        
        _currentUi = _romListUi;

        var cancellationToken = _cts.Token;

        var backgroundTasks = Task.Run(async () => 
        {
            using var keyManager = new KeyManager();
            using var fanController = new CoolingFanController();

            LinuxHelper.ForcePwmAudio();

            if (AppSettings.SshAccess)
                LinuxHelper.StartNM();

            await Task.WhenAll(
                keyManager.CheckKeysAsync(cancellationToken),
                fanController.ControlFanAsync(cancellationToken));
        });

        ShowMain();

        _cts.Cancel();
        await backgroundTasks;
    }

    static void WaitForTerminal()
    {
        //stdin must not be redirected
        while (Console.IsInputRedirected)
            Thread.Sleep(1);

        //terminal must have a real size
        while (Console.WindowWidth == 0 || Console.WindowHeight == 0)
            Thread.Sleep(1);
    }

    static void ShowMain()
    {
        var top = Application.Top;

        _win = new Window($"{AppSettings.ApplicationName} {AppSettings.ApplicationVersion}")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        top.Add(_win);

        _currentUi!.ShowUi(_win);

        top.KeyPress += args =>
        {
            _currentUi.ProcessInput(args);
        };

        Application.Run(ex =>
        {
            // Swallow the dumb "throw new Exception()" from CursesDriver.ProcessInput
            if (ex.ToString().Contains("Terminal.Gui.CursesDriver.ProcessInput", StringComparison.Ordinal))
                return true; // handled: keep running

            return false;
        });
        
        Application.Shutdown();
    }

    static void ChangeUi(IUi ui)
    {
        _currentUi = ui;
        _currentUi.ShowUi(_win!);
    }
}