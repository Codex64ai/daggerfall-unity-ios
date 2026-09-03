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
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MODS_JSON = os.path.join(HERE, "mods.json")
IMAGE_EXTS = (".png", ".jpg", ".jpeg", ".tga")
SCRIPT_EXTS = (".cs", ".dll.bytes", ".dll")

# The 472 TEXTURE.nnn archives in a vanilla arena2 (0..511 with gaps). A WorldData block whose
# flats point anywhere else needs a texture mod we do not ship - and a missing flat archive is
# not a missing sprite, it is an IndexOutOfRange inside the block builder that aborts the whole
# dungeon (2026-09-01: Detailed Main Quest Dungeons blacked out Privateer's Hold this way).
VANILLA_TEXTURE_ARCHIVES = frozenset(int(x) for x in "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,22,23,24,25,26,27,28,29,30,31,33,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,123,124,125,126,127,128,129,130,131,132,133,134,135,136,137,138,139,140,141,142,143,144,145,146,147,148,149,150,151,152,153,154,155,156,157,158,159,160,161,162,163,164,165,166,167,168,169,170,171,172,173,174,175,176,177,178,179,180,181,182,183,184,185,186,190,194,195,197,198,199,200,201,202,203,204,205,206,207,208,209,210,211,212,213,214,215,216,217,218,233,234,235,236,237,238,239,240,241,242,245,246,247,248,249,250,251,252,253,254,255,256,257,258,259,260,261,262,263,264,265,266,267,268,269,270,271,272,273,274,275,276,277,278,279,280,281,282,283,284,285,286,287,288,289,290,291,292,293,295,296,297,298,299,300,301,302,303,304,305,306,307,308,309,310,311,312,313,314,315,316,317,318,319,320,321,322,323,324,325,326,327,328,329,330,331,332,333,334,335,336,337,338,339,340,341,342,343,344,345,346,347,348,349,350,351,352,353,354,355,356,357,358,359,360,361,362,363,364,365,366,368,369,370,371,372,374,375,376,377,378,379,380,381,382,383,384,385,386,387,388,389,390,391,392,393,394,395,396,397,398,399,400,401,402,403,404,405,406,407,408,409,410,411,412,413,414,415,416,417,418,419,420,422,423,424,425,426,427,428,429,430,431,432,433,434,435,436,437,438,439,440,442,443,444,445,446,447,448,449,450,451,452,453,454,455,456,457,458,459,460,461,462,463,464,465,466,467,468,469,470,473,474,475,476,477,478,479,480,481,482,483,484,485,486,487,488,489,490,491,492,493,494,495,500,501,502,503,504,505,506,507,508,509,510,511".split(","))


# ----------------------------------------------------------------- validation (pure)

def _rel(path):
    """'Assets/Game/Mods/X/WorldData/a.json' -> 'WorldData/a.json'"""
    parts = path.replace("\\", "/").split("/")
    return "/".join(parts[4:])


def payload_roots(manifest):
    roots = set()
    for f in manifest.get("Files", []):
        parts = f.replace("\\", "/").split("/")
        # Assets/Game/Mods/<Name>/<Root>/...
        if len(parts) > 5:
            roots.add(parts[4])
    return roots


def normalize_paths(manifest, name):
    """Point every Files entry at Assets/Game/Mods/<name>/... - authors' manifests carry
    whatever folder name they used locally (AquaticSprites ships as UnderwaterSprites/)."""
    fixed = dict(manifest)
    fixed["Files"] = ["Assets/Game/Mods/%s/%s" % (name, _rel(f)) for f in manifest.get("Files", [])]
    return fixed


def validate_manifest(manifest, mod_dir):
    problems = []
    files = manifest.get("Files") or []
    if not files:
        problems.append("Files is empty")
    expected_prefix = "Assets/Game/Mods/%s/" % os.path.basename(os.path.normpath(mod_dir))
    for f in files:
        if not f.replace("\\", "/").startswith(expected_prefix):
            problems.append("path is outside this mod's folder (author's local name?): " + f)
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


def worlddata_archive_problems(manifest, mod_dir, provided_by_pack=False):
    """Flat texture archives referenced by the mod's WorldData blocks that vanilla does not have.
    provided_by_pack: the pin declares (archives_from) a texture pack in this same pack that
    supplies the extra archives - Daggerfall Expanded Textures for the Cliffworms dungeon mods."""
    import re
    problems = []
    if provided_by_pack:
        return problems
    for f in manifest.get("Files") or []:
        rel = _rel(f)
        if not (rel.startswith("WorldData/") and rel.endswith(".json")):
            continue
        path = os.path.join(mod_dir, rel)
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8", errors="replace") as fh:
            raw = fh.read()
        missing = sorted(set(int(a) for a in re.findall(r'"TextureArchive":\s*(\d+)', raw)
                             if int(a) not in VANILLA_TEXTURE_ARCHIVES))
        if missing:
            problems.append("%s uses texture archives not in vanilla arena2 %s (needs a texture mod we do not ship)"
                            % (os.path.basename(rel), missing))
    return problems


def validate_set(manifests):
    problems = []
    seen_lists, seen_guids = {}, {}
    shipped = set(m.get("_stem", "").lower() for m in manifests)
    for m in manifests:
        title = m.get("ModTitle", "?")
        for dep in m.get("Dependencies") or []:
            if not dep.get("IsOptional") and dep.get("Name", "").lower() not in shipped:
                problems.append("%s REQUIRES '%s', which this pack does not ship" % (title, dep.get("Name")))
        for ql in (m.get("Contributes") or {}).get("QuestLists") or []:
            if ql in seen_lists:
                problems.append("QuestList %s in both %s and %s (engine drops the second)"
                                % (ql, seen_lists[ql], title))
            seen_lists[ql] = title
        guid = m.get("GUID")
        if guid:
            if guid in seen_guids:
                problems.append("GUID %s shared by %s and %s" % (guid, seen_guids[guid], title))
            seen_guids[guid] = title
    return problems


def licence_problems(text, allow_permission=False, accept_substring=None):
    stripped = (text or "").strip()
    first = stripped.splitlines()[0].strip() if stripped else ""
    if first == "MIT License":
        return []
    if accept_substring and accept_substring in stripped:
        return []          # e.g. UBLaMF's License.md: CC BY-NC-SA 4.0 stated on its third line
    if allow_permission and first == "Permission" and len(stripped.splitlines()) > 1:
        return []
    return ["LICENSE first line is %r, expected 'MIT License'%s" % (first, " or 'Permission'" if allow_permission else "")]


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


# ----------------------------------------------------------------- generated manifests

import uuid
NON_QUEST_TXT = ("readme", "overview", "instructions", "changelog", "license", "licence")


def is_quest_txt(filename):
    """Quest scripts and QuestLists are .txt; READMEs are too. Tell them apart by name."""
    stem = os.path.splitext(os.path.basename(filename))[0].lower()
    return filename.lower().endswith(".txt") and not any(stem.startswith(n) for n in NON_QUEST_TXT)


def generate_manifest(name, files, title, author, description, renames=None, version="1.0"):
    """A .dfmod.json for a folder of loose quest files (Jay_H's packs ship none).

    files: relative paths under the pack (e.g. 'QuestPacks/JHRLQ/QuestList-RLQ.txt'). Every
    QuestList-*.txt becomes a Contributes.QuestLists entry and every other quest .txt a
    LooseQuestsList entry - the two declarations the engine needs to offer the quests at all.
    renames maps a QuestList name to a new one (the file is renamed by the caller) so two packs
    that both ship e.g. QuestList-IronmanMadness can coexist; the engine silently drops the
    second list of a name otherwise. The GUID is derived from the name so a rebuild is the same
    mod to the engine and to a player's saved Mods.json.
    """
    renames = renames or {}
    quest_lists, loose = [], []
    out_files = []
    for rel in sorted(files):
        if not is_quest_txt(rel):
            continue
        stem = os.path.splitext(os.path.basename(rel))[0]
        if stem.startswith("QuestList-"):
            ln = stem[len("QuestList-"):]
            new = renames.get(ln, ln)
            quest_lists.append(new)
            if new != ln:
                rel = os.path.join(os.path.dirname(rel), "QuestList-" + new + ".txt").replace("\\", "/")
        else:
            loose.append(stem)
        out_files.append("Assets/Game/Mods/%s/%s" % (name, rel))
    return {
        "ModTitle": title,
        "ModVersion": version,
        "ModAuthor": author,
        "ContactInfo": "",
        "DFUnity_Version": "1.1.1",
        "ModDescription": description,
        "GUID": str(uuid.uuid5(uuid.NAMESPACE_URL, "net.codex64.daggerfall/bundled-mods/" + name)),
        "Files": out_files,
        "Contributes": {"QuestLists": quest_lists, "LooseQuestsList": loose},
    }


def licence_text_for(entry):
    """MIT packs ship the repo's LICENSE verbatim; packs redistributed by the author's permission
    ship a Permission note instead (entry['licence'] = 'permission:<who granted what, when>')."""
    lic = entry.get("licence", "")
    if lic.startswith("permission:"):
        return "Permission\n\n" + lic[len("permission:"):].strip() + "\n"
    return None


# ----------------------------------------------------------------- I/O

def load_mods():
    with open(MODS_JSON, encoding="utf-8") as fh:
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
        subprocess.run(["git", "-C", tmp, "fetch", "-q", "--depth", "1", entry["repo"], entry["commit"]],
                       check=True)
        subprocess.run(["git", "-C", tmp, "checkout", "-q", "FETCH_HEAD"], check=True)

        src_root = os.path.join(tmp, entry["subdir"]) if entry.get("subdir") else tmp
        if not os.path.isdir(src_root):
            raise SystemExit("%s: subdir %r not in repo" % (entry["name"], entry.get("subdir")))

        if os.path.isdir(dest):
            shutil.rmtree(dest)
        os.makedirs(dest)

        if entry.get("generate"):
            # Loose quest files, no manifest upstream: copy every quest .txt (any depth) under
            # QuestPacks/<Name>/ preserving its relative path, renaming clashing QuestLists.
            g = entry["generate"]
            renames = g.get("rename_questlists") or {}
            rels = []
            for dirpath, _, names in os.walk(src_root):
                if ".git" in dirpath.split(os.sep):
                    continue
                for n in names:
                    if is_quest_txt(n):
                        rels.append(os.path.relpath(os.path.join(dirpath, n), src_root).replace(os.sep, "/"))
            if not rels:
                raise SystemExit(entry["name"] + ": no quest files found")
            copied = []
            for rel in rels:
                out_rel = "QuestPacks/%s/%s" % (entry["name"], rel)
                base = os.path.basename(rel)
                if base.startswith("QuestList-"):
                    ln = os.path.splitext(base)[0][len("QuestList-"):]
                    if ln in renames:
                        out_rel = os.path.join(os.path.dirname(out_rel), "QuestList-" + renames[ln] + ".txt").replace("\\", "/")
                dst = os.path.join(dest, out_rel)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(os.path.join(src_root, rel), dst)
                copied.append("QuestPacks/%s/%s" % (entry["name"], rel))
            manifest = generate_manifest(entry["name"], copied, g["title"], g["author"], g.get("description", ""), renames)
        else:
            if entry.get("manifest_override"):
                with open(os.path.join(HERE, entry["manifest_override"]), encoding="utf-8") as fh:
                    manifest = json.load(fh)
            else:
                with open(os.path.join(src_root, entry["manifest"]), encoding="utf-8") as fh:
                    manifest = json.load(fh)
            raw_files = list(manifest.get("Files", []))
            manifest = normalize_paths(manifest, entry["name"])
            # Copy exactly the listed files, each with its .meta beside it: prefabs, materials and
            # Unity .asset files reference each other and their textures by the GUID in the .meta,
            # so stripping metas (as the first Cliffworms packs did, harmlessly - JSON and quest text
            # have no links) silently breaks a model or material pack. Copying only what is listed
            # also keeps a 6,900-texture repo like Vanilla Enhanced down to its 1,268 used files.
            for f in raw_files:
                rel = _rel(f)
                src = os.path.join(src_root, rel)
                if not os.path.exists(src):
                    raise SystemExit("%s: manifest lists %s but the repo has no such file" % (entry["name"], rel))
                dst = os.path.join(dest, rel)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(src, dst)
                if os.path.exists(src + ".meta"):
                    shutil.copyfile(src + ".meta", dst + ".meta")
            # extra_roots: folders the listed assets depend on by GUID but do not list (UBLaMF's
            # prefabs -> Models/*.obj + Materials/*.mat). Unity pulls them into the bundle itself.
            for root in entry.get("extra_roots") or []:
                src = os.path.join(src_root, root)
                if not os.path.isdir(src):
                    raise SystemExit("%s: extra root %s/ not in repo" % (entry["name"], root))
                shutil.copytree(src, os.path.join(dest, root), dirs_exist_ok=True)

        note = licence_text_for(entry)
        if note is not None:
            with open(os.path.join(dest, "LICENSE"), "w", encoding="utf-8") as fh:
                fh.write(note)
        else:
            lic = os.path.join(tmp, entry.get("licence_file", "LICENSE"))
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
        manifest["_stem"] = entry["manifest"].replace(".dfmod.json", "")   # what the engine calls it
        manifests.append(manifest)
        problems += [entry["name"] + ": " + p for p in validate_manifest(manifest, dest)]
        providers = entry.get("archives_from") or []
        names = set(m["name"] for m in cfg["mods"])
        for prov in providers:
            if prov not in names:
                problems.append("%s: archives_from names %r, which is not in the pack" % (entry["name"], prov))
        problems += [entry["name"] + ": " + p for p in
                     worlddata_archive_problems(manifest, dest, provided_by_pack=bool(providers) and all(p in names for p in providers))]
        lic = os.path.join(dest, "LICENSE")
        if not os.path.exists(lic):
            problems.append(entry["name"] + ": LICENSE missing")
        else:
            with open(lic, encoding="utf-8", errors="replace") as fh:
                problems += [entry["name"] + ": " + p for p in
                             licence_problems(fh.read(), allow_permission=str(entry.get("licence", "")).startswith("permission:"),
                                              accept_substring=entry.get("licence_accept"))]
    # Pre-built bundles (converted desktop mods) are pack members too: dependencies may name them.
    for m in cfg["mods"]:
        if m.get("bundle"):
            manifests.append({"ModTitle": m.get("title", m["name"]), "GUID": None,
                              "_stem": os.path.splitext(os.path.basename(m["bundle"]))[0]})
    problems += validate_set(manifests)
    return problems


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--only", help="fetch/check one mod by name")
    ap.add_argument("--check", action="store_true", help="validate what is on disk; no network")
    args = ap.parse_args(argv)
    cfg = load_mods()
    entries = [m for m in cfg["mods"] if (not args.only or m["name"] == args.only) and not m.get("bundle")]
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
