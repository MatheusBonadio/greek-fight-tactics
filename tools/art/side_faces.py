import argparse
import os
import sys

from images import CLEAR, OPAQUE, from_pixels
from palette import ROOT, load_gpl
from tile_preview import CELL_HEIGHT, CELL_WIDTH, FLOOR_FOOTPRINT, FOOTPRINT_PIXEL

CANVAS = CELL_WIDTH
BAND_ROWS = 8
FACE_ROWS = 2 * BAND_ROWS
SPLIT_COLUMN = CELL_WIDTH // 2
DEFAULT_OUT = os.path.join(ROOT, "art", "placeholders", "tiles", "sides")

LIP = "^"
BASE = "."
JOINT = "#"
LIGHT = "o"
DARK = ":"

EARTH_BODY = (
    "..o.............",
    "..........:.....",
    "#####.......####",
    ".....#######....",
    "........o.......",
    "...:.........o..",
    ".##########.....",
    "...........#####",
)

STONE_BODY = (
    "oooooooooooooooo",
    ".......#.......#",
    "..:....#.......#",
    "################",
    "oooooooooooooooo",
    "...#.......#....",
    "...#...:...#....",
    "################",
)

WATER_BODY = (
    "..o....:....o...",
    "..o....:....o..:",
    "..o....:....o...",
    "..o....:....o.o.",
    "..o....:....o...",
    "..o....:....o..:",
    "..o....:....o...",
    "..o....:....o.o.",
)

MATERIALS = {
    "earth": (
        EARTH_BODY,
        {LIP: "ochre_mid", BASE: "ochre_shade", JOINT: "ochre_dark", LIGHT: "ochre_mid", DARK: "ochre_dark"},
        {LIP: "ochre_shade", BASE: "ochre_dark", JOINT: "hair_dark", LIGHT: "ochre_shade", DARK: "hair_dark"},
    ),
    "stone": (
        STONE_BODY,
        {LIP: "marble_light", BASE: "marble_mid", JOINT: "marble_cold_shade", LIGHT: "marble_mid", DARK: "marble_cold_shade"},
        {LIP: "marble_mid", BASE: "marble_cold_shade", JOINT: "marble_cold_dark", LIGHT: "marble_cold_shade", DARK: "marble_cold_dark"},
    ),
    "water": (
        WATER_BODY,
        {LIP: "aegean_light", BASE: "aegean_mid", JOINT: "aegean_dark", LIGHT: "aegean_light", DARK: "aegean_shade"},
        {LIP: "aegean_mid", BASE: "aegean_shade", JOINT: "aegean_dark", LIGHT: "aegean_mid", DARK: "aegean_dark"},
    ),
}


def footprint_bottom(column):
    return max(row for row in range(CELL_HEIGHT) if FLOOR_FOOTPRINT[row][column] == FOOTPRINT_PIXEL)


def symbol_at(body, column, depth):
    if depth == 0:
        return LIP
    return body[depth % BAND_ROWS][column % SPLIT_COLUMN]


def draw(material, palette=None):
    palette = palette or load_gpl()
    colors = {name: color for name, color in palette.entries}
    body, lit, shaded = MATERIALS[material]
    pixels = [CLEAR] * (CANVAS * CANVAS)
    for column in range(CANVAS):
        ramp = lit if column < SPLIT_COLUMN else shaded
        top = footprint_bottom(column) + 1
        for depth in range(FACE_ROWS):
            row = top + depth
            if row >= CANVAS:
                break
            pixels[row * CANVAS + column] = colors[ramp[symbol_at(body, column, depth)]] + (OPAQUE,)
    return from_pixels((CANVAS, CANVAS), pixels)


def main(arguments=None):
    parser = argparse.ArgumentParser(description="Draw the side faces that hang under raised cells and the map edge.")
    parser.add_argument("--out", default=DEFAULT_OUT, help="folder for side_<material>.png")
    options = parser.parse_args(arguments)
    os.makedirs(options.out, exist_ok=True)
    palette = load_gpl()
    for material in MATERIALS:
        path = os.path.join(options.out, f"side_{material}.png")
        draw(material, palette).save(path)
        print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
