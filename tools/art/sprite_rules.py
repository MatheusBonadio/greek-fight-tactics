import re
from collections import Counter
from dataclasses import dataclass, field
from typing import Optional

from images import OPAQUE, TRANSPARENT, rgba_pixels, stem
from palette import is_rgb555, to_hex
from tile_preview import CELL_HEIGHT, CELL_WIDTH, count_gaps, count_inside_footprint, count_outside_footprint

MAX_OPAQUE_COLORS = 15
BACKGROUND_MAX_COLORS = 64
LISTED_COLORS = 6

ERROR = "error"
WARNING = "warning"

COLORS_RULE = "colors"
ALPHA_RULE = "alpha"
RGB555_RULE = "rgb555"
PALETTE_RULE = "palette"
SIZE_RULE = "size"
TILING_RULE = "tiling"
FOOTPRINT_RULE = "footprint"
RULES = (COLORS_RULE, ALPHA_RULE, RGB555_RULE, PALETTE_RULE, SIZE_RULE, TILING_RULE, FOOTPRINT_RULE)

FLOOR_PREFIX = "floor_"


@dataclass(frozen=True)
class SizeRule:
    label: str
    pattern: str
    min_width: int
    max_width: int
    min_height: int
    max_height: Optional[int]
    diamond: bool = False
    max_colors: int = MAX_OPAQUE_COLORS
    hangs_below: bool = False

    def matches(self, name):
        return re.search(self.pattern, name) is not None

    def accepts(self, width, height):
        within_height = height >= self.min_height and (self.max_height is None or height <= self.max_height)
        return self.min_width <= width <= self.max_width and within_height

    def fixed_size(self):
        if self.min_width == self.max_width and self.min_height == self.max_height:
            return self.min_width, self.min_height
        return None

    def describe(self):
        width = f"{self.min_width}" if self.min_width == self.max_width else f"{self.min_width}-{self.max_width}"
        if self.max_height is None:
            height = f"{self.min_height}+"
        elif self.min_height == self.max_height:
            height = f"{self.min_height}"
        else:
            height = f"{self.min_height}-{self.max_height}"
        return f"{width}x{height}"


def exact(label, pattern, width, height, diamond=False, max_colors=MAX_OPAQUE_COLORS, hangs_below=False):
    return SizeRule(label, pattern, width, width, height, height, diamond, max_colors=max_colors, hangs_below=hangs_below)


SIZE_RULES = (
    exact("floor tile", r"^floor_", CELL_WIDTH, CELL_HEIGHT, diamond=True),
    exact("fog overlay", r"^fog(_edge_(ne|nw|se|sw))?$", CELL_WIDTH, CELL_HEIGHT, diamond=True),
    exact("range highlight", r"^hl_", CELL_WIDTH, CELL_HEIGHT, diamond=True),
    exact("exit marker", r"^overlay_exit_(ne|nw|se|sw)(_\d+)?$", CELL_WIDTH, CELL_HEIGHT, diamond=True),
    exact("side face", r"^side_", CELL_WIDTH, 2 * CELL_HEIGHT, hangs_below=True),
    exact("tall wall", r"^wall_.+_tall(_door_(left|right))?$", 32, 48),
    exact("wall", r"^wall_", 32, 32),
    exact("kiln glow strip", r"^block_kiln_glow$", 64, 40),
    exact("potter's wheel spin strip", r"^prop_potters_wheel_spin$", 128, 28),
    SizeRule("block, 32x(16+height)", r"^block_", 32, 32, 17, None),
    SizeRule("prop, up to 48x64", r"^prop_", 32, 48, 16, 64),
    SizeRule("overlay", r"^overlay_", 32, 32, 16, None),
    exact("character sheet", r"_sheet$", 96, 128),
    exact("character frame", r"_idle_(se|sw|ne|nw)$", 24, 32),
    exact("dialogue portrait", r"_portrait$", 32, 32),
    exact("bar", r"^bar_", 64, 8),
    exact("menu box", r"^box_menu$", 96, 88),
    exact("dialogue box", r"^box_dialogue$", 208, 48),
    exact("portrait frame", r"^frame_portrait$", 40, 40),
    exact("bar frame", r"^frame_bar$", 64, 8),
    exact("cursor", r"^cursor$", 8, 8),
    exact("fallacy icon", r"^icon_", 16, 16),
    exact("juror meter", r"^meter_jurors$", 200, 16),
    exact("title background", r"^bg_", 384, 216, max_colors=BACKGROUND_MAX_COLORS),
    exact("drifting cloud", r"^cloud_", 32, 16),
)


def size_rule_for(name):
    key = stem(name)
    return next((rule for rule in SIZE_RULES if rule.matches(key)), None)


def color_limit_for(name):
    rule = size_rule_for(name)
    return rule.max_colors if rule is not None else MAX_OPAQUE_COLORS


@dataclass(frozen=True)
class Issue:
    rule: str
    severity: str
    message: str


@dataclass
class SpriteReport:
    path: str
    width: int
    height: int
    color_counts: Counter
    transparent_pixels: int
    issues: list = field(default_factory=list)

    @property
    def colors(self):
        return len(self.color_counts)

    @property
    def has_transparency(self):
        return self.transparent_pixels > 0

    @property
    def errors(self):
        return [issue for issue in self.issues if issue.severity == ERROR]

    @property
    def warnings(self):
        return [issue for issue in self.issues if issue.severity == WARNING]

    @property
    def failed(self):
        return bool(self.errors)


def listed(colors):
    shown = ", ".join(to_hex(color) for color in colors[:LISTED_COLORS])
    hidden = len(colors) - LISTED_COLORS
    return f"{shown} and {hidden} more" if hidden > 0 else shown


def plural(count, singular, many=None):
    return f"{count} {singular if count == 1 else (many or singular + 's')}"


def inspect(path, image, palette, skip=()):
    pixels = rgba_pixels(image)
    counts = Counter(pixel for pixel in pixels if pixel[3] != TRANSPARENT)
    report = SpriteReport(path, image.width, image.height, counts, len(pixels) - sum(counts.values()))
    checks = (
        (COLORS_RULE, check_color_limit),
        (ALPHA_RULE, check_alpha),
        (RGB555_RULE, check_rgb555),
        (PALETTE_RULE, lambda current: check_palette(current, palette)),
        (SIZE_RULE, check_size),
        (TILING_RULE, lambda current: check_tiling(current, image)),
        (FOOTPRINT_RULE, lambda current: check_footprint(current, image)),
    )
    for rule, check in checks:
        if rule not in skip:
            report.issues.extend(check(report))
    return report


def check_color_limit(report):
    limit = color_limit_for(report.path)
    if report.colors <= limit:
        return []
    return [Issue(COLORS_RULE, ERROR, f"{report.colors} colours, the limit is {limit} + transparent")]


def check_alpha(report):
    partial = sum(count for color, count in report.color_counts.items() if color[3] != OPAQUE)
    if not partial:
        return []
    return [Issue(ALPHA_RULE, ERROR, f"{plural(partial, 'partially transparent pixel')}, alpha must be 0 or 255")]


def distinct_rgb(report):
    return sorted({color[:3] for color in report.color_counts})


def check_rgb555(report):
    outside = [color for color in distinct_rgb(report) if not is_rgb555(color)]
    if not outside:
        return []
    return [Issue(RGB555_RULE, ERROR, f"{plural(len(outside), 'colour')} with a channel that is not a multiple of 8: {listed(outside)}")]


def check_palette(report, palette):
    outside = [color for color in distinct_rgb(report) if color not in palette]
    if not outside:
        return []
    return [Issue(PALETTE_RULE, ERROR, f"{plural(len(outside), 'colour')} off the palette: {listed(outside)}")]


def check_size(report):
    rule = size_rule_for(report.path)
    if rule is None:
        return [Issue(SIZE_RULE, WARNING, "no size rule for this name prefix")]
    if rule.accepts(report.width, report.height):
        return []
    return [Issue(SIZE_RULE, ERROR, f"{report.width}x{report.height}, a {rule.label} must be {rule.describe()}")]


def check_tiling(report, image):
    if not stem(report.path).startswith(FLOOR_PREFIX):
        return []
    gaps = count_gaps(image)
    if not gaps:
        return []
    return [Issue(TILING_RULE, ERROR, f"{plural(gaps, 'gap pixel')} when the tile repeats on the isometric grid")]


def check_footprint(report, image):
    rule = size_rule_for(report.path)
    if rule is not None and rule.hangs_below:
        return check_hangs_below(image)
    if rule is None or not rule.diamond:
        return []
    outside = count_outside_footprint(image)
    if not outside:
        return []
    return [Issue(FOOTPRINT_RULE, ERROR, f"{plural(outside, 'opaque pixel')} outside the {CELL_WIDTH}x{CELL_HEIGHT} diamond, clear the background around it")]


def check_hangs_below(image):
    inside = count_inside_footprint(image)
    if not inside:
        return []
    return [Issue(FOOTPRINT_RULE, ERROR, f"{plural(inside, 'opaque pixel')} inside the {CELL_WIDTH}x{CELL_HEIGHT} diamond, a side face hangs below the cell and leaves the floor clear")]
