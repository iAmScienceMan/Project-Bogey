# Entity sprites

PNG sprites drawn on the tactical map, one per contact category. They are copied
next to the built executable and loaded at startup by `EntitySprites`.

| File             | Drawn for                                             |
|------------------|-------------------------------------------------------|
| `own.png`        | Friendly own-units                                    |
| `air.png`        | Tracks classified/identified as air                   |
| `surface.png`    | Tracks classified/identified as surface               |
| `subsurface.png` | Tracks classified/identified as subsurface            |
| `unknown.png`    | Unclassified, dropped, or fallback for a missing file |

Requirements: 8-bit non-interlaced PNG (grayscale, truecolor, indexed, with or
without alpha). Any missing file falls back to `unknown.png`, and if that is also
absent the renderer draws its built-in vector marker instead.

The bundled images are simple placeholders; replace them with real artwork.
