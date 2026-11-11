"""Refresh the clock art in ../assets and build the app icon. Run by build.cmd (optional step).

If tools/masters.ini exists (your own art folders; it is git-ignored), copies the art whose content changed
from there, checking the image sizes. Without it, the art already in assets/ is used as-is. Always writes
layout.ini if missing (your edits are kept) and renders poliwhirl.ico from body_SATURDAY.png (ICON_SOURCE).

tools/masters.ini:
    [masters]
    bodies = <folder with SUNDAY.png ... SATURDAY.png and "Poliwhirl Clock 4K!.png">
    hands  = <folder with the "4K - ... Hand ....png" files>
    pin    = <folder with center_pin.png>
"""
import os, sys, hashlib, shutil, configparser
try:
    from PIL import Image
except ImportError:
    print("  Pillow (PIL) is not installed - skipping the art/icon refresh, using assets\\ as-is.")
    sys.exit(0)

APP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(APP, "assets")
MASTERS_INI = os.path.join(APP, "tools", "masters.ini")
DAYS = ["SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY"]

def master_sources():
    """{asset name: (source path, required size)} from tools/masters.ini, or {} when there is none."""
    if not os.path.exists(MASTERS_INI):
        return {}
    cfg = configparser.ConfigParser()
    cfg.read(MASTERS_INI, encoding="utf-8")
    m = cfg["masters"]
    res, tex, pin = m["bodies"], m["hands"], m["pin"]
    src = {f"body_{d}.png": (os.path.join(res, f"{d}.png"), (1728, 1824)) for d in DAYS}
    src.update({
        "body_base.png": (os.path.join(res, "Poliwhirl Clock 4K!.png"), (1728, 1824)),
        "hand_hour.png": (os.path.join(tex, "4K - Hour Hand (192x372, Photoshop glow).png"), (192, 372)),
        "hand_minute.png": (os.path.join(tex, "4K - Minute Hand (192x372, Photoshop glow).png"), (192, 372)),
        "hand_second.png": (os.path.join(tex, "4K - Second Hand (192x372, 12x nearest).png"), (192, 372)),
        "center_pin.png": (os.path.join(pin, "center_pin.png"), (192, 120)),
    })
    return src

LAYOUT = """; Poliwhirl Clock layout - art pixel coordinates of the 1728x1824 body images.
; Clock centre = centre of a circle fitted through the 12 numerals.
PivotX=860.6
PivotY=622.0
; hands are 192x372 with the pivot at the bottom-centre; the pin is 192x120, centred
HandPivotX=96
HandPivotY=372
PinCenterX=96
PinCenterY=60
; default size and place as fractions of the screen = Pokemon Stadium 2
; (clock body 144x152 drawn at x=451, y=45 on the 640x480 screen, i.e. 45 px from the right edge)
BodyHeight=0.3166667
MarginRight=0.0703125
MarginTop=0.09375
"""

def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()

def main():
    os.makedirs(ASSETS, exist_ok=True)
    ok = True
    sources = master_sources()
    if not sources:
        print("  no tools\\masters.ini - using the art already in assets\\")
    for name, (src, size) in sources.items():
        dst = os.path.join(ASSETS, name)
        if not os.path.exists(src):
            print(f"  MISSING source for {name}: {src}"); ok = False; continue
        with Image.open(src) as im:
            if im.size != size:
                print(f"  WRONG SIZE {name}: {im.size}, expected {size}"); ok = False; continue
        if os.path.exists(dst) and sha(dst) == sha(src):
            print(f"  unchanged  {name}")
        else:
            shutil.copy2(src, dst); print(f"  updated    {name}")
    lay = os.path.join(ASSETS, "layout.ini")
    if not os.path.exists(lay):
        open(lay, "w", encoding="ascii").write(LAYOUT); print("  wrote      layout.ini")
    make_icon()
    return 0 if ok else 1

ICON_SOURCE = "body_SATURDAY.png"   # the whole SATURDAY clock body is the exe/tray icon

def make_icon():
    art = Image.open(os.path.join(ASSETS, ICON_SOURCE)).convert("RGBA")
    art = art.crop(art.getchannel("A").getbbox())          # drop the transparent margin
    side = max(art.size)                                   # centre it on a transparent square
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(art, ((side - art.width) // 2, (side - art.height) // 2), art)
    sq.save(os.path.join(ASSETS, "poliwhirl.ico"), format="ICO",
            sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
    sq.resize((256, 256), Image.LANCZOS).save(os.path.join(ASSETS, "icon_preview_256.png"))
    print("  wrote      poliwhirl.ico")

if __name__ == "__main__":
    sys.exit(main())
