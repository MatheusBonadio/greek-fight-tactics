import contextlib
import io
import tempfile
import unittest

from images import CLEAR, load_rgba, png_files
from palette import load_gpl
from sprite_rules import ALPHA_RULE, COLORS_RULE, ERROR, FOOTPRINT_RULE, MAX_OPAQUE_COLORS, PALETTE_RULE, RGB555_RULE, SIZE_RULE, TILING_RULE, WARNING, inspect, size_rule_for
from tests.builders import FLOOR_FOLDER, OVERLAY_FOLDER, diamond, filled, marble_floor, on_background, save, shrink_diamond, strip
from verify import main

BAR_SIZE = (64, 8)
OUTLINE = (8, 8, 16)
MARBLE_LIGHT = (248, 248, 240)


def run(arguments):
    output = io.StringIO()
    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
        code = main(arguments)
    return code, output.getvalue()


def rules_of(report, severity=ERROR):
    return {issue.rule for issue in report.issues if issue.severity == severity}


class ColourLimitTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_fifteen_colours_and_transparency_pass(self):
        image = strip(BAR_SIZE, list(self.palette.colors[:MAX_OPAQUE_COLORS]) + [CLEAR], OUTLINE)
        report = inspect("bar_test.png", image, self.palette)
        self.assertEqual(report.colors, MAX_OPAQUE_COLORS)
        self.assertTrue(report.has_transparency)
        self.assertFalse(report.failed, report.issues)

    def test_sixteen_opaque_colours_fail(self):
        image = strip(BAR_SIZE, self.palette.colors[:MAX_OPAQUE_COLORS + 1], OUTLINE)
        report = inspect("bar_test.png", image, self.palette)
        self.assertEqual(rules_of(report), {COLORS_RULE})

    def test_partial_alpha_counts_as_its_own_colour_and_fails(self):
        image = strip(BAR_SIZE, [(72, 200, 224, 140)], (72, 200, 224))
        report = inspect("bar_test.png", image, self.palette)
        self.assertEqual(report.colors, 2)
        self.assertEqual(rules_of(report), {ALPHA_RULE})


class ColourValueTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_a_channel_off_rgb555_fails_both_colour_rules(self):
        report = inspect("bar_test.png", strip(BAR_SIZE, [(6, 6, 11)], OUTLINE), self.palette)
        self.assertEqual(rules_of(report), {RGB555_RULE, PALETTE_RULE})

    def test_an_rgb555_colour_off_the_palette_fails_only_the_palette_rule(self):
        report = inspect("bar_test.png", strip(BAR_SIZE, [(16, 16, 32)], OUTLINE), self.palette)
        self.assertEqual(rules_of(report), {PALETTE_RULE})
        self.assertIn("#101020", report.issues[0].message)

    def test_skipped_rules_are_not_checked(self):
        report = inspect("bar_test.png", strip(BAR_SIZE, [(6, 6, 11, 90)], OUTLINE), self.palette, {ALPHA_RULE, RGB555_RULE, PALETTE_RULE})
        self.assertFalse(report.issues)


class SizeTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_a_wrong_size_for_the_prefix_fails(self):
        report = inspect("floor_test.png", filled((32, 32), OUTLINE), self.palette)
        self.assertEqual(rules_of(report), {SIZE_RULE})

    def test_an_unknown_prefix_only_warns(self):
        report = inspect("mystery.png", filled((10, 10), OUTLINE), self.palette)
        self.assertFalse(report.failed)
        self.assertEqual(rules_of(report, WARNING), {SIZE_RULE})

    def test_rules_follow_the_pixel_art_skill(self):
        expectations = {
            "floor_marble.png": (32, 16),
            "fog_edge_ne.png": (32, 16),
            "hl_refute.png": (32, 16),
            "wall_brick.png": (32, 32),
            "kleia_sheet.png": (96, 128),
            "kleia_idle_se.png": (24, 32),
            "bar_coherence.png": (64, 8),
            "box_menu.png": (96, 88),
            "box_dialogue.png": (208, 48),
            "frame_portrait.png": (40, 40),
            "cursor.png": (8, 8),
            "icon_ad_hominem.png": (16, 16),
            "meter_jurors.png": (200, 16),
        }
        for name, size in expectations.items():
            self.assertEqual(size_rule_for(name).fixed_size(), size, name)

    def test_blocks_grow_upwards_and_props_stay_within_48x64(self):
        block = size_rule_for("block_doric_column.png")
        prop = size_rule_for("prop_olive_tree.png")
        self.assertTrue(block.accepts(32, 56))
        self.assertFalse(block.accepts(32, 16))
        self.assertFalse(block.accepts(48, 56))
        self.assertTrue(prop.accepts(48, 64))
        self.assertTrue(prop.accepts(32, 28))
        self.assertFalse(prop.accepts(48, 72))
        self.assertFalse(prop.accepts(64, 32))


class FloorTilingTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_placeholder_floors_repeat_without_gaps(self):
        for path, _ in png_files([FLOOR_FOLDER]):
            report = inspect(path, load_rgba(path), self.palette)
            self.assertNotIn(TILING_RULE, rules_of(report), path)

    def test_a_shrunken_diamond_leaves_gaps(self):
        report = inspect("floor_test.png", shrink_diamond(marble_floor()), self.palette)
        self.assertEqual(rules_of(report), {TILING_RULE})

    def test_only_floor_tiles_are_checked_for_gaps(self):
        report = inspect("hl_test.png", shrink_diamond(marble_floor()), self.palette)
        self.assertFalse(report.issues)


class FootprintRuleTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_placeholder_floors_and_overlays_stay_inside_the_diamond(self):
        for path, _ in png_files([FLOOR_FOLDER, OVERLAY_FOLDER]):
            report = inspect(path, load_rgba(path), self.palette)
            self.assertNotIn(FOOTPRINT_RULE, rules_of(report), path)

    def test_a_floor_that_kept_its_background_fails_although_it_leaves_no_gaps(self):
        report = inspect("floor_test.png", on_background(marble_floor(), MARBLE_LIGHT), self.palette)
        self.assertEqual(rules_of(report), {FOOTPRINT_RULE})
        self.assertIn("244 opaque pixels outside the 32x16 diamond", report.issues[0].message)

    def test_overlays_on_the_cell_are_held_to_the_diamond(self):
        for name in ("hl_test.png", "fog.png"):
            report = inspect(name, filled((32, 16), MARBLE_LIGHT), self.palette)
            self.assertEqual(rules_of(report), {FOOTPRINT_RULE}, name)

    def test_blocks_may_fill_their_canvas(self):
        report = inspect("wall_test.png", filled((32, 32), MARBLE_LIGHT), self.palette)
        self.assertFalse(report.issues)


class VerifyCommandTests(unittest.TestCase):
    def test_exits_with_zero_and_reports_the_colour_count(self):
        with tempfile.TemporaryDirectory() as folder:
            save(marble_floor(), folder, "floor_marble.png")
            code, output = run([folder])
        self.assertEqual(code, 0, output)
        self.assertRegex(output, r"floor_marble\.png\s+32x16\s+4\s+yes\s+ok")
        self.assertIn("1 file: 1 ok, 0 with warnings, 0 failed", output)

    def test_exits_with_one_when_a_file_breaks_a_rule(self):
        with tempfile.TemporaryDirectory() as folder:
            save(marble_floor(), folder, "floor_marble.png")
            save(filled((64, 8), (6, 6, 11)), folder, "bar_bad.png")
            code, output = run([folder])
        self.assertEqual(code, 1)
        self.assertIn("error rgb555", output)
        self.assertIn("1 failed", output)

    def test_skip_lets_a_known_exception_through(self):
        with tempfile.TemporaryDirectory() as folder:
            save(diamond((16, 16, 32)), folder, "fog.png")
            code, _ = run([folder, "--skip", PALETTE_RULE])
        self.assertEqual(code, 0)

    def test_colors_lists_each_colour_with_its_palette_name(self):
        with tempfile.TemporaryDirectory() as folder:
            save(strip(BAR_SIZE, [(16, 16, 32)], OUTLINE), folder, "bar_test.png")
            _, output = run([folder, "--colors"])
        self.assertRegex(output, r"#080810\s+outline\s+511")
        self.assertRegex(output, r"#101020\s+off-palette\s+1")

    def test_exits_with_two_without_png_files(self):
        with tempfile.TemporaryDirectory() as folder:
            code, _ = run([folder])
        self.assertEqual(code, 2)


if __name__ == "__main__":
    unittest.main()
