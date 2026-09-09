"""Fail if the composition's pins disagree with each other.

compatibility/local.yaml is the source of truth for what runs. Two things can silently drift from
it: the platform package version in Directory.Packages.props, and the set of services the AppHost
actually asks for. Both are checked here rather than discovered when a suite hangs.
"""

import re
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
manifest_path = root / "compatibility" / "local.yaml"
packages_path = root / "Directory.Packages.props"
apphost_path = root / "src" / "Concertable.AppHost" / "AppHost.cs"

failures = []
manifest = manifest_path.read_text(encoding="utf-8")

platform = re.search(r"^platform:\s*\n\s+packages:\s*(\S+)\s*$", manifest, re.MULTILINE)
if not platform:
    failures.append("compatibility/local.yaml has no platform.packages")

pinned = re.search(
    r"<ConcertableDotNetPlatformVersion>([^<]+)</ConcertableDotNetPlatformVersion>",
    packages_path.read_text(encoding="utf-8"),
)
if not pinned:
    failures.append("Directory.Packages.props has no ConcertableDotNetPlatformVersion")

if platform and pinned and platform.group(1) != pinned.group(1):
    failures.append(
        f"platform.packages ({platform.group(1)}) != ConcertableDotNetPlatformVersion ({pinned.group(1)})"
    )

images_block = manifest.split("images:", 1)[-1]
manifest_services = set(re.findall(r"^  ([a-z0-9][a-z0-9-]*):\s*$", images_block, re.MULTILINE))
if not manifest_services:
    failures.append("compatibility/local.yaml pins no images")

for service, digest in re.findall(
    r"^  ([a-z0-9][a-z0-9-]*):\s*\n\s+repository:\s*(?:\S+)\s*\n\s+digest:\s*(\S+)\s*$",
    images_block,
    re.MULTILINE,
):
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        failures.append(f"images.{service}.digest is not a full sha256 digest: {digest}")

requested = set(re.findall(r'manifest\["([^"]+)"\]', apphost_path.read_text(encoding="utf-8")))
if unpinned := requested - manifest_services:
    failures.append(f"AppHost requests services the manifest does not pin: {sorted(unpinned)}")
if unused := manifest_services - requested:
    failures.append(f"manifest pins services the AppHost never composes: {sorted(unused)}")

if failures:
    for failure in failures:
        print(f"FAIL: {failure}")
    sys.exit(1)

print(f"pins agree: platform {pinned.group(1)}, {len(manifest_services)} images composed")
