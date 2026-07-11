from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "outputs" / "assets"
APP_DIR = ROOT / "outputs" / "CDriveGovernanceDesktop"
ASSET_DIR.mkdir(parents=True, exist_ok=True)
APP_DIR.mkdir(parents=True, exist_ok=True)


def lerp(a, b, t):
    return int(a + (b - a) * t)


def color_lerp(c1, c2, t):
    return tuple(lerp(a, b, t) for a, b in zip(c1, c2))


def rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    return mask


def load_font(size, bold=True):
    candidates = [
        r"C:\Windows\Fonts\seguisb.ttf",
        r"C:\Windows\Fonts\segoeuib.ttf",
        r"C:\Windows\Fonts\arialbd.ttf",
        r"C:\Windows\Fonts\msyhbd.ttc",
    ] if bold else [
        r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arial.ttf",
        r"C:\Windows\Fonts\msyh.ttc",
    ]
    for candidate in candidates:
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size=size)
    return ImageFont.load_default()


def draw_centered_text(draw, box, text, font, fill, stroke_width=0, stroke_fill=None):
    bbox = draw.textbbox((0, 0), text, font=font, stroke_width=stroke_width)
    w = bbox[2] - bbox[0]
    h = bbox[3] - bbox[1]
    x = box[0] + (box[2] - box[0] - w) / 2 - bbox[0]
    y = box[1] + (box[3] - box[1] - h) / 2 - bbox[1]
    draw.text((x, y), text, font=font, fill=fill, stroke_width=stroke_width, stroke_fill=stroke_fill)


def make_icon(size=1024):
    scale = size / 1024
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Background: restrained software/security palette with enough contrast for small sizes.
    bg = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = bg.load()
    top = (16, 33, 66)
    mid = (23, 96, 151)
    bottom = (16, 147, 111)
    for y in range(size):
        t = y / max(1, size - 1)
        if t < 0.58:
            c = color_lerp(top, mid, t / 0.58)
        else:
            c = color_lerp(mid, bottom, (t - 0.58) / 0.42)
        for x in range(size):
            glow = int(22 * max(0, 1 - ((x - size * 0.72) ** 2 + (y - size * 0.2) ** 2) ** 0.5 / (size * 0.62)))
            px[x, y] = (min(255, c[0] + glow), min(255, c[1] + glow), min(255, c[2] + glow), 255)
    img.alpha_composite(bg)

    mask = rounded_mask(size, int(220 * scale))
    img.putalpha(mask)

    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    # Soft inner platform.
    d.rounded_rectangle(
        (int(106 * scale), int(108 * scale), int(918 * scale), int(916 * scale)),
        radius=int(172 * scale),
        outline=(255, 255, 255, 42),
        width=max(1, int(4 * scale)),
    )

    # Disk/radar ring.
    cx, cy = int(512 * scale), int(490 * scale)
    outer = int(310 * scale)
    inner = int(196 * scale)
    d.ellipse((cx - outer, cy - outer, cx + outer, cy + outer), outline=(184, 245, 255, 84), width=int(20 * scale))
    d.ellipse((cx - inner, cy - inner, cx + inner, cy + inner), outline=(255, 255, 255, 48), width=int(8 * scale))
    d.arc((cx - outer, cy - outer, cx + outer, cy + outer), 204, 330, fill=(56, 226, 170, 255), width=int(24 * scale))
    d.arc((cx - outer, cy - outer, cx + outer, cy + outer), 22, 90, fill=(111, 205, 255, 230), width=int(18 * scale))

    # Shield shape behind MYL.
    shield = [
        (int(512 * scale), int(180 * scale)),
        (int(725 * scale), int(258 * scale)),
        (int(704 * scale), int(545 * scale)),
        (int(512 * scale), int(745 * scale)),
        (int(320 * scale), int(545 * scale)),
        (int(299 * scale), int(258 * scale)),
    ]
    d.polygon(shield, fill=(12, 28, 56, 180), outline=(224, 250, 255, 86))

    # Scan line and small circuit nodes.
    d.line((int(280 * scale), int(387 * scale), int(744 * scale), int(387 * scale)), fill=(98, 237, 191, 185), width=int(10 * scale))
    for x, y, r, color in [
        (256, 304, 17, (126, 229, 255, 230)),
        (765, 345, 14, (70, 232, 179, 230)),
        (735, 660, 16, (255, 255, 255, 185)),
        (286, 647, 13, (126, 229, 255, 210)),
    ]:
        x, y, r = int(x * scale), int(y * scale), int(r * scale)
        d.ellipse((x - r, y - r, x + r, y + r), fill=color)

    # MYL lettermark.
    font = load_font(int(206 * scale), bold=True)
    draw_centered_text(
        d,
        (int(176 * scale), int(384 * scale), int(848 * scale), int(650 * scale)),
        "MYL",
        font,
        fill=(248, 252, 255, 255),
        stroke_width=max(1, int(4 * scale)),
        stroke_fill=(7, 20, 43, 155),
    )

    # Bottom software/disk tray.
    d.rounded_rectangle(
        (int(286 * scale), int(725 * scale), int(738 * scale), int(812 * scale)),
        radius=int(36 * scale),
        fill=(255, 255, 255, 225),
    )
    d.rounded_rectangle(
        (int(328 * scale), int(756 * scale), int(594 * scale), int(779 * scale)),
        radius=int(12 * scale),
        fill=(17, 99, 151, 255),
    )
    d.ellipse((int(630 * scale), int(749 * scale), int(673 * scale), int(792 * scale)), fill=(30, 190, 133, 255))
    d.ellipse((int(680 * scale), int(749 * scale), int(723 * scale), int(792 * scale)), fill=(54, 150, 236, 255))

    # Depth and highlight.
    shadow = layer.filter(ImageFilter.GaussianBlur(radius=int(16 * scale)))
    shadow_mask = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow_mask.alpha_composite(shadow, (0, int(10 * scale)))
    img.alpha_composite(shadow_mask)
    img.alpha_composite(layer)
    gloss = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(gloss)
    gd.rounded_rectangle((int(92 * scale), int(74 * scale), int(932 * scale), int(368 * scale)), radius=int(150 * scale), fill=(255, 255, 255, 24))
    img.alpha_composite(gloss)

    img.putalpha(mask)
    return img


def write_svg(path):
    svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024" role="img" aria-label="MYL系统盘检测工具图标">
  <defs>
    <linearGradient id="bg" x1="160" y1="80" x2="880" y2="960" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#102142"/>
      <stop offset=".58" stop-color="#176097"/>
      <stop offset="1" stop-color="#10936f"/>
    </linearGradient>
    <filter id="softShadow" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="18" stdDeviation="18" flood-color="#06152b" flood-opacity=".35"/>
    </filter>
  </defs>
  <rect x="0" y="0" width="1024" height="1024" rx="220" fill="url(#bg)"/>
  <rect x="106" y="108" width="812" height="808" rx="172" fill="none" stroke="#ffffff" stroke-opacity=".18" stroke-width="4"/>
  <g filter="url(#softShadow)">
    <circle cx="512" cy="490" r="310" fill="none" stroke="#b8f5ff" stroke-opacity=".34" stroke-width="20"/>
    <circle cx="512" cy="490" r="196" fill="none" stroke="#ffffff" stroke-opacity=".2" stroke-width="8"/>
    <path d="M247 650a310 310 0 0 1 64-376" fill="none" stroke="#38e2aa" stroke-width="24" stroke-linecap="round"/>
    <path d="M704 236a310 310 0 0 1 112 247" fill="none" stroke="#6fcdff" stroke-width="18" stroke-linecap="round"/>
    <path d="M512 180l213 78-21 287-192 200-192-200-21-287z" fill="#0c1c38" fill-opacity=".72" stroke="#e0faff" stroke-opacity=".34"/>
    <path d="M280 387h464" stroke="#62edbf" stroke-width="10" stroke-linecap="round" stroke-opacity=".75"/>
    <circle cx="256" cy="304" r="17" fill="#7ee5ff"/>
    <circle cx="765" cy="345" r="14" fill="#46e8b3"/>
    <circle cx="735" cy="660" r="16" fill="#fff" fill-opacity=".75"/>
    <circle cx="286" cy="647" r="13" fill="#7ee5ff" fill-opacity=".85"/>
    <text x="512" y="585" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="206" font-weight="800" letter-spacing="0" fill="#f8fcff" stroke="#07142b" stroke-opacity=".6" stroke-width="4">MYL</text>
    <rect x="286" y="725" width="452" height="87" rx="36" fill="#fff" fill-opacity=".9"/>
    <rect x="328" y="756" width="266" height="23" rx="12" fill="#116397"/>
    <circle cx="651" cy="771" r="22" fill="#1ebe85"/>
    <circle cx="702" cy="771" r="22" fill="#3696ec"/>
  </g>
  <rect x="92" y="74" width="840" height="294" rx="150" fill="#ffffff" opacity=".1"/>
</svg>
"""
    path.write_text(svg, encoding="utf-8")


def main():
    source = make_icon(1024)
    png_1024 = ASSET_DIR / "myl_app_icon_1024.png"
    png_512 = ASSET_DIR / "myl_app_icon_512.png"
    png_256 = ASSET_DIR / "myl_app_icon_256.png"
    png_64 = ASSET_DIR / "myl_app_icon_64.png"
    ico_path = APP_DIR / "MYL系统盘检测工具.ico"
    svg_path = ASSET_DIR / "myl_app_icon.svg"

    source.save(png_1024)
    for size, path in [(512, png_512), (256, png_256), (64, png_64)]:
        source.resize((size, size), Image.Resampling.LANCZOS).save(path)
    source.save(
        ico_path,
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    write_svg(svg_path)
    for path in [svg_path, png_1024, png_512, png_256, png_64, ico_path]:
        print(path)


if __name__ == "__main__":
    main()
