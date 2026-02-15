# Midjourney Prompts for Perch

## App Icon

### Primary Icon (Perch bird on a branch)
```
A stylized perch bird sitting on a tree branch, minimalist flat design, modern app icon style, warm amber and deep teal color palette, geometric shapes, clean lines, no text, centered composition, solid color background, vector art style --ar 1:1 --s 250 --v 6.1
```

### Icon Variant (Perch bird with config/gear motif)
```
A stylized perch bird perched on a gear-shaped branch, representing configuration management, minimalist flat design, modern app icon, warm amber and slate blue color palette, clean geometric shapes, no text, solid dark background --ar 1:1 --s 250 --v 6.1
```

---

## Splash / Welcome Screen Background

### Abstract config network
```
Abstract digital landscape of interconnected nodes and flowing data streams, representing configuration syncing across machines, dark navy background, glowing teal and amber accent lines, soft depth of field, modern tech aesthetic, wide cinematic composition, no text --ar 16:9 --s 400 --v 6.1
```

### Cozy developer workspace
```
Isometric illustration of a cozy developer workspace with multiple monitors showing code and config files, connected by glowing symlink lines, warm lighting, dark theme UI on screens, coffee cup, plants, modern flat illustration style, no text --ar 16:9 --s 300 --v 6.1
```

---

## Profile Selection Images

### Developer
```
Isometric illustration of a developer workstation, dual monitors with code editors and terminal windows, mechanical keyboard, git branch diagrams floating above, dark theme, warm amber and teal accent lighting, modern flat illustration style, clean and minimal, no text --ar 4:3 --s 300 --v 6.1
```

### Creative
```
Isometric illustration of a creative professional workstation, large color-calibrated display showing digital art, graphics tablet, color swatches and design tools floating around, purple and magenta accent lighting, modern flat illustration style, clean and minimal, no text --ar 4:3 --s 300 --v 6.1
```

### Power User
```
Isometric illustration of a power user command center, triple monitors with terminals, system dashboards and htop-style process monitors, custom mechanical keyboard with macro keys, neon green and electric blue accent lighting, modern flat illustration style, clean and minimal, no text --ar 4:3 --s 300 --v 6.1
```

### Gamer
```
Isometric illustration of a gaming setup, ultrawide curved monitor with game running, RGB-lit mechanical keyboard and mouse, headset on stand, controller, game launcher icons floating, red and purple accent lighting, modern flat illustration style, clean and minimal, no text --ar 4:3 --s 300 --v 6.1
```

### Minimal
```
Isometric illustration of a clean minimal desk setup, single thin monitor with a simple clean desktop, wireless keyboard and trackpad, single plant, lots of white space, soft natural lighting, light grey and white tones with a single mint accent, modern flat illustration style, no text --ar 4:3 --s 300 --v 6.1
```

---

## Usage Notes

- All prompts use `--v 6.1` (latest Midjourney version)
- `--ar` sets aspect ratio: 1:1 for icons, 16:9 for backgrounds, 4:3 for profile cards
- `--s` (stylize) controls artistic interpretation: higher = more stylized
- After generating, upscale the best variant (U1-U4)
- For the app icon, also generate at `--ar 1:1` with `--no background` for a transparent-friendly result
- Profile images should have consistent style — generate all five in the same session or use `--sref` (style reference) from the first good result
