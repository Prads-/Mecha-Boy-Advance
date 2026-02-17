namespace GbaLoader.UI;

using GbaLoader.UI.Contracts;
using Terminal.Gui;

internal class SettingsUi : IUi
{
    private static readonly List<SettingItem> _settings;

    private readonly ListView _listView;
    private readonly Label _status;
    private readonly Action _goBack;

    static SettingsUi()
    {
        _settings =
        [
            new SettingItem
            {
                Name = "Audio Volume",
                Value = AppSettings.AudioVolume,
                OnChange = AppSettings.SetAudioVolume,
                OnSelected = () => { },
                ShowValue = true
            },
            new SettingItem
            {
                Name = "About",
                OnChange = _ => { },
                ShowValue = false,
                OnSelected = () =>
                {
                    MessageBox.Query(
                        "About",
                        GetMessageText(),
                        "Ok");
                }
            }
        ];
    }

    public SettingsUi(Action goBack)
    {
        _listView = new ListView(_settings)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            AllowsMarking = false
        };

        _listView.KeyPress += args =>
        {
            ProcessInput(args);
        };

        _status = new Label("Use ←/→ to adjust, B to go back")
        {
            X = 0,
            Y = Pos.Bottom(_listView),
            Width = Dim.Fill()
        };

        _goBack = goBack;
    }

    public void ProcessInput(View.KeyEventEventArgs args)
    {
        var idx = _listView.SelectedItem;
        
        if (idx < 0 || idx >= _settings.Count)
            return;

        var item = _settings[idx];

        if (args.KeyEvent.Key == Key.CursorLeft)
        {
            item.Value = Math.Max(0, item.Value - 1);
            item.OnChange(item.Value);
            
            _listView.SetSource(_settings);
            _listView.SelectedItem = idx;
            
            args.Handled = true;
            return;
        }

        if (args.KeyEvent.Key == Key.CursorRight)
        {
            item.Value = Math.Min(5, item.Value + 1);
            item.OnChange(item.Value);

            _listView.SetSource(_settings);
            _listView.SelectedItem = idx;

            args.Handled = true;
            return;
        }

        if (args.KeyEvent.Key == Key.Enter)
        {
            item.OnSelected();
            args.Handled = true;
            return;
        }

        if (args.KeyEvent.Key == Key.z || args.KeyEvent.Key == Key.Z)
        {
            _goBack();
            args.Handled = true;
            return;
        }
    }

    public void ShowUi(Window window)
    {
        window.RemoveAll();
        window.Add(_listView, _status);
    }

    private static string GetMessageText()
    {
        return $"Owner name: {AppSettings.OwnerName}\n" +
            $"Version: {AppSettings.ApplicationVersion}\n\n" +
            $"{AppSettings.Message}";
    }

    private class SettingItem
    {
        public required string Name { get; set; }
        public int Value { get; set; }
        public required Action<int> OnChange { get; set; }
        public required Action OnSelected { get; set; }
        public required bool ShowValue { get; set; }

        public override string ToString()
        {
            if (ShowValue)
                return $"{Name} - {Value}";

            return Name;
        }
    }
}
