import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DEFAULT_PALETTE = os.path.join(ROOT, "art", "palette", "snes.gpl")

GPL_HEADER = "GIMP Palette"
GPL_ENTRY = re.compile(r"^\s*(\d+)\s+(\d+)\s+(\d+)(?:\s+(.*?))?\s*$")
MAX_CHANNEL = 255
RGB555_STEP = 8
LUMA_WEIGHTS = (30, 59, 11)


def is_rgb555(color):
    return all(channel % RGB555_STEP == 0 for channel in color[:3])


def to_hex(color):
    return "#{:02X}{:02X}{:02X}".format(*color[:3])


def weighted_distance(color, other):
    return sum(weight * (channel - other_channel) ** 2 for weight, channel, other_channel in zip(LUMA_WEIGHTS, color[:3], other[:3]))


class Palette:
    def __init__(self, entries):
        self.entries = tuple((name, tuple(color)) for name, color in entries)
        self._names = {}
        colors = []
        for name, color in self.entries:
            if color not in self._names:
                self._names[color] = name
                colors.append(color)
        self.colors = tuple(colors)
        self._nearest = {}

    def __len__(self):
        return len(self.colors)

    def __contains__(self, color):
        return tuple(color[:3]) in self._names

    def name_of(self, color):
        return self._names.get(tuple(color[:3]))

    def index_of(self, color):
        return self.colors.index(tuple(color[:3]))

    def nearest(self, color):
        key = tuple(color[:3])
        found = self._nearest.get(key)
        if found is None:
            found = self._closest(key)
            self._nearest[key] = found
        return found

    def _closest(self, color):
        found = None
        shortest = None
        for candidate in self.colors:
            distance = weighted_distance(color, candidate)
            if shortest is None or distance < shortest:
                found, shortest = candidate, distance
        return found

    def subset(self, colors):
        wanted = {tuple(color[:3]) for color in colors}
        return Palette((name, color) for name, color in self.entries if color in wanted)


def load_gpl(path=DEFAULT_PALETTE):
    with open(path, encoding="utf-8") as handle:
        lines = handle.read().splitlines()

    if not lines or lines[0].strip() != GPL_HEADER:
        raise ValueError(f"{path} is not a GIMP palette")

    entries = []
    for line in lines[1:]:
        match = GPL_ENTRY.match(line)
        if not match:
            continue
        color = tuple(int(match.group(index)) for index in (1, 2, 3))
        if any(channel > MAX_CHANNEL for channel in color):
            raise ValueError(f"{path}: channel above {MAX_CHANNEL} in '{line.strip()}'")
        entries.append((match.group(4) or to_hex(color), color))

    if not entries:
        raise ValueError(f"{path} has no colours")
    return Palette(entries)
