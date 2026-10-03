# Store Listing Screenshots

This folder hosts screenshots submitted to Microsoft Partner Center.

## Specifications

- **Format**: PNG (preferred) or JPG (RGB, no alpha channel for screenshots)
- **Resolution**: 1920 x 1080 (1080p, 16:9 recommended) or 1366 x 768 minimum
- **Per-Locale Limit**: Maximum 10 screenshots per language listing (strictly enforced by Partner Center API)

## File Naming Convention

`scripts/publish_store.py` maps filenames directly to listing languages:

| Filename | Locale | Language |
| :--- | :--- | :--- |
| `tw1.png`, `tw2.png` | `zh-tw` | Traditional Chinese (Taiwan) |
| `cn1.png`, `cn2.png` | `zh-cn` | Simplified Chinese (China) |
| `en1.png`, `en2.png` | `en-us` | English (United States) |
| `ja1.png`, `ja2.png` | `ja-jp` | Japanese (Japan) |
| `ko1.png`, `ko2.png` | `ko-kr` | Korean (Korea) |
