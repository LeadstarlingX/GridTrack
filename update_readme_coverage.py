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
    "GridTrack.Presentation":   "Presentation",  # HTTP surface, covered via the integration suite
}

def assembly_label(pkg_name):
    # Strip .dll suffix and match by assembly prefix (handles namespace-level packages)
    name = pkg_name.removesuffix(".dll")
    for prefix, label in TARGET.items():
        if name == prefix or name.startswith(prefix + "."):
            return label
    return None

# Use line-rate from each package element (the value ReportGenerator computes and
# displays in the HTML report's Percentage column) weighted by line count so that
# assemblies split across multiple namespace-packages are aggregated correctly.
totals = {label: [0.0, 0] for label in TARGET.values()}  # [covered_weighted, total_lines]

for pkg in root.iter("package"):
    label = assembly_label(pkg.get("name", ""))
    if label is None:
        continue
    rate  = float(pkg.get("line-rate", 0))
    lines = sum(1 for _ in pkg.iter("line"))
    if lines == 0:
        continue
    totals[label][0] += rate * lines
    totals[label][1] += lines

rows = []
for prefix, label in TARGET.items():
    covered_weighted, total = totals[label]
    if total > 0:
        pct = covered_weighted / total * 100
        rows.append(f"| {label} | {pct:.1f}% |")

if not rows:
    found = sorted({p.get("name", "") for p in root.iter("package")})[:30]
    print(f"No matching packages found in Cobertura.xml\nFound packages: {found}")
    sys.exit(1)

table = "| Layer | Line Coverage |\n|-------|---------------|\n" + "\n".join(rows)

readme = pathlib.Path("README.md")
content = readme.read_text(encoding="utf-8")
updated = re.sub(
    r"<!-- COVERAGE_START -->.*?<!-- COVERAGE_END -->",
    f"<!-- COVERAGE_START -->\n{table}\n<!-- COVERAGE_END -->",
    content,
    flags=re.DOTALL,
)

readme.write_text(updated, encoding="utf-8")
print("README.md updated")
