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

    def test_files_must_live_under_this_mods_folder(self):
        m = manifest("X", ["Assets/Game/Mods/AuthorsLocalName/WorldData/a.json"])
        probs = fetch.validate_manifest(m, self.mod)
        self.assertTrue(any("outside this mod's folder" in p for p in probs))
        fixed = fetch.normalize_paths(m, "X")
        self.assertEqual(fixed["Files"], ["Assets/Game/Mods/X/WorldData/a.json"])
        self.assertEqual(fetch.validate_manifest(fixed, self.mod), [])

    def test_payload_roots(self):
        m = manifest("X", self.files("WorldData/a.json", "QuestPacks/Cliff/X/X01.txt"))
        self.assertEqual(fetch.payload_roots(m), {"WorldData", "QuestPacks"})


class WorldDataArchives(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.mod = os.path.join(self.tmp, "Assets", "Game", "Mods", "X")
        os.makedirs(os.path.join(self.mod, "WorldData"))

    def block(self, name, archives):
        flats = ",".join('{"Resources":{"FlatResource":{"TextureArchive":%d,"TextureRecord":0}}}' % a for a in archives)
        with open(os.path.join(self.mod, "WorldData", name), "w") as fh:
            fh.write('{"RdbBlock":{"ObjectRootList":[{"RdbObjects":[%s]}]}}' % flats)
        return "Assets/Game/Mods/X/WorldData/" + name

    def test_vanilla_archives_pass(self):
        m = manifest("X", [self.block("A.RDB.json", [199, 210, 511])])
        self.assertEqual(fetch.worlddata_archive_problems(m, self.mod), [])

    def test_expanded_textures_archives_are_rejected(self):
        m = manifest("X", [self.block("S0000999.RDB.json", [199, 10021, 20050])])
        probs = fetch.worlddata_archive_problems(m, self.mod)
        self.assertEqual(len(probs), 1)
        self.assertIn("S0000999.RDB.json", probs[0])
        self.assertIn("10021", probs[0])


class ValidateSet(unittest.TestCase):
    def test_required_dependency_must_be_in_the_pack(self):
        fixed = manifest("Fixed Dungeon Exteriors", []); fixed["_stem"] = "FixedDungeonExteriors"
        dde = manifest("Detailed Dungeon Exteriors", []); dde["_stem"] = "DetailedDungeonExteriors"
        dde["Dependencies"] = [{"Name": "fixeddungeonexteriors", "IsOptional": False}]
        self.assertEqual(fetch.validate_set([fixed, dde]), [])
        dde["Dependencies"] = [{"Name": "daggerfall expanded textures", "IsOptional": False}]
        self.assertTrue(any("REQUIRES 'daggerfall expanded textures'" in p for p in fetch.validate_set([fixed, dde])))
        dde["Dependencies"] = [{"Name": "mudex", "IsOptional": True}]
        self.assertEqual(fetch.validate_set([fixed, dde]), [])

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

    def test_permission_note_only_when_the_entry_declares_it(self):
        note = fetch.licence_text_for({"licence": "permission:Jay_H granted redistribution, 2026-09-02"})
        self.assertTrue(note.startswith("Permission\n"))
        self.assertIn("Jay_H", note)
        self.assertEqual(fetch.licence_problems(note, allow_permission=True), [])
        self.assertTrue(fetch.licence_problems(note, allow_permission=False))
        self.assertIsNone(fetch.licence_text_for({}))
        self.assertTrue(fetch.licence_problems("Permission\n", allow_permission=True))  # a bare word is not a record


class GeneratedManifest(unittest.TestCase):
    FILES = ["QuestPacks/X/JHRLQ/QuestList-RLQ.txt", "QuestPacks/X/JHRLQ/RLQ01.txt",
             "QuestPacks/X/JHRLQ/RLQ02.txt", "QuestPacks/X/README.txt", "QuestPacks/X/overview.txt"]

    def test_lists_and_quests_are_declared_and_readmes_dropped(self):
        m = fetch.generate_manifest("X", self.FILES, "Random Little Quests", "Jay_H", "desc")
        self.assertEqual(m["Contributes"]["QuestLists"], ["RLQ"])
        self.assertEqual(sorted(m["Contributes"]["LooseQuestsList"]), ["RLQ01", "RLQ02"])
        self.assertEqual(len(m["Files"]), 3)
        self.assertTrue(all(f.startswith("Assets/Game/Mods/X/") for f in m["Files"]))
        self.assertEqual(m["ModAuthor"], "Jay_H")

    def test_guid_is_stable_per_name_and_distinct_per_pack(self):
        a = fetch.generate_manifest("X", self.FILES, "t", "a", "")["GUID"]
        b = fetch.generate_manifest("X", self.FILES, "t2", "a", "")["GUID"]
        c = fetch.generate_manifest("Y", self.FILES, "t", "a", "")["GUID"]
        self.assertEqual(a, b)
        self.assertNotEqual(a, c)

    def test_questlist_rename_changes_the_declared_name_and_the_file(self):
        files = ["QuestPacks/X/IM/QuestList-IronmanMadness.txt", "QuestPacks/X/IM/IM01.txt"]
        m = fetch.generate_manifest("X", files, "t", "a", "", renames={"IronmanMadness": "IronmanMadnessInfighting"})
        self.assertEqual(m["Contributes"]["QuestLists"], ["IronmanMadnessInfighting"])
        self.assertIn("Assets/Game/Mods/X/QuestPacks/X/IM/QuestList-IronmanMadnessInfighting.txt", m["Files"])

    def test_is_quest_txt(self):
        self.assertTrue(fetch.is_quest_txt("QP1/Commoner/JHC001.txt"))
        self.assertFalse(fetch.is_quest_txt("QP1/README.txt"))
        self.assertFalse(fetch.is_quest_txt("AFR/overview.txt"))
        self.assertFalse(fetch.is_quest_txt("thing.json"))


if __name__ == "__main__":
    unittest.main()
