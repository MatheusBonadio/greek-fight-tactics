import contextlib
import io
import os
import struct
import tempfile
import unittest

from aseprite_palette import (
    DEFAULT_ASE,
    DEFAULT_GPL,
    OLD_PALETTE_CHUNK,
    PALETTE_CHUNK,
    PALETTES_FOLDER,
    USER_FOLDER_VARIABLE,
    main,
    parse_ase_palette,
    user_folder_for,
)
from images import OPAQUE
from palette import DEFAULT_PALETTE, load_gpl

HEADER = struct.Struct("<IHHHHHIHIIB3xHBBhhHH84x")
FRAME = struct.Struct("<IHHH2xI")
LAYER_CHUNK = 0x2004
COLOR_PROFILE_CHUNK = 0x2007
CEL_CHUNK = 0x2005
INDEXED_DEPTH = 8
HOME = os.path.join("home", "kleia")
DB32_START = [(0, 0, 0), (34, 32, 52), (69, 40, 60), (102, 57, 49)]


def chunk(kind, body):
    return struct.pack("<IH", 6 + len(body), kind) + body


def old_palette_chunk(colors):
    body = struct.pack("<HBB", 1, 0, len(colors) % 256)
    return chunk(OLD_PALETTE_CHUNK, body + b"".join(struct.pack("<BBB", *color) for color in colors))


def palette_chunk(colors, names=None):
    body = struct.pack("<III8x", len(colors), 0, len(colors) - 1)
    for index, color in enumerate(colors):
        name = (names or {}).get(index)
        body += struct.pack("<HBBBB", 1 if name else 0, *color, OPAQUE)
        if name:
            encoded = name.encode("utf-8")
            body += struct.pack("<H", len(encoded)) + encoded
    return chunk(PALETTE_CHUNK, body)


def ase_file(chunks, colors):
    frame_body = chunk(COLOR_PROFILE_CHUNK, bytes(8)) + b"".join(chunks) + chunk(LAYER_CHUNK, bytes(18)) + chunk(CEL_CHUNK, bytes(20))
    count = len(chunks) + 3
    frame = FRAME.pack(FRAME.size + len(frame_body), 0xF1FA, count, 100, count) + frame_body
    header = HEADER.pack(HEADER.size + len(frame), 0xA5E0, 1, colors, 1, INDEXED_DEPTH, 1, 100, 0, 0, 0, colors, 1, 1, 0, 0, 16, 16)
    return header + frame


def project_colors():
    return [color for _, color in load_gpl().entries]


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    mode = "wb" if isinstance(data, bytes) else "w"
    with open(path, mode) as handle:
        handle.write(data)


def run(arguments):
    output = io.StringIO()
    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
        code = main(arguments)
    return code, output.getvalue()


class UserFolderTests(unittest.TestCase):
    def test_the_aseprite_variable_wins_everywhere(self):
        for platform in ("win32", "darwin", "linux"):
            self.assertEqual(user_folder_for({USER_FOLDER_VARIABLE: "custom", "APPDATA": "roaming"}, platform, HOME), "custom")

    def test_windows_uses_the_roaming_app_data(self):
        self.assertEqual(user_folder_for({"APPDATA": "roaming"}, "win32", HOME), os.path.join("roaming", "Aseprite"))

    def test_macos_uses_application_support(self):
        self.assertEqual(user_folder_for({}, "darwin", HOME), os.path.join(HOME, "Library", "Application Support", "Aseprite"))

    def test_linux_uses_the_config_home(self):
        self.assertEqual(user_folder_for({"XDG_CONFIG_HOME": "config"}, "linux", HOME), os.path.join("config", "aseprite"))
        self.assertEqual(user_folder_for({}, "linux", HOME), os.path.join(HOME, ".config", "aseprite"))


class ParseAsePaletteTests(unittest.TestCase):
    def test_reads_the_old_colour_chunk_that_aseprite_writes_for_opaque_palettes(self):
        colors = project_colors()
        entries = parse_ase_palette(ase_file([old_palette_chunk(colors)], len(colors)))
        self.assertEqual(entries, [color + (OPAQUE,) for color in colors])

    def test_reads_the_palette_chunk_and_skips_entry_names(self):
        colors = project_colors()
        entries = parse_ase_palette(ase_file([palette_chunk(colors, {0: "outline", 5: "marble_dark"})], len(colors)))
        self.assertEqual(entries, [color + (OPAQUE,) for color in colors])

    def test_the_palette_chunk_wins_over_the_old_colour_chunk(self):
        colors = project_colors()
        data = ase_file([palette_chunk(colors), old_palette_chunk(DB32_START)], len(colors))
        self.assertEqual(parse_ase_palette(data)[:len(DB32_START)], [color + (OPAQUE,) for color in colors[:len(DB32_START)]])

    def test_the_header_sets_the_palette_size(self):
        self.assertEqual(len(parse_ase_palette(ase_file([old_palette_chunk(DB32_START)], 32))), 32)

    def test_rejects_other_files(self):
        with self.assertRaises(ValueError):
            parse_ase_palette(b"GIMP Palette\n" + bytes(200))

    def test_a_truncated_file_fails_to_parse(self):
        data = ase_file([old_palette_chunk(project_colors())], 33)
        with self.assertRaises(struct.error):
            parse_ase_palette(data[:170])


class InstallTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.user_folder = self.temporary.name
        self.palettes = os.path.join(self.user_folder, PALETTES_FOLDER)
        self.ase = os.path.join(self.palettes, DEFAULT_ASE)
        self.gpl = os.path.join(self.palettes, DEFAULT_GPL)

    def tearDown(self):
        self.temporary.cleanup()

    def run_tool(self, *arguments):
        return run(["--user-folder", self.user_folder, *arguments])

    def migrate_like_aseprite(self):
        colors = project_colors()
        write(self.ase, ase_file([old_palette_chunk(colors)], len(colors)))

    def read(self, path):
        with open(path, "rb") as handle:
            return handle.read()

    def test_a_fresh_install_copies_the_palette_for_aseprite_to_migrate(self):
        code, output = self.run_tool()
        self.assertEqual(code, 0, output)
        self.assertEqual(self.read(self.gpl), self.read(DEFAULT_PALETTE))
        self.assertIn("start it again", output)

    def test_the_check_waits_until_aseprite_has_saved_the_palette(self):
        self.run_tool()
        code, output = self.run_tool("--check")
        self.assertEqual(code, 1, output)
        self.assertIn(f"pending: {DEFAULT_GPL} is the palette, and Aseprite saves it as {DEFAULT_ASE}", output)

    def test_installing_while_pending_copies_nothing_again(self):
        self.run_tool()
        code, output = self.run_tool()
        self.assertEqual(code, 0, output)
        self.assertNotIn("copied", output)
        self.assertEqual(os.listdir(self.palettes), [DEFAULT_GPL])

    def test_the_default_aseprite_saved_before_is_set_aside(self):
        write(self.ase, ase_file([old_palette_chunk(DB32_START)], 32))
        code, output = self.run_tool()
        self.assertEqual(code, 0, output)
        self.assertFalse(os.path.exists(self.ase))
        self.assertEqual(len(parse_ase_palette(self.read(self.ase + ".bak"))), 32)
        self.assertIn(f"moved {DEFAULT_ASE} to {DEFAULT_ASE}.bak", output)

    def test_an_older_backup_is_kept(self):
        write(self.ase + ".bak", b"first")
        write(self.ase, ase_file([old_palette_chunk(DB32_START)], 32))
        self.run_tool()
        self.assertEqual(self.read(self.ase + ".bak"), b"first")
        self.assertTrue(os.path.isfile(self.ase + ".bak2"))

    def test_another_default_gpl_is_set_aside(self):
        write(self.gpl, "GIMP Palette\n  0   0   0\tblack\n")
        self.run_tool()
        self.assertEqual(self.read(self.gpl), self.read(DEFAULT_PALETTE))
        self.assertTrue(os.path.isfile(self.gpl + ".bak"))

    def test_the_check_passes_once_aseprite_saved_the_palette(self):
        self.run_tool()
        self.migrate_like_aseprite()
        code, output = self.run_tool("--check")
        self.assertEqual(code, 0, output)
        self.assertIn(f"ok: {DEFAULT_ASE} holds the 33 entries", output)

    def test_installing_again_changes_nothing(self):
        self.run_tool()
        self.migrate_like_aseprite()
        code, output = self.run_tool()
        self.assertEqual(code, 0, output)
        self.assertNotIn("moved", output)
        self.assertEqual(sorted(os.listdir(self.palettes)), [DEFAULT_ASE, DEFAULT_GPL])

    def test_the_check_fails_on_another_default(self):
        write(self.ase, ase_file([old_palette_chunk(DB32_START)], 32))
        code, output = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("entry 0 is #000000, outline is #080810", output)
        self.assertTrue(os.path.isfile(self.ase))

    def test_the_check_fails_before_anything_is_installed(self):
        code, output = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("does not exist", output)

    def test_a_broken_default_fails_the_check_and_is_replaced_on_install(self):
        write(self.ase, b"not an aseprite file")
        code, _ = self.run_tool("--check")
        self.assertEqual(code, 2)
        code, output = self.run_tool()
        self.assertEqual(code, 0, output)
        self.assertTrue(os.path.isfile(self.ase + ".bak"))


if __name__ == "__main__":
    unittest.main()
