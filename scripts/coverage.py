#!/usr/bin/env python3
"""Run tests with coverage and display per-assembly breakdown."""
import os
import shutil
import subprocess
import sys

SCRIPTS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPTS_DIR)
sys.path.insert(0, SCRIPTS_DIR)

def main():
    os.chdir(REPO_ROOT)

    # Clean stale results
    for test_dir in ("tests/Perch.Core.Tests/TestResults", "tests/Perch.Desktop.Tests/TestResults"):
        if os.path.isdir(test_dir):
            shutil.rmtree(test_dir)

    # Run tests with coverage
    result = subprocess.run(
        ["dotnet", "test", "Perch.slnx",
         "--settings", "coverage.runsettings",
         "--filter", "FullyQualifiedName!~Perch.SmokeTests",
         "--verbosity", "quiet"],
        cwd=REPO_ROOT,
    )
    if result.returncode != 0:
        sys.exit(result.returncode)

    print()

    # Parse and display
    from check_coverage import parse_coverage, print_coverage  # noqa: E402

    assembly_lines = parse_coverage()
    if not assembly_lines:
        print("No coverage files found")
        sys.exit(1)

    print_coverage(assembly_lines)


if __name__ == "__main__":
    main()
