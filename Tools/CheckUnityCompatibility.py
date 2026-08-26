#!/usr/bin/env python3
"""Static checks for Rogue Arena's supported Unity editor matrix."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTRACT_PATH = ROOT / "Tools" / "UnityCompatibility.json"
PROJECT_VERSION_PATH = ROOT / "ProjectSettings" / "ProjectVersion.txt"
PACKAGE_MANIFEST_PATH = ROOT / "Packages" / "manifest.json"
VERSION_RE = re.compile(r"^6000\.\d+\.\d+f\d+$")
REVISION_RE = re.compile(r"^[0-9a-f]{12}$")


def load_contract() -> dict:
    with CONTRACT_PATH.open(encoding="utf-8") as stream:
        return json.load(stream)


def run_checks(verbose: bool = True) -> list[str]:
    errors: list[str] = []

    try:
        contract = load_contract()
    except (OSError, json.JSONDecodeError) as exception:
        return [f"cannot read {CONTRACT_PATH.relative_to(ROOT)}: {exception}"]

    baseline = contract.get("baselineVersion")
    editors = contract.get("supportedEditors")
    packages = contract.get("packages")
    if not isinstance(editors, list) or not editors:
        return ["supportedEditors must be a non-empty list"]
    if not isinstance(packages, list) or not packages:
        return ["packages must be a non-empty list"]

    versions: list[str] = []
    revisions: dict[str, str] = {}
    for entry in editors:
        version = entry.get("version", "") if isinstance(entry, dict) else ""
        revision = entry.get("revision", "") if isinstance(entry, dict) else ""
        versions.append(version)
        revisions[version] = revision
        if not VERSION_RE.fullmatch(version):
            errors.append(f"invalid Unity editor version: {version!r}")
        if not REVISION_RE.fullmatch(revision):
            errors.append(f"invalid Unity revision for {version}: {revision!r}")

    if len(versions) != len(set(versions)):
        errors.append("supported Unity editor versions must be unique")
    if baseline not in versions:
        errors.append(f"baselineVersion {baseline!r} is not in supportedEditors")
    if versions and versions[0] != baseline:
        errors.append("the baseline editor must be the first supportedEditors entry")

    try:
        project_version_text = PROJECT_VERSION_PATH.read_text(encoding="utf-8")
    except OSError as exception:
        errors.append(f"cannot read ProjectVersion.txt: {exception}")
    else:
        version_match = re.search(r"^m_EditorVersion:\s*(\S+)", project_version_text, re.MULTILINE)
        revision_match = re.search(
            r"^m_EditorVersionWithRevision:\s*\S+\s+\(([0-9a-fA-F]+)\)",
            project_version_text,
            re.MULTILINE,
        )
        marker_version = version_match.group(1) if version_match else ""
        marker_revision = revision_match.group(1).lower() if revision_match else ""
        if marker_version != baseline:
            errors.append(
                f"ProjectVersion.txt must stay on oldest baseline {baseline}; found {marker_version or 'no marker'}"
            )
        expected_revision = revisions.get(baseline, "")
        if marker_revision != expected_revision:
            errors.append(
                f"ProjectVersion.txt revision must be {expected_revision}; found {marker_revision or 'no revision'}"
            )

    try:
        package_manifest = json.loads(PACKAGE_MANIFEST_PATH.read_text(encoding="utf-8"))
        dependencies = package_manifest["dependencies"]
    except (OSError, json.JSONDecodeError, KeyError, TypeError) as exception:
        errors.append(f"cannot read Packages/manifest.json dependencies: {exception}")
        dependencies = {}

    package_names: list[str] = []
    for entry in packages:
        name = entry.get("name", "") if isinstance(entry, dict) else ""
        version = entry.get("version", "") if isinstance(entry, dict) else ""
        package_names.append(name)
        actual = dependencies.get(name)
        if actual != version:
            errors.append(f"{name} must be pinned to {version}; found {actual!r}")
        if not re.fullmatch(r"\d+\.\d+\.\d+", version):
            errors.append(f"package {name} does not use an exact semantic version: {version!r}")

    if len(package_names) != len(set(package_names)):
        errors.append("package compatibility pins must be unique")

    if verbose:
        print("Rogue Arena Unity compatibility contract")
        print(f"  Serialized baseline: {baseline} ({revisions.get(baseline, 'unknown')})")
        print("  Supported editors:")
        for version in versions:
            print(f"    - {version} ({revisions.get(version, 'unknown')})")
        print("  Shared package pins:")
        for entry in packages:
            if isinstance(entry, dict):
                print(f"    - {entry.get('name')}@{entry.get('version')}")
        print()
        if errors:
            print(f"FAILED: {len(errors)} compatibility error(s)")
            for error in errors:
                print(f"  - {error}")
        else:
            print("PASSED: compatibility contract, project marker and package pins agree")

    return errors


def main() -> int:
    return 1 if run_checks(verbose=True) else 0


if __name__ == "__main__":
    sys.exit(main())
