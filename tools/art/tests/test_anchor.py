import contextlib
import io
import os
import tempfile
import unittest

from PIL import Image

from anchor import anchor_offset, base_anchor, main, recentred
from images import CLEAR, load_rgba
from tests.builders import opaque, save

BODY = (216, 120, 72)
CANVAS = (32, 40)
BASE_WIDTH = 20


def plate(width, center_row, center_x=16, canvas=CANVAS):
    image = Image.new("RGBA", canvas, CLEAR)
    half = width // 2
    for step in range(width // 4 + 1):
        for row in (center_row - step, center_row + step):
            for x in range(center_x - half + 2 * step, center_x + half - 2 * step):
                image.putpixel((x, row), opaque(BODY))
    for row in range(center_row - 20, center_row - width // 4):
        for x in range(center_x - 3, center_x + 3):
            image.putpixel((x, row), opaque(BODY))
    return image


def run(arguments):
    output = io.StringIO()
    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
        code = main(arguments)
    return code, output.getvalue()


class AnchorTests(unittest.TestCase):
    def test_a_base_on_the_cell_centre_needs_no_move(self):
        image = plate(BASE_WIDTH, CANVAS[1] - 8)
        self.assertEqual(anchor_offset(image), (0, 0))

    def test_the_widest_base_row_is_the_anchor(self):
        row, center, width = base_anchor(plate(BASE_WIDTH, 30))
        self.assertEqual((row, center, width), (30, 16, BASE_WIDTH))

    def test_a_base_drawn_low_and_left_is_moved_up_and_right(self):
        image = plate(BASE_WIDTH, CANVAS[1] - 5, center_x=15)
        self.assertEqual(anchor_offset(image), (1, -3))
        self.assertEqual(anchor_offset(recentred(image)), (0, 0))

    def test_a_full_width_block_is_already_the_cell(self):
        image = Image.new("RGBA", (32, 32), opaque(BODY))
        self.assertEqual(anchor_offset(image), (0, 0))

    def test_a_sprite_touching_the_top_grows_upwards(self):
        image = plate(BASE_WIDTH, 25, canvas=(32, 30))
        for x in range(13, 19):
            image.putpixel((x, 0), opaque(BODY))
        moved = recentred(image)
        self.assertEqual(moved.size, (32, 33))
        self.assertEqual(anchor_offset(moved), (0, 0))

    def test_the_command_reports_and_fixes_only_props_and_blocks(self):
        with tempfile.TemporaryDirectory() as folder:
            prop = save(plate(BASE_WIDTH, CANVAS[1] - 5), folder, "prop_tree.png")
            save(plate(BASE_WIDTH, CANVAS[1] - 5), folder, "kleia_idle_se.png")
            code, output = run([folder])
            self.assertEqual(code, 0)
            self.assertIn("off centre by +0, -3", output)
            self.assertIn("1 sprites, 1 off centre", output)

            code, output = run([folder, "--fix"])
            self.assertEqual(code, 0)
            self.assertEqual(anchor_offset(load_rgba(prop)), (0, 0))
            self.assertTrue(os.path.exists(prop))


if __name__ == "__main__":
    unittest.main()
