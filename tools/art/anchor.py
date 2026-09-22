import argparse
import re
import sys

from PIL import Image

from images import CLEAR, TRANSPARENT, display_path, load_rgba, png_files, stem
from tile_preview import CELL_HEIGHT, CELL_WIDTH

BASE_ROWS = 10
CELL_CENTER_FROM_BOTTOM = CELL_HEIGHT // 2
CELL_CENTER_X = CELL_WIDTH // 2
ANCHORED = re.compile(r"^(prop|block)_")


def row_extents(image):
    extents = {}
    for y in range(image.height):
        opaque = [x for x in range(image.width) if image.getpixel((x, y))[3] != TRANSPARENT]
        if opaque:
            extents[y] = (opaque[0], opaque[-1])
    return extents


def base_anchor(image):
    extents = row_extents(image)
    if not extents:
        return None
    bottom = max(extents)
    base = [y for y in extents if y > bottom - BASE_ROWS]
    widest = max(extents[y][1] - extents[y][0] + 1 for y in base)
    row = max(y for y in base if extents[y][1] - extents[y][0] + 1 == widest)
    left, right = extents[row]
    return row, (left + right + 1) / 2, widest


def anchor_offset(image):
    anchor = base_anchor(image)
    if anchor is None:
        return None
    row, center, width = anchor
    if width >= CELL_WIDTH:
        return 0, 0
    return round(CELL_CENTER_X - center), image.height - CELL_CENTER_FROM_BOTTOM - row


def shifted(image, dx, dy):
    extents = row_extents(image)
    lefts = [left for left, _ in extents.values()]
    rights = [right for _, right in extents.values()]
    if max(extents) + dy >= image.height or min(lefts) + dx < 0 or max(rights) + dx >= image.width:
        raise ValueError(f"moving by ({dx:+}, {dy:+}) would cut the sprite")
    grow = max(0, -dy - min(extents))
    out = Image.new("RGBA", (image.width, image.height + grow), CLEAR)
    out.alpha_composite(image, (dx, grow + dy))
    return out


def recentred(image):
    offset = anchor_offset(image)
    if offset is None or offset == (0, 0):
        return image
    return shifted(image, *offset)


def parse_arguments(argv):
    parser = argparse.ArgumentParser(
        prog="anchor.py",
        description="Measure how far the base of each prop_ and block_ sprite sits from the centre of its cell and optionally move it there. The base is the lowest of the widest rows among the bottom 10 rows; its centre belongs at x = 16 and 8 rows above the bottom of the canvas, the widest row of the floor diamond.",
    )
    parser.add_argument("paths", nargs="+", help="sprite PNGs or folders of sprites")
    parser.add_argument("--fix", action="store_true", help="move every off-centre sprite in place, growing the canvas upwards when the top has no room")
    return parser.parse_args(argv)


def main(argv=None):
    arguments = parse_arguments(argv)
    try:
        files = [path for path, _ in png_files(arguments.paths) if ANCHORED.search(stem(path))]
    except FileNotFoundError as error:
        print(error, file=sys.stderr)
        return 2

    off_centre = 0
    for path in files:
        image = load_rgba(path)
        offset = anchor_offset(image)
        if offset is None or offset == (0, 0):
            print(f"{display_path(path)}  ok")
            continue
        off_centre += 1
        if arguments.fix:
            fixed = recentred(image)
            fixed.save(path)
            print(f"{display_path(path)}  moved by {offset[0]:+}, {offset[1]:+}  {image.width}x{image.height} -> {fixed.width}x{fixed.height}")
        else:
            print(f"{display_path(path)}  off centre by {offset[0]:+}, {offset[1]:+}")

    print(f"{len(files)} sprites, {off_centre} off centre")
    return 0


if __name__ == "__main__":
    sys.exit(main())
