import argparse
import os

from images import CLEAR, TRANSPARENT, display_path, from_pixels, load_rgba, rgba_pixels, stem
from palette import ROOT
from sprite_rules import CHARACTER_HEIGHT, CHARACTER_WIDTH

DEFAULT_OUT = os.path.join(ROOT, "art", "placeholders", "characters")
FLEX = (0, 1, 1, 0)
IDLE_ANIMATIONS = ("idle", "idle_breath")
DIRECTIONS = ("se", "ne")


def scanlines(image):
    pixels = rgba_pixels(image)
    width = image.size[0]
    return [pixels[row * width:(row + 1) * width] for row in range(image.size[1])]


def spans(line):
    found, start = [], None
    for column, pixel in enumerate(line):
        if pixel[3] != TRANSPARENT and start is None:
            start = column
        elif pixel[3] == TRANSPARENT and start is not None:
            found.append((start, column - 1))
            start = None
    if start is not None:
        found.append((start, len(line) - 1))
    return found


def standing_rows(lines):
    return [row for row, line in enumerate(lines) if any(pixel[3] != TRANSPARENT for pixel in line)]


def hem_row(lines):
    whole = [row for row, line in enumerate(lines) if len(spans(line)) == 1]
    parted = [row for row in whole if row + 1 < len(lines) and len(spans(lines[row + 1])) > 1]
    if not parted:
        raise ValueError("the pose has no legs parting under the garment")
    return max(parted)


def flexed(pose, depth):
    if depth == 0:
        return pose.copy()
    lines = scanlines(pose)
    hem = hem_row(lines)
    standing = standing_rows(lines)
    if standing[0] < depth:
        raise ValueError("the head has no room to dip")
    if standing[-1] - hem <= depth:
        raise ValueError("the leg is too short to take the knee")
    blank = [CLEAR] * pose.size[0]
    moved = [blank[:] for _ in lines]
    for row in range(hem + 1):
        moved[row + depth] = lines[row][:]
    for row in range(hem + 1 + depth, len(lines)):
        moved[row] = lines[row][:]
    return from_pixels(pose.size, [pixel for line in moved for pixel in line])


def poses(strip):
    count = strip.size[0] // CHARACTER_WIDTH
    return [strip.crop((index * CHARACTER_WIDTH, 0, (index + 1) * CHARACTER_WIDTH, CHARACTER_HEIGHT))
            for index in range(count)]


def draw(strip):
    if strip.size[1] != CHARACTER_HEIGHT or strip.size[0] % CHARACTER_WIDTH:
        raise ValueError(f"a strip must be {CHARACTER_WIDTH}xN by {CHARACTER_HEIGHT}, got {strip.size}")
    rest = poses(strip)[0]
    frames = [flexed(rest, FLEX[index % len(FLEX)]) for index in range(strip.size[0] // CHARACTER_WIDTH)]
    lines = [[] for _ in range(CHARACTER_HEIGHT)]
    for frame in frames:
        for row, line in enumerate(scanlines(frame)):
            lines[row].extend(line)
    return from_pixels(strip.size, [pixel for line in lines for pixel in line])


def idle_strips(folder):
    wanted = {f"_{animation}_{direction}" for animation in IDLE_ANIMATIONS for direction in DIRECTIONS}
    return [os.path.join(folder, name) for name in sorted(os.listdir(folder))
            if name.lower().endswith(".png") and any(stem(name).endswith(suffix) for suffix in wanted)]


def animated(folder):
    return [path for path in idle_strips(folder) if load_rgba(path).size[0] > CHARACTER_WIDTH]


def main(argv=None):
    parser = argparse.ArgumentParser(description="Rebuild idle strips so the knee takes a slow bend")
    parser.add_argument("folder", nargs="?", default=DEFAULT_OUT)
    parser.add_argument("--check", action="store_true", help="fail when a committed strip is not what this draws")
    arguments = parser.parse_args(argv)

    stale = 0
    for path in animated(arguments.folder):
        strip = load_rgba(path)
        rebuilt = draw(strip)
        differs = rgba_pixels(rebuilt) != rgba_pixels(strip)
        if differs and not arguments.check:
            rebuilt.save(path)
        stale += differs
        state = "up to date" if not differs else "stale" if arguments.check else "rewritten"
        print(f"  {display_path(path):64} {strip.size[0] // CHARACTER_WIDTH} frames  {state}")
    if arguments.check and stale:
        print(f"{stale} idle strips are not what idle_flex.py draws")
    return 1 if arguments.check and stale else 0


if __name__ == "__main__":
    raise SystemExit(main())
