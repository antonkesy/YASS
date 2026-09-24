#!/usr/bin/env python3
"""Fails when line coverage across the Cobertura reports under TestResults/ is below the threshold.

Usage: coverage-gate.py [--min 80] [--dir TestResults]
Prints a per-assembly table so the CI log shows where coverage is lost.
"""
import argparse
import glob
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--min", type=float, default=80.0)
    parser.add_argument("--dir", default="TestResults")
    args = parser.parse_args()

    # Every test project instruments the assemblies it references, so the same source line shows up in
    # several reports. Merge by (assembly, file, line) taking the best hit count, never summing.
    hits: dict[tuple[str, str, int], int] = {}
    reports = glob.glob(f"{args.dir}/**/coverage.cobertura.xml", recursive=True)
    if not reports:
        print(f"no coverage reports found under {args.dir}", file=sys.stderr)
        return 2

    for report in reports:
        for package in ET.parse(report).getroot().iter("package"):
            name = package.get("name", "?")
            for cls in package.iter("class"):
                filename = cls.get("filename", "?")
                for line in cls.iter("line"):
                    key = (name, filename, int(line.get("number", "0")))
                    hits[key] = max(hits.get(key, 0), int(line.get("hits", "0")))

    per_assembly: dict[str, list[int]] = {}
    per_file: dict[tuple[str, str], list[int]] = {}
    for (name, filename, _), count in hits.items():
        per_assembly.setdefault(name, [0, 0])
        per_assembly[name][0] += 1
        per_assembly[name][1] += 1 if count > 0 else 0
        per_file.setdefault((name, filename), [0, 0])
        per_file[(name, filename)][0] += 1
        per_file[(name, filename)][1] += 1 if count > 0 else 0

    total = sum(v[0] for v in per_assembly.values())
    hit = sum(v[1] for v in per_assembly.values())
    for name, (lines, covered) in sorted(per_assembly.items()):
        print(f"{name:24s} {covered:6d}/{lines:<6d} {100 * covered / max(lines, 1):5.1f}%")
    pct = 100 * hit / max(total, 1)
    print(f"{'TOTAL':24s} {hit:6d}/{total:<6d} {pct:5.1f}%  (gate {args.min:.0f}%)")

    worst = sorted(per_file.items(), key=lambda kv: kv[1][0] - kv[1][1], reverse=True)[:8]
    print("least covered files:")
    for (_, filename), (lines, covered) in worst:
        print(f"  {filename:64s} {covered:5d}/{lines:<5d} {100 * covered / max(lines, 1):5.1f}%")

    if pct + 1e-9 < args.min:
        print(f"coverage {pct:.1f}% is below the {args.min:.0f}% gate", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
