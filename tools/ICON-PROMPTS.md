# Generating touch-HUD icons

## What the shipped icons actually are (measured, not guessed)

| Property | Value | How it was measured |
|---|---|---|
| Canvas | 224x224 RGBA | file headers |
| Native art grid | **56x56, upscaled 4x nearest-neighbour** | 95% of 4x4 blocks are uniform; only ~55% of 7x7 are |
| Edges | **hard - zero semi-transparent pixels** | alpha histogram |
| Colours | 51-64 distinct | quantised colour count |
| Glyph coverage | ~64% of the frame | opaque-pixel ratio |
| Lighting | lit from above; bright middle, dark base | mean luma per horizontal band |

`icon_pipeline.py` enforces every one of those **except the lighting and texture**, which
have to come from the drawing itself. So:

* **ComfyUI's job:** a well-drawn, well-lit object with real shading.
* **The pipeline's job:** grid, hard edges, palette, framing, transparency.

Generate at 512x512 and let the pipeline reduce it. Do not try to generate 56x56 directly -
models draw badly that small, and the pipeline gets a better result from downsampling a
clean large image.

## Shared style prompt

    Daggerfall 1996 DOS RPG inventory icon, single object centred, 3/4 view,
    hand-painted pixel art, warm parchment and aged leather palette, deep brown
    outlines, strong light from upper left with dark shadow beneath, high contrast,
    limited palette, crisp readable silhouette, flat plain background

## Shared negative prompt

    text, letters, numbers, watermark, signature, border, frame, drop shadow on
    background, gradient background, photorealistic, glossy, plastic, neon, modern UI,
    flat vector, emoji, sticker, multiple objects, cropped, blurry, soft focus,
    anti-aliased edges

Notes that matter:
* **"flat plain background"** - the pipeline strips the background by flood-filling from
  the corners, so any uniform backdrop works. Avoid busy or vignetted backdrops.
* **"single object centred"** - the pipeline crops to the glyph, so a lone object on a
  clean field converts best.
* Avoid text in the prompt; the buttons carry no words by design.

## Per-icon subjects

Append one of these to the shared style prompt.

| File | Subject prompt |
|---|---|
| `btn_sheet.png` | `an open character record sheet, aged parchment page with a wax seal and a quill, rolled edges` |
| `btn_automap.png` | `a hand-drawn dungeon floor plan on parchment, rectangular chambers joined by narrow corridors, ink lines` |
| `btn_mode_grab.png` | `an open leather-gloved hand reaching forward, palm toward viewer, fingers spread` |
| `btn_mode_steal.png` | `an iron padlock with a thin lockpick inserted, shackle open, worn metal` |
| `btn_mode_info.png` | `a single open eye with a dark iris, framed by faint lashes, watching` |
| `btn_mode_talk.png` | `two crossed brass speaking horns` or `a stone tavern mask, mouth open, speaking` |
| `joystick_knob.png` | not needed - derived from `joystick_bg.png` at 62% scale, so it matches by construction |

## Running the pipeline

    cd ~/daggerfall-mobile/tools
    python3 icon_pipeline.py raw/btn_sheet.png \
        ~/dev/daggerfall-unity/Assets/DaggerfallMobile/UI/btn_sheet.png \
        --palette-from ~/dev/daggerfall-unity/Assets/DaggerfallMobile/UI

It prints the resulting coverage and colour count - compare those against the table above
before accepting an icon. `--coverage` tunes how much of the frame the glyph fills
(default 0.64, matching the shipped set).

Then rebuild so the importer picks them up:

    Unity -batchmode -quit -projectPath ~/dev/daggerfall-unity \
      -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.ApplyAll

The importer maps by FILENAME, so no code changes are needed - and note it hides a
button's text label only when it successfully assigns an icon. A missing file therefore
shows up as a word on the button, which is exactly how the SHEET button ended up reading
"SHEET" on device.
