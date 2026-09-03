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


def stem_of(m):
    """Bundle file stem, lower-cased the way Unity writes them; a pre-built bundle keeps its own name."""
    if m.get("bundle"):
        return os.path.splitext(os.path.basename(m["bundle"]))[0].lower()
    return m["manifest"].replace(".dfmod.json", "").lower()


def stems(cfg):
    return sorted(stem_of(m) for m in cfg["mods"])


def prebuilt(cfg):
    """Entries whose bundle was converted from a desktop .dfmod (the DREAM pipeline) rather than
    built from source: {stem: (bundle path, licence text)}."""
    out = {}
    for m in cfg["mods"]:
        if m.get("bundle"):
            out[stem_of(m)] = (os.path.expanduser(m["bundle"]), m.get("licence", ""))
    return out


def check_bundles(cfg, mods_dir):
    """Problems that stop packing: missing bundle/licence for a pinned mod, or an unpinned bundle."""
    problems = []
    pre = prebuilt(cfg)
    want = set(stems(cfg)) - set(pre)
    have = set(os.path.splitext(f)[0].lower() for f in os.listdir(mods_dir) if f.endswith(".dfmod")) \
        if os.path.isdir(mods_dir) else set()
    for s in sorted(want - have):
        problems.append("pinned mod has no bundle: %s.dfmod (run BuildBundledMods)" % s)
    for s in sorted(have - want - set(pre)):
        problems.append("bundle is not in the pin list: %s.dfmod (stale - remove it)" % s)
    for s in sorted(want & have):
        if not os.path.exists(os.path.join(mods_dir, "Licenses", s + "-LICENSE.txt")):
            problems.append("bundle has no licence beside it: %s" % s)
    for s, (path, lic) in sorted(pre.items()):
        if not os.path.exists(path):
            problems.append("pre-built bundle missing: %s (run the converter)" % path)
        if not lic.startswith("permission:") and not lic.startswith("text:"):
            problems.append("pre-built bundle %s has no licence text in the pin" % s)
    return problems


def prebuilt_licence_text(lic):
    body = lic.split(":", 1)[1].strip() if ":" in lic else lic
    return ("Permission\n\n" if lic.startswith("permission:") else "") + body + "\n"


def authors_line(cfg):
    seen = []
    for m in cfg["mods"]:
        a = (m.get("generate") or {}).get("author") or "Cliffworms"
        if a not in seen:
            seen.append(a)
    return " and ".join(seen) if len(seen) <= 2 else ", ".join(seen[:-1]) + " and " + seen[-1]


def readme_text(cfg, titles):
    lines = [
        "Daggerfall Unity iOS - MIT mod pack",
        "",
        "%d mods by %s. Built for iOS from the authors' GitHub repositories with the port's mod builder;" % (len(cfg["mods"]), authors_line(cfg)),
        "the data is the authors' work, unmodified. Each mod's licence or permission record is in Mods/Licenses/.",
        "",
        "INSTALL: copy the .dfmod files you want from Mods/ into the app's Documents/Mods folder with",
        "the Files app (On My iPad > Daggerfall Unity > Mods), then restart the app. Each mod appears in",
        "the launcher's MODS window, switched on; untick any you do not want. Install one, some or all.",
        "",
        "Mods in this pack:",
    ]
    for m in cfg["mods"]:
        stem = stem_of(m)
        lines.append("  %-40s %s" % (stem + ".dfmod", titles.get(stem, m["name"])))
    lines += [
        "",
        "Licences: Mods/Licenses/ - MIT for Cliffworms' mods; Jay_H's are redistributed with his permission.",
        "Sources, pinned versions and what was deliberately left out: " + THIRD_PARTY_URL,
    ]
    return "\n".join(lines) + "\n"


def titles_from_sources(cfg):
    """Mod titles from the fetched manifests when present, else the pin-list names."""
    out = {}
    for m in cfg["mods"]:
        if m.get("bundle"):
            out[stem_of(m)] = m.get("title") or m["name"]
            continue
        path = os.path.join(REPO_ROOT, cfg["dest_root"], m["name"], m["manifest"])
        stem = stem_of(m)
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
        pre = prebuilt(cfg)
        for s in stems(cfg):
            if s in pre:
                path, lic = pre[s]
                z.write(path, "Mods/" + s + ".dfmod")
                z.writestr("Mods/Licenses/" + s + "-LICENSE.txt", prebuilt_licence_text(lic))
                continue
            z.write(os.path.join(MODS_DIR, s + ".dfmod"), "Mods/" + s + ".dfmod")
            z.write(os.path.join(MODS_DIR, "Licenses", s + "-LICENSE.txt"), "Mods/Licenses/" + s + "-LICENSE.txt")
    print("wrote %s (%d mods, %.1f MB)" % (args.out, len(cfg["mods"]), os.path.getsize(args.out) / 1e6))
    return 0


if __name__ == "__main__":
    sys.exit(main())
