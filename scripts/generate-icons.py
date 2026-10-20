#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate the EverythingPal multi-scale app icon set.

Design (Fluent-style, no third-party trademarks):
  * rounded-square app tile, indigo -> blue diagonal gradient (#6366F1 -> #2563EB)
    with a soft top-left highlight and a subtle bottom-right vignette,
  * white magnifying glass (lens + 45 degree handle) as the main glyph,
  * the "Command Palette" motif:
      - full detail (>= 128 px): three rounded rows inside the magnifier lens
        (and on the wide 310x150 tile, three rows to the right of the magnifier),
      - 49-127 px: two rows,
      - <= 48 px: bars are dropped so the glyph stays legible at 16 px,
  * unplated / store icons carry their own rounded tile silhouette on a
    transparent canvas, tile backgrounds (Square150 / Wide / Splash) are
    full-bleed artwork because Windows composites those into a tile.

The script is fully deterministic: no network, no external image assets, no
fonts (all geometry is vector-ish Pillow drawing, supersampled and downscaled
with Lanczos). Re-running it always produces byte-identical PNGs on the same
Pillow version.

Usage:
    python scripts/generate-icons.py                # write all assets + verify
    python scripts/generate-icons.py --check        # verify existing assets only
    python scripts/generate-icons.py --preview      # also write icon-preview.png
    python scripts/generate-icons.py --quiet        # less output

Pillow is required (`pip install pillow`). The DSH bundled Python already has
it: use the path from the `load_workspace_dependencies` tool, e.g.
    C:\\Users\\<you>\\.dsh\\dsh-runtimes\\dsh-primary-runtime\\dependencies\\python\\python.exe
"""

from __future__ import annotations

import argparse
import hashlib
import math
import os
import sys
from dataclasses import dataclass

try:
    from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont
except ImportError:  # pragma: no cover - handled for a friendly message
    sys.exit("Pillow is required: pip install pillow (or use the DSH bundled Python)")

# --------------------------------------------------------------------------
# Paths
# --------------------------------------------------------------------------

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPT_DIR)
ASSETS_DIR = os.path.join(REPO_ROOT, "src", "EverythingPal", "Assets")
STORE_ASSETS_DIR = os.path.join(REPO_ROOT, "docs", "store", "assets")

# --------------------------------------------------------------------------
# Palette / design constants
# --------------------------------------------------------------------------

COLOR_TOP_LEFT = (99, 102, 241)      # indigo 500  #6366F1
COLOR_BOTTOM_RIGHT = (37, 99, 235)   # blue 600    #2563EB
COLOR_VIGNETTE = (23, 37, 99)        # indigo 900  #172563
COLOR_WHITE = (255, 255, 255)
RIM_ALPHA = 56
CORNER_RADIUS_RATIO = 0.225          # Fluent-ish rounded square

GRADIENT_CACHE: Image.Image | None = None
GRADIENT_SIZE = 512


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def _build_gradient(px: int = GRADIENT_SIZE) -> Image.Image:
    """A small diagonal indigo->blue gradient with highlight + vignette."""
    global GRADIENT_CACHE
    if GRADIENT_CACHE is not None:
        return GRADIENT_CACHE

    try:
        import numpy as np  # bundled, but optional

        ax = np.linspace(-1.0, 1.0, px)
        x, y = np.meshgrid(ax, ax)
        t = _smoothstep_np(np.clip((x + y) * 0.25 + 0.5, 0.0, 1.0))
        c0 = np.asarray(COLOR_TOP_LEFT, dtype=np.float64)
        c1 = np.asarray(COLOR_BOTTOM_RIGHT, dtype=np.float64)
        rgb = c0[None, None, :] + (c1 - c0)[None, None, :] * t[..., None]
        # soft light from the upper-left
        highlight = 0.16 * np.exp(-(((x + 0.55) ** 2 + (y + 0.65) ** 2) / 0.30))
        rgb = rgb + (255.0 - rgb) * highlight[..., None]
        # gentle depth in the lower-right corner
        vignette = 0.10 * np.exp(-(((x - 0.78) ** 2 + (y - 0.82) ** 2) / 0.55))
        vig = np.asarray(COLOR_VIGNETTE, dtype=np.float64)
        rgb = rgb * (1.0 - vignette[..., None]) + vig[None, None, :] * vignette[..., None]
        alpha = np.full((px, px, 1), 255.0)
        arr = np.concatenate([np.clip(rgb, 0, 255), alpha], axis=2).astype(np.uint8)
        GRADIENT_CACHE = Image.fromarray(arr, "RGBA")
        return GRADIENT_CACHE
    except ImportError:
        pass

    # Pure-Python fallback (slower, same result).
    small = 192
    img = Image.new("RGBA", (small, small))
    px_obj = img.load()
    for j in range(small):
        y = (j + 0.5) / small * 2.0 - 1.0
        for i in range(small):
            x = (i + 0.5) / small * 2.0 - 1.0
            t = _smoothstep((x + y) * 0.25 + 0.5)
            r = _lerp(COLOR_TOP_LEFT[0], COLOR_BOTTOM_RIGHT[0], t)
            g = _lerp(COLOR_TOP_LEFT[1], COLOR_BOTTOM_RIGHT[1], t)
            b = _lerp(COLOR_TOP_LEFT[2], COLOR_BOTTOM_RIGHT[2], t)
            highlight = 0.16 * math.exp(-(((x + 0.55) ** 2 + (y + 0.65) ** 2) / 0.30))
            r = _lerp(r, 255.0, highlight)
            g = _lerp(g, 255.0, highlight)
            b = _lerp(b, 255.0, highlight)
            vignette = 0.10 * math.exp(-(((x - 0.78) ** 2 + (y - 0.82) ** 2) / 0.55))
            r = _lerp(r, COLOR_VIGNETTE[0], vignette)
            g = _lerp(g, COLOR_VIGNETTE[1], vignette)
            b = _lerp(b, COLOR_VIGNETTE[2], vignette)
            px_obj[i, j] = (int(r), int(g), int(b), 255)
    GRADIENT_CACHE = img.resize((px, px), Image.LANCZOS)
    return GRADIENT_CACHE


def _smoothstep_np(t):  # numpy version of _smoothstep
    return t * t * (3.0 - 2.0 * t)


# --------------------------------------------------------------------------
# Geometry helpers
# --------------------------------------------------------------------------


def _ss(size: int) -> int:
    """Supersampling factor for a target size."""
    if size <= 384:
        return 8
    if size <= 1024:
        return 4
    if size <= 2048:
        return 2
    return 1


def _detail_for(size: int) -> str:
    """How much Command-Palette motif a given pixel size can carry."""
    if size <= 48:
        return "micro"   # magnifier only: rows would turn into mud below ~50 px
    if size <= 127:
        return "small"   # two rows
    return "full"        # three rows


def _rounded_mask(size: int, radius: int) -> Image.Image:
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return mask


# --------------------------------------------------------------------------
# Glyph (white magnifying glass + palette rows)
# --------------------------------------------------------------------------


def _glyph_metrics(inner: int, detail: str) -> dict:
    if detail == "micro":
        return dict(r=0.210 * inner, w=0.100 * inner, lens=(0.452, 0.452), handle=0.185 * inner)
    return dict(r=0.200 * inner, w=0.088 * inner, lens=(0.451, 0.451), handle=0.190 * inner)


def _draw_glyph_layer(size: int, detail: str) -> Image.Image:
    """Transparent square layer with the centred magnifier glyph."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    _draw_glyph(layer, detail)
    return layer


def _draw_glyph(tile: Image.Image, detail: str) -> None:
    """Draw the magnifier glyph on top of `tile` (RGBA, opaque inside the tile)."""
    inner = tile.size[0]
    m = _glyph_metrics(inner, detail)
    cx = m["lens"][0] * inner
    cy = m["lens"][1] * inner
    r = m["r"]
    w = max(1, int(round(m["w"])))
    color = (255, 255, 255, 255)

    draw = ImageDraw.Draw(tile)
    # ring
    draw.ellipse(
        [cx - (r - w / 2), cy - (r - w / 2), cx + (r - w / 2), cy + (r - w / 2)],
        outline=color,
        width=w,
    )
    # handle at 45 degrees, round caps
    ang = math.radians(45.0)
    dx, dy = math.cos(ang), math.sin(ang)
    start = (cx + (r - w * 0.35) * dx, cy + (r - w * 0.35) * dy)
    end = (start[0] + m["handle"] * dx, start[1] + m["handle"] * dy)
    draw.line([start, end], fill=color, width=w, joint="curve")
    for p in (start, end):
        draw.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill=color)

    if detail == "micro":
        return

    # palette rows clipped to the inside of the lens (short lines, with margin
    # from the ring, so the lens still reads as a lens and not as a striped disc)
    if detail == "small":
        rows = [(-0.032, 0.098, 0.028), (0.032, 0.098, 0.028)]
    else:
        rows = [
            (-0.043, 0.104, 0.018),
            (0.0, 0.084, 0.018),
            (0.043, 0.094, 0.018),
        ]
    inner_r = r - w - inner * 0.014
    rows_layer = Image.new("RGBA", (inner, inner), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rows_layer)
    for oy, rw, rh in rows:
        h = max(1, int(round(rh * inner)))
        width = rw * inner
        y = cy + oy * inner
        x0 = cx - width / 2
        rd.rounded_rectangle(
            [x0, y - h / 2, x0 + width, y + h / 2],
            radius=h / 2,
            fill=(255, 255, 255, 235),
        )
    clip = Image.new("L", (inner, inner), 0)
    ImageDraw.Draw(clip).ellipse(
        [cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r], fill=255
    )
    rows_layer.putalpha(ImageChops.multiply(rows_layer.getchannel("A"), clip))
    tile.alpha_composite(rows_layer)


# --------------------------------------------------------------------------
# Icon renderers
# --------------------------------------------------------------------------


def _render_tile(inner: int, detail: str, rounded: bool, *, draw_glyph: bool = True) -> Image.Image:
    grad = _build_gradient().resize((inner, inner), Image.LANCZOS)
    radius = int(round(inner * CORNER_RADIUS_RATIO))
    if rounded:
        mask = _rounded_mask(inner, radius)
    else:
        mask = Image.new("L", (inner, inner), 255)
    tile = Image.new("RGBA", (inner, inner), (0, 0, 0, 0))
    tile.paste(grad, (0, 0), mask)

    # inner light rim (Fluent-ish edge)
    rim = Image.new("RGBA", (inner, inner), (0, 0, 0, 0))
    ImageDraw.Draw(rim).rounded_rectangle(
        [0, 0, inner - 1, inner - 1],
        radius=radius,
        outline=(255, 255, 255, RIM_ALPHA),
        width=max(1, int(round(inner * 0.007))),
    )
    rim.putalpha(ImageChops.multiply(rim.getchannel("A"), mask))
    tile.alpha_composite(rim)

    if draw_glyph:
        tile.alpha_composite(_draw_glyph_layer(inner, detail))
    return tile


def _render_app_icon(
    size: int,
    detail: str,
    *,
    rounded: bool = True,
    margin: float = 0.0,
    shadow: bool = False,
) -> Image.Image:
    """Square app-icon style asset on a transparent canvas."""
    ss = _ss(size)
    total = size * ss
    m = int(round(total * margin))
    inner = total - 2 * m
    tile = _render_tile(inner, detail, rounded)

    canvas = Image.new("RGBA", (total, total), (0, 0, 0, 0))
    if shadow:
        alpha = tile.getchannel("A").point(lambda a: int(a * 0.32))
        shadow_layer = Image.new("RGBA", (total, total), (15, 23, 60, 0))
        shadow_layer.putalpha(alpha)
        shadow_layer = shadow_layer.filter(ImageFilter.GaussianBlur(max(1, int(total * 0.018))))
        canvas.alpha_composite(shadow_layer, (m, m + int(total * 0.02)))
    canvas.alpha_composite(tile, (m, m))
    return canvas.resize((size, size), Image.LANCZOS)


def _render_tile_background(width: int, height: int, detail: str) -> Image.Image:
    """Full-bleed square tile artwork (no transparency, no rounded corners)."""
    ss = _ss(max(width, height))
    W, H = width * ss, height * ss
    tile = _build_gradient().resize((W, H), Image.LANCZOS)

    # glyph, sized by the shorter edge, centred (drawn on its own transparent
    # layer so no gradient/rim seam appears inside the tile)
    short = min(W, H)
    if detail == "micro":
        detail = "small"
    glyph = _draw_glyph_layer(short, detail)
    tile.alpha_composite(glyph, ((W - short) // 2, (H - short) // 2))
    return tile.resize((width, height), Image.LANCZOS).convert("RGBA")


def _render_wide(width: int, height: int, detail: str) -> Image.Image:
    """Wide tile: full-bleed gradient, magnifier left, three palette rows right."""
    ss = _ss(max(width, height))
    W, H = width * ss, height * ss
    tile = _build_gradient().resize((W, H), Image.LANCZOS)

    # glyph at ~0.92 * height so it leaves room for the rows on the right
    glyph_size = int(round(H * 0.92))
    glyph = _draw_glyph_layer(glyph_size, "full")
    glyph_x = int(round(W * 0.285 - glyph_size / 2))
    glyph_y = int(round(H * 0.50 - glyph_size / 2))
    tile.alpha_composite(glyph, (glyph_x, glyph_y))

    # palette rows on the right (the "Command Palette result list" motif)
    draw = ImageDraw.Draw(tile)
    row_x0 = W * 0.585
    row_w = W * 0.285
    bar_h = max(1, int(round(H * 0.052)))
    gap = H * 0.078
    center_y = H * 0.50
    widths = [1.0, 0.84, 0.93]
    total_h = len(widths) * bar_h + (len(widths) - 1) * gap
    y = center_y - total_h / 2 + bar_h / 2
    for wf in widths:
        draw.rounded_rectangle(
            [row_x0, y - bar_h / 2, row_x0 + row_w * wf, y + bar_h / 2],
            radius=bar_h / 2,
            fill=(255, 255, 255, 220),
        )
        y += bar_h + gap

    return tile.resize((width, height), Image.LANCZOS).convert("RGBA")


def _render_splash(width: int, height: int) -> Image.Image:
    """Splash screen: full-bleed gradient with a centred rounded app icon."""
    ss = _ss(max(width, height))
    W, H = width * ss, height * ss
    bg = _build_gradient().resize((W, H), Image.LANCZOS)

    icon_size = int(round(H * 0.56))
    # icon tile at supersampled resolution, with a soft drop shadow
    tile = _render_tile(icon_size, "full", rounded=True)
    alpha = tile.getchannel("A").point(lambda a: int(a * 0.40))
    sh_shadow = Image.new("RGBA", (icon_size, icon_size), (15, 23, 60, 255))
    sh_shadow.putalpha(alpha)
    sh_shadow = sh_shadow.filter(ImageFilter.GaussianBlur(max(1, int(icon_size * 0.035))))
    bg.alpha_composite(sh_shadow, ((W - icon_size) // 2, (H - icon_size) // 2 + int(icon_size * 0.03)))
    bg.alpha_composite(tile, ((W - icon_size) // 2, (H - icon_size) // 2))
    return bg.resize((width, height), Image.LANCZOS).convert("RGBA")


# --------------------------------------------------------------------------
# Asset table
# --------------------------------------------------------------------------


@dataclass(frozen=True)
class Asset:
    relpath: str          # relative to ASSETS_DIR (or STORE_ASSETS_DIR when store=True)
    kind: str             # "logo" | "unplated" | "tile" | "wide" | "splash" | "store"
    width: int
    height: int
    store: bool = False

    @property
    def path(self) -> str:
        base = STORE_ASSETS_DIR if self.store else ASSETS_DIR
        return os.path.join(base, self.relpath.replace("/", os.sep))


def _scale(base: int, factor: float) -> int:
    return int(math.floor(base * factor + 0.5))


def _build_asset_table() -> list[Asset]:
    assets: list[Asset] = []
    # Scale variants: the unqualified base file is the neutral fallback, and each
    # qualifier-named file is what MRT picks at that display scale (scale-100 is
    # an explicit copy of the base for completeness).
    scale_steps = ((1.0, ""), (1.0, ".scale-100"), (1.25, ".scale-125"),
                   (1.5, ".scale-150"), (2.0, ".scale-200"), (4.0, ".scale-400"))
    for factor, suffix in scale_steps:
        assets.append(Asset(f"StoreLogo{suffix}.png", "logo", _scale(50, factor), _scale(50, factor)))
    for factor, suffix in scale_steps:
        assets.append(Asset(f"Square44x44Logo{suffix}.png", "logo",
                            _scale(44, factor), _scale(44, factor)))
    for factor, suffix in scale_steps:
        assets.append(Asset(f"Square150x150Logo{suffix}.png", "tile",
                            _scale(150, factor), _scale(150, factor)))
    for factor, suffix in scale_steps:
        assets.append(Asset(f"Wide310x150Logo{suffix}.png", "wide",
                            _scale(310, factor), _scale(150, factor)))
    for factor, suffix in scale_steps:
        assets.append(Asset(f"SplashScreen{suffix}.png", "splash",
                            _scale(620, factor), _scale(300, factor)))
    for target in (16, 24, 32, 48):
        assets.append(Asset(
            f"Square44x44Logo.targetsize-{target}_altform-unplated.png",
            "unplated", target, target))
    # optional jumbo unplated variant (used for large shell icons)
    assets.append(Asset(
        "Square44x44Logo.targetsize-256_altform-unplated.png", "unplated", 256, 256))
    # lock-screen logo (kept for completeness; not referenced by the manifest today)
    assets.append(Asset("LockScreenLogo.png", "unplated", 24, 24))
    # Partner Center Store listing icon (1:1, 300x300 recommended)
    assets.append(Asset("store-icon-300.png", "store", 300, 300, store=True))
    return assets


ASSETS = _build_asset_table()


# --------------------------------------------------------------------------
# Rendering dispatch
# --------------------------------------------------------------------------


def _render(asset: Asset) -> Image.Image:
    detail = _detail_for(min(asset.width, asset.height))
    if asset.kind == "logo":
        return _render_app_icon(asset.width, detail, margin=0.0)
    if asset.kind == "store":
        return _render_app_icon(asset.width, detail, margin=0.0)
    if asset.kind == "unplated":
        return _render_app_icon(asset.width, detail, margin=0.02)
    if asset.kind == "tile":
        return _render_tile_background(asset.width, asset.height, detail)
    if asset.kind == "wide":
        return _render_wide(asset.width, asset.height, detail)
    if asset.kind == "splash":
        return _render_splash(asset.width, asset.height)
    raise ValueError(f"unknown kind: {asset.kind}")


# --------------------------------------------------------------------------
# Generate / verify / inventory
# --------------------------------------------------------------------------


def generate(quiet: bool = False) -> list[Asset]:
    os.makedirs(ASSETS_DIR, exist_ok=True)
    os.makedirs(STORE_ASSETS_DIR, exist_ok=True)
    for asset in ASSETS:
        img = _render(asset)
        os.makedirs(os.path.dirname(asset.path), exist_ok=True)
        img.save(asset.path, "PNG", optimize=True)
        if not quiet:
            print(f"  wrote {os.path.relpath(asset.path, REPO_ROOT)}  {img.size[0]}x{img.size[1]}")
    return ASSETS


def verify(quiet: bool = False) -> tuple[list[tuple], list[str]]:
    """Verify every expected PNG exists, has the right dimensions and is RGBA."""
    rows: list[tuple] = []
    errors: list[str] = []
    for asset in ASSETS:
        rel = os.path.relpath(asset.path, REPO_ROOT)
        if not os.path.isfile(asset.path):
            errors.append(f"MISSING {rel}")
            continue
        try:
            with Image.open(asset.path) as im:
                size = im.size
                mode = im.mode
                fmt = im.format
        except Exception as exc:  # noqa: BLE001
            errors.append(f"UNREADABLE {rel}: {exc}")
            continue
        nbytes = os.path.getsize(asset.path)
        digest = hashlib.sha256(open(asset.path, "rb").read()).hexdigest()
        rows.append((rel, size[0], size[1], mode, nbytes, digest))
        if size != (asset.width, asset.height):
            errors.append(f"SIZE {rel}: expected {asset.width}x{asset.height}, got {size[0]}x{size[1]}")
        if fmt != "PNG":
            errors.append(f"FORMAT {rel}: expected PNG, got {fmt}")
        if mode != "RGBA":
            errors.append(f"MODE {rel}: expected RGBA, got {mode}")
    return rows, errors


def write_inventory(rows: list[tuple]) -> str:
    os.makedirs(STORE_ASSETS_DIR, exist_ok=True)
    path = os.path.join(STORE_ASSETS_DIR, "icon-inventory.md")
    lines = [
        "# 应用图标资源清单 / App icon asset inventory",
        "",
        "> 由 `scripts/generate-icons.py` 自动生成，请勿手改。",
        "> Auto-generated by `scripts/generate-icons.py`; do not edit by hand.",
        "",
        "| 文件 / File | 尺寸 / Size | 字节 / Bytes | 角色 / Role | SHA-256 (前 12 位) |",
        "| --- | --- | ---: | --- | --- |",
    ]
    for rel, w, h, mode, nbytes, digest in rows:
        role = {
            "logo": "app icon (rounded, transparent corners)",
            "unplated": "unplated app icon (transparent corners)",
            "tile": "full-bleed tile artwork",
            "wide": "full-bleed wide tile artwork",
            "splash": "splash screen (full-bleed)",
            "store": "Partner Center Store listing icon (300x300)",
        }[next(a.kind for a in ASSETS if os.path.relpath(a.path, REPO_ROOT) == rel)]
        lines.append(f"| `{os.path.basename(rel)}` | {w}×{h} | {nbytes:,} | {role} | `{digest[:12]}` |")
    lines += [
        "",
        "## 生成方式 / How to regenerate",
        "",
        "```powershell",
        "# use the DSH bundled Python (has Pillow); any Python 3.10+ with Pillow works",
        "& $py scripts/generate-icons.py --preview",
        "& $py scripts/generate-icons.py --check",
        "```",
        "",
        "说明：≤48 px 的图标不画镜内“命令面板行”，否则在 16 px 下会糊成一团；",
        "49–127 px 使用两行，≥128 px 使用三行。所有尺寸都由同一套参数化几何绘制",
        "后 Lanczos 降采样，无网络、无外部素材，可复现。",
        "",
    ]
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    return path


# --------------------------------------------------------------------------
# Preview sheet (QA aid, not referenced by the package)
# --------------------------------------------------------------------------

PREVIEW_ITEMS: list[tuple[str, str]] = [
    ("src/EverythingPal/Assets/StoreLogo.png", "StoreLogo 50"),
    ("src/EverythingPal/Assets/Square44x44Logo.png", "Square44 44"),
    ("src/EverythingPal/Assets/Square44x44Logo.scale-400.png", "Square44 176"),
    ("src/EverythingPal/Assets/Square150x150Logo.png", "Square150 150"),
    ("src/EverythingPal/Assets/Square44x44Logo.targetsize-16_altform-unplated.png", "unplated 16"),
    ("src/EverythingPal/Assets/Square44x44Logo.targetsize-24_altform-unplated.png", "unplated 24"),
    ("src/EverythingPal/Assets/Square44x44Logo.targetsize-32_altform-unplated.png", "unplated 32"),
    ("src/EverythingPal/Assets/Square44x44Logo.targetsize-48_altform-unplated.png", "unplated 48"),
    ("src/EverythingPal/Assets/StoreLogo.scale-400.png", "StoreLogo 200"),
    ("src/EverythingPal/Assets/Wide310x150Logo.png", "Wide 310x150"),
    ("src/EverythingPal/Assets/SplashScreen.png", "Splash 620x300"),
    ("docs/store/assets/store-icon-300.png", "Store icon 300"),
]


def _preview() -> str:
    """Contact sheet on light/dark backgrounds; every asset is fitted to its cell."""
    os.makedirs(STORE_ASSETS_DIR, exist_ok=True)
    cell_w, cell_h = 240, 210
    cols = 3
    rows = math.ceil(len(PREVIEW_ITEMS) / cols)
    pad = 18
    header = 54
    img = Image.new("RGBA", (cols * cell_w + pad * 2, rows * cell_h + pad * 2 + header),
                    (243, 244, 246, 255))
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.load_default(size=15)
        small_font = ImageFont.load_default(size=12)
    except TypeError:  # very old Pillow
        font = ImageFont.load_default()
        small_font = font
    draw.text((pad, 16), "EverythingPal icon preview - light / dark", font=font,
              fill=(17, 24, 39, 255))
    draw.text((pad, 36), "small assets are nearest-neighbour upscaled for inspection",
              font=small_font, fill=(107, 114, 128, 255))
    for idx, (rel, label) in enumerate(PREVIEW_ITEMS):
        row, col = divmod(idx, cols)
        x0 = pad + col * cell_w
        y0 = header + pad + row * cell_h
        half_w = (cell_w - 8) // 2
        box_w, box_h = half_w - 24, cell_h - 74
        for theme, bg in ((0, (255, 255, 255, 255)), (1, (31, 41, 55, 255))):
            bx = x0 + theme * (half_w + 8)
            draw.rounded_rectangle([bx, y0, bx + half_w, y0 + cell_h - 44], radius=10, fill=bg)
        src = os.path.join(REPO_ROOT, rel.replace("/", os.sep))
        if not os.path.isfile(src):
            continue
        with Image.open(src) as im:
            w, h = im.size
            if max(w, h) <= 56:  # upscale tiny icons for inspection
                up = max(1, min(6, int(box_h // max(1, max(w, h)))))
                shown = im.resize((w * up, h * up), Image.NEAREST)
            else:
                factor = min(box_w / w, box_h / h, 1.0)
                shown = im.resize((max(1, int(w * factor)), max(1, int(h * factor))),
                                  Image.LANCZOS)
        for theme in (0, 1):
            bx = x0 + theme * (half_w + 8)
            px = bx + (half_w - shown.size[0]) // 2
            py = y0 + (cell_h - 44 - shown.size[1]) // 2
            img.alpha_composite(shown, (max(bx, px), max(y0, py)))
        draw.text((x0 + 2, y0 + cell_h - 38), label, font=small_font, fill=(17, 24, 39, 255))
    path = os.path.join(STORE_ASSETS_DIR, "icon-preview.png")
    img.convert("RGB").save(path, "PNG", optimize=True)
    return path


# --------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate/verify the EverythingPal icon set.")
    parser.add_argument("--check", action="store_true", help="verify existing assets, do not write")
    parser.add_argument("--preview", action="store_true", help="also write docs/store/assets/icon-preview.png")
    parser.add_argument("--quiet", action="store_true", help="only print the summary")
    args = parser.parse_args(argv)

    if args.check:
        rows, errors = verify(quiet=args.quiet)
    else:
        generate(quiet=args.quiet)
        rows, errors = verify(quiet=args.quiet)
        inv = write_inventory(rows)
        if not args.quiet:
            print(f"  wrote {os.path.relpath(inv, REPO_ROOT)}")
        if args.preview:
            p = _preview()
            if not args.quiet:
                print(f"  wrote {os.path.relpath(p, REPO_ROOT)}")

    print()
    print(f"{'file':<62} {'size':>10} {'bytes':>8}  sha256[:12]")
    print("-" * 98)
    for rel, w, h, mode, nbytes, digest in rows:
        print(f"{rel:<62} {f'{w}x{h}':>10} {nbytes:>8,}  {digest[:12]}")
    print()
    print(f"{len(rows)} PNG assets, {sum(r[4] for r in rows):,} bytes total")
    if errors:
        print("\nFAILED:")
        for e in errors:
            print(f"  - {e}")
        return 1
    print("OK: all assets present with the expected dimensions.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
