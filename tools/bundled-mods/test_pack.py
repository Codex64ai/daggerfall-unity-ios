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


class BuiltIn(unittest.TestCase):
    """Entries with builtin: true are data bundles shipped inside the app, never in the pack zip."""
    CFG2 = {"dest_root": "x", "mods": CFG["mods"] + [
        {"name": "RoleplayRealism", "manifest": "RoleplayRealism.dfmod.json", "builtin": True, "strip_code": True}]}

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.dir, "Licenses"))
        for stem in ("jobsofthethievesguild", "skyrim's adventures"):
            open(os.path.join(self.dir, stem + ".dfmod"), "w").close()
            open(os.path.join(self.dir, "Licenses", stem + "-LICENSE.txt"), "w").close()

    def test_builtin_is_not_a_pack_member(self):
        self.assertNotIn("roleplayrealism", pack.stems(self.CFG2))
        self.assertEqual(pack.check_bundles(self.CFG2, self.dir), [])          # absent built-in bundle: fine

    def test_builtin_bundle_present_is_not_stale(self):
        open(os.path.join(self.dir, "roleplayrealism.dfmod"), "w").close()
        self.assertEqual(pack.check_bundles(self.CFG2, self.dir), [])          # present: fine, not "unpinned"
        self.assertNotIn("roleplayrealism.dfmod", pack.readme_text(self.CFG2, {}))


class PrivateOnly(unittest.TestCase):
    """Entries with private_only: true are built as a bundle but never enter the pack zip."""
    CFG3 = {"dest_root": "x", "mods": CFG["mods"] + [
        {"name": "DynamicSkies", "manifest": "dynamic-skies.dfmod.json", "private_only": True, "strip_code": True}]}

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.dir, "Licenses"))
        for stem in ("jobsofthethievesguild", "skyrim's adventures"):
            open(os.path.join(self.dir, stem + ".dfmod"), "w").close()
            open(os.path.join(self.dir, "Licenses", stem + "-LICENSE.txt"), "w").close()

    def test_private_only_is_not_a_pack_member(self):
        self.assertNotIn("dynamic-skies", pack.stems(self.CFG3))
        self.assertEqual(pack.check_bundles(self.CFG3, self.dir), [])          # absent private-only bundle: fine

    def test_private_only_bundle_present_is_not_stale(self):
        open(os.path.join(self.dir, "dynamic-skies.dfmod"), "w").close()
        self.assertEqual(pack.check_bundles(self.CFG3, self.dir), [])          # present: fine, not "unpinned"
        self.assertNotIn("dynamic-skies.dfmod", pack.readme_text(self.CFG3, {}))


class PendingLicence(unittest.TestCase):
    """A pending: licence means no public redistribution right yet. private_only keeps the bundle
    out of the pack zip, but that is one boolean - so any entry that WOULD be packed with a
    pending licence is a hard problem, whatever else is set (I3 of the final review)."""
    WOD = {"name": "WorldOfDaggerfall", "manifest": "WorldOfDaggerfall.dfmod.json",
           "licence": "pending:no licence upstream; asked the author, draft only"}

    def cfg(self, **extra):
        return {"dest_root": "x", "mods": CFG["mods"] + [dict(self.WOD, **extra)]}

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.dir, "Licenses"))
        for stem in ("jobsofthethievesguild", "skyrim's adventures", "worldofdaggerfall"):
            open(os.path.join(self.dir, stem + ".dfmod"), "w").close()
            open(os.path.join(self.dir, "Licenses", stem + "-LICENSE.txt"), "w").close()

    def test_world_of_daggerfall_with_a_pending_licence_cannot_be_packed(self):
        probs = pack.check_bundles(self.cfg(), self.dir)
        self.assertTrue(any("pending licence" in p and "WorldOfDaggerfall" in p for p in probs), probs)

    def test_world_of_daggerfall_private_only_is_no_problem(self):
        self.assertNotIn("worldofdaggerfall", pack.stems(self.cfg(private_only=True)))
        self.assertEqual(pack.check_bundles(self.cfg(private_only=True), self.dir), [])


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
