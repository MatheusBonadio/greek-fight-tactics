import contextlib
import io
import os
import tempfile
import unittest

from images import TRANSPARENT, load_rgba, parse_color, png_files
from tests.builders import FLOOR_FOLDER, MARBLE_FLOOR, filled, marble_floor, save, shrink_diamond
from tile_preview import CELL_HEIGHT, CELL_WIDTH, DEFAULT_BACKGROUND, compose, count_gaps, main

TILE_CENTER = (CELL_WIDTH // 2, CELL_HEIGHT // 2)


def run(arguments):
    output = io.StringIO()
    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
        code = main(arguments)
    return code, output.getvalue()


class ComposeTests(unittest.TestCase):
    def test_four_copies_form_a_two_by_two_diamond(self):
        tile = marble_floor()
        canvas = compose(tile)
        self.assertEqual(canvas.size, (2 * CELL_WIDTH, 2 * CELL_HEIGHT))
        center = tile.getpixel(TILE_CENTER)
        for x, y in ((32, 8), (16, 16), (48, 16), (32, 24)):
            self.assertEqual(canvas.getpixel((x, y)), center, (x, y))
        for corner in ((0, 0), (63, 0), (0, 31), (63, 31)):
            self.assertEqual(canvas.getpixel(corner)[3], TRANSPARENT, corner)

    def test_the_front_copy_draws_the_shared_edge(self):
        tile = marble_floor()
        canvas = compose(tile)
        right_copy_top_left_edge = (13, 1)
        self.assertEqual(canvas.getpixel((16 + 29, 9)), tile.getpixel(right_copy_top_left_edge))

    def test_a_tall_block_is_lifted_so_its_base_sits_on_the_cell(self):
        wall = filled((CELL_WIDTH, 32), (216, 144, 56))
        canvas = compose(wall)
        self.assertEqual(canvas.size, (2 * CELL_WIDTH, 2 * CELL_HEIGHT + 16))

    def test_a_wide_prop_widens_the_canvas(self):
        self.assertEqual(compose(filled((48, 56), (120, 152, 56))).width, 2 * CELL_WIDTH + 16)


class GapTests(unittest.TestCase):
    def test_placeholder_floors_have_no_gaps(self):
        for path, _ in png_files([FLOOR_FOLDER]):
            self.assertEqual(count_gaps(load_rgba(path)), 0, path)

    def test_a_shrunken_diamond_leaves_gaps(self):
        self.assertGreater(count_gaps(shrink_diamond(marble_floor())), 0)

    def test_only_cell_sized_tiles_are_measured(self):
        self.assertIsNone(count_gaps(filled((CELL_WIDTH, 32), (8, 8, 16))))


class PreviewCommandTests(unittest.TestCase):
    def test_writes_a_scaled_preview_over_the_background(self):
        with tempfile.TemporaryDirectory() as folder:
            code, output = run([MARBLE_FLOOR, "--out", folder, "--scale", "3"])
            preview = load_rgba(os.path.join(folder, "floor_marble_tiling.png"))
        self.assertEqual(code, 0, output)
        self.assertEqual(preview.size, (3 * 2 * CELL_WIDTH, 3 * 2 * CELL_HEIGHT))
        self.assertEqual(preview.getpixel((0, 0)), parse_color(DEFAULT_BACKGROUND))
        self.assertIn("ok, no gaps", output)

    def test_background_none_keeps_transparency(self):
        with tempfile.TemporaryDirectory() as folder:
            run([MARBLE_FLOOR, "--out", folder, "--background", "none", "--scale", "1"])
            preview = load_rgba(os.path.join(folder, "floor_marble_tiling.png"))
        self.assertEqual(preview.getpixel((0, 0))[3], TRANSPARENT)

    def test_grid_sets_the_copies_per_side(self):
        with tempfile.TemporaryDirectory() as folder:
            run([MARBLE_FLOOR, "--out", folder, "--grid", "3", "--scale", "1"])
            preview = load_rgba(os.path.join(folder, "floor_marble_tiling.png"))
        self.assertEqual(preview.size, (3 * CELL_WIDTH, 3 * CELL_HEIGHT))

    def test_exits_with_one_when_a_tile_leaves_gaps(self):
        with tempfile.TemporaryDirectory() as folder:
            path = save(shrink_diamond(marble_floor()), folder, "floor_broken.png")
            code, output = run([path, "--out", os.path.join(folder, "out")])
        self.assertEqual(code, 1)
        self.assertIn("gap pixels", output)


if __name__ == "__main__":
    unittest.main()
