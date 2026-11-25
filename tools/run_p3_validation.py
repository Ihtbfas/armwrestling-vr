"""Run the formal Unity scene with the PC bridge and software STM32 on loopback."""

import argparse
import json
from pathlib import Path
import socket
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--pc", type=Path, required=True, help="PC repository root")
    parser.add_argument("--output", type=Path, required=True, help="New local evidence directory")
    args = parser.parse_args()
    vr = Path(__file__).resolve().parents[1]
    pc, output = args.pc.resolve(), args.output.resolve()
    python = pc / ".venv" / "Scripts" / "python.exe"
    if not python.is_file() or not args.unity.is_file():
        parser.error("Select an installed Unity editor and create the PC .venv first.")

    for socket_type, port in ((socket.SOCK_STREAM, 9101), (socket.SOCK_DGRAM, 9102)):
        with socket.socket(socket.AF_INET, socket_type) as probe:
            probe.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            probe.bind(("127.0.0.1", port))

    output.mkdir(parents=True)
    input_path = output / "input.txt"
    input_path.write_text("reset", encoding="utf-8")
    request_path = output / "fault-request.txt"
    result_path = output / "result.json"
    children = {}

    def start(name, command, cwd):
        children[name] = subprocess.Popen(
            [str(x) for x in command], cwd=cwd, stdout=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )

    def stop(name):
        child = children.pop(name, None)
        if child is not None and child.poll() is None:
            child.terminate()
            try:
                child.wait(5)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(5)

    def start_pc():
        start("pc", [python, "-m", "common.bridge", "--log-file", output / "pc.jsonl"], pc)

    def start_sim():
        start("sim", [python, "-m", "sim.mock_stm32", "--input-file", input_path,
                      "--torque", "0.8", "--log-file", output / "sim.jsonl"], pc)

    actions = {"stop-sim": lambda: stop("sim"), "start-sim": start_sim,
               "stop-pc": lambda: stop("pc"), "start-pc": start_pc}

    try:
        start_pc()
        start_sim()
        start("unity", [args.unity.resolve(), "-batchmode", "-projectPath", vr,
                        "-executeMethod", "ArmWrestlingVR.EditorTools.P3Validation.Run",
                        "-network", "-hostip", "127.0.0.1", "-hostport", "9102",
                        "-character", "demo", "-p3dir", output, "-p3faults",
                        "-logFile", output / "editor.log"], vr)
        deadline = time.monotonic() + 330
        completed = set()
        while children["unity"].poll() is None:
            if time.monotonic() > deadline:
                raise TimeoutError("Unity did not complete P3 validation within 330 seconds")
            request = request_path.read_text(encoding="utf-8").strip() if request_path.exists() else ""
            if request in actions and request not in completed:
                actions[request]()
                completed.add(request)
                print(request, flush=True)
            for name in ("pc", "sim"):
                if name in children and children[name].poll() is not None:
                    raise RuntimeError(f"{name} exited unexpectedly")
            time.sleep(0.04)
        if not result_path.is_file():
            raise RuntimeError(f"Unity exited before reporting a result. See {output / 'editor.log'}")
        result = json.loads(result_path.read_text(encoding="utf-8-sig"))
        print(result.get("error") or f"{result['status']}: {len(result['checks'])} observations")
        if children["unity"].returncode != 0 or result["status"] != "pass":
            return 1
        last_play_sequence = result["checks"][-1]["pc"]["seq"]
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            lines = (output / "pc.jsonl").read_text(encoding="utf-8").split("\n")
            for line in reversed(lines[:-1]):
                row = json.loads(line)
                if (row.get("type") == "status" and row["seq"] > last_play_sequence
                        and row["reason"] == "vr_timeout" and row["mode"] == "stop"
                        and row["target_torque_nm"] == 0 and row["telemetry_fresh"]):
                    (output / "post-play.json").write_text(json.dumps(row, indent=2), encoding="utf-8")
                    print("Editor exit: PC observed vr_timeout and STOP/0 with the simulator still live.")
                    return 0
            time.sleep(0.1)
        raise RuntimeError("PC did not record STOP/0 after the Editor exited")
    finally:
        for name in list(children):
            stop(name)


if __name__ == "__main__":
    raise SystemExit(main())
