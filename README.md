# NetSpeedMonitor

Real-time network speed floating window for Windows.

## Features

- Download / upload speed (MB/s)
- Color by speed: green / orange / red
- Draggable, always on top
- Starts at bottom-left of primary screen
- Right-click to exit

## Build

```bash
csc /target:winexe net_speed.cs /reference:System.Windows.Forms.dll /reference:System.Drawing.dll
```

## Disclaimer

- For personal learning and research only.
- Author is not responsible for any use.
- Not for commercial or illegal use.
