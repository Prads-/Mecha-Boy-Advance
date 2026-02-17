namespace GbaLoader.UI;

using Contracts;
using GbaLoader.Helpers;
using Terminal.Gui;

internal class RomListUi : IUi
{
    private static readonly List<Rom> _roms;
    
    private readonly ListView _listView;
    private readonly Label _status;
    private readonly Action _goToSettings;

    static RomListUi()
    {
        var gbaRomPaths = Directory.GetFiles(
            AppSettings.RomDirectory,
            "*.gba",
            SearchOption.AllDirectories);

        var gbcRomPaths = Directory.GetFiles(
            AppSettings.RomDirectory,
            "*.gbc",
            SearchOption.AllDirectories);

        var gbRomPaths = Directory.GetFiles(
            AppSettings.RomDirectory,
            "*.gb",
            SearchOption.AllDirectories);

        _roms = gbaRomPaths.Select(s => GetRom(s, "Game Boy Advance"))
            .Union(gbcRomPaths.Select(s => GetRom(s, "Game Boy Colour")))
            .Union(gbRomPaths.Select(s => GetRom(s, "Game Boy")))
            .OrderBy(s => s.Name)
            .ToList();
    }

    public RomListUi(Action goToSettings)
    {
        _listView = new ListView(_roms)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            AllowsMarking = false
        };

        _listView.OpenSelectedItem += args =>
        {
            var dlg = new Dialog("Launching", 40, 7);
            dlg.Add(new Label("Loading, please wait...")
            {
                X = Pos.Center(),
                Y = Pos.Center()
            });

            Application.Top.Add(dlg);
            Application.Refresh();

            try
            {
                EmulatorService.Launch(_roms[_listView.SelectedItem].Path);
            }
            catch { }
            finally
            {
                Application.Top.Remove(dlg);
                Application.Refresh();
            }

            //Letting keys to be flushed
            Thread.Sleep(1000);
        };

        _listView.KeyPress += args =>
        {
            ProcessInput(args);
        };

        _status = new Label("Use ↑/↓ to scroll. Start button to select game. Select button for settings")
        {
            X = 0,
            Y = Pos.Bottom(_listView),
            Width = Dim.Fill()
        };

        _goToSettings = goToSettings;
        _sshModeEnabled = AppSettings.SshAccess;
    }

    public void ProcessInput(View.KeyEventEventArgs args)
    {
        if (!_sshModeEnabled)
        {
            _konamiCode[_konamiIndex](args.KeyEvent.Key);

            //For some reason left and right key generate multiple events
            //If we mark these as handle, they seem to be ok
            if (args.KeyEvent.Key == Key.CursorLeft || args.KeyEvent.Key == Key.CursorRight)
                args.Handled = true;

            if (_konamiIndex == -1)
            {
                _sshModeEnabled = true;

                //Starting NM should give us ssh access
                LinuxHelper.StartNM();

                MessageBox.Query(
                    "SSH Enabled",
                    "Hooray! You have enabled SSH on the device 8)",
                    "Ok");
            }
        }

        if (args.KeyEvent.Key == Key.Backspace)
        {
            _goToSettings();
            args.Handled = true;
        }
    }

    public void ShowUi(Window window)
    {
        window.RemoveAll();
        window.Add(_listView, _status);
    }

    private static Rom GetRom(string romPath, string type)
    {
        var fileName = Path.GetFileName(romPath);

        return new Rom
        {
            Name = fileName,
            Path = romPath,
            Type = type
        };
    }

    private class Rom
    {
        public required string Name { get; set; }
        public required string Path { get; set; }
        public required string Type { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }
    }

    // Can be enabled using the konami code in roms page when ssh is disabled in config
    // Add this in because I didn't want to start NM on startup since it took long time to
    // get to multi user target but I needed a way to enable it if I ever wanted to debug something
    // or update the loader
    private static bool _sshModeEnabled;
    private static int _konamiIndex = 0;

    private static List<Action<Key>> _konamiCode = new List<Action<Key>>
    {
        key => _konamiIndex = key == Key.CursorUp ? 1 : 0,
        key => _konamiIndex = key == Key.CursorUp ? 2 : 0,
        key => _konamiIndex = key == Key.CursorDown ? 3 : 0,
        key => _konamiIndex = key == Key.CursorDown ? 4 : 0,
        key => _konamiIndex = key == Key.CursorLeft ? 5 : 0,
        key => _konamiIndex = key == Key.CursorRight ? 6 : 0,
        key => _konamiIndex = key == Key.CursorLeft ? 7 : 0,
        key => _konamiIndex = key == Key.CursorRight ? 8 : 0,
        key => _konamiIndex = key == Key.Z || key == Key.z ? 9 : 0,
        key => _konamiIndex = key == Key.X || key == Key.x ? -1 : 0,
    };
}