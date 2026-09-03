#!/usr/bin/env python3
"""python3 -m unittest tools/bundled-mods/test_pack.py"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import pack  # noqa: E402

CFG = {"dest_root": "Assets/Game/Mods", "mods": [
    {"name": "JOTG", "manifest": "JobsOfTheThievesGuild.dfmod.json"},
    {"name": "SkyrimsAdventures", "manifest": "Skyrim's Adventures.dfmod.json"},
]}


class CheckBundles(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.dir, "Licenses"))

    def add(self, stem, licence=True):
        open(os.path.join(self.dir, stem + ".dfmod"), "w").close()
        if licence:
            open(os.path.join(self.dir, "Licenses", stem + "-LICENSE.txt"), "w").close()

    def test_complete_pack_has_no_problems(self):
        self.add("jobsofthethievesguild")
        self.add("skyrim's adventures")
        self.assertEqual(pack.check_bundles(CFG, self.dir), [])

    def test_missing_bundle_is_reported(self):
        self.add("jobsofthethievesguild")
        probs = pack.check_bundles(CFG, self.dir)
        self.assertTrue(any("skyrim's adventures.dfmod" in p and "no bundle" in p for p in probs))

    def test_stale_unpinned_bundle_is_refused(self):
        self.add("jobsofthethievesguild")
        self.add("skyrim's adventures")
        self.add("detailedmainquestdungeons")
        probs = pack.check_bundles(CFG, self.dir)
        self.assertTrue(any("detailedmainquestdungeons" in p and "not in the pin list" in p for p in probs))

    def test_missing_licence_is_reported(self):
        self.add("jobsofthethievesguild", licence=False)
        self.add("skyrim's adventures")
        self.assertTrue(any("no licence" in p for p in pack.check_bundles(CFG, self.dir)))


class Readme(unittest.TestCase):
    def test_lists_every_mod_with_its_title(self):
        txt = pack.readme_text(CFG, {"jobsofthethievesguild": "Jobs of the Thieves Guild"})
        self.assertIn("2 mods by Cliffworms", txt)
        cfg2 = {"dest_root": "x", "mods": CFG["mods"] + [{"name": "JH", "manifest": "JH.dfmod.json", "generate": {"author": "Jay_H"}}]}
        self.assertIn("3 mods by Cliffworms and Jay_H", pack.readme_text(cfg2, {}))
        self.assertIn("jobsofthethievesguild.dfmod", txt)
        self.assertIn("Jobs of the Thieves Guild", txt)
        self.assertIn("skyrim's adventures.dfmod", txt)
        self.assertIn("THIRD-PARTY.md", txt)


if __name__ == "__main__":
    unittest.main()
