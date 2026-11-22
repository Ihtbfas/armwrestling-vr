"""Pack the uncropped, public-only Unity Play captures produced by P2FValidation.Run."""
import argparse
import csv
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw, ImageFont

MODELS = ("Male_Adult_01", "Female_Adult_01", "Sports_Female_02", "Male_Adult_03")
VENUES = ("Neon arena", "Daylight gym", "Competition hall")
ROOT = Path(__file__).resolve().parents[1]


def sheet(rows, destination, cell_width, font):
    # Keep the entire camera frame and add a caption above it.
    height = cell_width * 600 // 960
    header = 34
    image = Image.new("RGB", (len(rows[0]) * cell_width, len(rows) * (height + header)), "#17212A")
    draw = ImageDraw.Draw(image)
    for row_index, row in enumerate(rows):
        for col_index, (source, caption) in enumerate(row):
            with Image.open(source) as raw:
                assert raw.size == (960, 600), source
                picture = raw.convert("RGB").resize((cell_width, height), Image.Resampling.LANCZOS)
            x = col_index * cell_width
            y = row_index * (height + header)
            draw.text((x + 9, y + 5), caption, font=font, fill="white")
            image.paste(picture, (x, y + header))
    image.save(destination, optimize=True)


def motion(source, output):
    with tempfile.TemporaryDirectory() as temp:
        for frame in range(60):
            # Preserve real simulation timing: 30 Hz capture, every third frame exported.
            shutil.copy2(source / f"hand-{frame * 3:03}.jpg", Path(temp) / f"frame-{frame:03}.jpg")
        command = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-framerate", "10",
                   "-i", str(Path(temp) / "frame-%03d.jpg"), "-filter_complex",
                   "scale=640:400:flags=lanczos,split[a][b];[a]palettegen=max_colors=192[p];[b][p]paletteuse=dither=bayer",
                   "-loop", "0", str(output)]
        subprocess.run(command, check=True)
    with Image.open(output) as gif:
        assert gif.n_frames == 60
        for index in range(60):
            gif.seek(index)
            assert gif.info["duration"] == 100
    assert output.stat().st_size < 8 * 1024 * 1024


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "docs/validation/p2f-visual")
    args = parser.parse_args()
    dest = args.output
    dest.mkdir(parents=True, exist_ok=True)
    # Same public SIL-OFL font as the in-game HUD.
    font = ImageFont.truetype(str(ROOT / "Assets/ArmWrestling/Resources/Fonts/MatchUI-Regular.ttf"), 20)
    for model in MODELS:
        source = args.capture / model
        with (source / "sweep.csv").open() as file:
            records = list(csv.DictReader(file))
        assert len(records) == 181 and all(int(r["ik_calls"]) > 0 for r in records)
        assert [float(records[f]["angle"]) for f in (0, 45, 90, 180)] == [-30, 0, 30, -30]
        rows = []
        for view, label in (("front", "Front"), ("side", "Side"), ("hand", "Player hand view")):
            rows.append([(source / f"{view}-{f}.png", f"{label}   /   {angle}°") for f, angle in ((0, "-30"), (45, "0"), (90, "+30"))])
        sheet(rows, dest / (model + ".png"), 480, font)
    sheet([[(args.capture / f"venue-{i}.png", title) for i, title in enumerate(VENUES)]],
          dest / "venues.png", 600, font)
    sheet([[(args.capture / (model + "-ready.png"), model + "   /   Ready"),
            (args.capture / model / "hand-45.png", model + "   /   Contest at 0°")] for model in MODELS],
          dest / "ready-contest.png", 480, font)
    for name in ("lobby.png", "lobby-panel.png"):
        shutil.copy2(args.capture / name, dest / name)
    motion(args.capture / MODELS[1], dest / "female-adult-01-grip.gif")


if __name__ == "__main__":
    main()
