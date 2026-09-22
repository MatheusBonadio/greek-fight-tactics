import os

from PIL import Image

from images import CLEAR, OPAQUE, TRANSPARENT, from_pixels, load_rgba, rgba_pixels
from palette import ROOT
from tile_preview import CELL_HEIGHT, CELL_WIDTH, in_footprint

FLOOR_FOLDER = os.path.join(ROOT, "art", "placeholders", "tiles", "floor")
OVERLAY_FOLDER = os.path.join(ROOT, "art", "placeholders", "tiles", "overlays")
SIDE_FOLDER = os.path.join(ROOT, "art", "placeholders", "tiles", "sides")
MARBLE_FLOOR = os.path.join(FLOOR_FOLDER, "floor_marble.png")


def opaque(color):
    return tuple(color[:3]) + (OPAQUE,)


def filled(size, color):
    return Image.new("RGBA", size, opaque(color) if len(color) == 3 else color)


def strip(size, colors, fill):
    width, height = size
    pixels = [opaque(color) if len(color) == 3 else color for color in colors]
    pixels += [opaque(fill)] * (width * height - len(pixels))
    return from_pixels(size, pixels)


def marble_floor():
    return load_rgba(MARBLE_FLOOR)


def diamond(color):
    pixels = [opaque(color) if in_footprint(x, y) else CLEAR for y in range(CELL_HEIGHT) for x in range(CELL_WIDTH)]
    return from_pixels((CELL_WIDTH, CELL_HEIGHT), pixels)


def on_background(tile, color):
    return Image.alpha_composite(filled(tile.size, color), tile)


def shrink_diamond(tile):
    pixels = rgba_pixels(tile)
    width = tile.width
    for row in range(tile.height):
        opaque_columns = [column for column in range(width) if pixels[row * width + column][3] != TRANSPARENT]
        for column in opaque_columns[:1] + opaque_columns[-1:]:
            pixels[row * width + column] = CLEAR
    return from_pixels(tile.size, pixels)


def save(image, folder, relative):
    path = os.path.join(folder, relative)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    return path
