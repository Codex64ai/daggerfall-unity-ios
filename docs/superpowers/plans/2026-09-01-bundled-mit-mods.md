# Bundled MIT Mods Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship thirteen MIT Cliffworms mods inside the iOS app as `.dfmod` bundles that the launcher's MODS window can switch on and off, each with its licence text, and hand Ikram a test ipa.

**Architecture:** A Python fetch script clones the thirteen repos at pinned commits into `Assets/Game/Mods/<Name>/` (already gitignored) with upstream or corrected manifests, validating licences and quest-list declarations. The existing `MobileModBuilder` turns each manifest into an iOS AssetBundle in `Assets/StreamingAssets/Mods/` (gitignored) as a step of `MobileBuildSetup.ApplyAll`. One engine change makes the iOS mod scanner read that shipped folder after the player's `Documents/Mods`, so a player's own copy of a mod wins.

**Tech Stack:** Python 3.9 (stdlib + `git`), Unity 6000.3.23f1 editor scripts (C#), the DFU mod system (`ModManager`, `Mod`, `ModInfo`), `MobileSelfTest` for editor-side checks.

**Spec:** `docs/superpowers/specs/2026-09-01-bundled-mit-mods-design.md`

## Global Constraints

- Branch `unity6-upgrade`; Unity **6000.3.23f1**; iOS 15.0 floor. Commits authored as `Codex64ai <ikrammassabini@gmail.com>` with the `Co-Authored-By` / `Claude-Session` trailers.
- Repo stays small: fetched mod sources and built bundles are **never committed** (`Assets/Game/Mods/.gitignore` and `Assets/StreamingAssets/Mods/.gitignore` already ignore everything but their readmes).
- Every bundled mod must have `LICENSE` whose first line is `MIT License`. No `.cs` / `.dll.bytes` may enter a bundle.
- `IOSPilot` is never bundled into the app.
- Engine edits in `ModManager.cs` carry a `// MOBILE:` comment, matching the existing convention.
- `Assets/Editor/MobileSelfTest.cs` has a staging twin at `~/daggerfall-mobile/editor/MobileSelfTest.cs`. After editing the repo copy, copy repo → staging (never the other way without diffing first).
- Self-test CLI (graphics-enabled — do NOT pass `-nographics`):
  `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>`; grep the log for `FAIL` and the final `passed`/`failed` counts.
- The test ipa is uploaded to the **draft** release `testapp-unity6` as `DFU-Test-unity6-mitmods.ipa` with `gh release upload --clobber -R Codex64ai/daggerfall-unity-ios`. Never publish that draft.

---

## File structure

| Path | Responsibility |
|---|---|
| `tools/bundled-mods/mods.json` | The thirteen entries: name, repo URL, pinned commit, manifest file name, whether ours overrides it. Data only. |
| `tools/bundled-mods/manifests/MainQuestConsequences.dfmod.json` | Upstream manifest plus its two quest files and `Contributes`. |
| `tools/bundled-mods/manifests/DetailedDungeonExteriors.dfmod.json` | Authored manifest (upstream has none), depends on Fixed Dungeon Exteriors. |
| `tools/bundled-mods/fetch.py` | Clone at pin, copy payload + LICENSE, validate. Pure validation functions separated from I/O. |
| `tools/bundled-mods/test_fetch.py` | Unit tests for the validation functions. |
| `Assets/Game/Addons/ModSupport/ModManager.cs` | `MergeModFiles` helper and two-root scan on iOS. |
| `Assets/Editor/MobileModBuilder.cs` | `BuildMod` gains a `flatOutput` option so bundles land directly in a folder. |
| `Assets/Editor/MobileBuildSetup.cs` | `BuildBundledMods()` and its call from `ApplyAll()`. |
| `Assets/Editor/MobileSelfTest.cs` | `TestMergeModFiles`, `TestBundledModManifests`, `TestBundledModLicences`. |
| `THIRD-PARTY.md`, `README-iOS.md`, `~/daggerfall-mobile/RELEASE.md` | Attribution, player docs, release recipe. |

---

### Task 1: Pin list, corrected manifests, and the fetch script's validation rules

**Files:**
- Create: `tools/bundled-mods/mods.json`
- Create: `tools/bundled-mods/manifests/MainQuestConsequences.dfmod.json`
- Create: `tools/bundled-mods/manifests/DetailedDungeonExteriors.dfmod.json`
- Create: `tools/bundled-mods/fetch.py`
- Test: `tools/bundled-mods/test_fetch.py`

**Interfaces:**
- Produces: `fetch.py` functions `validate_manifest(manifest: dict, mod_dir: str) -> list[str]` (returns problems, empty = ok), `validate_set(manifests: list[dict]) -> list[str]` (cross-mod rules), `licence_problems(licence_text: str) -> list[str]`, `lowercase_image_names(manifest: dict, mod_dir: str) -> dict` (renames on disk + in `Files`), `payload_roots(manifest: dict) -> set[str]` (the top-level folders `Files` reference, e.g. `{"WorldData","QuestPacks"}`), and CLI `python3 tools/bundled-mods/fetch.py [--only NAME] [--check]`.
- Manifest field names are DFU's `ModInfo`: `ModTitle`, `ModVersion`, `ModAuthor`, `ContactInfo`, `DFUnity_Version`, `ModDescription`, `GUID`, `Files` (list of `Assets/Game/Mods/<Name>/...` paths), `Contributes` (`QuestLists`, `LooseQuestsList`), `Dependencies` (list of `{Name, IsOptional, IsPeer}`).

- [ ] **Step 1: Write `mods.json`**

```json
{
  "dest_root": "Assets/Game/Mods",
  "mods": [
    {"name": "FixedDungeonExteriors",     "repo": "https://github.com/Cliffworms/FixedDungeonExteriors.git",     "commit": "f384bb3f26aca8844615ce7c42a51ba999a52c0b", "manifest": "FixedDungeonExteriors.dfmod.json"},
    {"name": "VariedWealthyHomes",        "repo": "https://github.com/Cliffworms/VariedWealthyHomes.git",        "commit": "085a9f2aee56a58555d5848a58448ad4c185f3b3", "manifest": "VariedWealthyHomes.dfmod.json"},
    {"name": "MainQuestConsequences",     "repo": "https://github.com/Cliffworms/MainQuestConsequences.git",     "commit": "1cca6534e4f022c24f8bda4595679a91ab9c207f", "manifest": "MainQuestConsequences.dfmod.json", "manifest_override": "manifests/MainQuestConsequences.dfmod.json"},
    {"name": "DetailedDungeonExteriors",  "repo": "https://github.com/Cliffworms/DetailedDungeonExteriors.git",  "commit": "6d886ad9261601d58a931573125bdddcf36ed09c", "manifest": "DetailedDungeonExteriors.dfmod.json", "manifest_override": "manifests/DetailedDungeonExteriors.dfmod.json"},
    {"name": "DetailedMainQuestDungeons", "repo": "https://github.com/Cliffworms/DetailedMainQuestDungeons.git", "commit": "045022efe4e839f7f02e4d6f133ba20ad3b0b317", "manifest": "DetailedMainQuestDungeons.dfmod.json"},
    {"name": "AquaticSprites",            "repo": "https://github.com/Cliffworms/AquaticSprites.git",            "commit": "ea195e77a707cce4bd2a36a96b024534cd420ebb", "manifest": "UnderwaterSprites.dfmod.json"},
    {"name": "SmallerMQDungeons",         "repo": "https://github.com/Cliffworms/SmallerMQDungeons.git",         "commit": "51dc8db3449f33b0e1837be68b8305fe8c0d9b3e", "manifest": "Smaller Main Quest Dungeons.dfmod.json"},
    {"name": "LevelingInspiration",       "repo": "https://github.com/Cliffworms/LevelingInspiration.git",       "commit": "37aefbbec5cf0b063959507d7cd0c7d4571400ee", "manifest": "Leveling Inspiration.dfmod.json"},
    {"name": "SkyrimsAdventures",         "repo": "https://github.com/Cliffworms/SkyrimsAdventures.git",         "commit": "e5083f298e46d9c761701407c9352fc023dd8a78", "manifest": "Skyrim's Adventures.dfmod.json"},
    {"name": "JOTG",                      "repo": "https://github.com/Cliffworms/JOTG.git",                      "commit": "701440f383eece827ce8bbdc7ff39dd2a21709a5", "manifest": "JobsOfTheThievesGuild.dfmod.json"},
    {"name": "ArenasAdventures",          "repo": "https://github.com/Cliffworms/ArenasAdventures.git",          "commit": "9352a9288113607c5efbf5f51a3e1a7d6bb7687a", "manifest": "Arena's Adventures.dfmod.json"},
    {"name": "TownGreetingsIliacBay",     "repo": "https://github.com/Cliffworms/TownGreetingsIliacBay.git",     "commit": "203f9d2a7426995d8400e00112ac1c929fc58a02", "manifest": "Town Greetings of the Iliac Bay.dfmod.json"},
    {"name": "RumorsOfTheIliacBay",       "repo": "https://github.com/Cliffworms/RumorsOfTheIliacBay.git",       "commit": "b5641cd12a47b65b374401825bebd00dfa96ede2", "manifest": "RumorsOfTheIliacBay.dfmod.json"}
  ]
}
```

The `manifest` value is the file name the repo uses (the `.dfmod` output takes the same stem, which is what the MODS window shows as the file name). `manifest_override` is relative to `tools/bundled-mods/`.

- [ ] **Step 2: Write the two corrected manifests**

`manifests/MainQuestConsequences.dfmod.json` — start from the repo file at the pinned commit (`Assets/Game/Mods/MainQuestConsequences/MainQuestConsequences.dfmod.json` after a fetch), keep every field verbatim, and make exactly these changes: append the two quest files to `Files` and add `Contributes`:

```json
  "Files": [
    "... the 64 existing WorldData entries, unchanged ...",
    "Assets/Game/Mods/MainQuestConsequences/QuestPacks/Cliff/MQC/MQCControl.txt",
    "Assets/Game/Mods/MainQuestConsequences/QuestPacks/Cliff/MQC/QuestList-MQCControl.txt"
  ],
  "Contributes": {
    "QuestLists": ["MQCControl"],
    "LooseQuestsList": ["MQCControl"]
  }
```

`manifests/DetailedDungeonExteriors.dfmod.json` — authored:

```json
{
  "ModTitle": "Detailed Dungeon Exteriors",
  "ModVersion": "1.0",
  "ModAuthor": "Cliffworms",
  "ContactInfo": "https://github.com/Cliffworms/DetailedDungeonExteriors",
  "DFUnity_Version": "1.1.1",
  "ModDescription": "More detailed dungeon exteriors. Requires Fixed Dungeon Exteriors, whose block names it assumes. Manifest written by the iOS port; the data is Cliffworms' (MIT).",
  "GUID": "9c1f4d2e-7b3a-4e6f-9a0d-2d5c8e1b7f31",
  "Files": [
    "Assets/Game/Mods/DetailedDungeonExteriors/WorldData/FDEBARROWAA01.RMB.json",
    "... one entry per WorldData/*.RMB.json in the repo (10 total) — list them explicitly after the first fetch ..."
  ],
  "Dependencies": [
    {"Name": "FixedDungeonExteriors", "IsOptional": false, "IsPeer": false}
  ]
}
```

Dependency names in DFU are the dependency's **file name stem, exact case** (see `ModManager.CheckModDependencies`, `GetModFromName` compares `mod.FileName` with `StringComparison.Ordinal`); Fixed Dungeon Exteriors' file name is `FixedDungeonExteriors.dfmod`, hence `FixedDungeonExteriors`. Verify that comparison in `ModManager.cs` before finalising; if it compares GUIDs instead, use `4038da33-51a4-4238-8290-8b2cc73320b3`.

- [ ] **Step 3: Write the failing tests**

```python
#!/usr/bin/env python3
"""python3 -m unittest tools/bundled-mods/test_fetch.py"""
import os, sys, json, tempfile, unittest
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
            open(os.path.join(self.mod, rel), "w").write("{}")

    def files(self, *rels):
        return ["Assets/Game/Mods/X/" + r for r in rels]

    def test_clean_manifest_has_no_problems(self):
        m = manifest("X", self.files("WorldData/a.json", "QuestPacks/Cliff/X/QuestList-X.txt", "QuestPacks/Cliff/X/X01.txt"),
                     {"QuestLists": ["X"], "LooseQuestsList": ["X01"]})
        self.assertEqual(fetch.validate_manifest(m, self.mod), [])

    def test_missing_file_is_reported(self):
        m = manifest("X", self.files("WorldData/missing.json"))
        self.assertTrue(any("missing.json" in p for p in fetch.validate_manifest(m, self.mod)))

    def test_script_files_are_refused(self):
        open(os.path.join(self.mod, "Loader.cs"), "w").write("")
        m = manifest("X", self.files("Loader.cs"))
        self.assertTrue(any(".cs" in p for p in fetch.validate_manifest(m, self.mod)))

    def test_questlist_must_be_declared_in_contributes(self):
        m = manifest("X", self.files("QuestPacks/Cliff/X/QuestList-X.txt", "QuestPacks/Cliff/X/X01.txt"))
        probs = fetch.validate_manifest(m, self.mod)
        self.assertTrue(any("QuestList-X" in p and "Contributes" in p for p in probs))
        self.assertTrue(any("X01" in p and "LooseQuestsList" in p for p in probs))

    def test_uppercase_image_extension_is_reported_and_fixable(self):
        os.makedirs(os.path.join(self.mod, "Textures"))
        open(os.path.join(self.mod, "Textures", "1210_2-0.PNG"), "w").write("")
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
        a = manifest("A", []); b = manifest("B", []); b["GUID"] = a["GUID"]
        self.assertTrue(any("GUID" in p for p in fetch.validate_set([a, b])))

class Licence(unittest.TestCase):
    def test_mit_first_line_required(self):
        self.assertEqual(fetch.licence_problems("MIT License\n\nCopyright (c) 2025 Cliffworms\n"), [])
        self.assertTrue(fetch.licence_problems("All rights reserved"))
        self.assertTrue(fetch.licence_problems(""))

if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 4: Run the tests to see them fail**

Run: `cd ~/dev/daggerfall-unity && python3 -m unittest tools/bundled-mods/test_fetch.py 2>&1 | tail -3`
Expected: `ModuleNotFoundError: No module named 'fetch'`

- [ ] **Step 5: Write `fetch.py`**

```python
#!/usr/bin/env python3
"""Fetch the bundled MIT mods into Assets/Game/Mods/ at pinned commits, and validate them.

    python3 tools/bundled-mods/fetch.py            # all thirteen
    python3 tools/bundled-mods/fetch.py --only JOTG
    python3 tools/bundled-mods/fetch.py --check    # validate what is on disk, no network

What lands per mod: <Name>.dfmod.json (upstream's, or ours from manifests/), only the payload
folders the manifest references (WorldData/, QuestPacks/, Textures/ ...), and LICENSE. Never
ObjectGroups/ (authoring fragments), Scripts/, READMEs or the repo's .meta files.

Why it refuses things: a .cs can never run on iOS (IL2CPP, no JIT); an undeclared QuestList
installs cleanly and silently does nothing; an uppercase .PNG is a different file on iOS's
case-sensitive filesystem; a duplicate QuestList name is dropped by the engine without a
visible error; and MIT requires shipping the notice, so a missing LICENSE is a stop.
See docs/superpowers/specs/2026-09-01-bundled-mit-mods-design.md.
"""
import argparse, json, os, shutil, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MODS_JSON = os.path.join(HERE, "mods.json")
IMAGE_EXTS = (".png", ".jpg", ".jpeg", ".tga")
SCRIPT_EXTS = (".cs", ".dll.bytes", ".dll")

# ----------------------------------------------------------------- validation (pure)

def payload_roots(manifest):
    roots = set()
    for f in manifest.get("Files", []):
        parts = f.replace("\\", "/").split("/")
        # Assets/Game/Mods/<Name>/<Root>/...
        if len(parts) > 5:
            roots.add(parts[4])
    return roots

def _rel(path):
    """'Assets/Game/Mods/X/WorldData/a.json' -> 'WorldData/a.json'"""
    parts = path.replace("\\", "/").split("/")
    return "/".join(parts[4:])

def validate_manifest(manifest, mod_dir):
    problems = []
    files = manifest.get("Files") or []
    if not files:
        problems.append("Files is empty")
    contributes = manifest.get("Contributes") or {}
    quest_lists = set(contributes.get("QuestLists") or [])
    loose_quests = set(contributes.get("LooseQuestsList") or [])
    for f in files:
        rel = _rel(f)
        if not os.path.exists(os.path.join(mod_dir, rel)):
            problems.append("missing on disk: " + f)
        if f.endswith(SCRIPT_EXTS):
            problems.append("script file cannot run on iOS: " + f)
        base = os.path.basename(rel)
        stem, ext = os.path.splitext(base)
        if ext.lower() in IMAGE_EXTS and ext != ext.lower():
            problems.append("uppercase image extension (iOS is case-sensitive): " + f)
        if "/QuestPacks/" in "/" + rel and ext == ".txt":
            if stem.startswith("QuestList-"):
                if stem[len("QuestList-"):] not in quest_lists:
                    problems.append(stem + " is not declared in Contributes.QuestLists")
            elif stem not in loose_quests:
                problems.append(stem + " is not declared in Contributes.LooseQuestsList")
    return problems

def validate_set(manifests):
    problems = []
    seen_lists, seen_guids = {}, {}
    for m in manifests:
        title = m.get("ModTitle", "?")
        for ql in (m.get("Contributes") or {}).get("QuestLists") or []:
            if ql in seen_lists:
                problems.append("QuestList %s in both %s and %s (engine drops the second)" % (ql, seen_lists[ql], title))
            seen_lists[ql] = title
        guid = m.get("GUID")
        if guid:
            if guid in seen_guids:
                problems.append("GUID %s shared by %s and %s" % (guid, seen_guids[guid], title))
            seen_guids[guid] = title
    return problems

def licence_problems(text):
    first = (text or "").strip().splitlines()[0].strip() if (text or "").strip() else ""
    return [] if first == "MIT License" else ["LICENSE first line is %r, expected 'MIT License'" % first]

def lowercase_image_names(manifest, mod_dir):
    fixed = dict(manifest)
    new_files = []
    for f in manifest.get("Files", []):
        stem, ext = os.path.splitext(f)
        if ext.lower() in IMAGE_EXTS and ext != ext.lower():
            src = os.path.join(mod_dir, _rel(f))
            dst = os.path.join(mod_dir, _rel(stem + ext.lower()))
            if os.path.exists(src) and src != dst:
                os.rename(src, dst)
            f = stem + ext.lower()
        new_files.append(f)
    fixed["Files"] = new_files
    return fixed

# ----------------------------------------------------------------- I/O

def load_mods():
    with open(MODS_JSON) as fh:
        return json.load(fh)

def dest_dir(cfg, entry):
    return os.path.join(REPO_ROOT, cfg["dest_root"], entry["name"])

def read_manifest(cfg, entry):
    path = os.path.join(dest_dir(cfg, entry), entry["manifest"])
    with open(path, encoding="utf-8") as fh:
        return json.load(fh), path

def fetch_one(cfg, entry):
    dest = dest_dir(cfg, entry)
    tmp = tempfile.mkdtemp(prefix="bundled-mod-")
    try:
        subprocess.run(["git", "init", "-q", tmp], check=True)
        subprocess.run(["git", "-C", tmp, "fetch", "-q", "--depth", "1", entry["repo"], entry["commit"]], check=True)
        subprocess.run(["git", "-C", tmp, "checkout", "-q", "FETCH_HEAD"], check=True)

        if entry.get("manifest_override"):
            with open(os.path.join(HERE, entry["manifest_override"]), encoding="utf-8") as fh:
                manifest = json.load(fh)
        else:
            with open(os.path.join(tmp, entry["manifest"]), encoding="utf-8") as fh:
                manifest = json.load(fh)

        if os.path.isdir(dest):
            shutil.rmtree(dest)
        os.makedirs(dest)
        for root in sorted(payload_roots(manifest)):
            src = os.path.join(tmp, root)
            if not os.path.isdir(src):
                raise SystemExit("%s: manifest references %s/ but the repo has no such folder" % (entry["name"], root))
            shutil.copytree(src, os.path.join(dest, root), ignore=shutil.ignore_patterns("*.meta"))
        lic = os.path.join(tmp, "LICENSE")
        if not os.path.exists(lic):
            raise SystemExit(entry["name"] + ": no LICENSE in repo - cannot bundle")
        shutil.copyfile(lic, os.path.join(dest, "LICENSE"))

        manifest = lowercase_image_names(manifest, dest)
        with open(os.path.join(dest, entry["manifest"]), "w", encoding="utf-8") as fh:
            json.dump(manifest, fh, indent=2, ensure_ascii=False)
            fh.write("\n")
        print("fetched", entry["name"], "@", entry["commit"][:8], sorted(payload_roots(manifest)))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

def check_all(cfg, entries):
    manifests, problems = [], []
    for entry in entries:
        dest = dest_dir(cfg, entry)
        if not os.path.isdir(dest):
            problems.append(entry["name"] + ": not fetched")
            continue
        manifest, _ = read_manifest(cfg, entry)
        manifests.append(manifest)
        problems += [entry["name"] + ": " + p for p in validate_manifest(manifest, dest)]
        lic = os.path.join(dest, "LICENSE")
        if not os.path.exists(lic):
            problems.append(entry["name"] + ": LICENSE missing")
        else:
            with open(lic, encoding="utf-8", errors="replace") as fh:
                problems += [entry["name"] + ": " + p for p in licence_problems(fh.read())]
    problems += validate_set(manifests)
    return problems

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--only", help="fetch/check one mod by name")
    ap.add_argument("--check", action="store_true", help="validate what is on disk; no network")
    args = ap.parse_args(argv)
    cfg = load_mods()
    entries = [m for m in cfg["mods"] if not args.only or m["name"] == args.only]
    if not entries:
        raise SystemExit("no mod named " + str(args.only))
    if not args.check:
        for entry in entries:
            fetch_one(cfg, entry)
    problems = check_all(cfg, entries)
    for p in problems:
        print("PROBLEM:", p)
    print("%d mods, %d problems" % (len(entries), len(problems)))
    return 1 if problems else 0

if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `cd ~/dev/daggerfall-unity && python3 -m unittest tools/bundled-mods/test_fetch.py 2>&1 | tail -3`
Expected: `OK` with 9 tests.

- [ ] **Step 7: Fetch for real and fix the Detailed Dungeon Exteriors file list**

Run: `cd ~/dev/daggerfall-unity && python3 tools/bundled-mods/fetch.py`
Expected: thirteen `fetched ...` lines. If Detailed Dungeon Exteriors reports missing files, list the actual `WorldData/*.RMB.json` names (`ls Assets/Game/Mods/DetailedDungeonExteriors/WorldData`) into `manifests/DetailedDungeonExteriors.dfmod.json` and re-run `--only DetailedDungeonExteriors`. Then confirm `python3 tools/bundled-mods/fetch.py --check` prints `13 mods, 0 problems`, and `git status --short` shows nothing under `Assets/Game/Mods/` (gitignore working).

Also verify the dependency-name convention now: `grep -n "Dependencies\|FileName.ToLower\|dep.Name" Assets/Game/Addons/ModSupport/ModManager.cs | head` and adjust the DDE manifest's `Dependencies[0].Name` to whatever the engine compares (lower-cased file stem or GUID).

- [ ] **Step 8: Commit**

```bash
cd ~/dev/daggerfall-unity
git add tools/bundled-mods
git -c user.name=Codex64ai -c user.email=ikrammassabini@gmail.com commit -F - <<'EOF'
Bundled mods: pin thirteen MIT Cliffworms mods and a fetch script that refuses the unsafe

Thirteen data-only Cliffworms mods, all MIT with a LICENSE file, fetched at pinned commits
into the gitignored Assets/Game/Mods/. Two manifests are ours: Main Quest Consequences
forgot its control quest upstream (so cleared-dungeon variants never fired) and Detailed
Dungeon Exteriors ships none. The script stops on a .cs, an undeclared QuestList, an
uppercase image extension, a duplicate list name or GUID, or a non-MIT LICENSE.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01CwxAHZmWjgN85ZLMTx81ZM
EOF
```

---

### Task 2: The iOS mod scanner reads the shipped Mods folder too

**Files:**
- Modify: `Assets/Game/Addons/ModSupport/ModManager.cs:591-644` (`FindModsFromDirectory`)
- Modify: `Assets/Game/Addons/ModSupport/ModManager.cs` near line 340 (add `MergeModFiles` next to `GetAllModFileNames`)
- Test: `Assets/Editor/MobileSelfTest.cs` (new `TestMergeModFiles`, registered in `RunAll` after `TestUserContentFolders();`)

**Interfaces:**
- Produces: `public static string[] ModManager.MergeModFiles(string[] playerFiles, string[] shippedFiles)` — player files first in their given order, then shipped files whose file name (via `GetModNameFromPath`) is not already present. Pure, no I/O.
- Produces: `public static string ModManager.ShippedModDirectory` → `Path.Combine(Application.streamingAssetsPath, "Mods")`.

- [ ] **Step 1: Write the failing self-test**

In `Assets/Editor/MobileSelfTest.cs`, add after `TestUserContentFolders()`:

```csharp
        /// <summary>
        /// On iOS two folders hold .dfmod files: the player's Documents/Mods and the shipped
        /// StreamingAssets/Mods with the bundled MIT mods. The scanner merges them, player
        /// first, and a shipped file whose name the player also has is dropped - so a player
        /// who installs their own copy of a bundled mod gets theirs, not ours.
        /// </summary>
        static void TestMergeModFiles()
        {
            string p = "/Documents/Mods/", s = "/App/StreamingAssets/Mods/";

            string[] r = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.MergeModFiles(
                new string[0], new[] { s + "JOTG.dfmod", s + "FixedDungeonExteriors.dfmod" });
            Check(r.Length == 2 && r[0] == s + "JOTG.dfmod", "shipped-only: all shipped files, in order");

            r = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.MergeModFiles(
                new[] { p + "dream-sound.dfmod" }, new string[0]);
            Check(r.Length == 1 && r[0] == p + "dream-sound.dfmod", "player-only: unchanged");

            r = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.MergeModFiles(
                new[] { p + "dream-sound.dfmod" }, new[] { s + "JOTG.dfmod" });
            Check(r.Length == 2 && r[0] == p + "dream-sound.dfmod" && r[1] == s + "JOTG.dfmod",
                  "disjoint: player first, then shipped");

            r = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.MergeModFiles(
                new[] { p + "JOTG.dfmod" }, new[] { s + "JOTG.dfmod", s + "VariedWealthyHomes.dfmod" });
            Check(r.Length == 2 && r[0] == p + "JOTG.dfmod" && r[1] == s + "VariedWealthyHomes.dfmod",
                  "same file name in both: the player's copy is kept and the shipped one dropped");

            r = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.MergeModFiles(null, null);
            Check(r != null && r.Length == 0, "null inputs give an empty list, not an exception");
        }
```

and register it in `RunAll()` immediately after the line `TestUserContentFolders();`:

```csharp
            TestMergeModFiles();
```

- [ ] **Step 2: Run the self-test to see the compile failure**

Run: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile /private/tmp/claude-501/-Users-ikrammassabini/f66b5ce5-1009-4260-a7c6-831c8eeb2e5a/scratchpad/selftest-t2-red.log; grep -n "error CS\|MergeModFiles" /private/tmp/claude-501/-Users-ikrammassabini/f66b5ce5-1009-4260-a7c6-831c8eeb2e5a/scratchpad/selftest-t2-red.log | head`
Expected: `error CS0117: 'ModManager' does not contain a definition for 'MergeModFiles'`

- [ ] **Step 3: Implement `MergeModFiles` and the two-root scan**

In `ModManager.cs`, directly after `GetAllModFileNames()` (around line 340-350), add:

```csharp
        // MOBILE: the shipped Mods folder inside the app bundle. On iOS it holds the bundled
        // MIT mods and is read-only; ModDirectory (Documents/Mods) stays the writable one
        // where Mods.json and per-mod settings live. Elsewhere the two are the same folder.
        public static string ShippedModDirectory
        {
            get { return Path.Combine(Application.streamingAssetsPath, "Mods"); }
        }

        /// <summary>
        /// MOBILE: merge the player's .dfmod files with the shipped ones. Player files come first
        /// and win: a shipped file with the same file name is dropped, so a player who installs
        /// their own copy of a bundled mod is not shadowed by ours. Pure so the self-test can
        /// pin the rule on the Mac.
        /// </summary>
        public static string[] MergeModFiles(string[] playerFiles, string[] shippedFiles)
        {
            var merged = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (playerFiles != null)
                foreach (string f in playerFiles)
                {
                    string n = GetModNameFromPath(f);
                    if (!string.IsNullOrEmpty(n) && names.Add(n))
                        merged.Add(f);
                }
            if (shippedFiles != null)
                foreach (string f in shippedFiles)
                {
                    string n = GetModNameFromPath(f);
                    if (!string.IsNullOrEmpty(n) && names.Add(n))
                        merged.Add(f);
                }
            return merged.ToArray();
        }
```

(`System`, `System.Collections.Generic`, `System.IO` and `System.Linq` are already imported at the top of `ModManager.cs`; confirm with `sed -n '1,30p'`.)

Then replace lines 593-599 of `FindModsFromDirectory` — from `if (!Directory.Exists(ModDirectory))` through `var modFiles = Directory.GetFiles(...)` — with:

```csharp
            // MOBILE: on iOS the player's Documents/Mods and the app's shipped Mods folder are
            // both scanned, player first (see MergeModFiles). Off iOS ShippedModDirectory IS
            // ModDirectory, so the merge is a no-op and behaviour is unchanged.
            string[] playerFiles = Directory.Exists(ModDirectory)
                ? Directory.GetFiles(ModDirectory, "*" + MODEXTENSION, SearchOption.AllDirectories)
                : new string[0];
            string[] shippedFiles = new string[0];
            if (MobileContentPath.Active && Directory.Exists(ShippedModDirectory)
                && !string.Equals(Path.GetFullPath(ShippedModDirectory), Path.GetFullPath(ModDirectory), StringComparison.Ordinal))
            {
                shippedFiles = Directory.GetFiles(ShippedModDirectory, "*" + MODEXTENSION, SearchOption.AllDirectories);
            }
            if (playerFiles.Length == 0 && shippedFiles.Length == 0 && !Directory.Exists(ModDirectory))
            {
                Debug.Log("invalid mod directory: " + ModDirectory);
                return;
            }
            var modFiles = MergeModFiles(playerFiles, shippedFiles);
```

Leave the rest of the method (lines 600-644) untouched: `modFileNames`, the loop, `LoadPriority = i`, the `GetModIndex(mod.Title) < 0` guard and the `refresh` unload loop all operate on `modFiles` and need no change. Add `using DaggerfallWorkshop.Game.Mobile;` at the top of the file if `MobileContentPath` does not resolve (the `Awake` at line 142 already uses it, so it should).

- [ ] **Step 4: Run the self-test to see it pass**

Run the same Unity command with `-logFile .../selftest-t2-green.log`, then:
`grep -c "  PASS" .../selftest-t2-green.log; grep "  FAIL" .../selftest-t2-green.log; grep -n "self test\|passed\|failed" .../selftest-t2-green.log | tail -3`
Expected: no `FAIL` lines; the five new checks appear as `PASS`; total passed = previous 476 + 5 = 481.

- [ ] **Step 5: Sync the self-test to staging and commit**

```bash
cd ~/dev/daggerfall-unity
diff ~/daggerfall-mobile/editor/MobileSelfTest.cs <(git show HEAD:Assets/Editor/MobileSelfTest.cs) > /dev/null && echo "staging matched HEAD - safe to copy" || echo "STAGING DIFFERS FROM HEAD - inspect before copying"
cp Assets/Editor/MobileSelfTest.cs ~/daggerfall-mobile/editor/MobileSelfTest.cs
git add Assets/Game/Addons/ModSupport/ModManager.cs Assets/Editor/MobileSelfTest.cs
git -c user.name=Codex64ai -c user.email=ikrammassabini@gmail.com commit -F - <<'EOF'
Mods: on iOS the scanner also reads the app's shipped Mods folder, player copy first

ModDirectory was a straight redirect to Documents/Mods, so nothing inside the app bundle
could ever be a mod. Now both folders are scanned and merged by file name with the
player's copy winning, which is what lets the bundled MIT mods ship inside the app while
a player who installs their own build of the same mod still gets theirs. Five self-test
checks pin the merge rule.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01CwxAHZmWjgN85ZLMTx81ZM
EOF
```

If the `diff` printed "STAGING DIFFERS", stop and diff `~/daggerfall-mobile/editor/MobileSelfTest.cs` against `git show HEAD:Assets/Editor/MobileSelfTest.cs` to see whether staging carries anything the repo lacks before overwriting it.

---

### Task 3: Build the bundles as part of ApplyAll, with licences beside them

**Files:**
- Modify: `Assets/Editor/MobileModBuilder.cs:30-75` (`BuildMod` gains `bool flatOutput = false`)
- Modify: `Assets/Editor/MobileBuildSetup.cs:465-494` (`ApplyAll` calls `BuildBundledMods()`; new method)
- Test: `Assets/Editor/MobileSelfTest.cs` (new `TestBundledModManifests`, `TestBundledModLicences`)

**Interfaces:**
- Consumes: `MobileModBuilder.BuildMod(string manifestPath, string outputRoot, BuildTarget[] targets)` (existing); `ModManager.MODINFOEXTENSION` (`.dfmod.json`), `ModManager.MODEXTENSION` (`.dfmod`), `ModManager._serializer`.
- Produces: `public static string[] MobileModBuilder.BuildMod(string manifestPath, string outputRoot, BuildTarget[] targets, bool flatOutput = false)` — when `flatOutput` is true the bundle is written to `outputRoot/<stem>.dfmod` with no per-target subfolder. `public static string[] MobileBuildSetup.BundledManifests()` — every `*.dfmod.json` under `Assets/Game/Mods/` except any under `Assets/Game/Mods/IOSPilot/`. `public static void MobileBuildSetup.BuildBundledMods()` — builds each for `BuildTarget.iOS` into `Assets/StreamingAssets/Mods/` and copies `<moddir>/LICENSE` to `Assets/StreamingAssets/Mods/Licenses/<stem>-LICENSE.txt`. `public const string MobileBuildSetup.ShippedModsPath = "Assets/StreamingAssets/Mods"`.

- [ ] **Step 1: Write the failing self-tests**

In `MobileSelfTest.cs`, after `TestMergeModFiles()`:

```csharp
        /// <summary>
        /// Every fetched mod manifest (Assets/Game/Mods/*, minus the IOSPilot fixture) must
        /// parse, name no script, and reference only files that exist - the same rule the
        /// fetch script applies, enforced from the editor so a stale or hand-edited fetch
        /// cannot reach a build. Skips with a note when nothing has been fetched.
        /// </summary>
        static void TestBundledModManifests()
        {
            string[] manifests = MobileBuildSetup.BundledManifests();
            if (manifests.Length == 0)
            {
                log.AppendLine("  SKIP  bundled mod manifests (none fetched - run tools/bundled-mods/fetch.py)");
                return;
            }
            Check(manifests.Length == 13, "thirteen bundled mod manifests are present", manifests.Length + " found");
            Check(!manifests.Any(m => m.Replace('\\', '/').Contains("/IOSPilot/")), "IOSPilot is never bundled");

            int bad = 0;
            var titles = new HashSet<string>();
            foreach (string path in manifests)
            {
                DaggerfallWorkshop.Game.Utility.ModSupport.ModInfo info = null;
                bool ok = !DaggerfallWorkshop.Game.Utility.ModSupport.ModManager._serializer.TryDeserialize(
                    FullSerializer.fsJsonParser.Parse(File.ReadAllText(path)), ref info).Failed && info != null;
                if (!ok || string.IsNullOrWhiteSpace(info.ModTitle) || !titles.Add(info.ModTitle)) { bad++; continue; }
                if (info.Files == null || info.Files.Count == 0) { bad++; continue; }
                if (info.Files.Any(f => f.EndsWith(".cs") || f.EndsWith(".dll.bytes"))) { bad++; continue; }
                if (info.Files.Any(f => !File.Exists(f))) { bad++; continue; }
            }
            Check(bad == 0, "every bundled manifest parses, has a unique title, no scripts, and all files present",
                  bad + " bad");
        }

        /// <summary>
        /// MIT requires the notice to travel with the copy, so each shipped bundle must have its
        /// LICENSE beside it. Only meaningful after BuildBundledMods has run; skips otherwise.
        /// </summary>
        static void TestBundledModLicences()
        {
            string root = MobileBuildSetup.ShippedModsPath;
            string[] bundles = Directory.Exists(root)
                ? Directory.GetFiles(root, "*" + DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.MODEXTENSION, SearchOption.TopDirectoryOnly)
                : new string[0];
            if (bundles.Length == 0)
            {
                log.AppendLine("  SKIP  bundled mod licences (no bundles built yet - run MobileBuildSetup.BuildBundledMods)");
                return;
            }
            int missing = 0;
            foreach (string b in bundles)
            {
                string stem = Path.GetFileNameWithoutExtension(b);
                string lic = Path.Combine(root, "Licenses", stem + "-LICENSE.txt");
                if (!File.Exists(lic) || !File.ReadAllText(lic).TrimStart().StartsWith("MIT License"))
                    missing++;
            }
            Check(missing == 0, "every shipped bundle has an MIT LICENSE beside it", missing + " missing");
            Check(bundles.Length == MobileBuildSetup.BundledManifests().Length,
                  "one bundle per fetched manifest", bundles.Length + " bundles");
        }
```

Register both in `RunAll()` right after `TestMergeModFiles();`:

```csharp
            TestBundledModManifests();
            TestBundledModLicences();
```

`MobileSelfTest.cs` already has `using System.IO;`, `using System.Linq;` and `using System.Collections.Generic;` (confirm with `sed -n '1,40p'`); add any that are missing.

- [ ] **Step 2: Run the self-test to see the compile failure**

Same Unity command, `-logFile .../selftest-t3-red.log`. Expected: `error CS0117: 'MobileBuildSetup' does not contain a definition for 'BundledManifests'` (and `ShippedModsPath`).

- [ ] **Step 3: Add `flatOutput` to `MobileModBuilder.BuildMod`**

Change the signature at line 30 to:

```csharp
        public static string[] BuildMod(string manifestPath, string outputRoot, BuildTarget[] targets, bool flatOutput = false)
```

and replace the loop body (lines 64-73) with:

```csharp
            foreach (BuildTarget target in targets)
            {
                // flatOutput: the bundle goes straight into outputRoot (the app's shipped Mods
                // folder, scanned recursively but kept flat for tidiness). Otherwise the
                // per-target subfolder the DREAM workflow expects.
                string dir = flatOutput ? outputRoot : Path.Combine(outputRoot, target.ToString());
                Directory.CreateDirectory(dir);
                if (BuildPipeline.BuildAssetBundles(dir, buildMap,
                        BuildAssetBundleOptions.ChunkBasedCompression, target) == null)
                    throw new Exception("BuildAssetBundles failed for " + fileName + " (" + target + ")");
                built.Add(Path.Combine(dir, fileName));
            }
```

`BuildAssetBundles` also writes a manifest bundle named after the folder (`Mods` and `Mods.manifest`) plus `<name>.dfmod.manifest`. Those must not ship: after the loop, when `flatOutput` is true, delete `Path.Combine(dir, Path.GetFileName(dir))`, that path + `.manifest`, and `Path.Combine(dir, fileName + ".manifest")` if they exist.

- [ ] **Step 4: Add `BuildBundledMods` to `MobileBuildSetup`**

Before `ApplyAll()` (around line 459) add:

```csharp
        /// <summary>Where bundled mods are written; scanned by ModManager on iOS.</summary>
        public const string ShippedModsPath = "Assets/StreamingAssets/Mods";
        const string BundledSourceRoot = "Assets/Game/Mods";

        /// <summary>
        /// Every fetched bundled-mod manifest. IOSPilot is a build-path fixture with placeholder
        /// art and already loads as a virtual mod in the editor, so it is never bundled.
        /// </summary>
        public static string[] BundledManifests()
        {
            if (!Directory.Exists(BundledSourceRoot))
                return new string[0];
            return Directory.GetFiles(BundledSourceRoot, "*" + ModManager.MODINFOEXTENSION, SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !p.Contains("/IOSPilot/"))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Build the bundled MIT mods (tools/bundled-mods/fetch.py puts their sources under
        /// Assets/Game/Mods/) into iOS AssetBundles in the shipped Mods folder, each with its
        /// LICENSE beside it. Part of ApplyAll; also its own -executeMethod for iteration:
        ///   -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.BuildBundledMods
        /// A clone that never ran fetch.py still builds an app - just without the bundle.
        /// </summary>
        public static void BuildBundledMods()
        {
            string[] manifests = BundledManifests();
            if (manifests.Length == 0)
            {
                Debug.LogWarning("[MobileBuildSetup] no bundled mods under " + BundledSourceRoot +
                                 " - run tools/bundled-mods/fetch.py. Building without them.");
                return;
            }

            string licDir = Path.Combine(ShippedModsPath, "Licenses");
            Directory.CreateDirectory(licDir);

            // Stale bundles from a previous fetch must not linger.
            foreach (string old in Directory.GetFiles(ShippedModsPath, "*" + ModManager.MODEXTENSION))
                File.Delete(old);
            foreach (string old in Directory.GetFiles(licDir, "*-LICENSE.txt"))
                File.Delete(old);

            foreach (string manifest in manifests)
            {
                string[] built = MobileModBuilder.BuildMod(manifest, ShippedModsPath, new[] { BuildTarget.iOS }, flatOutput: true);
                string modDir = Path.GetDirectoryName(manifest);
                string licence = Path.Combine(modDir, "LICENSE");
                if (!File.Exists(licence))
                    throw new FileNotFoundException("bundled mod has no LICENSE - refusing to ship it", manifest);
                string stem = Path.GetFileName(manifest).Replace(ModManager.MODINFOEXTENSION, "");
                File.Copy(licence, Path.Combine(licDir, stem + "-LICENSE.txt"), true);
                Debug.Log("[MobileBuildSetup] bundled " + string.Join(", ", built));
            }
            AssetDatabase.Refresh();
            Debug.Log("[MobileBuildSetup] bundled mods: " + manifests.Length);
        }
```

Add `using DaggerfallWorkshop.Game.Utility.ModSupport;` to the file's usings (for `ModManager`). In `ApplyAll()`, insert a call as the first statement after `ApplyIOSSettings();`:

```csharp
            // Bundled MIT mods first: they are plain asset bundles and independent of the scene.
            BuildBundledMods();
```

- [ ] **Step 5: Build the bundles and run the self-test**

Run: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.BuildBundledMods -logFile .../bundle.log; grep -n "MobileBuildSetup\]\|error CS\|Exception" .../bundle.log | head -30; ls -la ~/dev/daggerfall-unity/Assets/StreamingAssets/Mods ~/dev/daggerfall-unity/Assets/StreamingAssets/Mods/Licenses`
Expected: 13 `bundled ...` lines, 13 `.dfmod` files, 13 `-LICENSE.txt`, no `.manifest` files, no `Mods` bundle file. Then run the self-test (`-logFile .../selftest-t3-green.log`). Expected: no `FAIL`; `TestBundledModManifests` 3 checks and `TestBundledModLicences` 2 checks pass; total passed 486.

Also confirm `git status --short` shows only the three edited files, nothing under `Assets/StreamingAssets/Mods/` or `Assets/Game/Mods/`. Note that Unity creates `.meta` files for the fetched sources and bundles; both gitignores cover them.

- [ ] **Step 6: Sync staging and commit**

```bash
cd ~/dev/daggerfall-unity
cp Assets/Editor/MobileSelfTest.cs ~/daggerfall-mobile/editor/MobileSelfTest.cs
ls ~/daggerfall-mobile/editor/ | grep -i "MobileBuildSetup\|MobileModBuilder" && echo "staging has editor twins - copy those too" 
git add Assets/Editor/MobileModBuilder.cs Assets/Editor/MobileBuildSetup.cs Assets/Editor/MobileSelfTest.cs
git -c user.name=Codex64ai -c user.email=ikrammassabini@gmail.com commit -F - <<'EOF'
Build: ApplyAll bundles the fetched MIT mods into the shipped Mods folder with their licences

MobileBuildSetup.BuildBundledMods runs every manifest under Assets/Game/Mods (never the
IOSPilot fixture) through MobileModBuilder for iOS, flat into StreamingAssets/Mods, and
copies each LICENSE to Licenses/<name>-LICENSE.txt. Stale bundles are cleared first and the
AssetBundle side-manifests are not shipped. Two self-tests: every fetched manifest is sane,
and every shipped bundle has an MIT licence beside it.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01CwxAHZmWjgN85ZLMTx81ZM
EOF
```

If staging holds twins of `MobileBuildSetup.cs` / `MobileModBuilder.cs`, copy the repo versions over them after confirming (with `diff` against `git show HEAD~1:<path>`) that staging did not carry unmerged changes.

---

### Task 4: Attribution and documentation

**Files:**
- Modify: `THIRD-PARTY.md` (append a section)
- Modify: `README-iOS.md` (new section after "Installing mods", around line 405; a line in "Licence and credits")
- Modify: `~/daggerfall-mobile/RELEASE.md` (recipe step 1a)
- Sync: `cp README-iOS.md ~/daggerfall-mobile/README-iOS.md`

- [ ] **Step 1: Append to THIRD-PARTY.md**

```markdown

## Bundled mods

Thirteen Daggerfall Unity mods by **Cliffworms** ship inside the iOS app as `.dfmod` bundles,
each switchable in the launcher's MODS window. All are MIT licensed (`Copyright (c) 2025
Cliffworms`); the licence text ships in the app at `StreamingAssets/Mods/Licenses/`. They are
fetched at the pinned commits by `tools/bundled-mods/fetch.py` and are not part of this
repository's history.

| Mod | Repository | Commit | Manifest |
|---|---|---|---|
| Fixed Dungeon Exteriors | https://github.com/Cliffworms/FixedDungeonExteriors | f384bb3f | upstream |
| Varied Wealthy Homes | https://github.com/Cliffworms/VariedWealthyHomes | 085a9f2a | upstream |
| Main Quest Consequences | https://github.com/Cliffworms/MainQuestConsequences | 1cca6534 | ours: adds the control quest upstream's manifest omits |
| Detailed Dungeon Exteriors | https://github.com/Cliffworms/DetailedDungeonExteriors | 6d886ad9 | ours: upstream ships none |
| Detailed Main Quest Dungeons | https://github.com/Cliffworms/DetailedMainQuestDungeons | 045022ef | upstream |
| Aquatic Sprites | https://github.com/Cliffworms/AquaticSprites | ea195e77 | upstream |
| Smaller Main Quest Dungeons | https://github.com/Cliffworms/SmallerMQDungeons | 51dc8db3 | upstream |
| Leveling Inspiration | https://github.com/Cliffworms/LevelingInspiration | 37aefbbe | upstream |
| Skyrim's Adventures | https://github.com/Cliffworms/SkyrimsAdventures | e5083f29 | upstream |
| Jobs of the Thieves Guild | https://github.com/Cliffworms/JOTG | 701440f3 | upstream |
| Arena's Adventures | https://github.com/Cliffworms/ArenasAdventures | 9352a928 | upstream |
| Town Greetings of the Iliac Bay | https://github.com/Cliffworms/TownGreetingsIliacBay | 203f9d2a | upstream |
| Rumors of the Iliac Bay | https://github.com/Cliffworms/RumorsOfTheIliacBay | b5641cd1 | upstream |

Our two manifests are in `tools/bundled-mods/manifests/`. The data in every bundle is
Cliffworms' work, unmodified except that one texture file name was lower-cased for iOS.
```

- [ ] **Step 2: Add a "Bundled mods" section to README-iOS.md**

Insert before the `### Converting a desktop `.dfmod`` heading (find it with `grep -n "^### Converting a desktop" README-iOS.md`):

```markdown
### Bundled mods

Thirteen of Cliffworms' MIT-licensed mods ship inside the app and appear in the launcher's
MODS window like any other mod, switched on by default:

- **World:** Fixed Dungeon Exteriors, Detailed Dungeon Exteriors, Varied Wealthy Homes,
  Main Quest Consequences, Smaller Main Quest Dungeons, Detailed Main Quest Dungeons,
  Aquatic Sprites.
- **Quests:** Leveling Inspiration, Skyrim's Adventures, Jobs of the Thieves Guild, Arena's
  Adventures, Town Greetings of the Iliac Bay, Rumors of the Iliac Bay.

Untick any of them in MODS to switch it off; the choice persists. If you install your own
copy of one of these mods into `Documents/Mods`, yours is used and the bundled one is ignored.
Authors, licences and pinned versions are in `THIRD-PARTY.md`; the licence texts ship in the
app. Detailed Dungeon Exteriors needs Fixed Dungeon Exteriors on.

```

Then in "Licence and credits", after the paragraph about the two compiled-in works, add:

```markdown
Thirteen MIT-licensed mods by Cliffworms are bundled as `.dfmod` files - see `THIRD-PARTY.md`.
```

- [ ] **Step 3: Add the fetch step to the release recipe**

In `~/daggerfall-mobile/RELEASE.md`, under "Cutting a public pre-alpha (Unity 6 line)", insert after step 1:

```markdown
1a. Fetch the bundled MIT mods (idempotent; needed on a fresh clone or after changing
    `tools/bundled-mods/mods.json`): `python3 tools/bundled-mods/fetch.py` and confirm it ends
    `13 mods, 0 problems`. `ApplyAll` then builds them into `StreamingAssets/Mods/`. The
    self-test's "bundled mod" checks say SKIP, not PASS, if this was forgotten.
```

- [ ] **Step 4: Commit and sync**

```bash
cd ~/dev/daggerfall-unity
cp README-iOS.md ~/daggerfall-mobile/README-iOS.md
git add THIRD-PARTY.md README-iOS.md
git -c user.name=Codex64ai -c user.email=ikrammassabini@gmail.com commit -F - <<'EOF'
Docs: credit the thirteen bundled Cliffworms mods and tell players how to switch them off

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01CwxAHZmWjgN85ZLMTx81ZM
EOF
git push fork unity6-upgrade
```

---

### Task 5: Test app build and hand-off

**Files:** none edited. Outputs: `~/dev/dfu-ios-build/` (Unity → Xcode project), `~/Desktop/DFU-Test-unity6-mitmods.ipa`, the draft release asset.

- [ ] **Step 1: Verify the sources and bundles are present**

Run: `cd ~/dev/daggerfall-unity && python3 tools/bundled-mods/fetch.py --check && ls Assets/StreamingAssets/Mods/*.dfmod | wc -l`
Expected: `13 mods, 0 problems` and `13`.

- [ ] **Step 2: ApplyAll (rebuilds bundles) then BuildIOS as the test app**

Run:
```bash
U=/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity
L=/private/tmp/claude-501/-Users-ikrammassabini/f66b5ce5-1009-4260-a7c6-831c8eeb2e5a/scratchpad
$U -batchmode -quit -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.ApplyAll -logFile $L/applyall.log
grep -n "MobileBuildSetup\]\|error CS\|Exception" $L/applyall.log | head -40
env DFU_IOS_TESTAPP=1 $U -batchmode -quit -buildTarget iOS -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.BuildIOS -logFile $L/buildios.log
grep -n "Build succeeded\|Build Finished\|error CS\|Exception\|BuildIOS" $L/buildios.log | head -20
```
Expected: `bundled mods: 13`, `ApplyAll complete`, and a successful iOS build into `~/dev/dfu-ios-build`. Confirm the bundles made it: `ls ~/dev/dfu-ios-build/Data/Raw/Mods/ | head -20` should show 13 `.dfmod` and `Licenses/`.

- [ ] **Step 3: Xcode, unsigned**

```bash
cd ~/dev/dfu-ios-build && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination 'generic/platform=iOS' -derivedDataPath ./DerivedData CODE_SIGNING_ALLOWED=NO build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`. The app is at `./DerivedData/Build/Products/Release-iphoneos/DFUTest.app` (the test-app product name; `ls` that folder to confirm the exact name).

- [ ] **Step 4: Zip the ipa and upload to the draft**

```bash
cd ~/dev/dfu-ios-build/DerivedData/Build/Products/Release-iphoneos && rm -rf Payload && mkdir Payload && cp -R *.app Payload/ && rm -f ~/Desktop/DFU-Test-unity6-mitmods.ipa && zip -qr ~/Desktop/DFU-Test-unity6-mitmods.ipa Payload && ls -la ~/Desktop/DFU-Test-unity6-mitmods.ipa
unzip -l ~/Desktop/DFU-Test-unity6-mitmods.ipa | grep -c "Data/Raw/Mods/.*\.dfmod$"
gh release upload testapp-unity6 ~/Desktop/DFU-Test-unity6-mitmods.ipa --clobber -R Codex64ai/daggerfall-unity-ios
gh release view testapp-unity6 -R Codex64ai/daggerfall-unity-ios --json isDraft,assets --jq '{isDraft, mitmods: [.assets[] | select(.name=="DFU-Test-unity6-mitmods.ipa") | {name,size,url}]}'
```
Expected: the ipa contains 13 `.dfmod` entries; `isDraft: true` is still true; the asset URL is printed. Hand Ikram the release page URL `https://github.com/Codex64ai/daggerfall-unity-ios/releases/tag/testapp-unity6` (he downloads the asset while signed in) and the on-device checklist from the spec: 16 entries in MODS, a switch persists across restart, a dungeon exterior differs, no "QuestList already registered" in Player.log.

- [ ] **Step 5: Record state in memory**

Update `~/.claude/projects/-Users-ikrammassabini/memory/daggerfall-ios-port.md`: the HANDOFF block's STATE line (new tip commit), roadmap item 3 progress ("phase 1 bundled mods built; test ipa DFU-Test-unity6-mitmods.ipa on the draft; device verification PENDING"), and the two operational facts: `fetch.py` must run on a fresh clone before ApplyAll, and the shipped-Mods scan is iOS-only.
