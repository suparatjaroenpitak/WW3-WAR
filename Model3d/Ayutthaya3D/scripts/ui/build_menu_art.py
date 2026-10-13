"""Original menu art for Ayutthaya Simulator. Inspired by the supplied layout, not a copy of a photograph."""

from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = Path(__file__).resolve().parents[2] / "ayutthaya simulator" / "My project" / "Assets" / "StreamingAssets" / "ui"
FONT = Path(__file__).resolve().parents[2] / "ayutthaya simulator" / "My project" / "Assets" / "_AyutthayaSimulator" / "Resources" / "UI" / "Fonts" / "Sarabun-SemiBold.ttf"
GOLD = (232, 197, 107, 255)
CREAM = (244, 230, 196, 255)


def font(size):
    return ImageFont.truetype(str(FONT), size)


def prang(draw, x, y, h, w, color):
    tiers = 5
    for i in range(tiers):
        t = i / tiers
        tw = w * (1 - t * 0.72)
        th = h * 0.16
        ty = y - h * (0.18 + t * 0.62)
        draw.polygon([(x - tw, ty + th), (x + tw, ty + th), (x + tw * 0.72, ty), (x - tw * 0.72, ty)], fill=color)
    draw.polygon([(x, y - h), (x + w * 0.08, y - h * 0.78), (x - w * 0.08, y - h * 0.78)], fill=color)


def background():
    w, h = 1920, 1080
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        t = y / h
        if t < 0.62:
            u = t / 0.62
            r = int(28 + u * 150 + (1 - u) * 20)
            g = int(18 + u * 70)
            b = int(48 + (1 - u) * 40 + u * 28)
        else:
            u = (t - 0.62) / 0.38
            r = int(170 - u * 40)
            g = int(78 - u * 30)
            b = int(42 - u * 10)
        for x in range(w):
            sun = math.exp(-((x - 1560) ** 2 + (y - 430) ** 2) / 180000)
            px[x, y] = (min(255, int(r + sun * 90)), min(255, int(g + sun * 50)), min(255, int(b + sun * 10)))
    draw = ImageDraw.Draw(img, "RGBA")
    draw.ellipse((1460, 330, 1660, 530), fill=(255, 186, 92, 230))
    draw.ellipse((1490, 360, 1630, 500), fill=(255, 220, 150, 180))
    for i, (x, base, height, width) in enumerate(((180, 760, 520, 90), (310, 790, 430, 70), (430, 800, 360, 58), (560, 820, 280, 48), (90, 800, 300, 50), (680, 830, 210, 36))):
        shade = (18 + i * 3, 12, 16, 255)
        prang(draw, x, base, height, width, shade)
        draw.rectangle((x - width, base, x + width, base + 30), fill=shade)
    draw.polygon([(0, 700), (900, 640), (1920, 690), (1920, 1080), (0, 1080)], fill=(18, 28, 38, 255))
    for y in range(700, 1080):
        for x in range(0, 1920, 3):
            wave = int(8 * math.sin(x * 0.02 + y * 0.04))
            src = px[min(1919, x), max(0, 640 - (y - 700) + wave)]
            px[x, y] = tuple(int(src[c] * 0.35 + (12, 22, 32)[c] * 0.65) for c in range(3))
    draw = ImageDraw.Draw(img, "RGBA")
    draw.polygon([(40, 860), (210, 860), (180, 900), (70, 900)], fill=(12, 10, 8, 255))
    draw.ellipse((150, 820, 190, 860), fill=(255, 196, 90, 220))
    draw.polygon([(1480, 780), (1680, 770), (1640, 810), (1520, 816)], fill=(20, 16, 12, 255))
    for lx in (120, 260, 410, 980, 1500, 1760):
        draw.ellipse((lx, 760, lx + 8, 768), fill=(255, 196, 110, 200))
    leaves = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(leaves)
    ld.ellipse((-180, -80, 280, 260), fill=(8, 18, 8, 180))
    ld.ellipse((1680, 820, 2100, 1200), fill=(8, 16, 8, 160))
    img = Image.alpha_composite(img.convert("RGBA"), leaves).convert("RGB")
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    img.save(OUT / "menu_background.png", quality=92)


def gold_text(text, size, tracking=0):
    f = font(size)
    dummy = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    box = dummy.textbbox((0, 0), text, font=f)
    width = box[2] - box[0] + 8
    height = box[3] - box[1] + 8
    mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(mask).text((4 - box[0], 4 - box[1]), text, font=f, fill=255)
    grad = Image.new("RGBA", (width, height))
    gp = grad.load()
    for y in range(height):
        u = y / max(1, height - 1)
        color = (255, 226, 150) if u < 0.45 else (196, 146, 62)
        for x in range(width):
            gp[x, y] = (*color, 255)
    out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    out.paste(grad, mask=mask)
    return out


def logo():
    title = gold_text("AYUTTHAYA", 92)
    sub = gold_text("SIMULATOR", 36)
    canvas = Image.new("RGBA", (title.width + 40, title.height + sub.height + 70), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    cx = canvas.width // 2
    prang(draw, cx, 58, 52, 16, GOLD)
    canvas.alpha_composite(title, ((canvas.width - title.width) // 2, 62))
    canvas.alpha_composite(sub, ((canvas.width - sub.width) // 2, 62 + title.height - 8))
    canvas.save(OUT / "logo.png")


def icon(name, painter):
    img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    painter(ImageDraw.Draw(img))
    img.save(OUT / f"icon_{name}.png")


def stroke(draw, points, width=7):
    draw.line(points, fill=GOLD, width=width, joint="curve")


def icons():
    icon("play", lambda d: d.polygon([(46, 34), (46, 94), (96, 64)], outline=GOLD))
    icon("continue", lambda d: (d.arc((34, 34, 94, 94), 40, 320, fill=GOLD, width=7), d.polygon([(88, 28), (104, 46), (84, 52)], fill=GOLD)))
    icon("load", lambda d: (d.rounded_rectangle((36, 48, 92, 96), radius=6, outline=GOLD, width=6), d.polygon([(36, 58), (52, 40), (76, 40), (76, 58)], outline=GOLD)))
    icon("settings", lambda d: (d.ellipse((46, 46, 82, 82), outline=GOLD, width=6), d.regular_polygon((64, 64, 34), 8, rotation=22, outline=GOLD, width=5)))
    icon("credits", lambda d: (d.ellipse((40, 36, 64, 60), outline=GOLD, width=5), d.ellipse((66, 40, 90, 62), outline=GOLD, width=5), d.arc((34, 66, 96, 104), 200, 340, fill=GOLD, width=5)))
    icon("quit", lambda d: (d.rounded_rectangle((34, 40, 78, 96), radius=4, outline=GOLD, width=6), d.line((78, 64, 100, 64), fill=GOLD, width=6), d.polygon([(90, 52), (104, 64), (90, 76)], fill=GOLD)))
    icon("graphics", lambda d: (d.rounded_rectangle((32, 40, 96, 88), radius=6, outline=GOLD, width=6), d.polygon([(48, 74), (60, 56), (74, 70), (86, 52), (86, 74)], fill=GOLD)))
    icon("audio", lambda d: (d.polygon([(40, 52), (58, 52), (78, 38), (78, 90), (58, 76), (40, 76)], outline=GOLD), d.arc((72, 48, 100, 80), 300, 60, fill=GOLD, width=5)))
    icon("controls", lambda d: (d.rounded_rectangle((36, 48, 92, 88), radius=16, outline=GOLD, width=6), d.ellipse((48, 60, 60, 72), outline=GOLD, width=4), d.ellipse((68, 60, 80, 72), outline=GOLD, width=4)))
    icon("language", lambda d: (d.ellipse((34, 34, 94, 94), outline=GOLD, width=6), d.arc((48, 34, 80, 94), 0, 360, fill=GOLD, width=4), d.line((34, 64, 94, 64), fill=GOLD, width=4)))


def game_icon():
    img = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((16, 16, 496, 496), radius=72, fill=(22, 14, 10, 255))
    d.rounded_rectangle((28, 28, 484, 484), radius=64, outline=GOLD, width=10)
    prang(d, 256, 390, 250, 70, GOLD)
    d.ellipse((228, 118, 284, 150), outline=GOLD, width=6)
    img.save(OUT / "game_icon.png")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    background()
    logo()
    icons()
    game_icon()
    print("wrote", OUT)


if __name__ == "__main__":
    main()
