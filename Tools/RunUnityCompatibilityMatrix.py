#!/usr/bin/env python3
"""Compile and validate Rogue Arena in every supported Unity editor.

Examples:
  python3 Tools/RunUnityCompatibilityMatrix.py
  python3 Tools/RunUnityCompatibilityMatrix.py \
    --editor 6000.0.58f2=/opt/Unity/6000.0.58f2/Editor/Unity \
    --editor 6000.3.22f1=/opt/Unity/6000.3.22f1/Editor/Unity \
    --editor 6000.5.9f1=/opt/Unity/6000.5.9f1/Editor/Unity

Without --editor, the script searches standard Unity Hub install locations.
Each editor receives a fresh temporary copy because newer editors can migrate
serialized project files. Logs are retained under Artifacts/unity-compatibility.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from CheckUnityCompatibility import CONTRACT_PATH, ROOT, run_checks


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--editor",
        action="append",
        default=[],
        metavar="VERSION=PATH",
        help="Unity executable for a supported version (repeat for each editor)",
    )
    parser.add_argument(
        "--allow-missing",
        action="store_true",
        help="test installed editors and skip missing matrix entries instead of failing",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=1800,
        help="timeout for each editor in seconds (default: 1800)",
    )
    return parser.parse_args()


def standard_editor_paths(version: str) -> list[Path]:
    home = Path.home()
    candidates = [
        home / "Unity" / "Hub" / "Editor" / version / "Editor" / "Unity",
        Path("/opt/Unity/Hub/Editor") / version / "Editor" / "Unity",
        Path("/Applications/Unity/Hub/Editor") / version / "Unity.app/Contents/MacOS/Unity",
    ]
    program_files = os.environ.get("PROGRAMFILES")
    if program_files:
        candidates.append(Path(program_files) / "Unity/Hub/Editor" / version / "Editor/Unity.exe")
    return candidates


def supplied_editors(values: list[str]) -> tuple[dict[str, Path], list[str]]:
    editors: dict[str, Path] = {}
    errors: list[str] = []
    for value in values:
        if "=" not in value:
            errors.append(f"--editor must be VERSION=PATH, got {value!r}")
            continue
        version, raw_path = value.split("=", 1)
        if version in editors:
            errors.append(f"duplicate --editor entry for {version}")
        editors[version] = Path(raw_path).expanduser().resolve()
    return editors, errors


def copy_project(destination: Path) -> None:
    ignored_names = {
        ".git", ".idea", ".vs", "Artifacts", "Build", "Builds", "Library", "Logs",
        "MemoryCaptures", "Obj", "Recordings", "Temp", "UserSettings", "__pycache__",
    }

    def ignore(_directory: str, names: list[str]) -> set[str]:
        return {name for name in names if name in ignored_names}

    shutil.copytree(ROOT, destination, ignore=ignore)


def run_editor(version: str, executable: Path, timeout: int, log_path: Path) -> tuple[bool, str]:
    with tempfile.TemporaryDirectory(prefix=f"rogue-arena-{version}-") as temporary:
        project_copy = Path(temporary) / "RogueShooter"
        copy_project(project_copy)
        command = [
            str(executable),
            "-batchmode",
            "-nographics",
            "-quit",
            "-projectPath",
            str(project_copy),
            "-executeMethod",
            "RogueArena.EditorTools.ProjectValidator.ValidateForCi",
            "-logFile",
            str(log_path),
        ]
        print(f"\n[{version}] {executable}")
        try:
            result = subprocess.run(
                command,
                cwd=project_copy,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                timeout=timeout,
                check=False,
            )
        except subprocess.TimeoutExpired:
            return False, f"timed out after {timeout} seconds (log: {log_path})"
        except OSError as exception:
            return False, f"could not launch editor: {exception}"

        try:
            log_text = log_path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            log_text = ""

        # Unity can occasionally return zero even when an executeMethod was not
        # reached (for example, after a script compilation failure). Require the
        # validator's own success marker as well as a successful process exit.
        if result.returncode == 0 and "VALIDATION PASSED" in log_text:
            return True, f"passed (log: {log_path})"

        combined_output = log_text or result.stdout or ""
        output_tail = combined_output.strip().splitlines()[-30:]
        if result.returncode != 0:
            detail = f"editor exited with {result.returncode} (log: {log_path})"
        else:
            detail = f"editor exited without the validation success marker (log: {log_path})"
        if output_tail:
            detail += "\n" + "\n".join(output_tail)
        return False, detail


def main() -> int:
    args = parse_args()
    static_errors = run_checks(verbose=True)
    if static_errors:
        return 1

    contract = json.loads(CONTRACT_PATH.read_text(encoding="utf-8"))
    supported = [entry["version"] for entry in contract["supportedEditors"]]
    configured, argument_errors = supplied_editors(args.editor)
    if argument_errors:
        for error in argument_errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 2

    unknown = sorted(set(configured) - set(supported))
    if unknown:
        print(f"ERROR: editor entries are not in the support matrix: {', '.join(unknown)}", file=sys.stderr)
        return 2

    for version in supported:
        if version in configured:
            continue
        configured[version] = next(
            (path for path in standard_editor_paths(version) if path.is_file()),
            Path(),
        )

    missing = [version for version in supported if not configured[version].is_file()]
    if missing and not args.allow_missing:
        print("\nERROR: these supported editors were not found:", file=sys.stderr)
        for version in missing:
            print(f"  - {version}", file=sys.stderr)
        print("Pass --editor VERSION=PATH for each install, or use --allow-missing.", file=sys.stderr)
        return 2

    log_directory = ROOT / "Artifacts" / "unity-compatibility"
    log_directory.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []
    tested = 0
    for version in supported:
        executable = configured[version]
        if not executable.is_file():
            print(f"\n[{version}] SKIPPED (editor not installed)")
            continue
        tested += 1
        passed, detail = run_editor(
            version,
            executable,
            args.timeout,
            log_directory / f"{version}.log",
        )
        print(f"[{version}] {'PASS' if passed else 'FAIL'}: {detail}")
        if not passed:
            failures.append(version)

    print(f"\nUnity matrix result: {tested - len(failures)}/{tested} installed editor(s) passed")
    if tested == 0:
        print("No Unity editors were tested.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
