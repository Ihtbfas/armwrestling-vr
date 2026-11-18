"""Restore only the three withheld assets and their original .meta files locally."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ASSETS = (
    "Assets/ArmWrestling/Resources/ArmWrestlingFont.ttf",
    "Assets/ArmWrestling/Resources/Characters/Ch03.fbx",
    "Assets/ArmWrestling/Resources/HDRI/circus_arena.hdr",
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="Local original banshouwan.zip; never uploaded")
    args = parser.parse_args()
    inventory = json.loads((ROOT / "docs/p0-source-inventory.json").read_text(encoding="utf-8"))
    expected = {entry["path"]: entry["sha256"] for entry in inventory["files"]}
    pending = []
    with zipfile.ZipFile(args.archive) as archive:
        for asset in ASSETS:
            for relative in (asset, asset + ".meta"):
                data = archive.read(inventory["entry"] + relative)
                if hashlib.sha256(data).hexdigest() != expected[relative]:
                    raise SystemExit("Archive asset does not match baseline: " + relative)
                target = ROOT / relative
                if target.exists() and target.read_bytes() != data:
                    raise SystemExit("Refusing to overwrite a modified local asset: " + relative)
                pending.append((target, data))
    # Check every payload before writing any file. Extraction never follows archive paths.
    for target, data in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        print("Restored locally:", target.relative_to(ROOT))
    print("Assets remain gitignored. Local use does not establish redistribution permission.")


if __name__ == "__main__":
    main()
