#!/usr/bin/env python3
"""Write the sample .img files that the in-Rhino test imports.

This generator needs GDAL, because only GDAL writes the ERDAS IMAGINE format. The unit
tests do not use these files, so a machine with no GDAL can still run them.

    python3 tools/make_sample_dem.py
"""

from __future__ import annotations

import math
import shutil
import subprocess
import sys
from pathlib import Path

SAMPLE_DIR = Path(__file__).resolve().parent.parent / "tests" / "samples"

NO_DATA = -9999.0
CELL_SIZE = 25.0
X_LOWER_LEFT = 500000.0
Y_LOWER_LEFT = 4600000.0


def find_gdal_translate() -> str:
    """Finds gdal_translate on the PATH or in the usual install folders."""
    found = shutil.which("gdal_translate")
    if found:
        return found

    for folder in ("/run/current-system/sw/bin", "/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin"):
        candidate = Path(folder) / "gdal_translate"
        if candidate.is_file():
            return str(candidate)

    print("gdal_translate is not installed. Install GDAL, for example with \"brew install gdal\".", file=sys.stderr)
    raise SystemExit(1)


def elevation(column: int, row: int) -> float:
    """A smooth hill. The value is exact at the corners, so the test can check them."""
    return 1200.0 + 80.0 * math.sin(column / 6.0) * math.cos(row / 5.0)


def write_ascii_grid(path: Path, columns: int, rows: int, void_corner: bool) -> None:
    lines = [
        f"ncols {columns}",
        f"nrows {rows}",
        f"xllcorner {X_LOWER_LEFT}",
        f"yllcorner {Y_LOWER_LEFT}",
        f"cellsize {CELL_SIZE}",
        f"NODATA_value {NO_DATA}",
    ]

    for row in range(rows):
        values = []
        for column in range(columns):
            is_void = void_corner and column < 3 and row < 3
            values.append(f"{NO_DATA if is_void else elevation(column, row):.6f}")
        lines.append(" ".join(values))

    path.write_text("\n".join(lines) + "\n", encoding="ascii")


def convert(gdal_translate: str, source: Path, target: Path) -> None:
    subprocess.run(
        [gdal_translate, "-q", "-of", "HFA", "-a_srs", "EPSG:32633", str(source), str(target)],
        check=True,
    )
    print(f"Wrote {target}")


def main() -> None:
    gdal_translate = find_gdal_translate()
    SAMPLE_DIR.mkdir(parents=True, exist_ok=True)

    cases = [
        ("hill.img", 40, 30, False),
        ("hill_voids.img", 40, 30, True),
        ("large.img", 400, 300, False),
    ]

    for name, columns, rows, void_corner in cases:
        ascii_path = SAMPLE_DIR / (Path(name).stem + ".asc")
        write_ascii_grid(ascii_path, columns, rows, void_corner)
        convert(gdal_translate, ascii_path, SAMPLE_DIR / name)
        ascii_path.unlink()
        (SAMPLE_DIR / (Path(name).stem + ".asc.aux.xml")).unlink(missing_ok=True)


if __name__ == "__main__":
    main()
