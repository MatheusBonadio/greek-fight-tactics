import argparse
import math
import os
import struct
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_OUT = os.path.join(ROOT, "audio", "placeholders")
SAMPLE_RATE = 11025
BIT_DEPTH = 16
PEAK = 2 ** (BIT_DEPTH - 1) - 1
CHANNELS = 1
BEAT_SECONDS = 0.25
BARS = 8
MUSIC_LEVEL = 0.22
SFX_LEVEL = 0.55
BLIP_LEVEL = 0.4
HOVER_LEVEL = 0.22

SEMITONE = 2 ** (1 / 12)
BASE_NOTE = 220.0
PENTATONIC = (0, 3, 5, 7, 10)


def note(degree, octave=0):
    step = PENTATONIC[degree % len(PENTATONIC)] + 12 * (octave + degree // len(PENTATONIC))
    return BASE_NOTE * SEMITONE ** step


def square(phase, duty=0.5):
    return 1.0 if phase % 1.0 < duty else -1.0


def triangle(phase):
    ramp = (phase % 1.0) * 4.0
    return ramp - 1.0 if ramp < 2.0 else 3.0 - ramp


def envelope(position, attack, release):
    if position < attack:
        return position / attack
    if position > 1.0 - release:
        return (1.0 - position) / release
    return 1.0


def tone(frequency, seconds, level, shape=square, duty=0.5, sweep=1.0, attack=0.02, release=0.35):
    count = max(1, int(seconds * SAMPLE_RATE))
    phase = 0.0
    samples = []
    for index in range(count):
        position = index / count
        step = frequency * (sweep ** position) / SAMPLE_RATE
        phase += step
        value = shape(phase, duty) if shape is square else shape(phase)
        samples.append(value * level * envelope(position, attack, release))
    return samples


def noise(seconds, level, sweep=1.0, attack=0.01, release=0.6, seed=1):
    count = max(1, int(seconds * SAMPLE_RATE))
    state = seed * 2654435761 % 2 ** 32
    samples = []
    held = 0.0
    for index in range(count):
        position = index / count
        state = (state * 1664525 + 1013904223) % 2 ** 32
        width = max(1, int(4 * (sweep ** position)))
        if index % width == 0:
            held = state / 2 ** 31 - 1.0
        samples.append(held * level * envelope(position, attack, release))
    return samples


def mix(*layers):
    length = max(len(layer) for layer in layers)
    out = [0.0] * length
    for layer in layers:
        for index, value in enumerate(layer):
            out[index] += value
    return out


def chain(*parts):
    out = []
    for part in parts:
        out.extend(part)
    return out


def silence(seconds):
    return [0.0] * max(1, int(seconds * SAMPLE_RATE))


def loop(melody, bass, duty):
    lead = []
    for degree, length in melody:
        lead.extend(silence(length * BEAT_SECONDS) if degree is None
                    else tone(note(degree, 1), length * BEAT_SECONDS, MUSIC_LEVEL, duty=duty, release=0.25))
    low = []
    for degree, length in bass:
        low.extend(silence(length * BEAT_SECONDS) if degree is None
                   else tone(note(degree, -1), length * BEAT_SECONDS, MUSIC_LEVEL * 0.8, shape=triangle, release=0.2))
    bars = max(1, math.ceil(BARS * 4 * BEAT_SECONDS * SAMPLE_RATE / max(len(lead), 1)))
    return mix(lead * bars, low * bars)


MELODIES = {
    "music_title": ([(0, 4), (2, 2), (1, 2), (3, 4), (2, 4)], [(0, 8), (3, 8)], 0.5),
    "music_kerameikos": ([(0, 2), (1, 2), (2, 4), (1, 2), (0, 6)], [(0, 8), (2, 8)], 0.5),
    "music_agora": ([(2, 2), (3, 2), (4, 2), (3, 2), (2, 4), (0, 4)], [(0, 4), (2, 4), (3, 4), (2, 4)], 0.25),
    "music_archive": ([(0, 6), (None, 2), (1, 6), (None, 2)], [(0, 16)], 0.5),
    "music_academy": ([(4, 4), (3, 2), (2, 2), (1, 4), (0, 4)], [(4, 8), (0, 8)], 0.25),
    "music_battle_sophist": ([(2, 1), (2, 1), (3, 2), (1, 1), (1, 1), (2, 2), (4, 4)], [(2, 2), (2, 2), (1, 2), (1, 2)], 0.125),
    "music_battle_philosopher": ([(0, 4), (2, 4), (1, 4), (3, 4)], [(0, 8), (1, 8)], 0.5),
    "music_street_trial_personal_attack": ([(1, 2), (1, 2), (0, 4), (1, 2), (2, 2), (0, 4)], [(1, 4), (0, 4)], 0.25),
    "music_street_trial_false_dilemma": ([(3, 2), (1, 2), (3, 2), (1, 2), (4, 4), (2, 4)], [(3, 4), (1, 4)], 0.25),
    "music_street_trial_political_fear": ([(0, 8), (1, 4), (0, 4)], [(0, 8), (4, 8)], 0.5),
}


def sfx(name):
    if name == "sfx_hover":
        return tone(note(4, 2), 0.028, HOVER_LEVEL, duty=0.125, attack=0.01, release=0.7)
    if name == "sfx_cursor":
        return tone(note(3, 2), 0.05, SFX_LEVEL, duty=0.25, release=0.5)
    if name == "sfx_confirm":
        return chain(tone(note(4, 1), 0.02, SFX_LEVEL, duty=0.5, attack=0.01, release=0.1),
                     tone(note(4, 2), 0.08, SFX_LEVEL, duty=0.5))
    if name == "sfx_cancel":
        return tone(note(2, 1), 0.11, SFX_LEVEL, duty=0.5, sweep=0.5)
    if name == "sfx_refute":
        return mix(tone(note(4, 1), 0.18, SFX_LEVEL, duty=0.125, sweep=0.35),
                   noise(0.18, SFX_LEVEL * 0.5, sweep=3.0, seed=7))
    if name == "sfx_maieutics":
        return chain(tone(note(0, 1), 0.07, SFX_LEVEL * 0.8, shape=triangle),
                     tone(note(2, 1), 0.07, SFX_LEVEL * 0.8, shape=triangle),
                     tone(note(4, 2), 0.16, SFX_LEVEL * 0.8, shape=triangle, release=0.5))
    if name == "sfx_damage":
        return mix(tone(note(1, 0), 0.22, SFX_LEVEL, duty=0.5, sweep=0.4),
                   noise(0.22, SFX_LEVEL * 0.6, sweep=0.4, seed=13))
    if name == "sfx_counter":
        return mix(tone(note(3, 0), 0.16, SFX_LEVEL, duty=0.125, sweep=2.4),
                   noise(0.16, SFX_LEVEL * 0.4, sweep=2.0, seed=23))
    if name == "sfx_victory":
        return chain(tone(note(0, 1), 0.1, SFX_LEVEL, duty=0.5),
                     tone(note(2, 1), 0.1, SFX_LEVEL, duty=0.5),
                     tone(note(4, 1), 0.1, SFX_LEVEL, duty=0.5),
                     tone(note(0, 2), 0.32, SFX_LEVEL, duty=0.5, release=0.6))
    if name == "sfx_defeat":
        return chain(tone(note(2, 0), 0.16, SFX_LEVEL, shape=triangle),
                     tone(note(1, 0), 0.16, SFX_LEVEL, shape=triangle),
                     tone(note(0, -1), 0.4, SFX_LEVEL, shape=triangle, sweep=0.7, release=0.7))
    if name == "voice_blip":
        return tone(note(2, 1), 0.04, BLIP_LEVEL, duty=0.25, attack=0.05, release=0.5)
    raise ValueError(f"unknown effect {name}")


EFFECTS = ("sfx_hover", "sfx_cursor", "sfx_confirm", "sfx_cancel", "sfx_refute", "sfx_maieutics",
           "sfx_damage", "sfx_counter", "sfx_victory", "sfx_defeat", "voice_blip")


def render(name):
    if name in MELODIES:
        melody, bass, duty = MELODIES[name]
        return loop(melody, bass, duty)
    return sfx(name)


def clip_names():
    return tuple(sorted(MELODIES)) + EFFECTS


def encode(samples):
    return b"".join(struct.pack("<h", max(-PEAK, min(PEAK, int(round(value * PEAK))))) for value in samples)


def write(path, samples):
    with wave.open(path, "wb") as handle:
        handle.setnchannels(CHANNELS)
        handle.setsampwidth(BIT_DEPTH // 8)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(encode(samples))


def read(path):
    with wave.open(path, "rb") as handle:
        return handle.readframes(handle.getnframes())


def main(argv=None):
    parser = argparse.ArgumentParser(description="Draw the placeholder sounds the game plays until real audio lands")
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--check", action="store_true", help="fail when a committed wav is not what this draws")
    arguments = parser.parse_args(argv)

    os.makedirs(arguments.out, exist_ok=True)
    stale = 0
    for name in clip_names():
        path = os.path.join(arguments.out, f"{name}.wav")
        frames = encode(render(name))
        differs = not os.path.exists(path) or read(path) != frames
        if differs and not arguments.check:
            write(path, render(name))
        stale += differs
        state = "up to date" if not differs else "stale" if arguments.check else "written"
        print(f"  {name:38} {len(frames) / 2 / SAMPLE_RATE:5.2f}s  {state}")
    if arguments.check and stale:
        print(f"{stale} placeholder sounds are not what placeholders.py draws")
    return 1 if arguments.check and stale else 0


if __name__ == "__main__":
    raise SystemExit(main())
