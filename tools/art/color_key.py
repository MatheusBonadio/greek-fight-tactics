import argparse
import os
import sys

from images import OPAQUE, from_pixels
from palette import ROOT, load_gpl

DEFAULT_OUT = os.path.join(ROOT, "artifacts", "art", "color_keys")
BAND_WIDTH = 1
DEFAULT_HEIGHT = 16
KEYS = {
    "kleia": (
        "outline",
        "outline_blue",
        "skin_light",
        "skin_mid",
        "skin_shade",
        "hair_dark",
        "hair_mid",
        "aegean_light",
        "aegean_mid",
        "aegean_shade",
        "marble_mid",
        "red_mid",
        "ochre_mid",
        "ochre_shade",
        "gold_mid",
    ),
    "thrasybios": (
        "outline",
        "outline_blue",
        "skin_light",
        "skin_mid",
        "skin_shade",
        "hair_mid",
        "hair_dark",
        "purple_light",
        "purple_mid",
        "purple_dark",
        "gold_light",
        "gold_mid",
        "marble_light",
        "marble_mid",
        "ochre_shade",
    ),
}


def colors_of(character, palette=None):
    palette = palette or load_gpl()
    named = {name: color for name, color in palette.entries}
    missing = [name for name in KEYS[character] if name not in named]
    if missing:
        raise ValueError(f"{character}: {', '.join(missing)} not in the palette")
    return tuple(named[name] + (OPAQUE,) for name in KEYS[character])


def draw(character, palette=None, height=DEFAULT_HEIGHT):
    bands = colors_of(character, palette)
    width = len(bands) * BAND_WIDTH
    pixels = [bands[column // BAND_WIDTH] for _ in range(height) for column in range(width)]
    return from_pixels((width, height), pixels)


def file_name(character):
    return f"color_key_{character}.png"


def main(arguments=None):
    parser = argparse.ArgumentParser(description="Draw the working colour key of a character, one 1 px band per colour.")
    parser.add_argument("characters", nargs="*", default=sorted(KEYS), help="characters to draw (default: all)")
    parser.add_argument("--out", default=DEFAULT_OUT, help="folder for color_key_<character>.png")
    parser.add_argument("--height", type=int, default=DEFAULT_HEIGHT, help="band height in pixels")
    options = parser.parse_args(arguments)

    wanted = options.characters or sorted(KEYS)
    unknown = [character for character in wanted if character not in KEYS]
    if unknown:
        parser.error(f"no colour key for {', '.join(unknown)}")
    if options.height < 1:
        parser.error("height must be at least 1")

    os.makedirs(options.out, exist_ok=True)
    palette = load_gpl()
    for character in wanted:
        path = os.path.join(options.out, file_name(character))
        draw(character, palette, options.height).save(path)
        print(f"{path} {len(KEYS[character])} colours")
    return 0


if __name__ == "__main__":
    sys.exit(main())
