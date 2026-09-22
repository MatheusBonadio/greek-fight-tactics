import argparse
import os
import sys
from collections import Counter
from dataclasses import dataclass
from typing import Tuple

from PIL import Image

from images import CLEAR, OPAQUE, display_path, from_pixels, load_rgba, parse_size, png_files, rgba_pixels, stem
from palette import DEFAULT_PALETTE, load_gpl
from sprite_rules import BACKGROUND_MAX_COLORS, MAX_OPAQUE_COLORS, color_limit_for, inspect, size_rule_for

DEFAULT_ALPHA_THRESHOLD = 128
MAX_COLOR_LIMIT = 256
SIZE_FROM_OPTION = "--size"
SIZE_FROM_REFERENCE = "--like"
SIZE_KEPT = "size kept"


@dataclass
class Quantized:
    image: Image.Image
    source_size: Tuple[int, int]
    colors: int
    reduced_from: int


def quantize_image(image, palette, size=None, alpha_threshold=DEFAULT_ALPHA_THRESHOLD, max_colors=MAX_OPAQUE_COLORS):
    source_size = image.size
    if size is not None and image.size != tuple(size):
        image = image.resize(tuple(size), Image.Resampling.NEAREST)

    kept = [pixel[:3] if pixel[3] >= alpha_threshold else None for pixel in rgba_pixels(image)]
    mapped = [None if color is None else palette.nearest(color) for color in kept]
    usage = Counter(color for color in mapped if color is not None)

    reduced_from = 0
    if max_colors and len(usage) > max_colors:
        reduced_from = len(usage)
        chosen = sorted(usage, key=lambda color: (-usage[color], palette.index_of(color)))[:max_colors]
        limited = palette.subset(chosen)
        mapped = [None if color is None else limited.nearest(color) for color in kept]

    pixels = [CLEAR if color is None else color + (OPAQUE,) for color in mapped]
    colors = len({color for color in mapped if color is not None})
    return Quantized(from_pixels(image.size, pixels), source_size, colors, reduced_from)


def reference_sizes(folder):
    sizes = {}
    for path, _ in png_files([folder]):
        with Image.open(path) as image:
            sizes.setdefault(stem(path), image.size)
    return sizes


def target_size(name, fixed_size, references):
    if fixed_size is not None:
        return fixed_size, SIZE_FROM_OPTION
    key = stem(name)
    if key in references:
        return references[key], SIZE_FROM_REFERENCE
    rule = size_rule_for(name)
    if rule is not None and rule.fixed_size() is not None:
        return rule.fixed_size(), rule.label
    return None, SIZE_KEPT


def size_argument(text):
    try:
        return parse_size(text)
    except ValueError as error:
        raise argparse.ArgumentTypeError(str(error))


def ranged(low, high):
    def parse(text):
        value = int(text)
        if not low <= value <= high:
            raise argparse.ArgumentTypeError(f"must be between {low} and {high}")
        return value
    return parse


def parse_arguments(argv):
    parser = argparse.ArgumentParser(
        prog="quantize.py",
        description="Quantize raw PNGs to the palette with dithering off, after a nearest-neighbour resize to the exact size of the sprite.",
    )
    parser.add_argument("paths", nargs="+", help="raw PNGs or folders of raw PNGs")
    parser.add_argument("--out", required=True, help="folder for the quantized PNGs; subfolders of an input folder are kept")
    parser.add_argument("--palette", default=DEFAULT_PALETTE, help="GIMP palette (default: art/palette/snes.gpl)")
    sizes = parser.add_mutually_exclusive_group()
    sizes.add_argument("--size", type=size_argument, help="exact WxH for every file, such as 32x16")
    sizes.add_argument("--like", metavar="FOLDER", help="take each size from the PNG with the same name in FOLDER, such as art/placeholders")
    parser.add_argument("--alpha-threshold", type=ranged(1, OPAQUE), default=DEFAULT_ALPHA_THRESHOLD, help="alpha below this turns transparent, the rest opaque (default: 128)")
    parser.add_argument("--max-colors", type=ranged(0, MAX_COLOR_LIMIT), default=None, help=f"keep the N most used palette colours, 0 for no limit (default: the limit of the name prefix, {MAX_OPAQUE_COLORS}, or {BACKGROUND_MAX_COLORS} for a bg_ background)")
    return parser.parse_args(argv)


def describe(result, size_source):
    source = "{}x{}".format(*result.source_size)
    final = "{}x{}".format(*result.image.size)
    resize = f"{source} -> {final} ({size_source})" if source != final else f"{final} ({size_source})"
    reduced = f", reduced from {result.reduced_from}" if result.reduced_from else ""
    return f"{resize}  {result.colors} colours{reduced}"


def main(argv=None):
    arguments = parse_arguments(argv)
    try:
        palette = load_gpl(arguments.palette)
        files = png_files(arguments.paths)
        references = reference_sizes(arguments.like) if arguments.like else {}
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        return 2

    if not files:
        print("no PNG files found", file=sys.stderr)
        return 2

    failed = 0
    for path, relative in files:
        size, size_source = target_size(path, arguments.size, references)
        max_colors = color_limit_for(path) if arguments.max_colors is None else arguments.max_colors
        result = quantize_image(load_rgba(path), palette, size, arguments.alpha_threshold, max_colors)

        target = os.path.join(arguments.out, relative)
        os.makedirs(os.path.dirname(os.path.abspath(target)), exist_ok=True)
        result.image.save(target)

        report = inspect(target, result.image, palette)
        failed += report.failed
        print(f"{display_path(path)} -> {display_path(target)}  {describe(result, size_source)}")
        for issue in report.issues:
            print(f"    {issue.severity} {issue.rule}: {issue.message}")

    print(f"{len(files)} files quantized, {failed} still breaking a rule")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
