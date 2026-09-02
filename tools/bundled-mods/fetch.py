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
                problems.append("QuestList %s in both %s and %s (engine drops the second)"
                                % (ql, seen_lists[ql], title))
            seen_lists[ql] = title
        guid = m.get("GUID")
        if guid:
            if guid in seen_guids:
                problems.append("GUID %s shared by %s and %s" % (guid, seen_guids[guid], title))
            seen_guids[guid] = title
    return problems


def licence_problems(text):
    stripped = (text or "").strip()
    first = stripped.splitlines()[0].strip() if stripped else ""
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
                raise SystemExit("%s: manifest references %s/ but the repo has no such folder"
                                 % (entry["name"], root))
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
