import os
from pathlib import Path
import sys
import shutil
import subprocess
import hashlib
import re
import stat


def is_reparse_point(path: Path) -> bool:
    attributes = getattr(path.lstat(), "st_file_attributes", 0)
    return bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))


if len(sys.argv) != 4:
    raise SystemExit("Usage: post-build.py <publish-dir> <project-dir> <version>")

target_dir = Path(sys.argv[1]).absolute()
project_dir = Path(sys.argv[2]).absolute()
version = sys.argv[3].strip()
if not re.fullmatch(r"[0-9A-Za-z][0-9A-Za-z._-]{0,79}", version):
    raise SystemExit(
        "Version must be a filename-safe release identifier using only "
        "letters, numbers, periods, underscores, and hyphens."
    )
if not target_dir.is_dir() or is_reparse_point(target_dir):
    raise SystemExit(f"Publish directory is missing or unsafe: {target_dir}")


# A published PureDS4 build is an offline installer. Fail package
# composition if any required runtime or installer payload is absent instead
# of producing an archive that later needs a network recovery path.
#
# The self-contained single-file publish bundles the .NET/WPF runtime,
# including its native libraries, inside PureDS4.exe. Application-owned
# rnnoise.dll remains beside it and is verified by NativeLibraryTrust.
required_offline_files = (
    "PureDS4.exe",
    "rnnoise.dll",
    "COPYING",
    "extras/install-viiper-backend.ps1",
    "extras/VIIPER-0.1.0-pureds4.1-x64.exe",
    "extras/VIIPER-0.1.0-pureds4.1-LICENSES.txt",
    "extras/USBip-0.9.7.7-x64.exe",
    "extras/USBip-0.9.7.7-LICENSE.txt",
    "extras/HidHide_1.5.230_x64.exe",
    "extras/HidHide-1.5.230-LICENSE.txt",
    "Resources/ControllerArtwork.NOTICE.txt",
    "ThirdParty/RNNoise.NET-LICENSE.txt",
    "ThirdParty/SharpOSC-LICENSE.txt",
    "ThirdParty/ManagedDependencies.NOTICE.txt",
    "ThirdParty/DotNet-LICENSE.txt",
    "ThirdParty/DotNet-THIRD-PARTY-NOTICES.txt",
    "ThirdParty/DotNet-RuntimePack-THIRD-PARTY-NOTICES.txt",
    "ThirdParty/System.Management-THIRD-PARTY-NOTICES.txt",
    "ThirdParty/SbcSharp/LICENSE.txt",
    "ThirdParty/SbcSharp/NOTICE.md",
)
missing_offline_files = [
    relative_path
    for relative_path in required_offline_files
    if not (target_dir / relative_path).is_file()
]
if missing_offline_files:
    missing = ", ".join(missing_offline_files)
    raise FileNotFoundError(
        f"Cannot compose the offline PureDS4 package; missing: {missing}"
    )


# Bind setup to the exact VIIPER executable copied by this publish. This
# sidecar is regenerated for every artifact, so no hand-maintained hash can
# drift when the bundled executable changes.
viiper_name = "VIIPER-0.1.0-pureds4.1-x64.exe"
viiper_path = target_dir / "extras" / viiper_name
viiper_hasher = hashlib.sha256()
with viiper_path.open("rb") as viiper_stream:
    for chunk in iter(lambda: viiper_stream.read(1024 * 1024), b""):
        viiper_hasher.update(chunk)
viiper_hash_path = viiper_path.with_name(viiper_name + ".sha256")
viiper_hash_path.write_text(
    f"{viiper_hasher.hexdigest()} *{viiper_name}\n",
    encoding="ascii",
)

# A single-file publish bundles the managed assemblies, the satellite
# resource assemblies, and the dependency manifest inside PureDS4.exe, so
# neither the language layout nor the dependency-path rewrite below has
# anything on disk to act on. Detect the layout from the loose application
# assembly rather than guessing, so a multi-file publish still gets both
# steps and still fails loudly when a payload is genuinely missing.
is_single_file_publish = not (target_dir / "PureDS4.dll").is_file()

framework_native_sidecars = (
    "D3DCompiler_47_cor3.dll",
    "PenImc_cor3.dll",
    "PresentationNative_cor3.dll",
    "vcruntime140_cor3.dll",
    "wpfgfx_cor3.dll",
)
if is_single_file_publish:
    loose_framework_native_files = [
        name for name in framework_native_sidecars
        if (target_dir / name).is_file()
    ]
    if loose_framework_native_files:
        raise SystemExit(
            "Single-file publish left framework-native sidecars in the root: "
            + ", ".join(loose_framework_native_files)
        )

# "idn" and "se" were inherited mistakes: "idn" is not a culture at all, and
# "se" is Northern Sami while the file it named held Swedish. The translations
# now use their real identifiers, "id" and "sv".
langs = ["ar", "cs", "de", "el", "es", "fi", "fr", "he", "hu-HU", "id", "it", "ja", "ms",
         "nl", "pl", "pt", "pt-BR", "ru", "sv", "tr", "uk-UA", "vi", "zh-Hans", "zh-Hant", "zh-CN"]

if not is_single_file_publish:
    # move l18n assemblies to a separate directory
    lang_dir = target_dir / "Lang"
    if not lang_dir.exists():
        Path.mkdir(lang_dir)

    for lang in langs:
        current_lang_dir = target_dir / lang
        target_lang_dir = lang_dir / lang
        if not target_lang_dir.exists():
            target_lang_dir.mkdir()

        if current_lang_dir.exists():
            for file in current_lang_dir.iterdir():
                if file.is_file():
                    shutil.move(file, target_lang_dir / file.name)
            current_lang_dir.rmdir()
else:
    # Guard the assumption: a bundled publish must not leave loose satellite
    # assemblies behind, or the shipped package would silently lose them.
    stray_satellites = [
        (target_dir / lang) for lang in langs if (target_dir / lang).is_dir()
    ]
    if stray_satellites:
        stray = ", ".join(entry.name for entry in stray_satellites)
        raise SystemExit(
            "Single-file publish still produced loose satellite assemblies: "
            + stray
        )


# Resolve companion tooling from this script, not from the caller's checkout
# layout. CI passes the repository root as project_dir; the historical parent
# lookup escaped that checkout and failed only on a clean runner.
if not is_single_file_publish:
    lang_script = Path(__file__).resolve().with_name("inject_deps_path.py")
    if not lang_script.is_file():
        raise FileNotFoundError(f"Dependency-path helper is missing: {lang_script}")
    deps_json_path = target_dir / "PureDS4.deps.json"
    if not deps_json_path.is_file():
        raise FileNotFoundError(
            f"Multi-file publish is missing its dependency manifest: {deps_json_path}"
        )
    subprocess.run([sys.executable, str(lang_script), str(deps_json_path)], check=True)

# Preserve the exact GitHub release channel in both portable and managed
# packages. The numeric Windows file version cannot distinguish an RC from a
# stable build, so Settings and the updater use this marker to include the
# installed prerelease notes without exposing prereleases to stable users.
release_marker = target_dir / "PureDS4.release"
release_marker.write_text(version.strip() + "\n", encoding="utf-8")

# Record every file owned by this package. DS4Updater uses this manifest on the
# next update to remove package files that no longer ship, without touching
# profiles, settings, plugins, or other user-created content.
manifest_name = ".pureds4-managed-files.txt"
manifest_path = target_dir / manifest_name
package_entries = list(target_dir.rglob("*"))
reparse_entry = next(
    (entry for entry in package_entries if is_reparse_point(entry)), None
)
if reparse_entry is not None:
    raise SystemExit(
        "Published package contains a reparse point: "
        + reparse_entry.relative_to(target_dir).as_posix()
    )
managed_files = sorted(
    file.relative_to(target_dir).as_posix()
    for file in package_entries
    if file.is_file() and file.name != manifest_name
)
if len({path.casefold() for path in managed_files}) != len(managed_files):
    raise SystemExit("Published package contains case-insensitive duplicate paths.")
manifest_path.write_text("\n".join(managed_files) + "\n", encoding="utf-8")


# rename target dir (net8.0-windows) to PureDS4
renamed_dir = target_dir.parent / "PureDS4"
if renamed_dir.exists():
    if is_reparse_point(renamed_dir):
        raise SystemExit(f"Refusing to replace reparse-point output: {renamed_dir}")
    prior_entries = list(renamed_dir.rglob("*"))
    prior_reparse = next(
        (entry for entry in prior_entries if is_reparse_point(entry)), None
    )
    if prior_reparse is not None:
        raise SystemExit(
            "Refusing to replace output containing a reparse point: "
            + str(prior_reparse)
        )
    shutil.rmtree(renamed_dir)

os.rename(target_dir, renamed_dir)

# create a zip
arch = target_dir.parents[1].name
zip_name = f"PureDS4_{version}_{arch}"
target_zip_path = target_dir.parent / f"{zip_name}.zip"
if target_zip_path.exists():
    os.remove(target_zip_path)

# Archive only the newly composed PureDS4 directory. Using the whole
# Release directory could recursively include an older ZIP from a prior local
# build and silently double the artifact size.
zip_dir = shutil.make_archive(
    zip_name,
    "zip",
    root_dir=renamed_dir.parent,
    base_dir=renamed_dir.name,
)

# move the zip to the build directory
shutil.move(zip_dir, target_zip_path)
