#!/usr/bin/env python3
"""Zip the built MIT mod bundles into the release asset MIT-ModPack-ios.zip.

    python3 tools/bundled-mods/pack.py [--out ~/Desktop/MIT-ModPack-ios.zip]

Reads Assets/StreamingAssets/Mods/*.dfmod and Licenses/ (produced by MobileBuildSetup.BuildBundledMods)
and tools/bundled-mods/mods.json (the pin list), and refuses to pack unless every pinned mod has a
bundle and a licence and nothing unpinned is present - so a stale bundle from a removed mod can never
ride along. The README inside the zip is generated from the same pin list.
"""
import argparse
import json
import os
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MODS_DIR = os.path.join(REPO_ROOT, "Assets", "StreamingAssets", "Mods")
MODS_JSON = os.path.join(HERE, "mods.json")
THIRD_PARTY_URL = "https://github.com/Codex64ai/daggerfall-unity-ios/blob/unity6-upgrade/THIRD-PARTY.md"


def stems(cfg):
    """Bundle file stems, lower-cased the way Unity writes them."""
    return sorted(m["manifest"].replace(".dfmod.json", "").lower() for m in cfg["mods"])


def check_bundles(cfg, mods_dir):
    """Problems that stop packing: missing bundle/licence for a pinned mod, or an unpinned bundle."""
    problems = []
    want = set(stems(cfg))
    have = set(os.path.splitext(f)[0].lower() for f in os.listdir(mods_dir) if f.endswith(".dfmod")) \
        if os.path.isdir(mods_dir) else set()
    for s in sorted(want - have):
        problems.append("pinned mod has no bundle: %s.dfmod (run BuildBundledMods)" % s)
    for s in sorted(have - want):
        problems.append("bundle is not in the pin list: %s.dfmod (stale - remove it)" % s)
    for s in sorted(want & have):
        if not os.path.exists(os.path.join(mods_dir, "Licenses", s + "-LICENSE.txt")):
            problems.append("bundle has no licence beside it: %s" % s)
    return problems


def readme_text(cfg, titles):
    lines = [
        "Daggerfall Unity iOS - MIT mod pack",
        "",
        "%d mods by Cliffworms, all MIT licensed. Built for iOS from the authors' GitHub repositories" % len(cfg["mods"]),
        "with the port's mod builder; the data is the authors' work, unmodified.",
        "",
        "INSTALL: copy the .dfmod files you want from Mods/ into the app's Documents/Mods folder with",
        "the Files app (On My iPad > Daggerfall Unity > Mods), then restart the app. Each mod appears in",
        "the launcher's MODS window, switched on; untick any you do not want. Install one, some or all.",
        "",
        "Mods in this pack:",
    ]
    for m in cfg["mods"]:
        stem = m["manifest"].replace(".dfmod.json", "").lower()
        lines.append("  %-40s %s" % (stem + ".dfmod", titles.get(stem, m["name"])))
    lines += [
        "",
        "Licences: Mods/Licenses/ (MIT, Copyright (c) 2025 Cliffworms).",
        "Sources, pinned versions and what was deliberately left out: " + THIRD_PARTY_URL,
    ]
    return "\n".join(lines) + "\n"


def titles_from_sources(cfg):
    """Mod titles from the fetched manifests when present, else the pin-list names."""
    out = {}
    for m in cfg["mods"]:
        path = os.path.join(REPO_ROOT, cfg["dest_root"], m["name"], m["manifest"])
        stem = m["manifest"].replace(".dfmod.json", "").lower()
        try:
            with open(path, encoding="utf-8") as fh:
                out[stem] = json.load(fh).get("ModTitle") or m["name"]
        except (OSError, ValueError):
            out[stem] = m["name"]
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", default=os.path.expanduser("~/Desktop/MIT-ModPack-ios.zip"))
    args = ap.parse_args(argv)
    with open(MODS_JSON, encoding="utf-8") as fh:
        cfg = json.load(fh)

    problems = check_bundles(cfg, MODS_DIR)
    for p in problems:
        print("PROBLEM:", p)
    if problems:
        return 1

    if os.path.exists(args.out):
        os.remove(args.out)
    with zipfile.ZipFile(args.out, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("README.txt", readme_text(cfg, titles_from_sources(cfg)))
        for s in stems(cfg):
            z.write(os.path.join(MODS_DIR, s + ".dfmod"), "Mods/" + s + ".dfmod")
            z.write(os.path.join(MODS_DIR, "Licenses", s + "-LICENSE.txt"), "Mods/Licenses/" + s + "-LICENSE.txt")
    print("wrote %s (%d mods, %.1f MB)" % (args.out, len(cfg["mods"]), os.path.getsize(args.out) / 1e6))
    return 0


if __name__ == "__main__":
    sys.exit(main())
