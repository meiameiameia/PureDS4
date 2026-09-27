"""Compose a Windows ICO from the transparent PureDS4 tile master.

Usage: python utils/generate-app-icon.py SOURCE.png OUTPUT_DIRECTORY [--preview] [--retune]

Pillow is needed only while generating the checked-in asset. The application
does not load Pillow. Every ICO frame comes from the same contained mark.
"""

from io import BytesIO
from pathlib import Path
import struct
import sys

from PIL import Image, ImageDraw


SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
SMALL_SIZES = (16, 20, 24, 32, 40, 48)


def source_image(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    if image.width != image.height or image.width < 256:
        raise ValueError("The icon master must be square and at least 256 px")
    alpha = image.getchannel("A")
    if alpha.getextrema() == (255, 255):
        raise ValueError("The icon master must have a transparent exterior")
    if alpha.getbbox() is None:
        raise ValueError("The icon master is empty")
    return image


def retune_source(image: Image.Image) -> Image.Image:
    """Match the approved tile artwork to the app's slate palette and ICO bounds."""
    recolored = []
    pixels = (image.get_flattened_data() if hasattr(image, "get_flattened_data")
              else image.getdata())
    for red, green, blue, alpha in pixels:
        if not alpha:
            recolored.append((0, 0, 0, 0))
            continue
        if blue > red + 25 and blue > green + 15:
            if green > 125 and red < 110:
                # The bridge uses the same accent as the application chrome.
                shade = (green - 200) * 0.35
                red, green, blue = (round(138 + shade), round(180 + shade),
                                    round(214 + shade))
            else:
                # Recolor the cobalt tile and its antialiasing toward muted slate.
                white_mix = min(1.0, red / 252.0)
                shade = (blue - (139 + 113 * white_mix)) * 0.45
                red = round(60 * (1 - white_mix) + 252 * white_mix + shade)
                green = round(86 * (1 - white_mix) + 252 * white_mix + shade)
                blue = round(104 * (1 - white_mix) + 252 * white_mix + shade)
        recolored.append(tuple(max(0, min(255, value)) for value in
                                (red, green, blue, alpha)))
    adjusted = Image.new("RGBA", image.size)
    adjusted.putdata(recolored)
    bounds = adjusted.getchannel("A").point(lambda value: 255 if value >= 128 else 0).getbbox()
    if bounds is None:
        raise ValueError("The icon master has no visible artwork")
    artwork = adjusted.crop(bounds).resize((960, 960), Image.Resampling.LANCZOS)
    master = Image.new("RGBA", (1024, 1024))
    master.alpha_composite(artwork, (32, 32))
    return master


def png_bytes(image: Image.Image) -> bytes:
    stream = BytesIO()
    image.save(stream, format="PNG")
    return stream.getvalue()


def write_ico(path: Path, images: list[tuple[int, Image.Image]]) -> None:
    blobs = [(size, png_bytes(image)) for size, image in images]
    offset = 6 + 16 * len(blobs)
    directory = bytearray(struct.pack("<HHH", 0, 1, len(blobs)))
    for size, blob in blobs:
        dimension = size if size < 256 else 0
        directory.extend(struct.pack(
            "<BBBBHHII", dimension, dimension, 0, 0, 1, 32,
            len(blob), offset,
        ))
        offset += len(blob)
    path.write_bytes(bytes(directory) + b"".join(blob for _, blob in blobs))


def preview(path: Path, source: Image.Image) -> None:
    # Actual-size icons sit above nearest-neighbor zooms on typical surfaces.
    backgrounds = (("dark", "#10171D"), ("slate", "#202B34"),
                   ("white", "#FFFFFF"), ("light", "#F4F7FA"))
    cell_width, cell_height = 176, 184
    sheet = Image.new(
        "RGB", (cell_width * len(SMALL_SIZES), cell_height * len(backgrounds)), "#FFFFFF"
    )
    draw = ImageDraw.Draw(sheet)
    for row, (name, color) in enumerate(backgrounds):
        y = row * cell_height
        for column, size in enumerate(SMALL_SIZES):
            x = column * cell_width
            tile = Image.new("RGBA", (cell_width, cell_height), color)
            icon = source.resize((size, size), Image.Resampling.LANCZOS)
            tile.alpha_composite(icon, ((cell_width - size) // 2, 28))
            zoom = icon.resize((size * 4, size * 4), Image.Resampling.NEAREST)
            tile.alpha_composite(zoom, ((cell_width - zoom.width) // 2, 78))
            sheet.paste(tile.convert("RGB"), (x, y))
            draw.text(
                (x + 8, y + 5), f"{name} / {size}px",
                fill="#FFFFFF" if row < 2 else "#000000",
            )
    sheet.save(path)


def main() -> int:
    if len(sys.argv) < 3 or any(arg not in ("--preview", "--retune") for arg in sys.argv[3:]):
        raise SystemExit(__doc__)
    source = Path(sys.argv[1]).resolve()
    output = Path(sys.argv[2]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    image = source_image(source)
    if "--retune" in sys.argv[3:]:
        image = retune_source(image)
        image.save(output / "AppIcon.png")
    images = [(size, image.resize((size, size), Image.Resampling.LANCZOS))
              for size in SIZES]
    write_ico(output / "DS4W.ico", images)
    if "--preview" in sys.argv[3:]:
        preview(output / "preview.png", image)
    print(f"Generated {output / 'DS4W.ico'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
