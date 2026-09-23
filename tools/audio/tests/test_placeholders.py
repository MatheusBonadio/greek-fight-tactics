import os
import unittest
import wave

from placeholders import (BIT_DEPTH, CHANNELS, DEFAULT_OUT, EFFECTS, MELODIES, PEAK, SAMPLE_RATE, clip_names, encode,
                          render)

SHORTEST_MUSIC_SECONDS = 4.0
LONGEST_EFFECT_SECONDS = 1.0


def committed(name):
    with wave.open(os.path.join(DEFAULT_OUT, f"{name}.wav"), "rb") as handle:
        return handle.getparams(), handle.readframes(handle.getnframes())


def seconds(frames):
    return len(frames) / (BIT_DEPTH // 8) / SAMPLE_RATE


class PlaceholderAudioTests(unittest.TestCase):
    def test_every_clip_the_game_asks_for_is_drawn(self):
        self.assertEqual(len(set(clip_names())), len(clip_names()))
        for name in clip_names():
            self.assertTrue(os.path.exists(os.path.join(DEFAULT_OUT, f"{name}.wav")), name)

    def test_the_committed_sounds_are_what_the_script_draws(self):
        for name in clip_names():
            _, frames = committed(name)
            self.assertEqual(encode(render(name)), frames, name)

    def test_every_clip_is_mono_sixteen_bit_at_the_project_rate(self):
        for name in clip_names():
            params, _ = committed(name)
            self.assertEqual(CHANNELS, params.nchannels, name)
            self.assertEqual(BIT_DEPTH // 8, params.sampwidth, name)
            self.assertEqual(SAMPLE_RATE, params.framerate, name)

    def test_no_clip_is_silent_and_none_of_them_clips(self):
        for name in clip_names():
            samples = render(name)
            self.assertTrue(any(abs(value) > 0.01 for value in samples), name)
            self.assertLessEqual(max(abs(value) for value in samples), 1.0, name)
            self.assertEqual(PEAK, 2 ** (BIT_DEPTH - 1) - 1)

    def test_music_loops_and_effects_do_not_overlap_in_length(self):
        for name in MELODIES:
            _, frames = committed(name)
            self.assertGreaterEqual(seconds(frames), SHORTEST_MUSIC_SECONDS, name)
        for name in EFFECTS:
            _, frames = committed(name)
            self.assertLessEqual(seconds(frames), LONGEST_EFFECT_SECONDS, name)

    def test_a_music_loop_ends_near_silence_so_it_can_repeat(self):
        for name in MELODIES:
            samples = render(name)
            self.assertLess(abs(samples[0]), 0.05, name)
            self.assertLess(abs(samples[-1]), 0.05, name)

    def test_drawing_twice_gives_the_same_bytes(self):
        for name in clip_names():
            self.assertEqual(encode(render(name)), encode(render(name)), name)


if __name__ == "__main__":
    unittest.main()
