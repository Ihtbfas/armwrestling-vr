"""Check the public source set, original GUIDs, and Unity YAML GUID references."""
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
WITHHELD = (
    "Assets/ArmWrestling/Resources/ArmWrestlingFont.ttf",
    "Assets/ArmWrestling/Resources/Characters/Ch03.fbx",
    "Assets/ArmWrestling/Resources/HDRI/circus_arena.hdr",
)
GUID = re.compile(r"\bguid:\s*([a-f0-9]{32})")


def main():
    inventory = json.loads((ROOT / "docs/p0-source-inventory.json").read_text(encoding="utf-8"))
    original = {entry["path"]: entry for entry in inventory["files"]}
    public = subprocess.check_output(
        ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=ROOT
    ).decode("utf-8").split("\0")
    public = sorted(set(filter(None, public)))
    errors = []
    for name in public:
        path = ROOT / name
        parts = name.lower().split("/")
        if any(part in {"library", "temp", "logs", "obj", "build", "builds", ".p0-local"} for part in parts):
            errors.append("Generated/local file in public set: " + name)
        if name in WITHHELD or name.removesuffix(".meta") in WITHHELD:
            errors.append("Withheld asset in public set: " + name)
        if path.is_file() and path.stat().st_size > 50 * 1024 * 1024:
            errors.append("Review large file before upload: " + name)
        if name.startswith("Assets/") and not name.endswith(".meta") and not Path(str(path) + ".meta").exists():
            errors.append("Missing .meta: " + name)

    project_guids = {}
    for meta in (ROOT / "Assets").rglob("*.meta"):
        match = GUID.search(meta.read_text(encoding="utf-8-sig"))
        if not match:
            errors.append("Missing GUID in " + str(meta.relative_to(ROOT)))
            continue
        guid = match.group(1)
        if guid in project_guids:
            errors.append("Duplicate GUID: " + str(meta.relative_to(ROOT)))
        project_guids[guid] = str(meta.relative_to(ROOT))
        relative = meta.relative_to(ROOT).as_posix()
        if relative in original and original[relative].get("guid") != guid:
            errors.append("Original GUID changed: " + relative)

    withheld_guids = {original[name + ".meta"]["guid"] for name in WITHHELD}
    # Audit the public checkout even when the operator restored optional art locally.
    known = set(project_guids) - withheld_guids
    for meta in (ROOT / "Library/PackageCache").rglob("*.meta"):
        match = GUID.search(meta.read_text(encoding="utf-8-sig", errors="replace"))
        if match:
            known.add(match.group(1))
    optional = set()
    for directory in (ROOT / "Assets", ROOT / "ProjectSettings"):
        for path in directory.rglob("*"):
            if path.suffix not in {".unity", ".asset", ".mat", ".controller", ".anim"}:
                continue
            for guid in GUID.findall(path.read_text(encoding="utf-8-sig", errors="replace")):
                if guid in known or guid.startswith("0000000000000000"):
                    continue
                if guid in withheld_guids:
                    optional.add(path.relative_to(ROOT).as_posix())
                else:
                    errors.append("Unresolved YAML GUID " + guid + " in " + str(path.relative_to(ROOT)))
    manifest = json.loads((ROOT / "Packages/manifest.json").read_text(encoding="utf-8"))
    for value in manifest["dependencies"].values():
        if value.startswith("file:"):
            errors.append("Machine-local package dependency: " + value)
    print(json.dumps({"public_files": len(public), "original_file_count": len(original),
                      "project_guid_count": len(project_guids),
                      "optional_withheld_references": sorted(optional), "errors": errors}, indent=2))
    raise SystemExit(bool(errors))


if __name__ == "__main__":
    main()
