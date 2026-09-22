import os
import unittest

from images import TRANSPARENT, load_rgba, rgba_pixels
from palette import load_gpl
from side_faces import BAND_ROWS, CANVAS, DEFAULT_OUT, FACE_ROWS, MATERIALS, SPLIT_COLUMN, draw, footprint_bottom
from sprite_rules import inspect


class SideFaceTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_every_material_passes_the_art_rules(self):
        for material in MATERIALS:
            report = inspect(f"side_{material}.png", draw(material, self.palette), self.palette)
            self.assertFalse(report.issues, (material, report.issues))

    def test_the_committed_sources_are_the_drawn_faces(self):
        for material in MATERIALS:
            committed = load_rgba(os.path.join(DEFAULT_OUT, f"side_{material}.png"))
            self.assertEqual(rgba_pixels(draw(material, self.palette)), rgba_pixels(committed), material)

    def test_each_column_hangs_two_levels_right_under_the_floor_edge(self):
        for material in MATERIALS:
            pixels = rgba_pixels(draw(material, self.palette))
            for column in range(CANVAS):
                rows = [row for row in range(CANVAS) if pixels[row * CANVAS + column][3] != TRANSPARENT]
                top = footprint_bottom(column) + 1
                self.assertEqual(list(range(top, min(CANVAS, top + FACE_ROWS))), rows, (material, column))

    def test_the_body_repeats_every_level_and_every_cell_so_walls_have_no_seam(self):
        for material in MATERIALS:
            body = MATERIALS[material][0]
            self.assertEqual(BAND_ROWS, len(body))
            self.assertTrue(all(len(row) == SPLIT_COLUMN for row in body), material)


if __name__ == "__main__":
    unittest.main()
