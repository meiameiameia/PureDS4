#!/usr/bin/env python3
"""Regression tests for the fail-closed release-input validator."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile


VALIDATOR = Path(__file__).with_name("validate-release-inputs.py")


def run_validator(root: Path, *extra: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            sys.executable,
            str(VALIDATOR),
            "--repo-root",
            str(root),
            "--manifest",
            str(root / "installer" / "release-inputs.json"),
            *extra,
        ],
        check=False,
        capture_output=True,
        text=True,
    )


def write_fixture(root: Path, *, blocked: bool = False) -> Path:
    artifact = root / "payload" / "component.dll"
    artifact.parent.mkdir(parents=True)
    artifact.write_bytes(b"known release input\n")
    license_path = root / "licenses" / "component.txt"
    license_path.parent.mkdir(parents=True)
    license_path.write_text("Permission notice\n", encoding="utf-8")
    (root / "installer").mkdir()
    project_dir = root / "DS4Windows"
    project_dir.mkdir()
    (project_dir / "DS4WinWPF.csproj").write_text(
        "<Project><ItemGroup>"
        '<Content Include="..\\payload\\component.dll" />'
        "</ItemGroup></Project>",
        encoding="utf-8",
    )
    component = {
        "id": "sample",
        "version": "1.0.0",
        "status": "blocked" if blocked else "verified",
        "sourceRepository": "https://example.invalid/source",
        "sourceCommit": "0123456789abcdef0123456789abcdef01234567",
        "licensePath": None if blocked else "licenses/component.txt",
        "artifacts": [
            {
                "kind": "repository",
                "path": "payload/component.dll",
                "sha256": hashlib.sha256(artifact.read_bytes()).hexdigest().upper(),
            }
        ],
    }
    if blocked:
        component["blocker"] = "Deliberate test blocker."
    manifest = {
        "schemaVersion": 1,
        "releaseReady": not blocked,
        "releaseBlockers": ["sample"] if blocked else [],
        "components": [component],
    }
    manifest_path = root / "installer" / "release-inputs.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    return artifact


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="ds4w-release-inputs-") as temp:
        root = Path(temp)
        artifact = write_fixture(root)
        result = run_validator(root, "--require-release-ready")
        require(result.returncode == 0, result.stderr or result.stdout)

        artifact.write_bytes(b"tampered\n")
        result = run_validator(root)
        require(result.returncode != 0, "A changed artifact hash was accepted.")
        require("hash mismatch" in result.stderr, result.stderr)

    with tempfile.TemporaryDirectory(prefix="ds4w-release-inventory-") as temp:
        root = Path(temp)
        write_fixture(root)
        extra_artifact = root / "payload" / "unmanifested.exe"
        extra_artifact.write_bytes(b"not declared\n")
        project = root / "DS4Windows" / "DS4WinWPF.csproj"
        project.write_text(
            "<Project><ItemGroup>"
            '<Content Include="..\\payload\\component.dll" />'
            '<Content Include="..\\payload\\unmanifested.exe" />'
            "</ItemGroup></Project>",
            encoding="utf-8",
        )
        result = run_validator(root)
        require(result.returncode != 0, "An unmanifested binary was accepted.")
        require("unmanifested project inputs" in result.stderr, result.stderr)

    with tempfile.TemporaryDirectory(prefix="ds4w-release-blocker-") as temp:
        root = Path(temp)
        write_fixture(root, blocked=True)
        result = run_validator(root)
        require(result.returncode == 0, result.stderr or result.stdout)
        result = run_validator(root, "--require-release-ready")
        require(result.returncode != 0, "A blocked public release was accepted.")
        require("Public release is blocked" in result.stderr, result.stderr)

    print("Release-input validator regression tests passed.")


if __name__ == "__main__":
    main()
