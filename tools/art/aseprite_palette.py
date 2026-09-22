import argparse
import filecmp
import os
import shutil
import struct
import sys

from images import OPAQUE, display_path
from palette import DEFAULT_PALETTE, load_gpl, to_hex

USER_FOLDER_VARIABLE = "ASEPRITE_USER_FOLDER"
APP_FOLDER = "Aseprite"
LINUX_APP_FOLDER = "aseprite"
PALETTES_FOLDER = "palettes"
DEFAULT_ASE = "default.ase"
DEFAULT_GPL = "default.gpl"
BACKUP_SUFFIX = ".bak"

ASE_HEADER_SIZE = 128
ASE_MAGIC = 0xA5E0
FRAME_MAGIC = 0xF1FA
WORD = struct.Struct("<H")
ASE_MAGIC_OFFSET = 4
ASE_FRAMES_OFFSET = 6
ASE_COLORS_OFFSET = 32
FRAME_HEADER = struct.Struct("<IHHH2xI")
CHUNK_HEADER = struct.Struct("<IH")
OLD_PALETTE_CHUNK = 0x0004
PALETTE_CHUNK = 0x2019
PALETTE_CHUNK_HEADER = struct.Struct("<III8x")
PALETTE_ENTRY = struct.Struct("<HBBBB")
PACKET_HEADER = struct.Struct("<BB")
RGB = struct.Struct("<BBB")
ENTRY_HAS_NAME = 1
FULL_PALETTE = 256
BLACK = (0, 0, 0, OPAQUE)

INSTALLED = "ok"
PENDING = "pending"
NOT_INSTALLED = "FAIL"


def user_folder_for(environment, platform, home):
    override = environment.get(USER_FOLDER_VARIABLE)
    if override:
        return override
    if platform.startswith("win"):
        return os.path.join(environment.get("APPDATA", os.path.join(home, "AppData", "Roaming")), APP_FOLDER)
    if platform == "darwin":
        return os.path.join(home, "Library", "Application Support", APP_FOLDER)
    return os.path.join(environment.get("XDG_CONFIG_HOME") or os.path.join(home, ".config"), LINUX_APP_FOLDER)


def default_user_folder():
    return user_folder_for(os.environ, sys.platform, os.path.expanduser("~"))


def read_ase_palette(path):
    with open(path, "rb") as handle:
        data = handle.read()
    try:
        return parse_ase_palette(data)
    except struct.error:
        raise ValueError(f"{path} is truncated") from None
    except ValueError as error:
        raise ValueError(f"{path}: {error}") from None


def parse_ase_palette(data):
    if len(data) < ASE_HEADER_SIZE or WORD.unpack_from(data, ASE_MAGIC_OFFSET)[0] != ASE_MAGIC:
        raise ValueError("not an Aseprite file")
    if WORD.unpack_from(data, ASE_FRAMES_OFFSET)[0] == 0:
        raise ValueError("the file has no frames")

    entries = [BLACK] * (WORD.unpack_from(data, ASE_COLORS_OFFSET)[0] or FULL_PALETTE)
    _, magic, old_chunks, _, new_chunks = FRAME_HEADER.unpack_from(data, ASE_HEADER_SIZE)
    if magic != FRAME_MAGIC:
        raise ValueError("the first frame has no frame header")

    position = ASE_HEADER_SIZE + FRAME_HEADER.size
    found_new_chunk = False
    for _ in range(new_chunks or old_chunks):
        size, kind = CHUNK_HEADER.unpack_from(data, position)
        if size < CHUNK_HEADER.size:
            raise ValueError(f"chunk of {size} bytes at offset {position}")
        body = position + CHUNK_HEADER.size
        if kind == PALETTE_CHUNK:
            entries = read_palette_chunk(data, body, entries)
            found_new_chunk = True
        elif kind == OLD_PALETTE_CHUNK and not found_new_chunk:
            entries = read_old_palette_chunk(data, body, entries)
        position += size
    return entries


def with_entry(entries, index, color):
    grown = entries + [BLACK] * (index + 1 - len(entries))
    grown[index] = color
    return grown


def read_palette_chunk(data, position, entries):
    size, first, last = PALETTE_CHUNK_HEADER.unpack_from(data, position)
    if size:
        entries = entries[:size] + [BLACK] * (size - len(entries))
    position += PALETTE_CHUNK_HEADER.size
    for index in range(first, last + 1):
        flags, red, green, blue, alpha = PALETTE_ENTRY.unpack_from(data, position)
        position += PALETTE_ENTRY.size
        entries = with_entry(entries, index, (red, green, blue, alpha))
        if flags & ENTRY_HAS_NAME:
            position += WORD.size + WORD.unpack_from(data, position)[0]
    return entries


def read_old_palette_chunk(data, position, entries):
    packets = WORD.unpack_from(data, position)[0]
    position += WORD.size
    skip = 0
    for _ in range(packets):
        skipped, count = PACKET_HEADER.unpack_from(data, position)
        position += PACKET_HEADER.size
        skip += skipped
        for index in range(skip, skip + (count or FULL_PALETTE)):
            entries = with_entry(entries, index, RGB.unpack_from(data, position) + (OPAQUE,))
            position += RGB.size
    return entries


def first_difference(found, palette):
    expected = [color + (OPAQUE,) for _, color in palette.entries]
    for index, (found_color, expected_color) in enumerate(zip(found, expected)):
        if found_color != expected_color:
            return f"entry {index} is {to_hex(found_color)}, {palette.entries[index][0]} is {to_hex(expected_color)}"
    if len(found) != len(expected):
        return f"{len(found)} entries instead of {len(expected)}"
    return None


def default_palette_state(folder, source, palette):
    ase = os.path.join(folder, DEFAULT_ASE)
    gpl = os.path.join(folder, DEFAULT_GPL)
    if os.path.isfile(ase):
        difference = first_difference(read_ase_palette(ase), palette)
        if difference:
            return NOT_INSTALLED, f"{DEFAULT_ASE} is another palette: {difference}"
        return INSTALLED, f"{DEFAULT_ASE} holds the {len(palette.entries)} entries of the palette"
    if os.path.isfile(gpl):
        if filecmp.cmp(gpl, source, shallow=False):
            return PENDING, f"{DEFAULT_GPL} is the palette, and Aseprite saves it as {DEFAULT_ASE} on its next start"
        return NOT_INSTALLED, f"{DEFAULT_GPL} is another palette and there is no {DEFAULT_ASE}"
    if os.path.isdir(folder):
        return NOT_INSTALLED, f"neither {DEFAULT_ASE} nor {DEFAULT_GPL} exists"
    return NOT_INSTALLED, "the folder does not exist"


def backup_path(path):
    candidate = path + BACKUP_SUFFIX
    number = 2
    while os.path.exists(candidate):
        candidate = f"{path}{BACKUP_SUFFIX}{number}"
        number += 1
    return candidate


def set_aside(path):
    backup = backup_path(path)
    os.replace(path, backup)
    return f"moved {os.path.basename(path)} to {os.path.basename(backup)}"


def already_installed(folder, source, palette):
    try:
        return default_palette_state(folder, source, palette)[0] == INSTALLED
    except ValueError:
        return False


def install(folder, source, palette):
    if already_installed(folder, source, palette):
        return []

    os.makedirs(folder, exist_ok=True)
    ase = os.path.join(folder, DEFAULT_ASE)
    gpl = os.path.join(folder, DEFAULT_GPL)
    actions = []
    if os.path.isfile(ase):
        actions.append(set_aside(ase))
    if os.path.isfile(gpl) and not filecmp.cmp(gpl, source, shallow=False):
        actions.append(set_aside(gpl))
    if not os.path.isfile(gpl):
        shutil.copyfile(source, gpl)
        actions.append(f"copied {display_path(source)} to {DEFAULT_GPL}")
    return actions


def parse_arguments(argv):
    parser = argparse.ArgumentParser(
        prog="aseprite_palette.py",
        description=f"Make the palette Aseprite's default: copy it to <user folder>/{PALETTES_FOLDER}/{DEFAULT_GPL} and set an older {DEFAULT_ASE} aside, so that Aseprite loads it on its next start. --check only compares.",
    )
    parser.add_argument("--check", action="store_true", help=f"change nothing, exit 0 only when Aseprite has saved the palette as its {DEFAULT_ASE}")
    parser.add_argument("--palette", default=DEFAULT_PALETTE, help="GIMP palette (default: art/palette/snes.gpl)")
    parser.add_argument("--user-folder", help=f"Aseprite user folder (default: ${USER_FOLDER_VARIABLE}, then %%APPDATA%%/Aseprite, ~/Library/Application Support/Aseprite or ~/.config/aseprite; for a portable install, with aseprite.ini next to the executable, the install folder)")
    return parser.parse_args(argv)


def main(argv=None):
    arguments = parse_arguments(argv)
    folder = os.path.join(arguments.user_folder or default_user_folder(), PALETTES_FOLDER)
    try:
        palette = load_gpl(arguments.palette)
        print(f"Aseprite palettes folder: {display_path(folder)}")
        if arguments.check:
            state, message = default_palette_state(folder, arguments.palette, palette)
            print(f"{state}: {message}")
            return 0 if state == INSTALLED else 1
        actions = install(folder, arguments.palette, palette)
        state, message = default_palette_state(folder, arguments.palette, palette)
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        return 2

    for action in actions:
        print(action)
    print(f"{state}: {message}")
    if state == PENDING:
        print("Close Aseprite if it is open and start it again, then run with --check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
