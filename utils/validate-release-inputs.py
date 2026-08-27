#!/usr/bin/env python3
"""Validate content-addressed, licensed inputs used by a release build."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


BINARY_SUFFIXES = {".dll", ".exe", ".msi"}
SHA256_RE = re.compile(r"^[0-9A-F]{64}$")


def fail(message: str) -> None:
    raise SystemExit(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def safe_repo_path(repo_root: Path, value: str) -> Path:
    candidate = (repo_root / value).resolve()
    try:
        candidate.relative_to(repo_root.resolve())
    except ValueError:
        fail(f"Release input escapes the repository: {value}")
    return candidate


def nuget_root() -> Path:
    configured = os.environ.get("NUGET_PACKAGES")
    return Path(configured) if configured else Path.home() / ".nuget" / "packages"


def artifact_identity(artifact: dict[str, object]) -> str:
    kind = artifact.get("kind")
    if kind == "repository":
        return str(artifact["path"]).replace("\\", "/").lower()
    if kind == "nuget":
        return (
            f"nuget:{artifact['packageId']}/{artifact['packageVersion']}/"
            f"{artifact['path']}"
        ).replace("\\", "/").lower()
    fail(f"Unknown release artifact kind: {kind}")
    return ""


def resolve_artifact(repo_root: Path, artifact: dict[str, object]) -> Path:
    if artifact["kind"] == "repository":
        return safe_repo_path(repo_root, str(artifact["path"]))
    return (
        nuget_root()
        / str(artifact["packageId"]).lower()
        / str(artifact["packageVersion"]).lower()
        / str(artifact["path"])
    ).resolve()


def expand_platform(value: str) -> list[str]:
    if "$(Platform)" not in value:
        return [value]
    return [value.replace("$(Platform)", platform) for platform in ("x64", "x86")]


def normalize_project_path(repo_root: Path, project_dir: Path, value: str) -> str:
    path = (project_dir / value).resolve()
    if not path.exists() and value.replace("/", "\\").startswith("..\\libs\\"):
        # The inherited HintPath says ../libs while the tracked compatibility
        # binaries live below PureDS4/libs. MSBuild still resolves the
        # references through its candidate search; inventory the tracked input.
        path = (project_dir / value.replace("..\\libs\\", "libs\\", 1)).resolve()
    try:
        return path.relative_to(repo_root.resolve()).as_posix().lower()
    except ValueError:
        fail(f"Binary project input escapes the repository: {value}")
    return ""


def discover_project_inputs(repo_root: Path) -> set[str]:
    project = repo_root / "PureDS4" / "PureDS4.csproj"
    if not project.is_file():
        fail(f"Application project is missing: {project}")
    root = ET.parse(project).getroot()
    package_properties: dict[str, tuple[str, str]] = {}
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] != "PackageReference":
            continue
        if element.attrib.get("GeneratePathProperty", "").lower() != "true":
            continue
        package_id = element.attrib["Include"]
        version = element.attrib["Version"]
        property_name = "Pkg" + re.sub(r"[^A-Za-z0-9]", "_", package_id)
        package_properties[property_name.lower()] = (package_id, version)

    discovered: set[str] = set()
    project_dir = project.parent
    for element in root.iter():
        tag = element.tag.rsplit("}", 1)[-1]
        values: list[str] = []
        if tag == "Content" and "Include" in element.attrib:
            values.append(element.attrib["Include"])
        elif tag == "None" and "Update" in element.attrib:
            values.append(element.attrib["Update"])
        elif tag == "Reference":
            values.extend(
                child.text or ""
                for child in element
                if child.tag.rsplit("}", 1)[-1] == "HintPath"
            )
        for raw_value in values:
            for value in expand_platform(raw_value):
                if Path(value).suffix.lower() not in BINARY_SUFFIXES:
                    continue
                package_match = re.match(r"^\$\(([^)]+)\)[\\/](.+)$", value)
                if package_match:
                    package = package_properties.get(package_match.group(1).lower())
                    if not package:
                        fail(f"Unresolved generated NuGet path property: {value}")
                    package_id, version = package
                    discovered.add(
                        f"nuget:{package_id}/{version}/{package_match.group(2)}"
                        .replace("\\", "/").lower()
                    )
                else:
                    discovered.add(
                        normalize_project_path(repo_root, project_dir, value)
                    )
    return discovered


def verify_nuget_lock(repo_root: Path, component: dict[str, object]) -> None:
    content_hash = component.get("nugetContentHash")
    if not content_hash:
        return
    package_id = str(component["artifacts"][0]["packageId"])
    version = str(component["artifacts"][0]["packageVersion"])
    lock_path = repo_root / "PureDS4" / "packages.lock.json"
    if not lock_path.is_file():
        fail(f"NuGet lock file is missing for {component['id']}: {lock_path}")
    lock = json.loads(lock_path.read_text(encoding="utf-8"))
    matches = []
    for dependencies in lock.get("dependencies", {}).values():
        entry = dependencies.get(package_id)
        if entry:
            matches.append(entry)
    if not any(
        entry.get("resolved") == version
        and entry.get("contentHash") == content_hash
        for entry in matches
    ):
        fail(f"NuGet lock identity does not match {component['id']} {version}.")


def verify_signature(path: Path, expected_thumbprint: str) -> None:
    env = os.environ.copy()
    env["DS4W_RELEASE_INPUT_PATH"] = str(path)
    script = (
        "$s=Get-AuthenticodeSignature -LiteralPath "
        "$env:DS4W_RELEASE_INPUT_PATH; "
        "@{Status=$s.Status.ToString();Thumbprint=$s.SignerCertificate.Thumbprint}"
        "|ConvertTo-Json -Compress"
    )
    powershell = shutil.which("pwsh") or shutil.which("powershell.exe")
    if not powershell:
        fail("PowerShell is required for Authenticode verification.")
    process = subprocess.run(
        [powershell, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
        check=False,
        capture_output=True,
        text=True,
        env=env,
    )
    if process.returncode != 0:
        fail(f"Authenticode inspection failed for {path}: {process.stderr.strip()}")
    try:
        signature = json.loads(process.stdout)
    except json.JSONDecodeError:
        fail(f"Authenticode inspection returned invalid data for {path}.")
    if signature.get("Status") != "Valid":
        fail(f"Authenticode signature is not valid for {path}.")
    actual = str(signature.get("Thumbprint", "")).upper()
    if actual != expected_thumbprint:
        fail(
            f"Authenticode signer changed for {path}: expected "
            f"{expected_thumbprint}, got {actual or 'none'}."
        )


def validate(args: argparse.Namespace) -> None:
    repo_root = args.repo_root.resolve()
    manifest_path = args.manifest.resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != 1:
        fail("Unsupported release-input manifest schema.")

    components = manifest.get("components")
    if not isinstance(components, list) or not components:
        fail("Release-input manifest has no components.")
    component_ids = [component.get("id") for component in components]
    if any(not component_id for component_id in component_ids):
        fail("Every release component requires an id.")
    if len(component_ids) != len(set(component_ids)):
        fail("Release component ids must be unique.")

    blocked_ids: list[str] = []
    manifest_inputs: set[str] = set()
    for component in components:
        status = component.get("status")
        if status not in {"verified", "blocked"}:
            fail(f"Invalid status for {component['id']}: {status}")
        if status == "blocked":
            blocked_ids.append(component["id"])
            if not component.get("blocker"):
                fail(f"Blocked component has no reason: {component['id']}")
        else:
            for field in ("sourceRepository", "sourceCommit", "licensePath"):
                if not component.get(field):
                    fail(f"Verified component {component['id']} lacks {field}.")
            license_path = safe_repo_path(repo_root, str(component["licensePath"]))
            if not license_path.is_file() or license_path.stat().st_size == 0:
                fail(f"License/notice is missing for {component['id']}: {license_path}")

        artifacts = component.get("artifacts")
        if not isinstance(artifacts, list) or not artifacts:
            fail(f"Release component has no artifacts: {component['id']}")
        for artifact in artifacts:
            identity = artifact_identity(artifact)
            if identity in manifest_inputs:
                fail(f"Release input is listed more than once: {identity}")
            manifest_inputs.add(identity)
            expected_hash = str(artifact.get("sha256", "")).upper()
            if not SHA256_RE.fullmatch(expected_hash):
                fail(f"Invalid SHA-256 for {identity}.")
            origin_hash = str(artifact.get("originSha256", "")).upper()
            if origin_hash and not SHA256_RE.fullmatch(origin_hash):
                fail(f"Invalid origin SHA-256 for {identity}.")
            artifact_path = resolve_artifact(repo_root, artifact)
            if not artifact_path.is_file():
                fail(f"Release input is missing: {artifact_path}")
            actual_hash = sha256(artifact_path)
            if actual_hash != expected_hash:
                fail(
                    f"Release input hash mismatch for {identity}: expected "
                    f"{expected_hash}, got {actual_hash}."
                )
            expected_signer = str(artifact.get("signerThumbprint", "")).upper()
            if expected_signer and not re.fullmatch(r"[0-9A-F]{40}", expected_signer):
                fail(f"Invalid signer thumbprint for {identity}.")
            if args.verify_signatures and expected_signer:
                verify_signature(artifact_path, expected_signer)
        verify_nuget_lock(repo_root, component)

    declared_blockers = manifest.get("releaseBlockers")
    if declared_blockers != blocked_ids:
        fail(
            "releaseBlockers must exactly match blocked components in manifest "
            "order."
        )
    calculated_ready = not blocked_ids
    if manifest.get("releaseReady") is not calculated_ready:
        fail("releaseReady does not match the component statuses.")

    if not args.skip_project_inventory:
        project_inputs = discover_project_inputs(repo_root)
        missing = sorted(project_inputs - manifest_inputs)
        stale = sorted(manifest_inputs - project_inputs)
        if missing or stale:
            details = []
            if missing:
                details.append("unmanifested project inputs: " + ", ".join(missing))
            if stale:
                details.append("manifest inputs not used by the project: " + ", ".join(stale))
            fail("Release input inventory differs from the project: " + "; ".join(details))

    if args.require_release_ready and blocked_ids:
        fail(
            "Public release is blocked by unresolved inputs: "
            + ", ".join(blocked_ids)
        )
    print(
        f"Release inputs verified: {len(components)} components, "
        f"{len(manifest_inputs)} artifacts; releaseReady={calculated_ready}."
    )


def parse_args() -> argparse.Namespace:
    script_root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=script_root)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=script_root / "installer" / "release-inputs.json",
    )
    parser.add_argument("--verify-signatures", action="store_true")
    parser.add_argument("--require-release-ready", action="store_true")
    parser.add_argument("--skip-project-inventory", action="store_true")
    return parser.parse_args()


if __name__ == "__main__":
    validate(parse_args())
