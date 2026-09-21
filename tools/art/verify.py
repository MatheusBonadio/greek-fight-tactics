import argparse
import os
import sys

from images import OPAQUE, display_path, load_rgba, png_files
from palette import DEFAULT_PALETTE, ROOT, load_gpl, to_hex
from sprite_rules import MAX_OPAQUE_COLORS, RULES, inspect, plural

DEFAULT_TARGET = os.path.join(ROOT, "art", "placeholders")
OFF_PALETTE = "off-palette"


def parse_arguments(argv):
    parser = argparse.ArgumentParser(
        prog="verify.py",
        description=f"Report the colour count of every PNG and fail when one breaks the pixel-art rules: more than {MAX_OPAQUE_COLORS} colours + transparent, partial alpha, channels outside RGB555, colours off the palette, the wrong size for its name, or a floor tile that leaves gaps.",
    )
    parser.add_argument("paths", nargs="*", default=[DEFAULT_TARGET], help="PNGs or folders (default: art/placeholders)")
    parser.add_argument("--palette", default=DEFAULT_PALETTE, help="GIMP palette (default: art/palette/snes.gpl)")
    parser.add_argument("--skip", action="append", choices=RULES, default=[], help="rule to ignore; repeat for more than one")
    parser.add_argument("--colors", action="store_true", help="list every colour of each file with its palette name and pixel count")
    return parser.parse_args(argv)


def result_of(report):
    if report.failed:
        return "FAIL"
    return "warn" if report.warnings else "ok"


def print_table(reports, list_colors, palette):
    rows = [(display_path(report.path), f"{report.width}x{report.height}", str(report.colors), "yes" if report.has_transparency else "no", result_of(report)) for report in reports]
    headers = ("FILE", "SIZE", "COLORS", "TRANSPARENT", "RESULT")
    widths = [max(len(header), *(len(row[index]) for row in rows)) for index, header in enumerate(headers)]
    line = "  ".join(f"{{:<{width}}}" for width in widths)
    print(line.format(*headers))
    for report, row in zip(reports, rows):
        print(line.format(*row).rstrip())
        for issue in report.issues:
            print(f"    {issue.severity} {issue.rule}: {issue.message}")
        if list_colors:
            for color, count in sorted(report.color_counts.items(), key=lambda entry: -entry[1]):
                alpha = "" if color[3] == OPAQUE else f" alpha {color[3]}"
                print(f"    {to_hex(color)}{alpha}  {palette.name_of(color) or OFF_PALETTE}  {count}")


def main(argv=None):
    arguments = parse_arguments(argv)
    try:
        palette = load_gpl(arguments.palette)
        files = png_files(arguments.paths)
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        return 2

    if not files:
        print("no PNG files found", file=sys.stderr)
        return 2

    reports = [inspect(path, load_rgba(path), palette, set(arguments.skip)) for path, _ in files]
    print_table(reports, arguments.colors, palette)

    failed = sum(1 for report in reports if report.failed)
    warned = sum(1 for report in reports if not report.failed and report.warnings)
    print(f"{plural(len(reports), 'file')}: {len(reports) - failed - warned} ok, {warned} with warnings, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
