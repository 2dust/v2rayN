# Optical size measurement

Source: KDE Plasma tray screenshot provided by reporter.

| Icon | Pixel bbox height |
|------|-------------------|
| Equalizer (approx span) | ~25 |
| Paper plane | ~21 |
| **v2rayN (current)** | **31** |
| Document | ~25 |
| Pause | ~25 |
| Psi | ~25 |

v2rayN is ~24% taller than the ~25px peer cluster (31/25 = 1.24).

Cause: shipped icon is nearly full-bleed (disc radius ≈ 125/128 of half-canvas), while Breeze symbolic tray icons keep transparent padding.

Fix applied in this pack: content optical scale ≈ 0.82 (ring diameter ≈ 16.3 on a 22px design canvas).
