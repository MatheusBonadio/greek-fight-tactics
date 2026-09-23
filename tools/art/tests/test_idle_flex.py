import os
import unittest

from idle_flex import (CHARACTER_HEIGHT, CHARACTER_WIDTH, DEFAULT_OUT, FLEX, animated, draw, flexed, hem_row, poses,
                       scanlines, spans, standing_rows)
from images import CLEAR, from_pixels, load_rgba, rgba_pixels
from palette import load_gpl
from sprite_rules import inspect

FIGURE = (
    (4, 10, 9, 15),
    (11, 25, 7, 17),
    (26, 31, 8, 11),
    (26, 31, 13, 16),
)
INK = (248, 248, 240, 255)
DEPTH = 1


def built(bands):
    pixels = [CLEAR] * (CHARACTER_WIDTH * CHARACTER_HEIGHT)
    for top, bottom, left, right in bands:
        for row in range(top, bottom + 1):
            for column in range(left, right + 1):
                pixels[row * CHARACTER_WIDTH + column] = INK
    return from_pixels((CHARACTER_WIDTH, CHARACTER_HEIGHT), pixels)


def repeated(pose, count):
    lines = [[] for _ in range(CHARACTER_HEIGHT)]
    for _ in range(count):
        for row, line in enumerate(scanlines(pose)):
            lines[row].extend(line)
    return from_pixels((CHARACTER_WIDTH * count, CHARACTER_HEIGHT), [pixel for line in lines for pixel in line])


class IdleFlexTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()
        self.pose = built(FIGURE)
        self.lines = scanlines(self.pose)
        self.hem = hem_row(self.lines)

    def test_the_garment_ends_where_the_legs_part(self):
        self.assertEqual(25, self.hem)

    def test_the_whole_body_dips_without_changing_shape(self):
        bent = scanlines(flexed(self.pose, DEPTH))
        for row in range(self.hem + 1):
            self.assertEqual(self.lines[row], bent[row + DEPTH], row)

    def test_the_feet_stay_exactly_where_they_were(self):
        bent = scanlines(flexed(self.pose, DEPTH))
        for row in range(self.hem + 1 + DEPTH, CHARACTER_HEIGHT):
            self.assertEqual(self.lines[row], bent[row], row)
        self.assertEqual(standing_rows(self.lines)[-1], standing_rows(bent)[-1])

    def test_the_visible_leg_loses_exactly_the_rows_the_knee_took(self):
        bent = scanlines(flexed(self.pose, DEPTH))
        legs = sum(1 for row in range(CHARACTER_HEIGHT) if row > self.hem and len(spans(self.lines[row])) > 1)
        bent_legs = sum(1 for row in range(CHARACTER_HEIGHT) if len(spans(bent[row])) > 1)
        self.assertEqual(legs - DEPTH, bent_legs)

    def test_nothing_moves_sideways(self):
        bent = scanlines(flexed(self.pose, DEPTH))
        for row, line in enumerate(bent):
            self.assertIn(spans(line), [spans(rest) for rest in self.lines], row)

    def test_a_head_against_the_ceiling_is_refused(self):
        with self.assertRaises(ValueError):
            flexed(built(((0, 25, 7, 17), (26, 31, 8, 11), (26, 31, 13, 16))), DEPTH)

    def test_a_leg_too_short_to_bend_is_refused(self):
        with self.assertRaises(ValueError):
            flexed(built(((4, 29, 7, 17), (30, 30, 8, 11), (30, 30, 13, 16))), DEPTH)

    def test_the_first_frame_is_the_pose_the_strip_came_with(self):
        self.assertEqual(rgba_pixels(self.pose), rgba_pixels(poses(draw(repeated(self.pose, len(FLEX))))[0]))

    def test_the_strip_stands_bends_holds_and_stands_again(self):
        frames = poses(draw(repeated(self.pose, len(FLEX))))
        self.assertEqual(rgba_pixels(self.pose), rgba_pixels(frames[0]))
        self.assertEqual(rgba_pixels(flexed(self.pose, 1)), rgba_pixels(frames[1]))
        self.assertEqual(rgba_pixels(frames[1]), rgba_pixels(frames[2]))
        self.assertEqual(rgba_pixels(frames[0]), rgba_pixels(frames[3]))

    def test_the_committed_strips_are_what_the_script_draws(self):
        strips = animated(DEFAULT_OUT)
        self.assertTrue(strips)
        for path in strips:
            committed = load_rgba(path)
            self.assertEqual(rgba_pixels(draw(committed)), rgba_pixels(committed), os.path.basename(path))

    def test_every_committed_strip_keeps_its_feet_and_its_garment(self):
        for path in animated(DEFAULT_OUT):
            frames = poses(load_rgba(path))
            rest, bent = scanlines(frames[0]), scanlines(frames[1])
            hem = hem_row(rest)
            self.assertEqual(standing_rows(rest)[-1], standing_rows(bent)[-1], os.path.basename(path))
            for row in range(hem + 1):
                self.assertEqual(rest[row], bent[row + DEPTH], (os.path.basename(path), row))

    def test_every_drawn_strip_passes_the_art_rules(self):
        for path in animated(DEFAULT_OUT):
            name = os.path.basename(path)
            report = inspect(name, draw(load_rgba(path)), self.palette)
            self.assertFalse(report.issues, (name, report.issues))


if __name__ == "__main__":
    unittest.main()
