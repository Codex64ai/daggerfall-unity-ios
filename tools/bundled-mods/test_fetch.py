#!/usr/bin/env python3
"""python3 -m unittest tools/bundled-mods/test_fetch.py"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import fetch  # noqa: E402


def manifest(name, files, contributes=None):
    m = {"ModTitle": name, "ModAuthor": "Cliffworms", "GUID": "g-" + name, "Files": files}
    if contributes is not None:
        m["Contributes"] = contributes
    return m


class ValidateManifest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.mod = os.path.join(self.tmp, "Assets", "Game", "Mods", "X")
        os.makedirs(os.path.join(self.mod, "WorldData"))
        os.makedirs(os.path.join(self.mod, "QuestPacks", "Cliff", "X"))
        for rel in ("WorldData/a.json", "QuestPacks/Cliff/X/QuestList-X.txt", "QuestPacks/Cliff/X/X01.txt"):
            with open(os.path.join(self.mod, rel), "w") as fh:
                fh.write("{}")

    def files(self, *rels):
        return ["Assets/Game/Mods/X/" + r for r in rels]

    def test_clean_manifest_has_no_problems(self):
        m = manifest("X", self.files("WorldData/a.json", "QuestPacks/Cliff/X/QuestList-X.txt",
                                     "QuestPacks/Cliff/X/X01.txt"),
                     {"QuestLists": ["X"], "LooseQuestsList": ["X01"]})
        self.assertEqual(fetch.validate_manifest(m, self.mod), [])

    def test_missing_file_is_reported(self):
        m = manifest("X", self.files("WorldData/missing.json"))
        self.assertTrue(any("missing.json" in p for p in fetch.validate_manifest(m, self.mod)))

    def test_script_files_are_refused(self):
        with open(os.path.join(self.mod, "Loader.cs"), "w") as fh:
            fh.write("")
        m = manifest("X", self.files("Loader.cs"))
        self.assertTrue(any(".cs" in p for p in fetch.validate_manifest(m, self.mod)))

    def test_questlist_must_be_declared_in_contributes(self):
        m = manifest("X", self.files("QuestPacks/Cliff/X/QuestList-X.txt", "QuestPacks/Cliff/X/X01.txt"))
        probs = fetch.validate_manifest(m, self.mod)
        self.assertTrue(any("QuestList-X" in p and "Contributes" in p for p in probs))
        self.assertTrue(any("X01" in p and "LooseQuestsList" in p for p in probs))

    def test_uppercase_image_extension_is_reported_and_fixable(self):
        os.makedirs(os.path.join(self.mod, "Textures"))
        with open(os.path.join(self.mod, "Textures", "1210_2-0.PNG"), "w") as fh:
            fh.write("")
        m = manifest("X", self.files("Textures/1210_2-0.PNG"))
        self.assertTrue(any("1210_2-0.PNG" in p for p in fetch.validate_manifest(m, self.mod)))
        fixed = fetch.lowercase_image_names(m, self.mod)
        self.assertIn("Assets/Game/Mods/X/Textures/1210_2-0.png", fixed["Files"])
        self.assertTrue(os.path.exists(os.path.join(self.mod, "Textures", "1210_2-0.png")))
        self.assertEqual(fetch.validate_manifest(fixed, self.mod), [])

    def test_payload_roots(self):
        m = manifest("X", self.files("WorldData/a.json", "QuestPacks/Cliff/X/X01.txt"))
        self.assertEqual(fetch.payload_roots(m), {"WorldData", "QuestPacks"})


class ValidateSet(unittest.TestCase):
    def test_duplicate_questlist_names_across_mods(self):
        a = manifest("A", [], {"QuestLists": ["SKYRIM"]})
        b = manifest("B", [], {"QuestLists": ["SKYRIM"]})
        self.assertTrue(any("SKYRIM" in p for p in fetch.validate_set([a, b])))
        self.assertEqual(fetch.validate_set([a, manifest("C", [], {"QuestLists": ["JOTG"]})]), [])

    def test_duplicate_guid_across_mods(self):
        a = manifest("A", [])
        b = manifest("B", [])
        b["GUID"] = a["GUID"]
        self.assertTrue(any("GUID" in p for p in fetch.validate_set([a, b])))


class Licence(unittest.TestCase):
    def test_mit_first_line_required(self):
        self.assertEqual(fetch.licence_problems("MIT License\n\nCopyright (c) 2025 Cliffworms\n"), [])
        self.assertTrue(fetch.licence_problems("All rights reserved"))
        self.assertTrue(fetch.licence_problems(""))


if __name__ == "__main__":
    unittest.main()
