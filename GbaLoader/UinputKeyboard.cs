using GbaLoader;

using System.Runtime.InteropServices;

public sealed class UInputKeyboard : IDisposable
{
    private const int O_WRONLY = 1;

    private const int EV_SYN = 0x00;
    private const int EV_KEY = 0x01;

    private const int SYN_REPORT = 0;

    const int UINPUT_IOCTL_BASE = 'U';

    static readonly uint UI_SET_EVBIT =
        IoctlNum.IOW(UINPUT_IOCTL_BASE, 100, sizeof(int));

    static readonly uint UI_SET_KEYBIT =
        IoctlNum.IOW(UINPUT_IOCTL_BASE, 101, sizeof(int));

    static readonly uint UI_DEV_CREATE =
        IoctlNum.IO(UINPUT_IOCTL_BASE, 1);

    static readonly uint UI_DEV_DESTROY =
        IoctlNum.IO(UINPUT_IOCTL_BASE, 2);

    private int _fd;
    private bool _created;

    public UInputKeyboard(string deviceName = "gba-loader-input-controller",
                          ushort vendorId = 0x1234,
                          ushort productId = 0x5678,
                          ushort version = 1)
    {
        _fd = open("/dev/uinput", O_WRONLY);
        
        if (_fd < 0)
            throw new InvalidOperationException($"Failed to initialise input errno={Marshal.GetLastWin32Error()}");

        // Enable key events
        Ioctl(_fd, UI_SET_EVBIT, EV_KEY);
        Ioctl(_fd, UI_SET_EVBIT, EV_SYN);
        EnableKeys(_fd);

        // Create device descriptor
        var uidev = new uinput_user_dev
        {
            name = deviceName,
            id_bustype = 0x03, // BUS_USB
            id_vendor = vendorId,
            id_product = productId,
            id_version = version,
            ff_effects_max = 0,
            absmax = new int[64],
            absmin = new int[64],
            absfuzz = new int[64],
            absflat = new int[64],
        };

        WriteStruct(_fd, uidev);

        Ioctl(_fd, UI_DEV_CREATE);
        _created = true;

        // Give kernel time to register the device
        Thread.Sleep(200);
    }

    public void KeyDown(int linuxKeyCode)
    {
        EnsureNotDisposed();
        Emit(EV_KEY, (ushort)linuxKeyCode, 1);
        Sync();
    }

    public void KeyUp(int linuxKeyCode)
    {
        EnsureNotDisposed();
        Emit(EV_KEY, (ushort)linuxKeyCode, 0);
        Sync();
    }

    private void Sync() => Emit(EV_SYN, SYN_REPORT, 0);

    private void Emit(ushort type, ushort code, int value)
    {
        var ev = new input_event
        {
            time = new timeval(),
            type = type,
            code = code,
            value = value
        };

        WriteStruct(_fd, ev);
    }

    public void Dispose()
    {
        if (_fd >= 0)
        {
            try
            {
                if (_created)
                    Ioctl(_fd, UI_DEV_DESTROY);

                close(_fd);
            }
            catch
            {
                // swallow cleanup errors
            }
            _fd = -1;
        }
        GC.SuppressFinalize(this);
    }

    ~UInputKeyboard()
    {
        Dispose();
    }

    private void EnsureNotDisposed()
    {
        if (_fd < 0)
            throw new ObjectDisposedException(nameof(UInputKeyboard));
    }

    private static void Ioctl(int fd, uint request)
    {
        int r = ioctl(fd, request);
        if (r < 0)
            throw new InvalidOperationException($"ioctl(0x{request:X}) failed errno={Marshal.GetLastWin32Error()}");
    }

    private static void Ioctl(int fd, uint request, int value)
    {
        int r = ioctl(fd, request, value);
        if (r < 0)
            throw new InvalidOperationException($"ioctl(0x{request:X}) failed errno={Marshal.GetLastWin32Error()}");
    }

    private static void WriteStruct<T>(int fd, T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] bytes = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            Marshal.Copy(ptr, bytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        write(fd, bytes, bytes.Length);
    }

    // -------- Linux structs --------

    [StructLayout(LayoutKind.Sequential)]
    private struct timeval
    {
        public long tv_sec;
        public long tv_usec;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct input_event
    {
        public timeval time;
        public ushort type;
        public ushort code;
        public int value;
    }

    // Mirrors linux/uinput.h uinput_user_dev
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct uinput_user_dev
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;

        public ushort id_bustype;
        public ushort id_vendor;
        public ushort id_product;
        public ushort id_version;

        public int ff_effects_max;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmax;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmin;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absfuzz;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absflat;
    }

    // -------- libc imports --------

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, int value);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr write(int fd, byte[] buffer, int count);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    // ioctl encoding (Linux generic)
    private static class IoctlNum
    {
        const int IOC_NRBITS = 8;
        const int IOC_TYPEBITS = 8;
        const int IOC_SIZEBITS = 14;

        const int IOC_NRSHIFT = 0;
        const int IOC_TYPESHIFT = IOC_NRSHIFT + IOC_NRBITS;
        const int IOC_SIZESHIFT = IOC_TYPESHIFT + IOC_TYPEBITS;
        const int IOC_DIRSHIFT = IOC_SIZESHIFT + IOC_SIZEBITS;

        const int IOC_NONE = 0;
        const int IOC_WRITE = 1;

        static uint IOC(int dir, int type, int nr, int size) =>
            (uint)((dir << IOC_DIRSHIFT) |
                   (type << IOC_TYPESHIFT) |
                   (nr << IOC_NRSHIFT) |
                   (size << IOC_SIZESHIFT));

        public static uint IOW(int type, int nr, int size) => IOC(IOC_WRITE, type, nr, size);
        public static uint IO(int type, int nr) => IOC(IOC_NONE, type, nr, 0);
    }

    private static void EnableKeys(int fd)
    {
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_A);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_S);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_X);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_Z);

        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_ENTER);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_BACKSPACE);

        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_UP);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_DOWN);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_LEFT);
        Ioctl(fd, UI_SET_KEYBIT, LinuxKeyCodes.KEY_RIGHT);
    }

    public static class LinuxKeyCodes
    {
        public const int KEY_A = 30;
        public const int KEY_S = 31;
        public const int KEY_X = 45;
        public const int KEY_Z = 44;

        public const int KEY_ENTER = 28;
        public const int KEY_BACKSPACE = 14;
        
        public const int KEY_UP = 103;
        public const int KEY_DOWN = 108;
        public const int KEY_LEFT = 105;
        public const int KEY_RIGHT = 106;
    }
}