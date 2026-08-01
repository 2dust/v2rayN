# Tray icon proposal: optical size + monochrome variants

Optional assets for discussion. **Does not change default icons** unless maintainers choose to adopt them.

## Problems

1. **Optical size** — shipped `NotifyIcon*.ico` is nearly full-bleed. On KDE Plasma it paints larger than neighboring monochrome tray icons.

   Measured from a Plasma tray screenshot:

   | Icon | Approx. pixel height |
   |------|----------------------|
   | Paper plane / document / pause / psi | ~21–25 |
   | **v2rayN (current)** | **~31** |

   Ratio ≈ 1.24× (about **24% taller** than the ~25px peer cluster).

2. **Style clash** — solid saturated discs stand out against Breeze-style monochrome trays.

Related: #7912 (dark tray icon; closed as not planned for DE-specific behavior). This proposal focuses on **padding/size** and **optional monochrome assets**, not requiring DE-specific APIs.

## What is in this folder

| Path | Description |
|------|-------------|
| `A-outline/` | Official stylized V as ring + outline (SVG `currentColor`, PNG, ICO) |
| `B-fill-v/` | Ring + solid V silhouette |
| `NotifyIcon1.ico` … `NotifyIcon4.ico` | Four proxy-state colors kept as **tinted ring + white V**, with padding |
| `compare-optical-size.png` | Side-by-side at tray scale |
| `MEASUREMENT.md` | Measurement notes |

Geometry is pixel-traced from the official `v2rayN.png` mark (same italic V with top-left serif and 45° arm), not a different logo.

Content optical scale ≈ **0.82** of full-bleed so the glyph better matches peer tray icons.

## Try today (no rebuild)

The app already supports overrides next to the binary:

- `NotifyIcon1.ico` … `NotifyIcon4.ico` (see `GetNotifyIcon` / `AvaUtils.GetAppIcon`)

Copy the status ICOs from this folder into the application directory to test.

## Suggested options for maintainers

1. **Minimal** — only add transparent padding to existing colored icons (size parity, keep current art).
2. **Optional pack** — ship these under a docs/misc path and document the override filenames (this PR).
3. **Platform default** — on Linux, prefer padded/monochrome assets (small code branch when resolving icon path).

Color-as-proxy-state can be preserved (ring tint variants included). Full monochrome can stay opt-in for dark monochrome panels.
