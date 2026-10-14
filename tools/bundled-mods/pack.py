#!/usr/bin/env python3
"""Zip the built MIT mod bundles into the release asset MIT-ModPack-ios.zip.

    python3 tools/bundled-mods/pack.py [--out ~/Desktop/MIT-ModPack-ios.zip]
    python3 tools/bundled-mods/pack.py --private [--mods-dir DIR] [--dream-dir DIR] [--out ~/Desktop/NonMIT-ModPack-ios.zip]

--private builds the Non-MIT pack instead: every private_only bundle (plus the converted DREAM files in
--dream-dir) as <stem>-ios.dfmod, and pack-index.json (file, size, unlock key) that the app's
MobileOriginalMods gate reads. Those files load only once the player's own original mod is in Documents/Mods.
--mods-dir reads the private_only bundles from DIR instead of Assets/StreamingAssets/Mods. --private first runs
tools/known_ours.py --check and refuses to pack while MobileOriginalModsKnownOurs.cs is stale. A private pack
bigger than --split-mb (default 1900 MiB; GitHub release assets must be under 2 GiB) is written as
<out>-part1.zip, <out>-part2.zip, ... - each with the README and the whole pack-index.json.

Reads Assets/StreamingAssets/Mods/*.dfmod and Licenses/ (produced by MobileBuildSetup.BuildBundledMods)
and tools/bundled-mods/mods.json (the pin list), and refuses to pack unless every pinned mod has a
bundle and a licence and nothing unpinned is present - so a stale bundle from a removed mod can never
ride along. The README inside the zip is generated from the same pin list.
"""
import argparse
import json
import os
import subprocess
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MODS_DIR = os.path.join(REPO_ROOT, "Assets", "StreamingAssets", "Mods")
MODS_JSON = os.path.join(HERE, "mods.json")
KNOWN_OURS_CHECK = [sys.executable, os.path.join(REPO_ROOT, "tools", "known_ours.py"), "--check"]
THIRD_PARTY_URL = "https://github.com/Codex64ai/daggerfall-unity-ios/blob/unity6-upgrade/THIRD-PARTY.md"


def stem_of(m):
    """Bundle file stem, lower-cased the way Unity writes them; a pre-built bundle keeps its own name."""
    if m.get("bundle"):
        return os.path.splitext(os.path.basename(m["bundle"]))[0].lower()
    return m["manifest"].replace(".dfmod.json", "").lower()


def excluded_from_pack(m):
    """builtin: true ships inside the app (code compiled in); private_only: true has no public
    licence yet. Either way the pack zip neither requires nor refuses the bundle."""
    return bool(m.get("builtin") or m.get("private_only"))


def builtin_stems(cfg):
    """Data bundles excluded from the pack zip (builtin or private_only - see excluded_from_pack):
    they never appear there, so the pack neither requires nor refuses them."""
    return set(stem_of(m) for m in cfg["mods"] if excluded_from_pack(m))


def pack_mods(cfg):
    return [m for m in cfg["mods"] if not excluded_from_pack(m)]


def stems(cfg):
    return sorted(stem_of(m) for m in pack_mods(cfg))


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
    for s in sorted(have - want - set(pre) - builtin_stems(cfg)):
        problems.append("bundle is not in the pin list: %s.dfmod (stale - remove it)" % s)
    for s in sorted(want & have):
        if not os.path.exists(os.path.join(mods_dir, "Licenses", s + "-LICENSE.txt")):
            problems.append("bundle has no licence beside it: %s" % s)
    for s, (path, lic) in sorted(pre.items()):
        if not os.path.exists(path):
            problems.append("pre-built bundle missing: %s (run the converter)" % path)
        if not lic.startswith("permission:") and not lic.startswith("text:"):
            problems.append("pre-built bundle %s has no licence text in the pin" % s)
    # A pending: licence records that we have no redistribution right yet (World of Daggerfall).
    # private_only keeps such an entry out of pack_mods, but that is a single boolean one merge or
    # rebase can drop; this is the second line of defence, applied to whatever WOULD be packed.
    for m in pack_mods(cfg):
        if str(m.get("licence", "")).startswith("pending:"):
            problems.append("pinned mod has a pending licence and cannot ship in the public pack: %s"
                            % m["name"])
    return problems


def prebuilt_licence_text(lic):
    body = lic.split(":", 1)[1].strip() if ":" in lic else lic
    return ("Permission\n\n" if lic.startswith("permission:") else "") + body + "\n"


def authors_line(cfg):
    seen = []
    for m in pack_mods(cfg):
        a = (m.get("generate") or {}).get("author") or "Cliffworms"
        if a not in seen:
            seen.append(a)
    return " and ".join(seen) if len(seen) <= 2 else ", ".join(seen[:-1]) + " and " + seen[-1]


def readme_text(cfg, titles):
    lines = [
        "Daggerfall Unity iOS - MIT mod pack",
        "",
        "%d mods by %s. Built for iOS from the authors' GitHub repositories with the port's mod builder;" % (len(pack_mods(cfg)), authors_line(cfg)),
        "the data is the authors' work, unmodified. Each mod's licence or permission record is in Mods/Licenses/.",
        "",
        "INSTALL: copy the .dfmod files you want from Mods/ into the app's Documents/Mods folder with",
        "the Files app (On My iPad > Daggerfall Unity > Mods), then restart the app. Each mod appears in",
        "the launcher's MODS window, switched on; untick any you do not want. Install one, some or all.",
        "",
        "Mods in this pack:",
    ]
    for m in pack_mods(cfg):
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
    for m in pack_mods(cfg):
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


# --- Non-MIT pack (--private) -------------------------------------------------------------------

# Same urls/names as the C# rows in Assets/Scripts/Game/Mobile/MobileOriginalMods.cs.
ORIGINALS = {
    "DREAM": ("DREAM", "https://www.nexusmods.com/daggerfallunity/mods/5"),
    "DREAMSKY": ("DREAM SKY", "https://www.nexusmods.com/daggerfallunity/mods/664"),
    "DEX": ("Daggerfall Enemy Expansion", "https://www.nexusmods.com/daggerfallunity/mods/372"),
    "REALGRASS": ("Real Grass", "https://www.nexusmods.com/daggerfallunity/mods/20"),
    "DYNAMICSKIES": ("Dynamic Skies", "https://github.com/drcarademono/dynamic-skies"),
    "WOD": ("World of Daggerfall", "https://github.com/drcarademono/world-of-daggerfall"),
    "WODBIOMES": ("World of Daggerfall - Biomes", "https://github.com/drcarademono/wod-biomes"),
    "WODTERRAIN": ("World of Daggerfall - Terrain", "https://github.com/drcarademono/wod-terrain"),
    "DISTANTTERRAIN": ("Distant Terrain of the World of Daggerfall", "https://www.nexusmods.com/daggerfallunity/mods/1284"),
}


def ios_name(stem):
    """Our Non-MIT pack file name (mirrors MobileOriginalMods.IosName):
    "dream - mobs (2 of 6)" -> "dream-mobs_2_of_6-ios.dfmod". Idempotent on an -ios name."""
    s = stem.lower()
    if s.endswith(".dfmod"):
        s = s[:-len(".dfmod")]
    s = s.replace(" & ", " ").replace(" - ", "-").replace(" (", "_").replace(" of ", "_of_").replace(")", "")
    s = s.replace(" ", "-")
    if not s.endswith("-ios"):
        s += "-ios"
    return s + ".dfmod"


def private_stems(cfg):
    """{stem: unlock_key} for private_only entries that are not builtin; an entry without a key is an error."""
    out = {}
    for m in cfg["mods"]:
        if m.get("private_only") and not m.get("builtin"):
            key = m.get("unlock_key")
            if not key:
                raise ValueError("private_only entry has no unlock_key: %s" % m["name"])
            out[stem_of(m)] = key
    return out


def dream_key(stem):
    """"dream-sky" (any spelling) -> DREAMSKY; every other dream-* file -> DREAM."""
    return "DREAMSKY" if ios_name(stem) == "dream-sky-ios.dfmod" else "DREAM"


def pack_index(entries):
    """[(file, size, key)] -> JSON text: a top-level array of {file, size, key}, sorted by file."""
    rows = [{"file": f, "size": int(n), "key": k} for f, n, k in sorted(entries)]
    return json.dumps(rows, indent=1) + "\n"


def private_readme_text(entries):
    lines = [
        "Daggerfall Unity iOS - Non-MIT mod pack (private test draft)",
        "",
        "These are iOS conversions of mods whose authors have not (yet) given a redistribution licence.",
        "Each file loads only once the app has seen your own copy of the original mod in Documents/Mods",
        "(the downloaded archive or the desktop .dfmod); until then it shows as a greyed row in MODS.",
        "",
        "INSTALL: copy Mods/*.dfmod and pack-index.json into Documents/Mods with the Files app, add the",
        "original mods you own beside them, then restart the app.",
        "",
    ]
    for f, _, key in sorted(entries):
        name, url = ORIGINALS.get(key, (key, ""))
        lines.append("  %-44s %-15s needs the original %s from %s in Documents/Mods"
                     % (os.path.basename(f), key, name, url))
    return "\n".join(lines) + "\n"


def private_bundle_paths(cfg, mods_dir):
    """{stem: path} of every private_only bundle, plus the problems (missing files)."""
    paths, problems = {}, []
    for m in cfg["mods"]:
        if not (m.get("private_only") and not m.get("builtin")):
            continue
        stem = stem_of(m)
        path = os.path.expanduser(m["bundle"]) if m.get("bundle") else os.path.join(mods_dir, stem + ".dfmod")
        if not os.path.exists(path):
            problems.append("private bundle missing: %s" % path)
        paths[stem] = path
    return paths, problems


def known_ours_fresh():
    """True when tools/known_ours.py --check says MobileOriginalModsKnownOurs.cs is up to date."""
    return subprocess.call(KNOWN_OURS_CHECK) == 0


def main_private(args, cfg):
    if not known_ours_fresh():
        print("PROBLEM: MobileOriginalModsKnownOurs.cs is stale; run python3 tools/known_ours.py and commit it")
        return 1
    try:
        keys = private_stems(cfg)
    except ValueError as e:
        print("PROBLEM:", e)
        return 1
    paths, problems = private_bundle_paths(cfg, args.mods_dir or MODS_DIR)
    files = [(paths[s], ios_name(s), keys[s]) for s in sorted(keys)]
    if args.dream_dir:
        if not os.path.isdir(args.dream_dir):
            problems.append("--dream-dir is not a folder: %s" % args.dream_dir)
        else:
            for leaf in sorted(os.listdir(args.dream_dir)):
                if leaf.lower().endswith(".dfmod") and leaf.lower().startswith("dream"):
                    stem = leaf[:-len(".dfmod")]
                    files.append((os.path.join(args.dream_dir, leaf), ios_name(stem), dream_key(stem)))
    seen = set()
    for _, name, _ in files:
        if name in seen:
            problems.append("two files would ship as %s" % name)
        seen.add(name)
    for p in problems:
        print("PROBLEM:", p)
    if problems:
        return 1

    entries = [("Mods/" + name, os.path.getsize(src), key) for src, name, key in files]
    limit = int(args.split_mb * (1 << 20)) if args.split_mb else None
    parts = split_parts([((src, name), os.path.getsize(src)) for src, name, _ in files], limit)
    if len(parts) == 1:
        outs = [args.out]
    else:
        base, ext = os.path.splitext(args.out)
        outs = ["%s-part%d%s" % (base, i + 1, ext) for i in range(len(parts))]
    readme, index = private_readme_text(entries), pack_index(entries)
    for out, part in zip(outs, parts):
        if os.path.exists(out):
            os.remove(out)
        with zipfile.ZipFile(out, "w", zipfile.ZIP_STORED, allowZip64=True) as z:
            z.writestr("README.txt", readme)
            z.writestr("pack-index.json", index)  # every part carries the whole index
            for src, name in part:
                z.write(src, "Mods/" + name)
        print("wrote %s (%d files, %.1f MB)" % (out, len(part), os.path.getsize(out) / 1e6))
    return 0


def split_parts(items, limit):
    """[(item, size)] -> [[item, ...], ...]: greedy in order, a new part when the next item would pass limit
    (None = one part). An item larger than limit gets a part of its own."""
    parts, cur, total = [], [], 0
    for item, size in items:
        if cur and limit is not None and total + size > limit:
            parts.append(cur)
            cur, total = [], 0
        cur.append(item)
        total += size
    if cur or not parts:
        parts.append(cur)
    return parts


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", default=None)
    ap.add_argument("--private", action="store_true", help="build the Non-MIT pack (-ios names + pack-index.json)")
    ap.add_argument("--dream-dir", default=None, help="folder of converted DREAM .dfmod files (with --private)")
    ap.add_argument("--split-mb", type=float, default=1900,
                    help="with --private: split into <out>-partN.zip of at most this many MiB (GitHub assets must be < 2 GiB; 0 = one zip)")
    ap.add_argument("--mods-dir", default=None, help="folder of the private_only bundles (with --private; default StreamingAssets/Mods)")
    args = ap.parse_args(argv)
    with open(MODS_JSON, encoding="utf-8") as fh:
        cfg = json.load(fh)
    if args.private:
        if not args.out:
            args.out = os.path.expanduser("~/Desktop/NonMIT-ModPack-ios.zip")
        return main_private(args, cfg)
    if not args.out:
        args.out = os.path.expanduser("~/Desktop/MIT-ModPack-ios.zip")

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
    print("wrote %s (%d mods, %.1f MB)" % (args.out, len(pack_mods(cfg)), os.path.getsize(args.out) / 1e6))
    return 0


if __name__ == "__main__":
    sys.exit(main())
