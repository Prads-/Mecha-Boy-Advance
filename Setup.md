# Linux setup guide for Mecha Boy Advance

## Flash the OS to the SD card
I used the Raspberry PI Imager tool and the Raspberry Pi OS 64 bit Lite. Make sure you turn SSH on to make life easier ;)

## First boot
Log in using SSH:

```
ssh pi@pi.local
```

Update packages:

```
sudo apt update
sudo apt upgrade -y
```

## Install Waveshare LCD driver/config

If you are using Waveshare LCD, follow the "Config Driver" section: https://www.waveshare.com/wiki/3.5inch_RPi_LCD_(B)#For_All_Raspberry_Pi_Versions (Only follow the "Config Driver" section, ignore the rest).

After you apply it and reboot, confirm driver is running:

```
ls -l /dev/fb*
dmesg | grep -i -E "ili9486|fbtft|ads7846|fb"
```

There should now be 2 frame buffers, fb0 and fb1. We will get rid of fb1 later so we only have fb0 for the LCD.

## Install minimal X11 kiosk stack (no desktop)

```
sudo apt install -y \
  xserver-xorg xinit xserver-xorg-video-fbdev \
  matchbox-window-manager
```

## Bind Xorg to the LCD framebuffer (/dev/fb0)

```
sudo mkdir -p /etc/X11/xorg.conf.d
sudo tee /etc/X11/xorg.conf.d/99-lcd.conf >/dev/null <<'EOF'
Section "Device"
  Identifier "LCD"
  Driver "fbdev"
  Option "fbdev" "/dev/fb0"
EndSection
EOF
```

## Allow starting X from systemd/SSH

```
sudo nano /etc/X11/Xwrapper.config
```

and set:

```
allowed_users=anybody
needs_root_rights=yes
```

## Setup xterm and dotnet

Install xterm:

```
sudo apt install -y xterm
```

Install .net 8.0:

```
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 8.0 --runtime dotnet
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

## Enable unput and gpio

```
sudo nano /etc/udev/rules.d/99-uinput.rules
```

and paste: `KERNEL=="uinput", MODE="0660", GROUP="input", OPTIONS+="static_node=uinput"`

Then run:

```
sudo usermod -aG input pi
sudo usermod -aG gpio pi
sudo udevadm control --reload-rules
sudo udevadm trigger
sudo reboot
```

## Setup C# loader

Open the `GbaLoader.sln` file in Visual Studio (or whatever IDE you use for C#) and build the project. Copy the built binaries to the `/home/pi/loader` directory.

## Setup X

Create ~/.xinitrc that will load our C# loader:

```
cat > /home/pi/.xinitrc <<'EOF'
#!/bin/sh
matchbox-window-manager -use_cursor no -use_titlebar no &

exec xterm -fullscreen -geometry 80x24 -fg white -bg black -e sh -lc '
# Wait until terminal has a real size
while ! stty size >/dev/null 2>&1; do
  sleep 0.05
done

cd /home/pi/loader || exit 1
export TERM=xterm-256color
exec /home/pi/.dotnet/dotnet ./GbaLoader.dll
'
EOF
chmod +x /home/pi/.xinitrc
```

## Auto-start X on boot on the LCD (systemd service)

Create lcd-kiosk.service to start X on startup:

```
sudo tee /etc/systemd/system/lcd-kiosk.service >/dev/null <<'EOF'
[Unit]
Description=LCD Kiosk X11 Session
After=getty@tty1.service systemd-user-sessions.service
Conflicts=getty@tty1.service

[Service]
User=pi
Environment=HOME=/home/pi
Environment=USER=pi
WorkingDirectory=/home/pi

StandardInput=tty
TTYPath=/dev/tty1
TTYReset=yes
TTYVHangup=yes

ExecStart=/usr/bin/startx -- -nocursor
Restart=always
RestartSec=1

[Install]
WantedBy=multi-user.target
EOF
```

Then enable the service:

```
sudo systemctl daemon-reload
sudo systemctl enable lcd-kiosk.service
```

## Mount ROM USB

Create this script to mount the rom usb to `/mnt/roms` on boot:

```
sudo tee /usr/local/bin/usb-rom-mount.sh >/dev/null <<'EOF'
#!/bin/bash
set -euo pipefail

MNT="/mnt/roms"
mkdir -p "$MNT"

# If already mounted, do nothing
if mountpoint -q "$MNT"; then
  exit 0
fi

udevadm settle

# Find first removable /dev/sdX partition with a filesystem.
# This works well on Raspberry Pi where TRAN can be blank.
DEV="$(lsblk -rpno NAME,TYPE,FSTYPE,RM | awk '
  $2=="part" && $3!="" && $4==1 && $1 ~ /^\/dev\/sd[a-z][0-9]+$/ { print $1; exit }
')"

if [ -z "${DEV:-}" ]; then
  echo "No removable sdX partition found" >&2
  echo "Debug lsblk:" >&2
  lsblk -rpno NAME,TYPE,FSTYPE,RM,MODEL >&2 || true
  exit 1
fi

FSTYPE="$(lsblk -no FSTYPE "$DEV" | head -n1)"
if [ -z "${FSTYPE:-}" ]; then
  echo "No filesystem detected on $DEV" >&2
  exit 1
fi

# Mount with sensible defaults.
# For vfat/exfat, force ownership to pi (uid/gid 1000) so saves can be created.
OPTS="rw"

case "$FSTYPE" in
  vfat|fat|msdos|exfat)
    OPTS="$OPTS,uid=1000,gid=1000,umask=0002"
    ;;
  *)
    # For native Linux FS (ext4 etc) permissions are real; leave as-is.
    OPTS="$OPTS"
    ;;
esac

mount -t "$FSTYPE" -o "$OPTS" "$DEV" "$MNT"

echo "Mounted $DEV ($FSTYPE) at $MNT"
EOF

sudo chmod +x /usr/local/bin/usb-rom-mount.sh
```

Then create a systemd service to run the script:

```
sudo tee /etc/systemd/system/usb-rom-mount.service >/dev/null <<'EOF'
[Unit]
Description=Mount USB ROM storage at /mnt/roms
After=systemd-udev-settle.service
Wants=systemd-udev-settle.service

[Service]
Type=oneshot
ExecStart=/usr/local/bin/usb-rom-mount.sh
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
```

Enable and start systemd service:

```
sudo systemctl daemon-reload
sudo systemctl enable usb-rom-mount.service
sudo systemctl start usb-rom-mount.service
```

Update unit part of the lcd-kiosk.service service to wait for the roms usb to be mounted:

```
[Unit]
Description=LCD Kiosk X11 Session
After=getty@tty1.service systemd-user-sessions.service usb-rom-mount.service
Wants=usb-rom-mount.service
Conflicts=getty@tty1.service
```

## Install mGBA

```
sudo apt install -y mgba-sdl
```

## Allow sudo access

```
sudo visudo -f /etc/sudoers.d/loader-power
```

and paste: `pi ALL=(root) NOPASSWD: /sbin/shutdown, /sbin/poweroff, /sbin/reboot`

## Setup audio

We are using PCM5102 DAC and PAM8403 AMP. Important: Some PCM5102 boards require solder bridges on the back (e.g. SCK, H1L-H4L) to enable I2S mode. Without these, output will be silent. Please research and check you board needs them soldered or not.

First, disable the on board audio by editing the `/boot/firmware/config.txt` and including `dtparam=audio=off` line. This disables HDMI / headphone audio so I2S can claim the pins.

Then enable I2S + DAC Overlay by adding these lines:

```
dtparam=i2s=on
dtoverlay=hifiberry-dac
```

The PCM5102 is compatible with the hifiberry-dac overlay.

## Optimisations

Disable HDMI by adding these lines to `/boot/firmware/config.txt`

```
hdmi_ignore_hotplug=1
enable_tvout=0
```

Also comment out all the HDMI related lines that were previously added when you configured the lcd driver:

```
# hdmi_force_hotplug=1
# hdmi_group=2
# hdmi_mode=87
# hdmi_cvt 640 480 60 6 0 0 0
# hdmi_drive=2
```

## Disable Camera / MMAL Stack

```
sudo nano /etc/modprobe.d/blacklist.conf
```

and add these lines:

```
blacklist bcm2835_v4l2
blacklist bcm2835_camera
blacklist bcm2835_codec
blacklist bcm2835_isp
blacklist bcm2835_mmal_vchiq
blacklist vc_sm_cma
```

## Disable Bluetooth

Edit `/boot/firmware/config.txt` by adding this line:

```
dtoverlay=disable-bt
```

Then disable the bluetooth service:

```
systemctl disable hciuart.service
systemctl disable bluetooth.service
```

## Edit Kernel Command Line

`sudo nano /boot/firmware/cmdline.txt`

Add this to the kernel command line:

```
quiet loglevel=0 consoleblank=0 vt.global_cursor_default=0 fastboot noswap ro usbcore.autosuspend=-1
```

Do not add more lines to that file, it should all be in a single line.

## Boost the Waveshare 3.5 B v2 LCD Performance

If you are using the SPI LCD pannel, you need to update the driver params so that it runs in playable speed. I am running it on 60 MHz at 60 FPS (it actually doesn't hit that, not even close lol) but that means you will sacrifice some colour accuracy.

`sudo nano /boot/firmware/cmdline.txt`

and update the `dtoverlay=waveshare35b-v2` line to:

```
dtoverlay=waveshare35b-v2,fps=60,speed=60000000
```

You can verify the speed after rebooting and running:

```
cat /sys/class/graphics/fb0/virtual_size
cat /sys/class/graphics/fb0/stride
cat /sys/class/graphics/fb0/bits_per_pixel
fbset -fb /dev/fb0 -i
```

## Overclock PI CPU & Boot Turbo for 15 seconds

Add these lines to `/boot/firmware/config.txt`:

```
initial_turbo=15
force_turbo=1

arm_freq=1200
core_freq=500
sdram_freq=500
over_voltage=2

```

Verify using:

```
vcgencmd measure_clock arm
vcgencmd get_throttled
```

## Remove Cloud Init crap

```
apt purge cloud-init
rm -rf /etc/cloud /var/lib/cloud
```

## Remove udev settle delay

```
systemctl disable systemd-udev-settle.service
systemctl mask systemd-udev-settle.service
```

## Remove Swap & ZRAM

```
systemctl disable dphys-swapfile.service
systemctl mask dphys-swapfile.service

systemctl disable rpi-resize-swap-file.service
systemctl mask rpi-resize-swap-file.service

systemctl disable systemd-zram-setup@zram0.service
systemctl mask systemd-zram-setup@zram0.service
```

## Disable Maintenance Timers

```
systemctl disable apt-daily.timer
systemctl disable apt-daily-upgrade.timer
systemctl disable man-db.timer
systemctl disable logrotate.timer
systemctl disable dpkg-db-backup.timer
systemctl disable e2scrub_all.timer
systemctl disable fstrim.timer
```

## SSH via Socket Activation

This one makes boot slightly faster by disabling ssh service that multi user target requires and instead using ssh socket service that will enable async from boot

```
systemctl disable ssh.service
systemctl enable ssh.socket
```

Do not mask `ssh.service` or else `ssh.socket` won't work

## Disable Network Manager

You can enable NM when needed using `appsettings.json` config in the loader app or using the konami code in the rom list UI in the loader app. So we can disable NM on boot which reduces around 5 seconds of boot time:

```
sudo systemctl disable NetworkManager.service
```