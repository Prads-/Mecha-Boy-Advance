namespace GbaLoader;

using System.Device.Gpio;

internal class GpioKey
{
    private readonly GpioController _gpioController;
    private readonly int _pin;
    private readonly UInputKeyboard _uInputKeyboard;

    private readonly int? _keyCode;
    private Action? _keyAction;

    private bool _lastKeyState;

    public GpioKey(
        GpioController gpioController,
        UInputKeyboard uInputKeyboard,
        int pin,
        int keyCode)
    {
        _pin = pin;
        _keyCode = keyCode;
        _keyAction = null;
        _uInputKeyboard = uInputKeyboard;
        _lastKeyState = false;
        _gpioController = gpioController;

        _gpioController.OpenPin(pin, PinMode.InputPullUp);
    }

    public GpioKey(
        GpioController gpioController,
        UInputKeyboard uInputKeyboard,
        int pin,
        Action keyAction)
    {
        _pin = pin;
        _keyCode = null;
        _keyAction = keyAction;
        _uInputKeyboard = uInputKeyboard;
        _lastKeyState = false;
        _gpioController = gpioController;

        _gpioController.OpenPin(pin, PinMode.InputPullUp);
    }

    public void CheckKey()
    {
        var currentState = _gpioController.Read(_pin) == PinValue.Low;

        try
        {
            if (_keyCode.HasValue)
            {
                if (currentState && !_lastKeyState)
                    _uInputKeyboard.KeyDown(_keyCode.Value);
                else if (!currentState && _lastKeyState)
                    _uInputKeyboard.KeyUp(_keyCode.Value);
            }
            else if (_keyAction != null)
            {
                if (currentState && !_lastKeyState)
                    _keyAction.Invoke();
            }
        }
        finally
        {
            _lastKeyState = currentState;
        }
    }
}