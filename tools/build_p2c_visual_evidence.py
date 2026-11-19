"""Publish selected, licensed Editor captures without modifying source frames.

Requires Pillow, ffmpeg and a licensed DejaVu Sans font (Matplotlib bundles it).
Only pass the Rocketbox captures identified in the gallery's provenance notes.
"""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
LIMIT = 8 * 1024 * 1024
BACKGROUND = "#151923"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def pair(before, after, stem, frame, font, labels=("BEFORE 41ef8ae", "AFTER 2370f43")):
    images = [Image.open(folder / f"{stem}-{frame:03d}.png").convert("RGB")
              for folder in (before, after)]
    assert images[0].size == images[1].size == (768, 576)
    output = Image.new("RGB", (1536, 612), BACKGROUND)
    draw = ImageDraw.Draw(output)
    angle = {0: -30, 45: 0, 90: 30}[frame]
    for col, (picture, label) in enumerate(zip(images, labels)):
        draw.text((col * 768 + 12, 5), f"{label} | {angle:+d} deg | {stem}",
                  font=font, fill="white")
        output.paste(picture, (col * 768, 36))
    return output


def animated_pair(before, after, sequence, output, ffmpeg, font, cache, labels=("BEFORE 41ef8ae", "AFTER 2370f43")):
    # Whole original frames, stacked vertically so each hand/body view stays 640 px wide.
    # At 10 fps take every third 30-Hz source frame; no generated intermediate poses.
    for fps, colors in ((10, 128), (8, 96), (8, 64)):
        for n in range(6 * fps):
            source = round(n * 30 / fps)
            angle = -30 * math.cos(2 * math.pi * source / 180)
            canvas = Image.new("RGB", (640, 1024), BACKGROUND)
            draw = ImageDraw.Draw(canvas)
            for row, (folder, label) in enumerate(zip((before, after), labels)):
                picture = Image.open(folder / f"{sequence}-{source:03d}.jpg").convert("RGB")
                assert picture.size == (768, 576)
                draw.text((12, row * 512 + 5), f"{label} | {angle:+05.1f} deg",
                          font=font, fill="white")
                canvas.paste(picture.resize((640, 480), Image.Resampling.LANCZOS), (0, row * 512 + 32))
            canvas.save(cache / f"frame-{n:03d}.png")
        filters = (f"split[a][b];[a]palettegen=max_colors={colors}:stats_mode=full[p];"
                   "[b][p]paletteuse=dither=none:diff_mode=rectangle")
        result = subprocess.run([
            ffmpeg, "-y", "-hide_banner", "-loglevel", "error", "-framerate", str(fps),
            "-i", str(cache / "frame-%03d.png"), "-frames:v", str(6 * fps),
            "-vf", filters, "-loop", "0", str(output)
        ], capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stderr)
        if output.stat().st_size < LIMIT:
            with Image.open(output) as gif:
                assert gif.n_frames == 6 * fps
                durations = []
                for n in range(gif.n_frames):
                    gif.seek(n)
                    gif.load()  # Decode every frame, including palette disposal.
                    durations.append(gif.info.get("duration", 0))
                assert abs(sum(durations) / 1000 - 6) < 0.02
            return {"fps": fps, "frames": 6 * fps, "seconds": sum(durations) / 1000, "colors": colors}
    raise RuntimeError(f"GIF exceeds 8 MiB: {output}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, default=ROOT / ".p0-local/p2c-contact/20261119-145142/legacy")
    parser.add_argument("--after", type=Path, default=ROOT / ".p0-local/p2c-contact/20261119-151827/contact")
    parser.add_argument("--output", type=Path, default=ROOT / "docs/validation/p2c-visual")
    parser.add_argument("--font", type=Path)
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg"))
    args = parser.parse_args()
    args.before, args.after, args.output = (p.resolve() for p in (args.before, args.after, args.output))
    if not args.ffmpeg:
        parser.error("ffmpeg must be on PATH or supplied with --ffmpeg")
    if args.font is None:
        import matplotlib
        args.font = Path(matplotlib.get_data_path()) / "fonts/ttf/DejaVuSans.ttf"
    font_license = args.font.with_name("LICENSE_DEJAVU")
    if not font_license.is_file():
        parser.error("Provide DejaVuSans.ttf with the bundled LICENSE_DEJAVU beside it")
    args.output.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype(str(args.font), 22)
    gif_font = ImageFont.truetype(str(args.font), 18)
    manifest = {"captured_runtime_ref": "2370f432212f04b0f94d1d5429b236a01b09fa9c",
                "before_behavior_ref": "41ef8ae0970dbfb86ac11d8a5f58bd2c989308c1",
                "before_mode": "LegacyContactGrip", "after_mode": "ContactGrip",
                "validation": "Unity Editor Play / 2022.3.62f3c1 / Windows Direct3D11",
                "source_folders": [str(p.relative_to(ROOT).as_posix()) for p in (args.before, args.after)],
                "files": {}, "source_keyframes": {}}
    rows = [list(csv.DictReader((folder.parent / "measurements.csv").open()))
            for folder in (args.before, args.after)]
    assert len(rows[0]) == len(rows[1]) == 181
    for field in ("angle", "grip_x", "grip_y", "grip_z", "normalized_state_time"):
        assert all(a[field] == b[field] for a, b in zip(*rows)), field
    views = {"hands": "eye", "front": "front", "side": "side", "player": "player"}
    for view, stem in views.items():
        path = args.output / f"neutral-{view}.png"
        pair(args.before, args.after, stem, 45, font).save(path, optimize=True)
        manifest["files"][path.name] = {"angle": 0, "camera": stem, "layout": "before left / after right"}
        for folder in (args.before, args.after):
            source = folder / f"{stem}-045.png"
            manifest["source_keyframes"][source.relative_to(ROOT).as_posix()] = digest(source)
        if view == "player":
            continue
        sheet = Image.new("RGB", (1536, 1836), BACKGROUND)
        for row, frame in enumerate((0, 45, 90)):
            sheet.paste(pair(args.before, args.after, stem, frame, font), (0, row * 612))
            for folder in (args.before, args.after):
                source = folder / f"{stem}-{frame:03d}.png"
                manifest["source_keyframes"][source.relative_to(ROOT).as_posix()] = digest(source)
        path = args.output / f"keyframes-{view}.png"
        sheet.save(path, optimize=True)
        manifest["files"][path.name] = {"angles_top_to_bottom": [-30, 0, 30], "camera": stem,
                                       "layout": "before left / after right; original uncropped frames"}
    for view, sequence in (("hands", "e"), ("body-front", "wide-front"), ("body-side", "wide-side")):
        path = args.output / f"{view}-motion.gif"
        with tempfile.TemporaryDirectory(prefix="p2c-gif-") as temp:
            info = animated_pair(args.before, args.after, sequence, path, args.ffmpeg, gif_font, Path(temp))
        info.update({"camera_sequence": sequence, "layout": "before top / after bottom; no crop",
                     "angle_motion": "-30 -> 0 -> +30 -> 0 -> -30"})
        manifest["files"][path.name] = info
        print(path.name, path.stat().st_size, info)
    for name, info in manifest["files"].items():
        path = args.output / name
        info.update({"bytes": path.stat().st_size, "sha256": digest(path)})
    shutil.copyfile(ROOT / "Assets/ArmWrestling/Resources/P2A/ROCKETBOX-LICENSE.txt",
                    args.output / "ROCKETBOX-LICENSE.txt")
    (args.output / "DEJAVU-LICENSE.txt").write_text(
        "\n".join(line.rstrip() for line in font_license.read_text(encoding="utf-8").splitlines()) + "\n", encoding="utf-8")
    (args.output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
