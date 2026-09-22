import os
import unittest

from images import load_rgba, rgba_pixels
from palette import load_gpl
from sprite_rules import inspect
from title_frames import (
    BREATH,
    CLOUD,
    CYPRESS,
    DEFAULT_OUT,
    FOLIAGE,
    FRAMES,
    GLINT,
    GLOW,
    HALO,
    SEA,
    SKY,
    STILL_FRAME,
    SUNLIT,
    draw,
    file_name,
    open_water_rows,
    source_path,
)


def painted(image):
    width, height = image.size
    pixels = rgba_pixels(image)
    return {(index % width, index // width): pixel for index, pixel in enumerate(pixels)}


def moves(before, after):
    return (
        (before in SEA + GLINT and after in SEA + GLINT)
        or (before in HALO and after == GLOW)
        or (before == GLOW and after in HALO)
        or (before in SKY and after in CLOUD)
        or (before in CLOUD and after in SKY)
        or SUNLIT.get(before) == after
        or (before in FOLIAGE) != (after in FOLIAGE)
    )


class TitleFrameTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()
        self.source = load_rgba(source_path(DEFAULT_OUT))
        self.width, self.height = self.source.size
        self.still = painted(self.source)
        self.frames = [draw(frame, self.source) for frame in range(FRAMES)]

    def test_every_frame_passes_the_art_rules(self):
        for frame in range(FRAMES):
            report = inspect(file_name(frame), self.frames[frame], self.palette)
            self.assertFalse(report.issues, (file_name(frame), report.issues))

    def test_the_committed_frames_are_the_drawn_sunset(self):
        for frame in range(FRAMES):
            if frame == STILL_FRAME:
                continue

            committed = load_rgba(os.path.join(DEFAULT_OUT, file_name(frame)))
            self.assertEqual(rgba_pixels(self.frames[frame]), rgba_pixels(committed), file_name(frame))

    def test_the_still_frame_opens_the_loop_untouched(self):
        self.assertEqual(rgba_pixels(self.source), rgba_pixels(self.frames[STILL_FRAME]))
        self.assertEqual(0, BREATH[STILL_FRAME])

    def test_no_frame_stands_completely_still(self):
        for frame in range(FRAMES):
            if frame == STILL_FRAME:
                continue

            self.assertNotEqual(rgba_pixels(self.source), rgba_pixels(self.frames[frame]), file_name(frame))

    def test_only_the_water_sun_clouds_light_and_leaves_move(self):
        for frame in range(FRAMES):
            moved = painted(self.frames[frame])
            for cell, after in moved.items():
                before = self.still[cell]
                if before != after:
                    self.assertTrue(moves(before, after), (file_name(frame), cell, before, after))

    def test_the_open_sea_keeps_every_wave_it_started_with(self):
        rows = open_water_rows(self.source.load(), self.width, self.height)
        beyond_the_trees = range(CYPRESS[2], self.width)
        for frame in range(FRAMES):
            moved = painted(self.frames[frame])
            for y in rows:
                before = sorted(self.still[(x, y)] for x in beyond_the_trees if self.still[(x, y)] in SEA + GLINT)
                after = sorted(moved[(x, y)] for x in beyond_the_trees if moved[(x, y)] in SEA + GLINT)
                self.assertEqual(before, after, (file_name(frame), y))

    def test_the_glow_breathes_out_and_back(self):
        lit = [sum(1 for pixel in rgba_pixels(image) if pixel == GLOW) for image in self.frames]
        widest = max(range(FRAMES), key=lambda frame: lit[frame])
        narrowest = min(range(FRAMES), key=lambda frame: lit[frame])
        self.assertEqual(max(BREATH), BREATH[widest])
        self.assertEqual(min(BREATH), BREATH[narrowest])
        self.assertGreater(lit[widest], lit[STILL_FRAME])
        self.assertLess(lit[narrowest], lit[STILL_FRAME])


if __name__ == "__main__":
    unittest.main()
