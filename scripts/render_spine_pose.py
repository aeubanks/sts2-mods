#!/usr/bin/env python3
"""Render a Spine 3.x setup pose (.atlas + .json + .png) into a crisp RGBA PNG.

Uses 2x supersampling with pure-Python affine fitting (no numpy required).
Intended for porting StS 1 character Spine assets into StS 2 Sprite2D scenes.
"""

import argparse
import json
import math
import os
from PIL import Image


def solve_3x3(A, B):
    M = [row[:] + [b] for row, b in zip(A, B)]
    for i in range(3):
        piv = max(range(i, 3), key=lambda r: abs(M[r][i]))
        M[i], M[piv] = M[piv], M[i]
        div = M[i][i]
        if abs(div) < 1e-12:
            return None
        M[i] = [v / div for v in M[i]]
        for r in range(3):
            if r != i:
                f = M[r][i]
                M[r] = [M[r][c] - f * M[i][c] for c in range(4)]
    return [M[i][3] for i in range(3)]


def fit_affine_ls(dst_pts, src_pts):
    ATA = [[0.0] * 3 for _ in range(3)]
    ATu = [0.0] * 3
    ATv = [0.0] * 3
    for (x, y), (u, v) in zip(dst_pts, src_pts):
        row = (x, y, 1.0)
        for i in range(3):
            ATu[i] += row[i] * u
            ATv[i] += row[i] * v
            for j in range(3):
                ATA[i][j] += row[i] * row[j]
    cu = solve_3x3(ATA, ATu)
    cv = solve_3x3(ATA, ATv)
    if cu is None or cv is None:
        return None
    return (cu[0], cu[1], cu[2], cv[0], cv[1], cv[2])


def parse_atlas(atlas_path):
    """Parse a Spine .atlas file, supporting multi-page atlases."""
    base_dir = os.path.dirname(atlas_path)
    pages = {}
    regions = {}
    current_page = None
    current_region = None

    with open(atlas_path, "r", encoding="utf-8") as f:
        lines = [line.rstrip("\r\n") for line in f]

    for line in lines:
        if not line.strip():
            current_region = None
            continue
        if not line.startswith("  "):
            if line.lower().endswith(".png"):
                current_page = line.strip()
                pages[current_page] = Image.open(os.path.join(base_dir, current_page)).convert("RGBA")
                current_region = None
            elif current_region is None and ":" in line:
                continue
            else:
                current_region = line.strip()
                regions[current_region] = {"_page": current_page}
        elif current_region is not None:
            k, v = [x.strip() for x in line.split(":", 1)]
            regions[current_region][k] = v

    return pages, regions


def render_spine_pose(
    atlas_path,
    json_path,
    out_path,
    width=304,
    height=320,
    scale=1.2,
    origin_x=None,
    origin_y=None,
    skin_name="default",
    flip_x=False,
    skip_slots=(),
):
    pages, regions = parse_atlas(atlas_path)
    with open(json_path, "r", encoding="utf-8") as f:
        skel = json.load(f)

    bones_by_name = {}
    bone_list = []
    for b in skel.get("bones", []):
        bd = {
            "name": b["name"],
            "parent": b.get("parent"),
            "x": b.get("x", 0.0),
            "y": b.get("y", 0.0),
            "rotation": b.get("rotation", 0.0),
            "scaleX": b.get("scaleX", 1.0),
            "scaleY": b.get("scaleY", 1.0),
            "length": b.get("length", 0.0),
            "transform": b.get("transform", "normal"),
            "inheritRotation": b.get("inheritRotation", True),
        }
        bones_by_name[bd["name"]] = bd
        bone_list.append(bd)

    def update_world():
        for b in bone_list:
            p = bones_by_name.get(b["parent"])
            rad = math.radians(b["rotation"])
            c, s = math.cos(rad), math.sin(rad)
            la, lb = c * b["scaleX"], -s * b["scaleY"]
            lc, ld = s * b["scaleX"], c * b["scaleY"]
            if p is None:
                b["wx"], b["wy"] = b["x"], b["y"]
                b["a"], b["b"], b["c"], b["d"] = la, lb, lc, ld
            else:
                b["wx"] = p["a"] * b["x"] + p["b"] * b["y"] + p["wx"]
                b["wy"] = p["c"] * b["x"] + p["d"] * b["y"] + p["wy"]
                if b["transform"] == "noRotationOrReflection" or not b["inheritRotation"]:
                    b["a"], b["b"], b["c"], b["d"] = la, lb, lc, ld
                else:
                    b["a"] = p["a"] * la + p["b"] * lc
                    b["b"] = p["a"] * lb + p["b"] * ld
                    b["c"] = p["c"] * la + p["d"] * lc
                    b["d"] = p["c"] * lb + p["d"] * ld

    update_world()
    for ik in skel.get("ik", []):
        if ik.get("mix", 1.0) == 0:
            continue
        if len(ik.get("bones", [])) == 2:
            b1 = bones_by_name[ik["bones"][0]]
            b2 = bones_by_name[ik["bones"][1]]
            tgt = bones_by_name[ik["target"]]
            bend_dir = 1 if ik.get("bendPositive", True) else -1

            p = bones_by_name.get(b1["parent"])
            if (
                p is not None
                and b1["transform"] not in ("noRotationOrReflection", "onlyTranslation")
                and b1["inheritRotation"]
            ):
                dx, dy = tgt["wx"] - p["wx"], tgt["wy"] - p["wy"]
                det = p["a"] * p["d"] - p["b"] * p["c"]
                if abs(det) < 1e-9:
                    continue
                tx = (p["d"] * dx - p["b"] * dy) / det - b1["x"]
                ty = (-p["c"] * dx + p["a"] * dy) / det - b1["y"]
                if det < 0:
                    bend_dir = -bend_dir
            else:
                tx, ty = tgt["wx"] - b1["wx"], tgt["wy"] - b1["wy"]

            l1 = math.hypot(b2["x"] * b1["scaleX"], b2["y"] * b1["scaleY"]) or b1["length"]
            l2 = b2["length"]
            d = math.hypot(tx, ty)
            if l1 > 0 and l2 > 0:
                cos_a2 = max(-1.0, min(1.0, (d * d - l1 * l1 - l2 * l2) / (2 * l1 * l2)))
                a2 = math.acos(cos_a2) * bend_dir
                a1 = math.atan2(ty, tx) - math.atan2(l2 * math.sin(a2), l1 + l2 * math.cos(a2))
                if b2["x"] != 0.0 or b2["y"] != 0.0:
                    a1 -= math.atan2(b2["y"] * b1["scaleY"], b2["x"] * b1["scaleX"])
                b1["rotation"] = math.degrees(a1)
                b2["rotation"] = math.degrees(a2)
                update_world()

    ss = 2
    W, H = width * ss, height * ss
    ox = (width / 2.0 if origin_x is None else origin_x) * ss
    oy = (height - 28.0 if origin_y is None else origin_y) * ss
    sc = scale * ss
    sx_sign = -1.0 if flip_x else 1.0
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def to_px(wx, wy):
        return (ox + wx * sc * sx_sign, oy - wy * sc)

    skins = skel.get("skins", {})
    if isinstance(skins, list):
        skin_map = {s["name"]: s.get("attachments", {}) for s in skins}
    else:
        skin_map = skins
    if skin_name != "default" and skin_name not in skin_map:
        raise ValueError(f"Skin '{skin_name}' not found in {json_path} (available: {list(skin_map.keys())})")
    active_skin = skin_map.get(skin_name, next(iter(skin_map.values()), {}))
    default_skin = skin_map.get("default", {})

    skip_set = set(skip_slots)
    for slot in skel.get("slots", []):
        sname = slot["name"]
        if sname in skip_set:
            continue
        color_hex = slot.get("color", "ffffffff")
        if len(color_hex) == 8 and color_hex[-2:].lower() == "00":
            continue
        aname = slot.get("attachment")
        if not aname:
            continue
        att = active_skin.get(sname, {}).get(aname) or default_skin.get(sname, {}).get(aname)
        if not att:
            continue
        att_type = att.get("type", "region")
        if att_type not in ("region", "mesh", "weightedmesh"):
            continue
        reg_name = att.get("path", att.get("name", aname))
        reg = regions.get(reg_name) or regions.get(aname)
        if not reg:
            continue

        atlas_img = pages[reg["_page"]]
        rx, ry = [int(v) for v in reg["xy"].split(",")]
        rw, rh = [int(v) for v in reg["size"].split(",")]
        orig_w, orig_h = [int(v) for v in reg["orig"].split(",")]
        off_x, off_y = [int(v) for v in reg["offset"].split(",")]
        rot_val = reg.get("rotate", "false").lower()
        if rot_val in ("true", "90"):
            crop = atlas_img.crop((rx, ry, rx + rh, ry + rw)).rotate(-90, expand=True)
        else:
            crop = atlas_img.crop((rx, ry, rx + rw, ry + rh))
        part = Image.new("RGBA", (orig_w, orig_h), (0, 0, 0, 0))
        part.paste(crop, (off_x, orig_h - rh - off_y))

        b = bones_by_name[slot["bone"]]
        if att_type == "region":
            ax, ay = att.get("x", 0.0), att.get("y", 0.0)
            arot = att.get("rotation", 0.0)
            asx, asy = att.get("scaleX", 1.0), att.get("scaleY", 1.0)
            aw, ah = att.get("width", orig_w), att.get("height", orig_h)
            rad = math.radians(arot)
            ca, sa = math.cos(rad), math.sin(rad)

            def local_to_world(lx, ly):
                lx *= asx
                ly *= asy
                bx = ca * lx - sa * ly + ax
                by = sa * lx + ca * ly + ay
                wx = b["a"] * bx + b["b"] * by + b["wx"]
                wy = b["c"] * bx + b["d"] * by + b["wy"]
                return to_px(wx, wy)

            dst_pts = [
                local_to_world(-aw / 2, ah / 2),
                local_to_world(aw / 2, ah / 2),
                local_to_world(-aw / 2, -ah / 2),
            ]
            src_pts = [(0.0, 0.0), (float(orig_w), 0.0), (0.0, float(orig_h))]
            coeffs = fit_affine_ls(dst_pts, src_pts)
        else:
            verts = att["vertices"]
            uvs = att["uvs"]
            n_uv = len(uvs) // 2
            world_pts = []
            if len(verts) == len(uvs):
                for i in range(n_uv):
                    vx, vy = verts[2 * i], verts[2 * i + 1]
                    wx = b["a"] * vx + b["b"] * vy + b["wx"]
                    wy = b["c"] * vx + b["d"] * vy + b["wy"]
                    world_pts.append(to_px(wx, wy))
            else:
                idx = 0
                while idx < len(verts):
                    count = int(verts[idx])
                    idx += 1
                    wx = wy = 0.0
                    for _ in range(count):
                        bi = int(verts[idx])
                        vx, vy, w = verts[idx + 1], verts[idx + 2], verts[idx + 3]
                        idx += 4
                        bb = bone_list[bi]
                        wx += (bb["a"] * vx + bb["b"] * vy + bb["wx"]) * w
                        wy += (bb["c"] * vx + bb["d"] * vy + bb["wy"]) * w
                    world_pts.append(to_px(wx, wy))
            src_pts = [(uvs[2 * i] * orig_w, uvs[2 * i + 1] * orig_h) for i in range(n_uv)]
            coeffs = fit_affine_ls(world_pts, src_pts)

        if coeffs is None:
            continue
        warped = part.transform((W, H), Image.Transform.AFFINE, coeffs, resample=Image.Resampling.BICUBIC)
        canvas = Image.alpha_composite(canvas, warped)

    final_img = canvas.resize((width, height), Image.Resampling.LANCZOS)
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    final_img.save(out_path)
    return final_img.size, final_img.getbbox()


def main():
    p = argparse.ArgumentParser(description="Render Spine 3.x setup pose to PNG.")
    p.add_argument("--atlas", required=True)
    p.add_argument("--json", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--width", type=int, default=304)
    p.add_argument("--height", type=int, default=320)
    p.add_argument("--scale", type=float, default=1.2)
    p.add_argument("--origin-x", type=float, default=None)
    p.add_argument("--origin-y", type=float, default=None)
    p.add_argument("--skin", default="default")
    p.add_argument("--flip-x", action="store_true")
    p.add_argument("--skip-slot", action="append", default=[])
    args = p.parse_args()
    size, bbox = render_spine_pose(
        args.atlas,
        args.json,
        args.out,
        width=args.width,
        height=args.height,
        scale=args.scale,
        origin_x=args.origin_x,
        origin_y=args.origin_y,
        skin_name=args.skin,
        flip_x=args.flip_x,
        skip_slots=args.skip_slot,
    )
    print(f"Rendered {args.out}: size={size}, bbox={bbox}")


if __name__ == "__main__":
    main()
