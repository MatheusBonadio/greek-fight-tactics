import os
import tempfile
import unittest

from palette import LUMA_WEIGHTS, Palette, is_rgb555, load_gpl, to_hex, weighted_distance

PROJECT_PALETTE_ENTRIES = 44
PROJECT_PALETTE_COLORS = 43


def write_palette(folder, text):
    path = os.path.join(folder, "test.gpl")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(text)
    return path


class ProjectPaletteTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_every_channel_is_a_multiple_of_eight(self):
        self.assertTrue(all(is_rgb555(color) for color in self.palette.colors))

    def test_duplicate_entries_share_one_colour_with_the_first_name(self):
        self.assertEqual(len(self.palette.entries), PROJECT_PALETTE_ENTRIES)
        self.assertEqual(len(self.palette), PROJECT_PALETTE_COLORS)
        self.assertEqual(self.palette.name_of((248, 248, 240)), "marble_light")

    def test_names_come_from_the_file(self):
        self.assertEqual(self.palette.name_of((16, 24, 40)), "outline_blue")
        self.assertIsNone(self.palette.name_of((16, 16, 32)))


class PaletteParsingTests(unittest.TestCase):
    def test_rejects_a_file_without_the_gimp_header(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(ValueError):
                load_gpl(write_palette(folder, "8 8 16 outline\n"))

    def test_rejects_a_channel_above_255(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(ValueError):
                load_gpl(write_palette(folder, "GIMP Palette\nName: x\n300 0 0 red\n"))

    def test_rejects_a_palette_without_colours(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(ValueError):
                load_gpl(write_palette(folder, "GIMP Palette\nName: x\nColumns: 8\n#\n"))

    def test_unnamed_entry_is_named_by_its_hex(self):
        with tempfile.TemporaryDirectory() as folder:
            palette = load_gpl(write_palette(folder, "GIMP Palette\n  8  16  24\n"))
        self.assertEqual(palette.name_of((8, 16, 24)), "#081018")


class NearestColourTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_palette_colours_map_to_themselves(self):
        for color in self.palette.colors:
            self.assertEqual(self.palette.nearest(color), color, to_hex(color))

    def test_off_palette_colours_map_to_the_closest_entry(self):
        self.assertEqual(self.palette.nearest((250, 250, 238)), (248, 248, 240))
        self.assertEqual(self.palette.nearest((250, 251, 250)), (248, 248, 248))
        self.assertEqual(self.palette.nearest((70, 198, 226)), (72, 200, 224))
        self.assertEqual(self.palette.nearest((190, 60, 44)), (192, 56, 40))

    def test_violets_and_navies_keep_their_hue(self):
        purple_light = (192, 120, 224)
        aegean_dark = (16, 48, 80)
        self.assertEqual(self.palette.nearest((200, 80, 220)), purple_light)
        self.assertEqual(self.palette.nearest((227, 72, 255)), purple_light)
        self.assertEqual(self.palette.nearest((40, 40, 90)), aegean_dark)
        self.assertEqual(self.palette.nearest((48, 48, 96)), aegean_dark)

    def test_luminance_weights_scale_the_squared_channel_differences(self):
        self.assertEqual(weighted_distance((0, 0, 0), (1, 1, 1)), sum(LUMA_WEIGHTS))
        self.assertEqual(weighted_distance((0, 0, 0), (10, 0, 0)), LUMA_WEIGHTS[0] * 100)
        self.assertEqual(weighted_distance((0, 0, 30), (0, 0, 0)), LUMA_WEIGHTS[2] * 900)

    def test_green_counts_about_five_times_more_than_blue(self):
        palette = Palette([("blue", (0, 0, 30)), ("green", (0, 10, 0))])
        self.assertEqual(palette.nearest((0, 0, 0)), (0, 10, 0))

    def test_a_tie_resolves_to_the_first_entry(self):
        palette = Palette([("dark", (0, 0, 0)), ("light", (16, 0, 0))])
        self.assertEqual(palette.nearest((8, 0, 0)), (0, 0, 0))

    def test_subset_only_offers_the_requested_colours(self):
        subset = self.palette.subset([(8, 8, 16), (248, 248, 240)])
        self.assertEqual(subset.colors, ((8, 8, 16), (248, 248, 240)))
        self.assertEqual(subset.nearest((200, 60, 40)), (8, 8, 16))


class Rgb555Tests(unittest.TestCase):
    def test_multiples_of_eight_are_rgb555(self):
        self.assertTrue(is_rgb555((0, 8, 248)))

    def test_any_other_channel_is_not(self):
        self.assertFalse(is_rgb555((6, 6, 11)))
        self.assertFalse(is_rgb555((255, 248, 248)))


if __name__ == "__main__":
    unittest.main()
