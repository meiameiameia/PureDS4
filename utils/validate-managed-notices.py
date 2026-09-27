#!/usr/bin/env python3
"""Keep distributed managed notices aligned with the actual x64 runtime graph."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import shutil
import sys
import xml.etree.ElementTree as ET


# Build-only packages and the vendored SharpOSC assembly are deliberately
# absent. New runtime packages must be reviewed before their notices can ship.
LICENSES = {
    "AvalonEdit/6.3.0.90": "MIT",
    "bloomtom.HttpProgress/2.3.2": "MIT",
    "DotNetProjects.Extended.Wpf.Toolkit/5.0.106": "MS-PL",
    "H.NotifyIcon/2.0.74": "MIT",
    "H.NotifyIcon.Wpf/2.0.74": "MIT",
    "MdXaml/1.27.0": "MIT",
    "MdXaml.Plugins/1.27.0": "MIT",
    "NAudio/2.2.1": "MIT",
    "NAudio.Asio/2.2.1": "MIT",
    "NAudio.Core/2.2.1": "MIT",
    "NAudio.Midi/2.2.1": "MIT",
    "NAudio.Wasapi/2.2.1": "MIT",
    "NAudio.WinForms/2.2.1": "MIT",
    "NAudio.WinMM/2.2.1": "MIT",
    "NLog/5.1.1": "BSD-3-Clause",
    "Ookii.Dialogs.Wpf/5.0.1": "BSD-3-Clause",
    "System.Management/7.0.2": "MIT",
    "TaskScheduler/2.10.1": "MIT",
    "WPFLocalizeExtension/3.9.4": "MS-PL",
    "WpfScreenHelper/2.1.0": "MIT",
    "XAMLMarkupExtensions/2.1.3": "MS-PL",
}

COPYRIGHT_NOTICE_FILES = {
    "System.Management-THIRD-PARTY-NOTICES.txt":
        ("system.management", "7.0.2", "THIRD-PARTY-NOTICES.TXT"),
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def package_root(packages: Path, package: str) -> Path:
    name, version = package.split("/", 1)
    return packages / name.lower() / version


def runtime_packages(deps: dict) -> tuple[set[str], str]:
    targets = deps.get("targets", {})
    rid_targets = [name for name in targets if name.endswith("/win-x64")]
    require(len(rid_targets) == 1, "Expected exactly one win-x64 dependency target")
    target = targets[rid_targets[0]]
    libraries = deps.get("libraries", {})
    packages = {
        name
        for name, assets in target.items()
        if libraries.get(name, {}).get("type") == "package"
        and any(assets.get(kind) for kind in ("runtime", "runtimeTargets", "native"))
    }
    runtime_packs = [
        name for name in target
        if name.startswith("runtimepack.Microsoft.NETCore.App.Runtime.win-x64/")
    ]
    require(len(runtime_packs) == 1, "Expected exactly one .NET win-x64 runtime pack")
    return packages, runtime_packs[0].split("/", 1)[1]


def validate(deps: dict, packages: Path, notice: str) -> str:
    actual, runtime_version = runtime_packages(deps)
    if actual != set(LICENSES):
        missing = sorted(set(LICENSES) - actual)
        unexpected = sorted(actual - set(LICENSES))
        raise ValueError(
            "Managed notice graph drift: missing=" + repr(missing)
            + "; unexpected=" + repr(unexpected)
        )
    listed: dict[str, str] = {}
    current_license = ""
    for line in notice.splitlines():
        if line in ("MIT:", "BSD-3-Clause:", "Microsoft Public License (Ms-PL):"):
            current_license = "MS-PL" if line.startswith("Microsoft") else line[:-1]
        match = re.match(r"^  (\S+/\S+) — ", line)
        if match:
            require(match.group(1) not in listed,
                    f"Managed notice repeats {match.group(1)}")
            listed[match.group(1)] = current_license
    require(set(listed) == set(LICENSES),
            "Managed notice package list drift: missing="
            + repr(sorted(set(LICENSES) - set(listed))) + "; unexpected="
            + repr(sorted(set(listed) - set(LICENSES))))
    for package, expected_license in LICENSES.items():
        require(listed[package] == expected_license,
                f"Managed notice has the wrong license group for {package}")
        root = package_root(packages, package)
        nuspecs = list(root.glob("*.nuspec"))
        require(len(nuspecs) == 1, f"Missing package metadata: {package}")
        package_xml = ET.parse(nuspecs[0]).getroot()
        metadata = next((element for element in package_xml
                         if element.tag.rsplit("}", 1)[-1] == "metadata"), None)
        require(metadata is not None, f"Missing nuspec metadata: {package}")
        license_element = next((element for element in metadata
                                if element.tag.rsplit("}", 1)[-1] == "license"), None)
        require(license_element is not None, f"Missing license metadata: {package}")
        license_id = (license_element.text or "").strip()
        if license_element.get("type") == "file":
            license_path = root / license_id
            require(license_path.is_file(), f"Missing package license file: {package}")
            license_body = license_path.read_text(encoding="utf-8-sig")
            require(expected_license.lower() in license_body.lower()
                    or (expected_license == "MIT" and "Permission is hereby granted" in license_body)
                    or (expected_license == "MS-PL" and "Microsoft Public License" in license_body),
                    f"Unexpected license file for {package}: {license_id}")
        else:
            require(license_id.upper() == expected_license.upper(),
                    f"License changed for {package}: {license_id}")

    for heading in ("MIT License", "BSD 3-Clause License", "Microsoft Public License (Ms-PL)"):
        require(heading in notice, f"Managed notice is missing {heading}")
    require("The above copyright notice and this permission notice" in notice,
            "Managed notice is missing the MIT permission notice")
    require("Redistributions in binary form" in notice,
            "Managed notice is missing BSD binary distribution terms")
    require("3. Conditions and Limitations" in notice,
            "Managed notice is missing Ms-PL conditions")
    return runtime_version


def distribute_notices(packages: Path, runtime_version: str, publish: Path) -> None:
    require(publish.is_dir(), f"Publish directory is missing: {publish}")
    dotnet = shutil.which("dotnet")
    require(dotnet is not None, "dotnet executable is unavailable")
    dotnet_root = Path(dotnet).resolve().parent
    sources = {
        "DotNet-LICENSE.txt": dotnet_root / "LICENSE.txt",
        "DotNet-THIRD-PARTY-NOTICES.txt": dotnet_root / "ThirdPartyNotices.txt",
        "DotNet-RuntimePack-THIRD-PARTY-NOTICES.txt":
            packages / "microsoft.netcore.app.runtime.win-x64"
            / runtime_version / "THIRD-PARTY-NOTICES.TXT",
    }
    for output_name, (name, version, source_name) in COPYRIGHT_NOTICE_FILES.items():
        sources[output_name] = packages / name / version / source_name
    for output_name, source in sources.items():
        require(source.is_file() and source.stat().st_size > 100,
                f"Required runtime notice is missing: {source}")
        target = publish / "ThirdParty" / output_name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--deps-json", type=Path, required=True)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--notice", type=Path, required=True)
    parser.add_argument("--publish-root", type=Path)
    args = parser.parse_args()
    deps = json.loads(args.deps_json.read_text(encoding="utf-8"))
    notice = args.notice.read_text(encoding="utf-8-sig")
    runtime_version = validate(deps, args.packages, notice)
    if args.publish_root:
        distribute_notices(args.packages, runtime_version, args.publish_root)
    print(f"Managed notices match {len(LICENSES)} runtime packages; .NET runtime {runtime_version}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, ET.ParseError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1)
