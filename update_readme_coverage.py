import xml.etree.ElementTree as ET
import re, pathlib, sys

cob = pathlib.Path("./TestResults/CoverageReport/Cobertura.xml")
if not cob.exists():
    print("Cobertura.xml not found — run 'task coverage' first")
    sys.exit(1)

tree = ET.parse(cob)
root = tree.getroot()

TARGET = {
    "GridTrack.Domain":         "Domain",
    "GridTrack.Application":    "Application",
    "GridTrack.Infrastructure": "Infrastructure",
}

def assembly_label(pkg_name):
    # Strip .dll suffix and match by assembly prefix (handles namespace-level packages)
    name = pkg_name.removesuffix(".dll")
    for prefix, label in TARGET.items():
        if name == prefix or name.startswith(prefix + "."):
            return label
    return None

totals = {label: [0, 0] for label in TARGET.values()}  # [covered_lines, total_lines]

for pkg in root.iter("package"):
    label = assembly_label(pkg.get("name", ""))
    if label is None:
        continue
    for line_elem in pkg.iter("line"):
        totals[label][1] += 1
        if int(line_elem.get("hits", 0)) > 0:
            totals[label][0] += 1

rows = []
for prefix, label in TARGET.items():
    covered, total = totals[label]
    if total > 0:
        pct = covered / total * 100
        rows.append(f"| {label} | {pct:.1f}% |")

if not rows:
    found = sorted({p.get("name", "") for p in root.iter("package")})[:30]
    print(f"No matching packages found in Cobertura.xml\nFound packages: {found}")
    sys.exit(1)

table = "| Layer | Line Coverage |\n|-------|---------------|\n" + "\n".join(rows)

readme = pathlib.Path("README.md")
content = readme.read_text()
updated = re.sub(
    r"<!-- COVERAGE_START -->.*?<!-- COVERAGE_END -->",
    f"<!-- COVERAGE_START -->\n{table}\n<!-- COVERAGE_END -->",
    content,
    flags=re.DOTALL,
)
if updated == content:
    print("WARNING: markers not found in README.md")
    sys.exit(1)
readme.write_text(updated)
print("README.md updated")
