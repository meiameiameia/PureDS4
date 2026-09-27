#!/usr/bin/env python3
"""Small fixture checks for the fail-closed managed-notice publish gate."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile


script = Path(__file__).with_name("validate-managed-notices.py")
spec = importlib.util.spec_from_file_location("managed_notices", script)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def rejects(action, reason: str) -> None:
    try:
        action()
    except ValueError as error:
        assert reason in str(error), (reason, str(error))
    else:
        raise AssertionError(f"Expected a rejection mentioning {reason}")


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="managed-notices-") as temp:
        root = Path(temp)
        packages = root / "packages"
        notice = (Path(__file__).resolve().parent.parent / "PureDS4" / "ThirdParty"
                  / "ManagedDependencies.NOTICE.txt").read_text(encoding="utf-8")
        target = {
            "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/8.0.27": {},
        }
        libraries = {}
        for name, license_id in module.LICENSES.items():
            target[name] = {"runtime": {"lib/example.dll": {}}}
            libraries[name] = {"type": "package"}
            package = module.package_root(packages, name)
            package.mkdir(parents=True)
            (package / "package.nuspec").write_text(
                f'<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">'
                f'<metadata><license type="expression">{license_id}</license>'
                f'</metadata></package>', encoding="utf-8")
        deps = {"targets": {".NETCoreApp,Version=v8.0/win-x64": target},
                "libraries": libraries}
        assert module.validate(deps, packages, notice) == "8.0.27"

        extra = "New.Runtime.Package/1.0"
        target[extra] = {"runtime": {"lib/new.dll": {}}}
        libraries[extra] = {"type": "package"}
        rejects(lambda: module.validate(deps, packages, notice), "unexpected")
        del target[extra], libraries[extra]

        removed = target.pop("NAudio/2.2.1")
        rejects(lambda: module.validate(deps, packages, notice), "missing")
        target["NAudio/2.2.1"] = removed

        rejects(lambda: module.validate(
            deps, packages, notice.replace("  NLog/5.1.1 — ", "  NLog/5.1.1 - ")),
            "Managed notice package list drift")

        rejects(lambda: module.validate(
            deps, packages, notice.replace("BSD-3-Clause:\n  NLog", "MIT:\n  NLog")),
            "wrong license group for NLog/5.1.1")

        nlog = module.package_root(packages, "NLog/5.1.1") / "package.nuspec"
        nlog.write_text(nlog.read_text(encoding="utf-8").replace(
            "BSD-3-Clause", "MIT"), encoding="utf-8")
        rejects(lambda: module.validate(deps, packages, notice), "License changed")

    print("Managed notice gate: graph, missing notice, and license drift pass")


if __name__ == "__main__":
    main()
