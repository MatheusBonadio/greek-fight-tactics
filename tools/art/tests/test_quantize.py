import contextlib
import io
import os
import tempfile
import unittest

from PIL import Image

from images import CLEAR, OPAQUE, TRANSPARENT, from_pixels, load_rgba, rgba_pixels
from palette import load_gpl
from quantize import SIZE_FROM_OPTION, SIZE_FROM_REFERENCE, SIZE_KEPT, main, quantize_image, target_size
from sprite_rules import MAX_OPAQUE_COLORS
from tests.builders import filled, marble_floor, on_background, save, strip

UPSCALE = 8


def run(arguments):
    output = io.StringIO()
    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
        code = main(arguments)
    return code, output.getvalue()


class QuantizeImageTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_output_holds_only_palette_colours_and_binary_alpha(self):
        raw = from_pixels((4, 1), [(3, 200, 99, 255), (250, 10, 10, 200), (120, 121, 122, 40), (9, 9, 9, 128)])
        pixels = rgba_pixels(quantize_image(raw, self.palette).image)
        for pixel in pixels:
            self.assertIn(pixel[3], (TRANSPARENT, OPAQUE))
            if pixel[3] == OPAQUE:
                self.assertIn(pixel[:3], self.palette)

    def test_a_flat_colour_stays_flat_because_dithering_is_off(self):
        between_two_entries = (184, 184, 180)
        result = quantize_image(filled((8, 8), between_two_entries), self.palette)
        self.assertEqual(len(set(rgba_pixels(result.image))), 1)

    def test_a_violet_raw_tile_stays_purple(self):
        result = quantize_image(filled((8, 8), (200, 80, 220)), self.palette)
        self.assertEqual(set(rgba_pixels(result.image)), {(192, 120, 224, OPAQUE)})

    def test_each_column_of_a_horizontal_gradient_stays_uniform(self):
        width, height = 64, 8
        gradient = from_pixels((width, height), [(column * 4, column * 3, 255 - column * 2, OPAQUE) for _ in range(height) for column in range(width)])
        pixels = rgba_pixels(quantize_image(gradient, self.palette, max_colors=0).image)
        for column in range(width):
            self.assertEqual(len({pixels[row * width + column] for row in range(height)}), 1)

    def test_alpha_below_the_threshold_turns_transparent(self):
        raw = from_pixels((2, 1), [(248, 248, 240, 127), (248, 248, 240, 128)])
        pixels = rgba_pixels(quantize_image(raw, self.palette, alpha_threshold=128).image)
        self.assertEqual(pixels, [CLEAR, (248, 248, 240, OPAQUE)])

    def test_resizes_with_nearest_neighbour_to_the_exact_size(self):
        tile = marble_floor()
        raw = tile.resize((tile.width * UPSCALE, tile.height * UPSCALE), Image.Resampling.NEAREST)
        result = quantize_image(raw, self.palette, size=tile.size)
        self.assertEqual(result.image.size, tile.size)
        self.assertEqual(result.source_size, raw.size)
        expected = [CLEAR if pixel[3] == TRANSPARENT else pixel for pixel in rgba_pixels(tile)]
        self.assertEqual(rgba_pixels(result.image), expected)

    def test_downscaling_never_blends_neighbouring_colours(self):
        raw = strip((2, 1), [(8, 8, 16), (248, 248, 240)], (8, 8, 16)).resize((64, 32), Image.Resampling.NEAREST)
        result = quantize_image(raw, self.palette, size=(3, 5))
        self.assertLessEqual(set(rgba_pixels(result.image)), {(8, 8, 16, OPAQUE), (248, 248, 240, OPAQUE)})

    def test_keeps_the_most_used_colours_when_over_the_limit(self):
        colors = self.palette.colors[:20]
        pixels = [color for index, color in enumerate(colors) for _ in range(40 - index)]
        raw = from_pixels((len(pixels), 1), [color + (OPAQUE,) for color in pixels])
        result = quantize_image(raw, self.palette)
        kept = {pixel[:3] for pixel in rgba_pixels(result.image)}
        self.assertEqual(result.colors, MAX_OPAQUE_COLORS)
        self.assertEqual(result.reduced_from, len(colors))
        self.assertEqual(kept, set(colors[:MAX_OPAQUE_COLORS]))

    def test_zero_max_colours_disables_the_limit(self):
        colors = self.palette.colors[:20]
        raw = from_pixels((len(colors), 1), [color + (OPAQUE,) for color in colors])
        result = quantize_image(raw, self.palette, max_colors=0)
        self.assertEqual(result.colors, len(colors))
        self.assertEqual(result.reduced_from, 0)


class TargetSizeTests(unittest.TestCase):
    def test_the_size_option_wins(self):
        self.assertEqual(target_size("floor_x.png", (10, 20), {"floor_x": (1, 1)}), ((10, 20), SIZE_FROM_OPTION))

    def test_a_reference_file_with_the_same_name_comes_next(self):
        self.assertEqual(target_size("raw/block_kiln.png", None, {"block_kiln": (32, 40)}), ((32, 40), SIZE_FROM_REFERENCE))

    def test_a_fixed_size_rule_comes_from_the_name(self):
        self.assertEqual(target_size("floor_new.png", None, {}), ((32, 16), "floor tile"))
        self.assertEqual(target_size("kleia_sheet.png", None, {}), ((96, 128), "character sheet"))

    def test_a_variable_size_rule_keeps_the_source_size(self):
        self.assertEqual(target_size("block_new.png", None, {}), (None, SIZE_KEPT))


class QuantizeCommandTests(unittest.TestCase):
    def test_quantizes_a_folder_and_keeps_its_subfolders(self):
        tile = marble_floor()
        with tempfile.TemporaryDirectory() as folder:
            raw = os.path.join(folder, "raw")
            out = os.path.join(folder, "out")
            save(tile.resize((64, 32), Image.Resampling.NEAREST), raw, os.path.join("tiles", "floor_new.png"))
            save(filled((32, 40), (200, 150, 60)), raw, "block_new.png")

            code, output = run([raw, "--out", out])

            self.assertEqual(code, 0, output)
            self.assertEqual(load_rgba(os.path.join(out, "tiles", "floor_new.png")).size, (32, 16))
            self.assertEqual(load_rgba(os.path.join(out, "block_new.png")).size, (32, 40))

    def test_like_takes_the_size_of_the_placeholder_with_the_same_name(self):
        with tempfile.TemporaryDirectory() as folder:
            save(filled((32, 40), (216, 144, 56)), os.path.join(folder, "placeholders"), "block_new.png")
            save(filled((320, 400), (210, 140, 60)), os.path.join(folder, "raw"), "block_new.png")

            code, output = run([os.path.join(folder, "raw"), "--out", os.path.join(folder, "out"), "--like", os.path.join(folder, "placeholders")])

            self.assertEqual(code, 0, output)
            self.assertEqual(load_rgba(os.path.join(folder, "out", "block_new.png")).size, (32, 40))

    def test_exits_with_one_when_an_output_still_breaks_a_rule(self):
        with tempfile.TemporaryDirectory() as folder:
            save(filled((8, 8), (200, 150, 60)), os.path.join(folder, "raw"), "block_tiny.png")
            code, output = run([os.path.join(folder, "raw"), "--out", os.path.join(folder, "out")])
        self.assertEqual(code, 1)
        self.assertIn("error size", output)

    def test_exits_with_one_when_a_raw_floor_keeps_its_background(self):
        raw_floor = on_background(marble_floor(), (255, 255, 255)).resize((512, 256), Image.Resampling.NEAREST)
        with tempfile.TemporaryDirectory() as folder:
            save(raw_floor, os.path.join(folder, "raw"), "floor_marble.png")
            code, output = run([os.path.join(folder, "raw"), "--out", os.path.join(folder, "out")])
        self.assertEqual(code, 1)
        self.assertIn("error footprint", output)

    def test_exits_with_two_when_the_input_is_missing(self):
        with tempfile.TemporaryDirectory() as folder:
            code, _ = run([os.path.join(folder, "missing"), "--out", folder])
        self.assertEqual(code, 2)


if __name__ == "__main__":
    unittest.main()
