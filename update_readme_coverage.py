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

rows = []
for pkg in root.iter("package"):
    name = pkg.get("name", "")
    if name in TARGET:
        lr = float(pkg.get("line-rate", 0)) * 100
        rows.append(f"| {TARGET[name]} | {lr:.1f}% |")

if not rows:
    print("No matching packages found in Cobertura.xml")
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