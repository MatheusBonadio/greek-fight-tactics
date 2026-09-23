import unittest

from color_key import BAND_WIDTH, DEFAULT_HEIGHT, KEYS, colors_of, draw, file_name
from images import TRANSPARENT, rgba_pixels
from palette import is_rgb555, load_gpl

KLEIA_COLOR_COUNT = 15


class ColorKeyTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_every_key_colour_comes_from_the_palette(self):
        for character in KEYS:
            for color in colors_of(character, self.palette):
                self.assertIn(color[:3], self.palette, (character, color))
                self.assertTrue(is_rgb555(color), (character, color))

    def test_kleia_carries_the_fifteen_colours_of_her_ficha(self):
        self.assertEqual(len(KEYS["kleia"]), KLEIA_COLOR_COUNT)
        self.assertEqual(len(set(KEYS["kleia"])), KLEIA_COLOR_COUNT)

    def test_each_colour_gets_one_band_and_nothing_is_transparent(self):
        for character in KEYS:
            image = draw(character, self.palette)
            self.assertEqual(image.size, (len(KEYS[character]) * BAND_WIDTH, DEFAULT_HEIGHT), character)
            pixels = rgba_pixels(image)
            self.assertEqual({pixel[:3] for pixel in pixels}, {color[:3] for color in colors_of(character, self.palette)}, character)
            self.assertFalse(any(pixel[3] == TRANSPARENT for pixel in pixels), character)

    def test_every_row_repeats_the_same_bands(self):
        for character in KEYS:
            image = draw(character, self.palette)
            width, height = image.size
            pixels = rgba_pixels(image)
            first = pixels[:width]
            for row in range(1, height):
                self.assertEqual(pixels[row * width:(row + 1) * width], first, (character, row))

    def test_the_file_name_follows_the_delivery_convention(self):
        self.assertEqual(file_name("kleia"), "color_key_kleia.png")


if __name__ == "__main__":
    unittest.main()
