# Poliwhirl Clock

The Poliwhirl clock from **Pokémon Stadium 2**'s Mystery Gift feature, now as your own live 4K desktop clock widget for Windows!
It's synchronized to your own internal PC's time and weekday👉🏻 Ticks every second, shows today's weekday, sits in the topright corner just like in the game, and it plays the
game's own ticking sound too!(can be turned off by right clicking on the clock to open its menu)

> **Unofficial fan mini project by me as a BIG FAN of the game .** This is not affiliated with or endorsed by Nintendo, The Pokémon Company, Creatures Inc.
> or GAME FREAK inc. The artwork and sound are their property. See [DISCLAIMER.md](DISCLAIMER.md).

<p align="center"><img src="images/preview.png" alt="Poliwhirl Clock at 10:09:35 on a Saturday" width="360"></p>

## How was this made?!

- The clock's code, textures and placement were tracked in the game rom's assembly code.
  The Mystery Gift screen draws the body at (451, 45) on the 640×480 screen, so I made sure the clock keeps those proportions on any screen.
- The tick you hear is the game's own sound effect, decoded from its VADPCM sample and converted into .WAV
- The weekday colored text was handmade in Photoshop, and are 6× upscales of the game's 144×152 texture. The hands were redrawn as vector shapes
  traced from the original 16×31 sprites(long live the Pen tool!)

**Made with:** the Poliwhirl image sitting on the cloud was upscaled with Topaz Bloom, and the hands were redrawn in Adobe Photoshop. The weekdays texts were made handmade in Photoshop faithfully.

## Download and run

1. Download **`PoliwhirlClock-<version>.zip`** from [Releases](../../releases)👈🏻 and extract it anywhere you like!
Desktop, Laptop, Documents, a USB stick…you know the drill).
2. Double-click **`PoliwhirlClock.exe`** and done.

The images and the tick sound are built into the exe. Just to keep it clean for you.

**Requirements:** Windows 10 (version 1903 or later) or Windows 11. The .NET Framework 4.8 it needs is already
built into these versions, so there is nothing to install.

**"Windows protected your PC"?** The exe isn't code-signed, so Windows SmartScreen warns about downloaded copies.
Click **More info → Run anyway**. The full source code is in this repository.

## Features

- Sits on the desktop in your topright corner, at the same place and size like in Stadium 2!
  (31.7% of the screen height). If you got a 3840×2160 monitor, then it's drawn at real 4K pixels.
- Follows your Windows clock. The hands use the game's code formula: hour `(h + m/60)·30°`, minute `(m + s/60)·6°`,
  second `s·6°`.
- Shows the colored text for today's weekday (SUNDAY … SATURDAY) and switches right at midnight respectively.
- Plays Pokémon Stadium 2's clock tick once per second (it can be turned off).
- Stays behind your windows like a desktop gadget, and stays visible with **Win + D**.
- Sharp on any resolution and display scaling (per-monitor DPI aware).

## Using it

Right-click the clock, or the Poliwhirl icon in the system tray:

| Menu item | What it does |
|-----------|--------------|
| **Size** | 50% / 75% / Stadium 2 size (100%) / 125% / 150%. Keeps the top-right corner in place |
| **Position** | Nudge it in steps of 1% of the screen height (diagonal, up, down, left, right). The menu stays open so you can click several times. "Back to the Stadium 2 spot" undoes all moving |
| **Lock position** | Stops accidental dragging |
| **Always on top** | Keeps it above all windows instead of on the desktop |
| **Tick sound** | Turns the tick on or off |
| **Tick volume** | Quiet (25%) / Stadium 2 level (50%) / Loud (75%) / Full (100%). Clicking one plays a preview |
| **Reload artwork** | Re-reads `assets\` after you change the art |
| **Start with Windows** | Adds it to, or removes it from, your Windows start-up list |
| **Exit** | Closes the clock |

You can also **drag** the clock with the left mouse button. Double-click the tray icon to show the clock on top of
everything for 6 seconds.

**Settings** are saved in `settings.ini` next to the exe. If that folder isn't writable (for example under
`Program Files`), they go to `%APPDATA%\PoliwhirlClock` instead.

**To uninstall:** turn off *Start with Windows* (if you turned it on), choose *Exit*, then delete the folder
(and `%APPDATA%\PoliwhirlClock` if it exists).

## Changing the art or sound

All the art is built into the exe. To replace any of it, create an `assets` folder next to `PoliwhirlClock.exe`
and put just the files you want to change in it, with these names. Files you don't provide keep using the
built-in version.

| File | Size | Notes |
|------|------|-------|
| `body_SUNDAY.png` … `body_SATURDAY.png` | 1728×1824 | one per weekday, transparent background |
| `body_base.png` | 1728×1824 | fallback body |
| `hand_hour.png`, `hand_minute.png`, `hand_second.png` | 192×372 | pivot at the bottom-centre |
| `center_pin.png` | 192×120 | drawn on top, centred on the pivot |
| `layout.ini` | — | dial centre in the body art, and the default size/position |
| `tick.wav` | — | any 16-bit mono PCM WAV |

Then use **Reload artwork** in the menu (the tick sound is read when the clock starts).

## Building from source

Run **`build.cmd`**. It uses the C# compiler that ships with Windows, so nothing has to be installed.
The script closes a running clock, then compiles `src\PoliwhirlClock.cs` into `PoliwhirlClock.exe` with
everything in `assets\` built in.

The artwork is not included in this repository. Before building, put the files listed under
*Changing the art or sound* into an `assets\` folder next to `build.cmd`.

Optional: with Python 3 and Pillow installed, `build.cmd` also regenerates the icon (`tools\make_assets.py`).
If you keep your own master art elsewhere, list the folders in a `tools\masters.ini`; the docstring in
`make_assets.py` shows the format.

To make the release zip, run **`package.cmd <version>`**. It builds, then writes `dist\PoliwhirlClock-<version>.zip`.

## Licence

The **source code and scripts** are under the [MIT licence](LICENSE). The **artwork and audio** are not covered
by it: they belong to Nintendo / Creatures Inc. / GAME FREAK inc. (see [DISCLAIMER.md](DISCLAIMER.md)).
