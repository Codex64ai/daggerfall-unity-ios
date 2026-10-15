#!/usr/bin/env python3
"""python3 -m unittest tools/bundled-mods/test_pack.py"""
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import pack  # noqa: E402

CFG = {"dest_root": "Assets/Game/Mods", "mods": [
    {"name": "JOTG", "authors": ["Cliffworms"], "manifest": "JobsOfTheThievesGuild.dfmod.json"},
    {"name": "SkyrimsAdventures", "authors": ["Cliffworms"], "manifest": "Skyrim's Adventures.dfmod.json"},
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
        self.assertIn("2 mods by Cliffworms.", txt)
        cfg2 = {"dest_root": "x", "mods": CFG["mods"] + [{"name": "JH", "manifest": "JH.dfmod.json", "generate": {"author": "Jay_H"}}]}
        self.assertIn("3 mods by Cliffworms and Jay_H.", pack.readme_text(cfg2, {}))
        self.assertIn("jobsofthethievesguild.dfmod", txt)
        self.assertIn("Jobs of the Thieves Guild - by Cliffworms (MIT)", txt)
        self.assertIn("skyrim's adventures.dfmod", txt)
        self.assertIn("THIRD-PARTY.md", txt)

    def test_credits_every_author_most_mods_first(self):
        cfg = {"dest_root": "x", "mods": CFG["mods"] + [
            {"name": "BA", "authors": ["Josh Steinhauer"], "manifest": "BA.dfmod.json"},
            {"name": "VE", "authors": ["drcarademono", "Kokey"], "manifest": "VE.dfmod.json",
             "licence": "permission:PERMISSION PENDING - x"},
            {"name": "UB", "authors": ["XJDHDR"], "manifest": "UB.dfmod.json", "licence_accept": "CC BY-NC-SA 4.0"}]}
        txt = pack.readme_text(cfg, {"ve": "Vanilla Enhanced"})
        self.assertIn("5 mods by Cliffworms, Josh Steinhauer, drcarademono, Kokey and XJDHDR.", txt)
        self.assertIn("Vanilla Enhanced - by drcarademono and Kokey (permission pending)", txt)
        self.assertIn("(CC BY-NC-SA 4.0)", txt)
        self.assertNotIn("MIT for Cliffworms", txt)

    def test_pinned_mod_without_authors_cannot_be_packed(self):
        d = tempfile.mkdtemp()
        os.makedirs(os.path.join(d, "Licenses"))
        for stem in ("x",):
            open(os.path.join(d, stem + ".dfmod"), "w").close()
            open(os.path.join(d, "Licenses", stem + "-LICENSE.txt"), "w").close()
        cfg = {"dest_root": "x", "mods": [{"name": "X", "manifest": "X.dfmod.json"}]}
        self.assertTrue(any("no authors" in p for p in pack.check_bundles(cfg, d)))

    def test_real_pin_list_credits_more_than_two_authors(self):
        with open(pack.MODS_JSON, encoding="utf-8") as fh:
            cfg = json.load(fh)
        self.assertEqual([m["name"] for m in cfg["mods"] if not pack.authors_of(m)], [])
        line = pack.authors_line(cfg)
        for who in ("Cliffworms", "Josh Steinhauer", "Kirk.O", "numidium", "DunnyOfPenwick", "RebornZA", "Deepfighter"):
            self.assertIn(who, line)
        # Gate-33: the Jay_H / UBLaMF / Vanilla Enhanced / DET mods moved to the Non-MIT pack.
        for who in ("Jay_H", "drcarademono", "Kokey", "XJDHDR", "Ninelan"):
            self.assertNotIn(who, line)

    def test_private_readme_credits_the_original_authors(self):
        with open(pack.MODS_JSON, encoding="utf-8") as fh:
            cfg = json.load(fh)
        txt = pack.private_readme_text([("Mods/dream-hud-menu-ios.dfmod", 1, "DREAM"),
                                        ("Mods/daggerfallbestiaryproject-ios.dfmod", 2, "DEX")], cfg)
        self.assertIn("DREAM by King of Worms", txt)
        self.assertIn("Daggerfall Enemy Expansion by Kamer and Kab", txt)


OK_CHECK = [sys.executable, "-c", "import sys; sys.exit(0)"]
STALE_CHECK = [sys.executable, "-c", "import sys; sys.exit(1)"]


class NonMitPack(unittest.TestCase):
    """--private: the Non-MIT zip ships every file as <stem>-ios.dfmod plus pack-index.json (the C#
    MobileOriginalMods.LoadPackIndex reads a top-level JSON array of {file, size, key})."""

    def setUp(self):
        self._check = pack.KNOWN_OURS_CHECK
        pack.KNOWN_OURS_CHECK = OK_CHECK

    def tearDown(self):
        pack.KNOWN_OURS_CHECK = self._check

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

    def test_originals_cover_every_unlock_key(self):
        import json
        with open(os.path.join(os.path.dirname(__file__), "mods.json"), encoding="utf-8") as fh:
            cfg = json.load(fh)
        keys = {m["unlock_key"] for m in cfg["mods"] if m.get("unlock_key")} | {"DREAM", "DREAMSKY"}
        self.assertEqual(sorted(keys - set(pack.ORIGINALS)), [])
        self.assertEqual(len(pack.ORIGINALS), 32)

    def test_private_readme_wording(self):
        txt = pack.private_readme_text([("Mods/jh-questpack1-ios.dfmod", 1, "JH_QP1")])
        self.assertIn("author's permission", txt)
        self.assertNotIn("have not (yet) given a redistribution licence", txt)

    def test_shipped_private_entries_all_have_keys(self):
        import json
        with open(os.path.join(os.path.dirname(__file__), "mods.json"), encoding="utf-8") as fh:
            cfg = json.load(fh)
        self.assertEqual(sorted(pack.private_stems(cfg).values()),
                         sorted(["DEX", "REALGRASS", "DYNAMICSKIES", "WOD", "WODBIOMES", "WODTERRAIN", "DISTANTTERRAIN"]
                                + ["JH_QP1", "JH_RLQ", "JH_ROLES", "JH_REPCON", "JH_CGK", "JH_TTC", "JH_BC", "JH_MUNDANE",
                                   "JH_MEDEM", "JH_INVITE_DB", "JH_INVITE_TG", "JH_VAMPIRE", "JH_WEREBOAR", "JH_WEREWOLF",
                                   "JH_CHEAT", "JH_WEATHER", "JH_NOISES", "JH_MQREP", "JH_IRONMAN", "JH_IRONMAN", "JH_SDR",
                                   "DET"] + ["UBLAMF"] * 6 + ["VE"] * 5))

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

    def _private_fixture(self):
        import json
        root = tempfile.mkdtemp()
        mods = os.path.join(root, "elsewhere")
        os.makedirs(mods)
        with open(os.path.join(mods, "dex.dfmod"), "wb") as fh:
            fh.write(b"x" * 44)
        cfgp = os.path.join(root, "mods.json")
        with open(cfgp, "w", encoding="utf-8") as fh:
            json.dump({"dest_root": "x", "mods": [
                {"name": "DEX", "manifest": "dex.dfmod.json", "private_only": True, "unlock_key": "DEX"}]}, fh)
        return root, mods, cfgp

    def test_private_reads_bundles_from_mods_dir(self):
        import json
        import zipfile
        root, mods, cfgp = self._private_fixture()
        old = (pack.MODS_DIR, pack.MODS_JSON)
        pack.MODS_DIR, pack.MODS_JSON = os.path.join(root, "empty"), cfgp
        try:
            z = os.path.join(root, "out.zip")
            self.assertEqual(pack.main(["--private", "--mods-dir", mods, "--out", z]), 0)
            with zipfile.ZipFile(z) as zf:
                index = json.loads(zf.read("pack-index.json"))
            self.assertEqual(index, [{"file": "Mods/dex-ios.dfmod", "size": 44, "key": "DEX"}])
        finally:
            pack.MODS_DIR, pack.MODS_JSON = old

    def test_private_takes_several_dream_dirs(self):
        import json
        import zipfile
        root, mods, cfgp = self._private_fixture()
        d1, d2 = os.path.join(root, "draft"), os.path.join(root, "ios")
        for d, leaf in ((d1, "dream-mobs_1_of_6.dfmod"), (d2, "dream - textures (3 of 10).dfmod"), (d2, "dream - sky.dfmod")):
            os.makedirs(d, exist_ok=True)
            with open(os.path.join(d, leaf), "wb") as fh:
                fh.write(b"x" * 5)
        old = (pack.MODS_DIR, pack.MODS_JSON)
        pack.MODS_DIR, pack.MODS_JSON = mods, cfgp
        try:
            z = os.path.join(root, "out.zip")
            self.assertEqual(pack.main(["--private", "--dream-dir", d1, "--dream-dir", d2, "--out", z]), 0)
            with zipfile.ZipFile(z) as zf:
                index = {r["file"]: r["key"] for r in json.loads(zf.read("pack-index.json"))}
            self.assertEqual(index, {"Mods/dex-ios.dfmod": "DEX", "Mods/dream-mobs_1_of_6-ios.dfmod": "DREAM",
                                     "Mods/dream-textures_3_of_10-ios.dfmod": "DREAM", "Mods/dream-sky-ios.dfmod": "DREAMSKY"})
            # the same -ios name from two folders is refused
            shutil_src = os.path.join(d2, "dream-mobs_1_of_6.dfmod")
            with open(shutil_src, "wb") as fh:
                fh.write(b"y")
            self.assertEqual(pack.main(["--private", "--dream-dir", d1, "--dream-dir", d2, "--out", z]), 1)
        finally:
            pack.MODS_DIR, pack.MODS_JSON = old

    def test_private_refuses_a_stale_known_ours(self):
        root, mods, cfgp = self._private_fixture()
        old = (pack.MODS_DIR, pack.MODS_JSON)
        pack.MODS_DIR, pack.MODS_JSON = mods, cfgp
        pack.KNOWN_OURS_CHECK = STALE_CHECK
        try:
            z = os.path.join(root, "out.zip")
            self.assertNotEqual(pack.main(["--private", "--out", z]), 0)
            self.assertFalse(os.path.exists(z))
        finally:
            pack.MODS_DIR, pack.MODS_JSON = old

    def test_private_zip_splits_under_the_asset_limit(self):
        import json
        import zipfile
        root, mods, cfgp = self._private_fixture()
        dream = os.path.join(root, "dream")
        os.makedirs(dream)
        for leaf in ("dream - mobs (1 of 2).dfmod", "dream - mobs (2 of 2).dfmod"):
            with open(os.path.join(dream, leaf), "wb") as fh:
                fh.write(b"y" * 600000)
        old = (pack.MODS_DIR, pack.MODS_JSON)
        pack.MODS_DIR, pack.MODS_JSON = mods, cfgp
        try:
            z = os.path.join(root, "NonMIT-ModPack-ios.zip")
            self.assertEqual(pack.main(["--private", "--dream-dir", dream, "--out", z, "--split-mb", "1"]), 0)
            self.assertFalse(os.path.exists(z))
            parts = [os.path.join(root, "NonMIT-ModPack-ios-part%d.zip" % i) for i in (1, 2)]
            self.assertTrue(all(os.path.exists(p) for p in parts))
            self.assertFalse(os.path.exists(os.path.join(root, "NonMIT-ModPack-ios-part3.zip")))
            shipped = []
            for p in parts:
                with zipfile.ZipFile(p) as zf:
                    names = zf.namelist()
                    self.assertIn("README.txt", names)
                    self.assertEqual(len(json.loads(zf.read("pack-index.json"))), 3)  # every part carries the full index
                    shipped += [n for n in names if n.startswith("Mods/")]
                self.assertLess(os.path.getsize(p), (1 << 20) + 4096)
            self.assertEqual(sorted(shipped), ["Mods/dex-ios.dfmod", "Mods/dream-mobs_1_of_2-ios.dfmod",
                                               "Mods/dream-mobs_2_of_2-ios.dfmod"])
        finally:
            pack.MODS_DIR, pack.MODS_JSON = old

    def test_part_plan_is_greedy_in_order(self):
        self.assertEqual(pack.split_parts([("a", 5), ("b", 5), ("c", 5)], 10), [["a", "b"], ["c"]])
        self.assertEqual(pack.split_parts([("a", 15), ("b", 1)], 10), [["a"], ["b"]])
        self.assertEqual(pack.split_parts([("a", 1)], None), [["a"]])

    def test_known_ours_check_runs_the_real_script(self):
        self.assertEqual(self._check[-2:], [os.path.join(pack.REPO_ROOT, "tools", "known_ours.py"), "--check"])

    def test_known_ours_reads_with_old_names_aliases(self):
        import importlib.util
        path = os.path.join(pack.REPO_ROOT, "tools", "known_ours.py")
        spec = importlib.util.spec_from_file_location("known_ours", path)
        ko = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(ko)
        cs = 'WithOldNames(StemRow("K", "D", "https://x", "World of Daggerfall - Biomes", 1L << 20), "wod-biomes.dfmod"),'
        names = ko.table_names(cs)
        self.assertIn("wod-biomes.dfmod", names)
        self.assertIn("world of daggerfall - biomes.dfmod", names)
        with open(ko.CS_TABLE, encoding="utf-8") as fh:
            self.assertIn("wod-biomes.dfmod", ko.table_names(fh.read()))


if __name__ == "__main__":
    unittest.main()
