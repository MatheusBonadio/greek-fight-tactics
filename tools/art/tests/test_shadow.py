import os
import unittest

from images import TRANSPARENT, load_rgba, rgba_pixels
from palette import load_gpl
from shadow import DEFAULT_OUT, FILE_NAME, RADIUS_X, RADIUS_Y, SHADOW_COLOR, draw
from sprite_rules import inspect
from tile_preview import CELL_HEIGHT, CELL_WIDTH, in_footprint


def rows(image):
    pixels = rgba_pixels(image)
    return [pixels[row * CELL_WIDTH:(row + 1) * CELL_WIDTH] for row in range(CELL_HEIGHT)]


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


class ShadowTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()
        self.shadow = draw(self.palette)

    def test_the_committed_sprite_is_the_drawn_shadow(self):
        committed = load_rgba(os.path.join(DEFAULT_OUT, FILE_NAME))
        self.assertEqual(rgba_pixels(self.shadow), rgba_pixels(committed))

    def test_the_ellipse_is_solid_and_never_dithered(self):
        for row in rows(self.shadow):
            self.assertLessEqual(len(spans(row)), 1, spans(row))

    def test_it_passes_the_art_rules(self):
        report = inspect(FILE_NAME, self.shadow, self.palette)
        self.assertFalse(report.issues, report.issues)

    def test_it_is_one_palette_colour_with_no_partial_alpha(self):
        used = {pixel for pixel in rgba_pixels(self.shadow) if pixel[3] != TRANSPARENT}
        self.assertEqual({dict(self.palette.entries)[SHADOW_COLOR] + (255,)}, used)

    def test_it_stays_inside_the_cell_diamond(self):
        for row, line in enumerate(rows(self.shadow)):
            for column, pixel in enumerate(line):
                if pixel[3] != TRANSPARENT:
                    self.assertTrue(in_footprint(column, row), (column, row))

    def test_it_is_wider_than_a_pair_of_feet(self):
        widest = max((span[1] - span[0] + 1) for line in rows(self.shadow) for span in spans(line))
        self.assertEqual(int(RADIUS_X * 2), widest)
        self.assertGreaterEqual(RADIUS_X, 2 * RADIUS_Y - 0.001)


if __name__ == "__main__":
    unittest.main()
