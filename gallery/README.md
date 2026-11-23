# GuideStrap mod gallery

GuideStrap's **Mods → Mod gallery** window reads `index.json` from this folder.
Anything added here shows up for everyone after you push it to GitHub.

## Adding a mod

1. Put the file in this folder:
   - `crosshairs/` – a PNG, up to 64×64, with the center of the crosshair in the middle of the image.
   - `cursors/` – a PNG, up to 64×64. Roblox centers cursors, so put the arrow tip in the middle of the image.
   - `skies/` – a `.zip` with the sky's `.tex` files (like `sky512_up.tex`).
   - `sounds/` – a `.zip` with `.ogg` or `.mp3` files named like Roblox's sounds (`ouch.ogg`, `action_jump.mp3`…).
2. Add an entry to `index.json`:

```json
{
  "name": "My crosshair",
  "type": "crosshair",
  "author": "Your name",
  "description": "One short line about it.",
  "url": "https://raw.githubusercontent.com/guideships/GuideStrap/master/gallery/crosshairs/my-crosshair.png",
  "preview": "optional https link to a picture (used for skies and sound packs)"
}
```

`type` is one of `crosshair`, `cursor`, `sky` or `soundpack`.

## Rules

- Only cosmetic files: images, skies and sounds. Nothing that changes how a game plays.
- Only upload things you made or have permission to share.
