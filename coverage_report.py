import xml.etree.ElementTree as ET
import glob
import sys

if len(sys.argv) > 1:
    files = sys.argv[1:]
else:
    files = glob.glob('coverage/**/coverage.cobertura.xml', recursive=True)
    if not files:
        files = glob.glob('TestResults/**/coverage.cobertura.xml', recursive=True)
class_data = {}

for f in files:
    tree = ET.parse(f)
    root = tree.getroot()
    for package in root.findall('.//package'):
        pkg_name = package.get('name', '')
        for cls in package.findall('.//class'):
            name = cls.get('name', '')
            filename = cls.get('filename', '')
            lines = cls.findall('.//line')
            total = len(lines)
            covered = sum(1 for l in lines if int(l.get('hits', 0)) > 0)
            uncovered = total - covered
            key = f'{pkg_name}::{name}'
            # Dedup: take the version with MORE coverage (like check_coverage.py does)
            if key not in class_data or covered > class_data[key][2]:
                class_data[key] = (name, total, covered, uncovered)

sorted_classes = sorted(class_data.values(), key=lambda x: x[3], reverse=True)
total_lines = sum(v[1] for v in class_data.values())
total_covered = sum(v[2] for v in class_data.values())
print(f"Total: {total_covered}/{total_lines} ({total_covered/total_lines*100:.1f}%)")
print()
print(f"{'Class':<70} {'Uncov':>6} {'Total':>6} {'Rate':>7}")
print('-' * 92)
for name, total, covered, uncovered in sorted_classes:
    if uncovered > 0:
        rate = covered/total*100 if total > 0 else 0
        print(f"{name:<70} {uncovered:>6} {total:>6} {rate:>6.1f}%")
