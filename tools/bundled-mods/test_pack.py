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


class RealGrassIsPrivateOnly(unittest.TestCase):
    """The SHIPPED entry, not a synthetic one. Real Grass's bundle now carries three of VMblast's
    textures, whose only permission is "authorized for this project only", so the thing that keeps
    it off the public pack has to be asserted against tools/bundled-mods/mods.json itself: a rename,
    a dropped private_only or a rewritten licence string would otherwise put project-scoped art into
    a public download with nothing to say so."""

    VMBLAST = ("Use of these textures is authorized for this project only "
               "[RealGrass for Daggerfall Unity].")

    def setUp(self):
        import json
        with open(os.path.join(os.path.dirname(__file__), "mods.json"), encoding="utf-8") as fh:
            self.cfg = json.load(fh)
        self.entry = next(m for m in self.cfg["mods"] if m["name"] == "RealGrass")

    def test_entry_is_private_only_with_a_pending_licence(self):
        self.assertTrue(self.entry.get("private_only"))
        self.assertTrue(str(self.entry.get("licence", "")).startswith("pending:"))

    def test_bundle_is_not_a_pack_member(self):
        self.assertNotIn("realgrass", pack.stems(self.cfg))
        self.assertTrue(pack.excluded_from_pack(self.entry))

    def test_dropping_private_only_is_a_hard_problem(self):
        public = {"dest_root": self.cfg["dest_root"],
                  "mods": [dict(self.entry, private_only=False)]}
        d = tempfile.mkdtemp()
        os.makedirs(os.path.join(d, "Licenses"))
        open(os.path.join(d, "realgrass.dfmod"), "w").close()
        open(os.path.join(d, "Licenses", "realgrass-LICENSE.txt"), "w").close()
        probs = pack.check_bundles(public, d)
        self.assertTrue(any("pending licence" in p and "RealGrass" in p for p in probs), probs)

    def test_licence_quotes_vmblast_verbatim_and_says_private_draft_only(self):
        lic = self.entry["licence"]
        self.assertIn(self.VMBLAST, lic)
        self.assertIn("private test draft", lic)
        self.assertIn("never in the public mod pack", lic)

    def test_shipped_files_are_the_five_the_port_loads(self):
        import json
        with open(os.path.join(os.path.dirname(__file__), self.entry["manifest_override"]),
                  encoding="utf-8") as fh:
            files = json.load(fh)["Files"]
        self.assertEqual([os.path.basename(f) for f in files],
                         ["Grass_tex.psd", "GrassDetails_01.psd", "GrassDetails_06.psd",
                          "BrownGrass_tex.png", "GreenGrass_tex.png"])

    def test_the_other_vmblast_textures_and_every_mesh_are_excluded(self):
        globs = self.entry["exclude_globs"]
        for g in ("*.fbx", "*.prefab", "*.mat", "*.cs", "Fireflies*", "Rock*.png",
                  "DesertGrass.psd", "GrassDetails_02.psd", "GrassDetails_03.psd",
                  "GrassDetails_04.psd", "GrassDetails_05.psd"):
            self.assertIn(g, globs)


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


class NonMitPack(unittest.TestCase):
    """--private: the Non-MIT zip ships every file as <stem>-ios.dfmod plus pack-index.json (the C#
    MobileOriginalMods.LoadPackIndex reads a top-level JSON array of {file, size, key})."""

    def test_ios_name(self):
        self.assertEqual(pack.ios_name("dream - hud & menu"), "dream-hud-menu-ios.dfmod")
        self.assertEqual(pack.ios_name("dream - mobs (2 of 6)"), "dream-mobs_2_of_6-ios.dfmod")
        self.assertEqual(pack.ios_name("DistantTerrain"), "distantterrain-ios.dfmod")
        self.assertEqual(pack.ios_name("World of Daggerfall - Biomes"), "world_of_daggerfall-biomes-ios.dfmod")  # the " of " rule, same as C# IosName
        self.assertEqual(pack.ios_name("dream-hud-menu-ios.dfmod"), "dream-hud-menu-ios.dfmod")
        self.assertEqual(pack.ios_name(pack.ios_name("dream - sky")), "dream-sky-ios.dfmod")

    def test_private_stems_need_an_unlock_key(self):
        cfg = {"dest_root": "x", "mods": CFG["mods"] + [
            {"name": "DEX", "manifest": "DaggerfallBestiaryProject.dfmod.json", "private_only": True, "unlock_key": "DEX"},
            {"name": "RR", "manifest": "RoleplayRealism.dfmod.json", "builtin": True, "private_only": True}]}
        self.assertEqual(pack.private_stems(cfg), {"daggerfallbestiaryproject": "DEX"})
        cfg["mods"].append({"name": "X", "manifest": "x.dfmod.json", "private_only": True})
        with self.assertRaises(ValueError):
            pack.private_stems(cfg)

    def test_dream_key(self):
        self.assertEqual(pack.dream_key("dream-sky"), "DREAMSKY")
        self.assertEqual(pack.dream_key("dream - sky"), "DREAMSKY")
        self.assertEqual(pack.dream_key("dream-mobs_1_of_6"), "DREAM")

    def test_pack_index_is_a_sorted_array(self):
        import json
        rows = json.loads(pack.pack_index([("Mods/b-ios.dfmod", 2, "B"), ("Mods/a-ios.dfmod", 1, "A")]))
        self.assertIsInstance(rows, list)
        self.assertEqual([r["file"] for r in rows], ["Mods/a-ios.dfmod", "Mods/b-ios.dfmod"])
        self.assertEqual(rows[0], {"file": "Mods/a-ios.dfmod", "size": 1, "key": "A"})

    def test_shipped_private_entries_all_have_keys(self):
        import json
        with open(os.path.join(os.path.dirname(__file__), "mods.json"), encoding="utf-8") as fh:
            cfg = json.load(fh)
        self.assertEqual(sorted(pack.private_stems(cfg).values()),
                         sorted(["DEX", "REALGRASS", "DYNAMICSKIES", "WOD", "WODBIOMES", "WODTERRAIN", "DISTANTTERRAIN"]))

    def test_end_to_end_private_zip(self):
        import json
        import zipfile
        root = tempfile.mkdtemp()
        mods, dream = os.path.join(root, "Mods"), os.path.join(root, "dream")
        os.makedirs(mods)
        os.makedirs(dream)
        for path, n in ((os.path.join(mods, "dex.dfmod"), 11), (os.path.join(dream, "dream - sky.dfmod"), 22),
                        (os.path.join(dream, "dream - mobs (1 of 2).dfmod"), 33)):
            with open(path, "wb") as fh:
                fh.write(b"x" * n)
        cfgp = os.path.join(root, "mods.json")
        with open(cfgp, "w", encoding="utf-8") as fh:
            json.dump({"dest_root": "x", "mods": CFG["mods"] + [
                {"name": "DEX", "manifest": "dex.dfmod.json", "private_only": True, "unlock_key": "DEX"}]}, fh)
        old = (pack.MODS_DIR, pack.MODS_JSON)
        pack.MODS_DIR, pack.MODS_JSON = mods, cfgp
        try:
            z = os.path.join(root, "out.zip")
            self.assertEqual(pack.main(["--private", "--dream-dir", dream, "--out", z]), 0)
            with zipfile.ZipFile(z) as zf:
                names = sorted(zf.namelist())
                index = json.loads(zf.read("pack-index.json"))
                readme = zf.read("README.txt").decode("utf-8")
        finally:
            pack.MODS_DIR, pack.MODS_JSON = old
        self.assertEqual(names, sorted(["Mods/dex-ios.dfmod", "Mods/dream-sky-ios.dfmod",
                                        "Mods/dream-mobs_1_of_2-ios.dfmod", "pack-index.json", "README.txt"]))
        self.assertEqual({r["file"]: (r["size"], r["key"]) for r in index},
                         {"Mods/dex-ios.dfmod": (11, "DEX"), "Mods/dream-sky-ios.dfmod": (22, "DREAMSKY"),
                          "Mods/dream-mobs_1_of_2-ios.dfmod": (33, "DREAM")})
        self.assertIn("dex-ios.dfmod", readme)
        self.assertIn("Documents/Mods", readme)

    def test_private_zip_refuses_a_missing_bundle(self):
        import json
        root = tempfile.mkdtemp()
        cfgp = os.path.join(root, "mods.json")
        with open(cfgp, "w", encoding="utf-8") as fh:
            json.dump({"dest_root": "x", "mods": [
                {"name": "DEX", "manifest": "dex.dfmod.json", "private_only": True, "unlock_key": "DEX"}]}, fh)
        old = (pack.MODS_DIR, pack.MODS_JSON)
        pack.MODS_DIR, pack.MODS_JSON = root, cfgp
        try:
            self.assertEqual(pack.main(["--private", "--out", os.path.join(root, "o.zip")]), 1)
        finally:
            pack.MODS_DIR, pack.MODS_JSON = old


if __name__ == "__main__":
    unittest.main()
