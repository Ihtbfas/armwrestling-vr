"""Select only public Rocketbox captures; preserve the private second rig locally."""
import argparse
import csv
import json
from pathlib import Path
import shutil
import tempfile

from PIL import Image, ImageFont, ImageDraw
import matplotlib
from build_p2c_visual_evidence import pair, animated_pair, digest, ROOT


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, required=True)
    parser.add_argument("--after", type=Path, required=True)
    parser.add_argument("--source-ref", required=True)
    parser.add_argument("--before-route", choices=("contact","cross"), default="contact")
    parser.add_argument("--before-ref", default="2370f432212f04b0f94d1d5429b236a01b09fa9c")
    parser.add_argument("--output", type=Path, default=ROOT / "docs/validation/p2d-visual")
    parser.add_argument("--guides", type=Path)
    parser.add_argument("--no-front-motion", action="store_true")
    args = parser.parse_args()
    before, after = args.before.resolve(), args.after.resolve()
    if args.guides:
        args.guides = args.guides.resolve()
    # Capture runner reserves these exact route names for public Rocketbox-only data.
    if before.name != args.before_route or after.name != "cross":
        parser.error("Only P2-C contact and P2-D public cross capture routes may be published")
    rows = [list(csv.DictReader((p.parent / "measurements.csv").open())) for p in (before, after)]
    assert len(rows[0]) == len(rows[1]) == 181
    for field in ("angle", "grip_x", "grip_y", "grip_z", "normalized_state_time"):
        assert all(a[field] == b[field] for a, b in zip(*rows)), field
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    font_path = Path(matplotlib.get_data_path()) / "fonts/ttf/DejaVuSans.ttf"
    font = ImageFont.truetype(str(font_path), 22)
    gif_font = ImageFont.truetype(str(font_path), 18)
    labels = ("BEFORE "+args.before_ref[:7], "AFTER " + args.source_ref[:7])
    manifest = {
        "source_ref": args.source_ref,
        "before_behavior_ref": args.before_ref,
        "before_runtime_ref": args.source_ref if args.before_route == "cross" else args.before_ref,
        "validation": "Real Unity Editor Play / 2022.3.62f3c1 / Direct3D11",
        "avatar": "MIT Microsoft Rocketbox Male_Adult_01; both roles",
        "source_folders": [str(p.relative_to(ROOT).as_posix()) for p in (before, after)],
        "matched_inputs": ["angle", "grip_x", "grip_y", "grip_z", "normalized_state_time"],
        "source_keyframes": {}, "files": {},
    }
    for view, stem in {"hands": "eye", "grip-front": "hand-front", "grip-side": "hand-side",
                       "front": "front", "side": "side", "player": "player", **({"grip-back": "hand-back"} if args.before_route == "cross" else {})}.items():
        path = output / f"neutral-{view}.png"
        picture = pair(before, after, stem, 45, font, labels)
        picture.resize((1200, 478), Image.Resampling.LANCZOS).save(path, optimize=True)
        manifest["files"][path.name] = {"angle": 0, "camera": stem, "layout": "before left / after right; uncropped"}
        for folder in (before, after):
            source = folder / f"{stem}-045.png"
            manifest["source_keyframes"][str(source.relative_to(ROOT).as_posix())] = digest(source)
        if view not in ("hands", "front", "side"):
            continue
        sheet = Image.new("RGB", (1200, 1434))
        for row, frame in enumerate((0, 45, 90)):
            sheet.paste(pair(before, after, stem, frame, font, labels).resize((1200, 478), Image.Resampling.LANCZOS), (0, row * 478))
            for folder in (before, after):
                source = folder / f"{stem}-{frame:03d}.png"
                manifest["source_keyframes"][str(source.relative_to(ROOT).as_posix())] = digest(source)
        path = output / f"keyframes-{view}.png"
        sheet.save(path, optimize=True)
        while path.stat().st_size >= 1024 * 1024 and sheet.width > 900:
            sheet = sheet.resize((round(sheet.width * 0.9), round(sheet.height * 0.9)), Image.Resampling.LANCZOS)
            sheet.save(path, optimize=True)
        manifest["files"][path.name] = {"angles": [-30, 0, 30], "camera": stem, "layout": "before left / after right"}
    for view, sequence in (("hands", "e"), ("body-side", "wide-side")) + (() if args.no_front_motion else (("body-front", "wide-front"),)):
        path = output / f"{view}-motion.gif"
        with tempfile.TemporaryDirectory(prefix="p2d-gif-") as temp:
            info = animated_pair(before, after, sequence, path, shutil.which("ffmpeg"), gif_font, Path(temp), labels)
        manifest["files"][path.name] = {**info, "camera": sequence, "layout": "before top / after bottom; uncropped"}
    if args.guides:
        assert args.guides.name == "contact-guide-preview", "Only the public default editor preview may be published"
        for i in range(3):
            source = args.guides / f"targets-{i}.png"
            image = Image.open(source).convert("RGB")
            canvas = Image.new("RGB", (768,612), "#151923")
            canvas.paste(image,(0,36))
            ImageDraw.Draw(canvas).text((10,5),"CONTACT GUIDES | cyan: player / green: opponent | Editor",font=gif_font,fill="white")
            path = output / f"contact-guides-{i}.png"; canvas.save(path,optimize=True)
            manifest["files"][path.name] = {"camera": i,"type": "real Editor preview + projected marker overlay; not main-scene raw frame"}
            manifest["source_keyframes"][str(source.relative_to(ROOT).as_posix())] = digest(source)
    for name, info in manifest["files"].items():
        path = output / name
        assert path.stat().st_size < (8 * 1024 * 1024 if path.suffix == ".gif" else 1024 * 1024), name
        info.update(bytes=path.stat().st_size, sha256=digest(path))
        print(name, info["bytes"])
    shutil.copyfile(ROOT / "Assets/ArmWrestling/Resources/P2A/ROCKETBOX-LICENSE.txt", output / "ROCKETBOX-LICENSE.txt")
    (output / "DEJAVU-LICENSE.txt").write_text(
        "\n".join(line.rstrip() for line in font_path.with_name("LICENSE_DEJAVU").read_text(encoding="utf-8").splitlines()) + "\n", encoding="utf-8")
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
