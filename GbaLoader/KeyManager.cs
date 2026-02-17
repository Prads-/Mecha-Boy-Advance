namespace GbaLoader;

using System.Device.Gpio;

internal class KeyManager : IDisposable
{
    private readonly GpioController _gpioController;
    private readonly UInputKeyboard _uInputKeyboard;
    
    private readonly GpioKey _startKey;
    private readonly GpioKey _selectKey;
    private readonly GpioKey _resetKey;
    private readonly GpioKey _upKey;
    private readonly GpioKey _downKey;
    private readonly GpioKey _leftKey;
    private readonly GpioKey _rightKey;
    private readonly GpioKey _aKey;
    private readonly GpioKey _bKey;
    private readonly GpioKey _lKey;
    private readonly GpioKey _rKey;

    public KeyManager()
    {
        _gpioController = new GpioController();
        _uInputKeyboard = new UInputKeyboard();

        _startKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            27, //Pin 13
            UInputKeyboard.LinuxKeyCodes.KEY_ENTER);

        _selectKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            4, //Pin 7
            UInputKeyboard.LinuxKeyCodes.KEY_BACKSPACE);

        _resetKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            16, //Pin 36
            EmulatorService.Stop);

        _upKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            13, //Pin 33
            UInputKeyboard.LinuxKeyCodes.KEY_UP);

        _downKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            22, //Pin 15
            UInputKeyboard.LinuxKeyCodes.KEY_DOWN);

        _leftKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            5, //Pin 29
            UInputKeyboard.LinuxKeyCodes.KEY_LEFT);

        _rightKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            6, //Pin 31
            UInputKeyboard.LinuxKeyCodes.KEY_RIGHT);

        _aKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            12, //Pin 32
            UInputKeyboard.LinuxKeyCodes.KEY_X);

        _bKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            23, //Pin 16
            UInputKeyboard.LinuxKeyCodes.KEY_Z);

        _lKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            20, //Pin 38
            UInputKeyboard.LinuxKeyCodes.KEY_A);

        _rKey = new GpioKey(
            _gpioController,
            _uInputKeyboard,
            26, //Pin 37
            UInputKeyboard.LinuxKeyCodes.KEY_S);
    }

    public async Task CheckKeysAsync(
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                //Directional
                _upKey.CheckKey();
                _downKey.CheckKey();
                _leftKey.CheckKey();
                _rightKey.CheckKey();

                //A and B
                _aKey.CheckKey();
                _bKey.CheckKey();

                //L and R
                _lKey.CheckKey();
                _rKey.CheckKey();

                //Start and select
                _startKey.CheckKey();
                _selectKey.CheckKey();

                //Reset
                _resetKey.CheckKey();
            } catch { }

            await Task.Delay(20, token)
                .ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _gpioController?.Dispose();
        _uInputKeyboard?.Dispose();
    }
}
