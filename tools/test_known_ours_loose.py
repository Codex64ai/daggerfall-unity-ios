#!/usr/bin/env python3
"""python3 -m unittest tools/test_known_ours_loose.py"""
import os
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import known_ours_loose as k  # noqa: E402


class KnownOursLoose(unittest.TestCase):
    def zip_with(self, files):
        d = tempfile.mkdtemp()
        p = os.path.join(d, "x.zip")
        with zipfile.ZipFile(p, "w") as z:
            z.writestr("dream-music-ogg/", b"")
            for name, data in files.items():
                z.writestr(name, data)
        return p

    def test_entries_are_leaf_lower_size_key_without_readmes(self):
        p = self.zip_with({"dream-music-ogg/Song_D1.ogg": b"abc", "dream-movies-ios/README-FLC.txt": b"x",
                           "README-DREAM-extras.txt": b"y", "Presets/dream - backgrounds/DREAM 2026.json": b"12345"})
        self.assertEqual(k.entries(p), {("song_d1.ogg", 3, "DREAM"), ("dream 2026.json", 5, "DREAM")})

    def test_same_name_at_two_sizes_gives_two_rows(self):
        p = self.zip_with({"a/DREAM 2026.json": b"1", "b/DREAM 2026.json": b"22"})
        self.assertEqual(k.collect([p]), [("dream 2026.json", 1, "DREAM"), ("dream 2026.json", 2, "DREAM")])

    def test_render_is_csharp_rows(self):
        text = k.render([("song_d1.ogg", 3, "DREAM")])
        self.assertIn('new LooseFile("song_d1.ogg", 3L, "DREAM"),', text)
        self.assertIn("public static readonly LooseFile[] KnownOursLoose", text)

    def test_generated_file_is_fresh_when_the_zips_are_present(self):
        if not all(os.path.isfile(z) for z in k.DEFAULT_ZIPS):
            self.skipTest("DREAM loose-file zips not on this machine")
        self.assertEqual(k.main(["--check"]), 0)


if __name__ == "__main__":
    unittest.main()
