import os

from PIL import Image

OPAQUE = 255
TRANSPARENT = 0
CLEAR = (0, 0, 0, 0)
PNG_EXTENSION = ".png"


def load_rgba(path):
    with Image.open(path) as source:
        return source.convert("RGBA")


def rgba_pixels(image):
    data = iter(image.convert("RGBA").tobytes())
    return list(zip(data, data, data, data))


def from_pixels(size, pixels):
    return Image.frombytes("RGBA", size, bytes(channel for pixel in pixels for channel in pixel))


def parse_size(text):
    width, separator, height = text.lower().partition("x")
    if not separator or not width.isdigit() or not height.isdigit() or int(width) == 0 or int(height) == 0:
        raise ValueError(f"size must look like 32x16, got '{text}'")
    return int(width), int(height)


def parse_color(text):
    digits = text.lstrip("#")
    if len(digits) != 6 or any(character not in "0123456789abcdefABCDEF" for character in digits):
        raise ValueError(f"colour must look like F800F8, got '{text}'")
    return tuple(int(digits[index:index + 2], 16) for index in (0, 2, 4)) + (OPAQUE,)


def is_png(path):
    return path.lower().endswith(PNG_EXTENSION)


def png_files(paths):
    found = []
    for path in paths:
        if os.path.isdir(path):
            for folder, _, names in os.walk(path):
                for name in names:
                    if is_png(name):
                        full = os.path.join(folder, name)
                        found.append((full, os.path.relpath(full, path)))
        elif os.path.isfile(path) and is_png(path):
            found.append((path, os.path.basename(path)))
        else:
            raise FileNotFoundError(f"{path} is not a PNG file or a folder")
    return sorted(found, key=lambda entry: entry[0].lower())


def display_path(path):
    try:
        relative = os.path.relpath(path)
    except ValueError:
        relative = path
    shown = path if relative.startswith("..") else relative
    return shown.replace(os.sep, "/")


def stem(path):
    return os.path.splitext(os.path.basename(path))[0].lower()
