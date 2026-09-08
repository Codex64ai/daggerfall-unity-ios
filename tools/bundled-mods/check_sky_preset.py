#!/usr/bin/env python3
"""Check a Dynamic Skies preset extraction: every *TextureFile name in its JSON has a texture, and
there are the seven SkyboxSettings files the mod's FindPresetMod requires."""
import json, os, re, sys
root = sys.argv[1]
texts = {os.path.splitext(f)[0] for _, _, fs in os.walk(root) for f in fs if f.lower().endswith((".png", ".jpg", ".tga"))}
jsons = [os.path.join(d, f) for d, _, fs in os.walk(root) for f in fs if f.endswith(".json")]
wanted = set()
for j in jsons:
    wanted |= set(re.findall(r'TextureFile\\?":\s*\\?"([^"\\]+)', open(j, encoding="utf-8").read()))
settings = [os.path.basename(j) for j in jsons if os.path.basename(j).startswith("Skybox") and "Night" not in j]
missing = sorted(w for w in wanted if w not in texts)
print("presets:", len(settings), sorted(settings)); print("textures referenced:", len(wanted), "present:", len(wanted) - len(missing))
if missing: print("MISSING:", missing)
sys.exit(1 if missing or len(settings) < 7 else 0)
