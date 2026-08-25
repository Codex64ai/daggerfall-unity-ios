#!/usr/bin/env python3
"""Phase 0 inventory for a Daggerfall Unity .dfmod, from an iOS feasibility angle.

A .dfmod is a Unity AssetBundle built for a desktop target. iOS refuses it outright, so
the question is never "does it load" - it is "what is inside, and what would it cost if we
rebuilt it for iOS". Two things decide that:

  CODE      iOS compiles ahead of time. A mod with a C# entry point (.cs.txt or .dll.bytes)
            can never run, no matter how it is repackaged. Those parts are simply lost.

  MEMORY    iOS has no DXT/BC support. Via the loose-file path textures land as ARGB32,
            which for a large pack will not fit. Rebuilt into an iOS bundle they can be
            ASTC, which is roughly 18x smaller. This script prices both so the decision is
            made on numbers rather than optimism.

Usage:
    python3 dfmod_inspect.py <file.dfmod> [more.dfmod ...] [--top 20] [--json out.json]
"""
import argparse, collections, json, os, sys

try:
    import UnityPy
except ImportError:
    sys.exit("UnityPy missing.  pip3 install UnityPy")

# Unity TextureFormat ids. Only the ones that actually turn up in DFU mods and iOS builds.
# The distinction that matters: DXT/BC is desktop-only - iOS cannot sample it at all, which
# is why a desktop-built bundle is useless here even before the platform check rejects it.
TEXFMT = {
    1: "Alpha8", 2: "ARGB4444", 3: "RGB24", 4: "RGBA32", 5: "ARGB32", 7: "RGB565",
    9: "R16", 10: "DXT1", 12: "DXT5", 13: "RGBA4444", 14: "BGRA32",
    22: "RGB9e5Float", 26: "BC6H", 27: "BC7", 28: "DXT1Crunched", 29: "DXT5Crunched",
    34: "ETC_RGB4", 45: "ETC2_RGB", 46: "ETC2_RGBA1", 47: "ETC2_RGBA8",
    48: "ASTC_4x4", 49: "ASTC_5x5", 50: "ASTC_6x6", 51: "ASTC_8x8",
    52: "ASTC_10x10", 53: "ASTC_12x12",
    54: "ASTC_RGBA_4x4", 55: "ASTC_RGBA_5x5", 56: "ASTC_RGBA_6x6",
    57: "ASTC_RGBA_8x8", 58: "ASTC_RGBA_10x10", 59: "ASTC_RGBA_12x12",
}
DESKTOP_ONLY = {"DXT1", "DXT5", "DXT1Crunched", "DXT5Crunched", "BC6H", "BC7"}


def fmtname(v):
    """Resolve a texture format to a name whether UnityPy hands back an enum or an int."""
    n = getattr(v, "name", None)
    if n:
        return n
    try:
        return TEXFMT.get(int(v), f"fmt{int(v)}")
    except (TypeError, ValueError):
        return str(v)


MIP = 4 / 3          # full mip chain costs ~33% over the base level


def argb32(w, h):    return w * h * 4 * MIP
def astc6(w, h):     return w * h * (128 / 36 / 8) * MIP     # ASTC 6x6, ~3.56 bpp
def astc8(w, h):     return w * h * (128 / 64 / 8) * MIP     # ASTC 8x8, ~2.00 bpp
def mb(n):           return n / (1024 * 1024)


def inspect(path):
    env = UnityPy.load(path)
    tex, audio, code, other = [], [], [], collections.Counter()

    for obj in env.objects:
        t = obj.type.name
        try:
            d = obj.read()
        except Exception:
            other[t + " (unreadable)"] += 1
            continue

        if t == "Texture2D":
            w, h = getattr(d, "m_Width", 0), getattr(d, "m_Height", 0)
            fmt = getattr(d, "m_TextureFormat", None)
            fmt = fmtname(fmt)
            tex.append((getattr(d, "m_Name", "?"), w, h, fmt))
        elif t == "AudioClip":
            audio.append((getattr(d, "m_Name", "?"), getattr(d, "m_Size", 0)))
        elif t == "MonoScript":
            code.append(("MonoScript", getattr(d, "m_ClassName", "?")))
        elif t == "TextAsset":
            n = getattr(d, "m_Name", "")
            if n.endswith((".cs", ".cs.txt", ".dll", ".dll.bytes")):
                code.append(("TextAsset", n))
            else:
                other["TextAsset"] += 1
        else:
            other[t] += 1
    return tex, audio, code, other


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--top", type=int, default=15, help="largest textures to list")
    ap.add_argument("--json", help="write machine-readable inventory here")
    a = ap.parse_args()

    grand = collections.Counter()
    all_tex, all_code, report = [], [], {}

    for f in a.files:
        if not os.path.exists(f):
            print(f"!! missing: {f}");  continue
        print(f"\n{'=' * 72}\n{os.path.basename(f)}   ({mb(os.path.getsize(f)):.1f} MB on disk)\n{'=' * 72}")
        try:
            tex, audio, code, other = inspect(f)
        except Exception as e:
            print(f"  could not read as a Unity bundle: {e}");  continue

        all_tex += tex;  all_code += code
        raw = sum(argb32(w, h) for _, w, h, _ in tex)
        a6  = sum(astc6(w, h)  for _, w, h, _ in tex)
        a8  = sum(astc8(w, h)  for _, w, h, _ in tex)
        grand["raw"] += raw;  grand["a6"] += a6;  grand["a8"] += a8
        grand["tex"] += len(tex);  grand["audio"] += len(audio);  grand["code"] += len(code)

        print(f"  textures {len(tex):<6} audio {len(audio):<6} code assets {len(code)}")
        if other:
            print("  other   " + ", ".join(f"{k}x{v}" for k, v in other.most_common(6)))
        if tex:
            print(f"\n  memory if rebuilt for iOS")
            print(f"    ARGB32 (loose-file path)   {mb(raw):9.1f} MB")
            print(f"    ASTC 6x6 (bundle path)     {mb(a6):9.1f} MB   {raw / max(a6,1):.1f}x smaller")
            print(f"    ASTC 8x8 (bundle path)     {mb(a8):9.1f} MB   {raw / max(a8,1):.1f}x smaller")
        dtop = collections.Counter(f for _, _, _, f in tex if f in DESKTOP_ONLY)
        if dtop:
            print(f"\n  desktop-only texture formats (unusable on iOS, must be re-encoded):")
            for f, n in dtop.most_common():
                print(f"    {f:<14} {n} textures")

        if code:
            print(f"\n  !! CONTAINS CODE - these parts cannot run on iOS, ever:")
            for kind, name in code[:10]:
                print(f"       {kind:<10} {name}")
            if len(code) > 10:
                print(f"       ... and {len(code) - 10} more")
        report[os.path.basename(f)] = {
            "textures": len(tex), "audio": len(audio), "code": len(code),
            "argb32_mb": round(mb(raw), 1), "astc6_mb": round(mb(a6), 1),
        }

    if len(a.files) > 1:
        print(f"\n{'=' * 72}\nTOTAL across {len(a.files)} files\n{'=' * 72}")
        print(f"  textures {grand['tex']}   audio {grand['audio']}   code assets {grand['code']}")
        print(f"    ARGB32    {mb(grand['raw']):9.1f} MB")
        print(f"    ASTC 6x6  {mb(grand['a6']):9.1f} MB")
        print(f"    ASTC 8x8  {mb(grand['a8']):9.1f} MB")

    if all_tex:
        print(f"\n  largest {a.top} textures (the ones that decide the budget)")
        for name, w, h, fmt in sorted(all_tex, key=lambda t: -t[1] * t[2])[:a.top]:
            print(f"    {w:>5}x{h:<5} {mb(argb32(w,h)):7.1f} MB raw  {fmt:<12} {name[:40]}")
        big = sum(1 for _, w, h, _ in all_tex if w * h >= 2048 * 2048)
        if big:
            print(f"\n  {big} textures are 2048x2048 or larger - these are the downscale candidates")

    if a.json:
        json.dump(report, open(a.json, "w"), indent=2)
        print(f"\n  wrote {a.json}")


if __name__ == "__main__":
    main()
