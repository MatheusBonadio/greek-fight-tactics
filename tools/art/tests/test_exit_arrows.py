import os
import unittest

from exit_arrows import DEFAULT_OUT, HEADINGS, PULSE, draw, file_name
from images import TRANSPARENT, load_rgba, rgba_pixels
from palette import load_gpl
from sprite_rules import inspect
from tile_preview import CELL_WIDTH

SCREEN_DIRECTIONS = {"ne": (1, -1), "sw": (-1, 1), "nw": (-1, -1), "se": (1, 1)}
FARTHEST_FRAME = 2


def centroid(image):
    pixels = rgba_pixels(image)
    opaque = [(index % CELL_WIDTH, index // CELL_WIDTH) for index, pixel in enumerate(pixels) if pixel[3] != TRANSPARENT]
    return sum(x for x, _ in opaque) / len(opaque), sum(y for _, y in opaque) / len(opaque)


class ExitArrowTests(unittest.TestCase):
    def setUp(self):
        self.palette = load_gpl()

    def test_every_frame_passes_the_art_rules(self):
        for heading in HEADINGS:
            for frame in range(len(PULSE)):
                name = file_name(heading, frame)
                report = inspect(name, draw(heading, frame, self.palette), self.palette)
                self.assertFalse(report.issues, (name, report.issues))

    def test_the_committed_frames_are_the_drawn_arrows(self):
        for heading in HEADINGS:
            for frame in range(len(PULSE)):
                name = file_name(heading, frame)
                committed = load_rgba(os.path.join(DEFAULT_OUT, name))
                self.assertEqual(rgba_pixels(draw(heading, frame, self.palette)), rgba_pixels(committed), name)

    def test_the_pulse_pushes_each_arrow_toward_its_edge_and_back(self):
        for heading, (right, down) in SCREEN_DIRECTIONS.items():
            start_x, start_y = centroid(draw(heading, 0, self.palette))
            end_x, end_y = centroid(draw(heading, FARTHEST_FRAME, self.palette))
            self.assertGreater((end_x - start_x) * right, 0, heading)
            self.assertGreater((end_y - start_y) * down, 0, heading)
            self.assertEqual(PULSE[1], PULSE[-1], "the pulse comes back the way it went")

    def test_the_arrow_points_at_its_edge(self):
        for heading, (right, down) in SCREEN_DIRECTIONS.items():
            image = draw(heading, 0, self.palette)
            pixels = rgba_pixels(image)
            opaque = [(index % CELL_WIDTH, index // CELL_WIDTH) for index, pixel in enumerate(pixels) if pixel[3] != TRANSPARENT]
            center_x, center_y = centroid(image)
            tip = max(opaque, key=lambda point: (point[0] - center_x) * right * 2 + (point[1] - center_y) * down)
            self.assertGreater((tip[0] - center_x) * right, 0, heading)


if __name__ == "__main__":
    unittest.main()
