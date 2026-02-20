#!/usr/bin/env python3
import glob
import json
import sys
import xml.etree.ElementTree as ET

def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ("linux", "windows"):
        print("Usage: python scripts/check-coverage.py <linux|windows>")
        sys.exit(2)

    job = sys.argv[1]

    files = glob.glob("**/coverage.cobertura.xml", recursive=True)
    if not files:
        print("No coverage files found")
        sys.exit(1)

    # Collect per-assembly line hits across all coverage files, taking max
    # hits per (assembly, filename, line_number) to deduplicate assemblies
    # that appear in multiple test projects.
    assembly_lines = {}
    for path in files:
        root = ET.parse(path).getroot()
        for pkg in root.findall(".//package"):
            name = pkg.attrib.get("name", "")
            lines = assembly_lines.setdefault(name, {})
            for cls in pkg.findall(".//class"):
                cls_name = cls.attrib.get("name", "")
                for line in cls.findall(".//line"):
                    key = (cls_name, line.attrib.get("number", ""))
                    hits = int(line.attrib.get("hits", 0))
                    prev = lines.get(key, 0)
                    lines[key] = max(prev, hits)

    if not assembly_lines:
        print("No lines found in coverage data")
        sys.exit(1)

    total_covered = 0
    total_valid = 0
    for name in sorted(assembly_lines):
        lines = assembly_lines[name]
        valid = len(lines)
        covered = sum(1 for h in lines.values() if h > 0)
        total_valid += valid
        total_covered += covered
        pct = covered / valid * 100 if valid else 0
        print(f"  {name}: {pct:.1f}% ({covered}/{valid})")

    coverage = int(total_covered / total_valid * 1000) / 10
    print(f"Line coverage ({job}): {coverage}% ({total_covered}/{total_valid})")

    with open("coverage-baseline.json") as f:
        baselines = json.load(f)

    baseline = baselines.get(job, 0.0)
    print(f"Baseline: {baseline}%")

    if coverage < baseline:
        print(f"FAIL: coverage {coverage}% is below baseline {baseline}%")
        sys.exit(1)

    if coverage >= baseline + 1.0:
        print(f"Consider updating baseline from {baseline}% to {coverage}%")

    print("OK")


if __name__ == "__main__":
    main()
