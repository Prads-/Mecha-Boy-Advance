using System.Device.Gpio;
using System.Diagnostics;

namespace GbaLoader;

internal class CoolingFanController : IDisposable
{
    private readonly GpioController _gpioController;
    
    private bool _fanRunning;

    private const int Pin = 14;
    private const float FanStart = 55.0f;
    private const float FanStop = 48.0f;

    public CoolingFanController()
    {
        _gpioController = new GpioController();
        _gpioController.OpenPin(Pin, PinMode.Output);

        _gpioController.Write(Pin, PinValue.Low);
        _fanRunning = false;
    }

    public async Task ControlFanAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var cpuTemp = GetCpuTemp();

                if (_fanRunning)
                {
                    if (cpuTemp <= FanStop)
                    {
                        StopFan();
                        _fanRunning = false;
                    }
                }
                else
                {
                    if (cpuTemp >= FanStart)
                    {
                        StartFan();
                        _fanRunning = true;
                    }
                }
            }
            catch { }

            await Task.Delay(3000, token)
                .ConfigureAwait(false);
        }
    }

    private void StartFan()
    {
        _gpioController.Write(Pin, PinValue.High);
    }

    private void StopFan()
    {
        _gpioController.Write(Pin, PinValue.Low);
    }

    private static float GetCpuTemp()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "vcgencmd",
            Arguments = "measure_temp",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        var output = p?.StandardOutput.ReadToEnd().Trim();
        p?.WaitForExit();

        if (string.IsNullOrWhiteSpace(output))
        {
            //Just to be safe, we will turn the fan on in case of error
            return 100.0f;
        }

        // output format: temp=46.2'C
        int start = output.IndexOf('=') + 1;
        int end = output.IndexOf('\'');

        var number = output[start..end];

        return float.Parse(number);
    }

    public void Dispose()
    {
        _gpioController?.Dispose();
    }
}