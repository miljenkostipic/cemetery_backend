import sys
import xml.etree.ElementTree as ET
from pathlib import Path

THRESHOLDS = {
    "Cemetery.Domain": (1.0, 1.0),
    "Cemetery.Application": (1.0, 1.0),
    "Cemetery.Api": (0.9, None),
    "Cemetery.Infrastructure": (0.9, None),
}


def main() -> int:
    report = Path(sys.argv[1])
    packages = {
        package.attrib["name"]: package
        for package in ET.parse(report).getroot().find("packages")
    }
    failed = False
    for name, (line_target, branch_target) in THRESHOLDS.items():
        package = packages.get(name)
        if package is None:
            print(f"{name}: missing from the coverage report")
            failed = True
            continue
        line_rate = float(package.attrib["line-rate"])
        branch_rate = float(package.attrib["branch-rate"])
        line_ok = line_rate >= line_target
        branch_ok = branch_target is None or branch_rate >= branch_target
        state = "ok" if line_ok and branch_ok else "below target"
        branch_text = f"{branch_rate:.1%}"
        if branch_target is not None:
            branch_text += f" (target {branch_target:.0%})"
        print(f"{name}: line {line_rate:.1%} (target {line_target:.0%}), branch {branch_text} - {state}")
        failed = failed or not line_ok or not branch_ok
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
