import argparse
import os
import sys

from PIL import Image

from images import CLEAR, TRANSPARENT, display_path, load_rgba, parse_color, png_files, rgba_pixels
from palette import ROOT

CELL_WIDTH = 32
CELL_HEIGHT = 16
HALF_WIDTH = CELL_WIDTH // 2
HALF_HEIGHT = CELL_HEIGHT // 2
PREVIEW_GRID = 2
GAP_GRID = 3
DEFAULT_SCALE = 4
DEFAULT_BACKGROUND = "F800F8"
NO_BACKGROUND = "none"
DEFAULT_OUT = os.path.join(ROOT, "artifacts", "art", "tiling")
PREVIEW_SUFFIX = "_tiling.png"
FOOTPRINT_PIXEL = "#"
FLOOR_FOOTPRINT = (
    "...............##...............",
    ".............######.............",
    "...........##########...........",
    ".........##############.........",
    ".......##################.......",
    ".....######################.....",
    "...##########################...",
    ".##############################.",
    "################################",
    "..############################..",
    "....########################....",
    "......####################......",
    ".........###############........",
    "...........###########..........",
    ".............#######............",
    "...............###..............",
)


def in_footprint(x, y):
    return FLOOR_FOOTPRINT[y][x] == FOOTPRINT_PIXEL


def overflow(tile):
    return max(tile.width - CELL_WIDTH, 0), max(tile.height - CELL_HEIGHT, 0)


def cell_origin(column, row, grid, lift):
    return (column - row + grid - 1) * HALF_WIDTH, (column + row) * HALF_HEIGHT + lift


def draw_order(grid):
    return sorted(((column, row) for column in range(grid) for row in range(grid)), key=lambda cell: (cell[0] + cell[1], cell[0]))


def compose(tile, grid=PREVIEW_GRID):
    extra_width, lift = overflow(tile)
    canvas = Image.new("RGBA", (grid * CELL_WIDTH + extra_width, grid * CELL_HEIGHT + lift), CLEAR)
    for column, row in draw_order(grid):
        x, y = cell_origin(column, row, grid, lift)
        canvas.alpha_composite(tile, (x, y + CELL_HEIGHT - tile.height))
    return canvas


def count_gaps(tile):
    if tile.size != (CELL_WIDTH, CELL_HEIGHT):
        return None
    center = GAP_GRID // 2
    left, top = cell_origin(center, center, GAP_GRID, 0)
    window = compose(tile, GAP_GRID).crop((left, top, left + CELL_WIDTH, top + CELL_HEIGHT))
    return sum(1 for pixel in rgba_pixels(window) if pixel[3] == TRANSPARENT)


def count_outside_footprint(tile):
    if tile.size != (CELL_WIDTH, CELL_HEIGHT):
        return None
    pixels = rgba_pixels(tile)
    return sum(1 for index, pixel in enumerate(pixels) if pixel[3] != TRANSPARENT and not in_footprint(index % CELL_WIDTH, index // CELL_WIDTH))


def cell_problems(tile):
    gaps = count_gaps(tile)
    if gaps is None:
        return None
    outside = count_outside_footprint(tile)
    problems = []
    if outside:
        problems.append(f"{outside} opaque pixels outside the diamond")
    if gaps:
        problems.append(f"{gaps} gap pixels when repeated")
    return problems


def render_preview(tile, grid=PREVIEW_GRID, scale=DEFAULT_SCALE, background=None):
    preview = compose(tile, grid)
    if background is not None:
        preview = Image.alpha_composite(Image.new("RGBA", preview.size, background), preview)
    if scale != 1:
        preview = preview.resize((preview.width * scale, preview.height * scale), Image.Resampling.NEAREST)
    return preview


def positive(text):
    value = int(text)
    if value < 1:
        raise argparse.ArgumentTypeError("must be 1 or more")
    return value


def background_color(text):
    if text.lower() == NO_BACKGROUND:
        return None
    try:
        return parse_color(text)
    except ValueError as error:
        raise argparse.ArgumentTypeError(str(error))


def parse_arguments(argv):
    parser = argparse.ArgumentParser(
        prog="tile_preview.py",
        description="Paste copies of each tile on the 32x16 isometric grid, export an upscaled preview and fail when a 32x16 tile has opaque pixels outside the diamond or leaves gaps when it repeats.",
    )
    parser.add_argument("paths", nargs="+", help="tile PNGs or folders of tiles")
    parser.add_argument("--out", default=DEFAULT_OUT, help="folder for the <tile>_tiling.png previews (default: artifacts/art/tiling)")
    parser.add_argument("--grid", type=positive, default=PREVIEW_GRID, help="copies per side of the diamond (default: 2, four copies)")
    parser.add_argument("--scale", type=positive, default=DEFAULT_SCALE, help="nearest-neighbour upscale of the preview (default: 4)")
    parser.add_argument("--background", type=background_color, default=DEFAULT_BACKGROUND, help="preview background as RRGGBB, or 'none' (default: F800F8, so gaps stand out)")
    return parser.parse_args(argv)


def main(argv=None):
    arguments = parse_arguments(argv)
    try:
        files = png_files(arguments.paths)
    except FileNotFoundError as error:
        print(error, file=sys.stderr)
        return 2

    os.makedirs(arguments.out, exist_ok=True)
    failed = 0
    for path, _ in files:
        tile = load_rgba(path)
        target = os.path.join(arguments.out, os.path.splitext(os.path.basename(path))[0] + PREVIEW_SUFFIX)
        render_preview(tile, arguments.grid, arguments.scale, arguments.background).save(target)

        problems = cell_problems(tile)
        if problems is None:
            verdict = f"fit n/a ({tile.width}x{tile.height} is not a {CELL_WIDTH}x{CELL_HEIGHT} floor tile)"
        elif problems:
            verdict = "FAIL " + ", ".join(problems)
            failed += 1
        else:
            verdict = "ok, no gaps and nothing outside the diamond"
        print(f"{display_path(path)} -> {display_path(target)}  {verdict}")

    print(f"{len(files)} previews, {failed} that do not fit the diamond")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
