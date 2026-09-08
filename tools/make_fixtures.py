#!/usr/bin/env python3
"""Write the fixtures that the unit tests read.

The generator uses the standard library alone, so the unit tests need no GDAL.
Run it from the repository root:

    python3 tools/make_fixtures.py
"""

from __future__ import annotations

import json
import math
import struct
from pathlib import Path

FIXTURE_DIR = Path(__file__).resolve().parent.parent / "tests" / "Import_DEM.Tests" / "fixtures"

NO_DATA = -9999.0


def write_grid(
    name: str,
    columns: int,
    rows: int,
    samples: list[float],
    first_sample_x: float = 500012.5,
    first_sample_y: float = 4600737.5,
    step_x: float = 25.0,
    step_y: float = 25.0,
    byte_order: str = "I",
    extra_header: dict[str, str] | None = None,
    truncate_bytes: int = 0,
) -> None:
    """Writes one ESRI .hdr labelled grid pair, in the layout that gdal_translate writes."""
    header = {
        "BYTEORDER": byte_order,
        "LAYOUT": "BIL",
        "NROWS": str(rows),
        "NCOLS": str(columns),
        "NBANDS": "1",
        "NBITS": "32",
        "BANDROWBYTES": str(columns * 4),
        "TOTALROWBYTES": str(columns * 4),
        "PIXELTYPE": "FLOAT",
        "ULXMAP": repr(first_sample_x),
        "ULYMAP": repr(first_sample_y),
        "XDIM": repr(step_x),
        "YDIM": repr(step_y),
        "NODATA": repr(NO_DATA),
    }
    header.update(extra_header or {})

    lines = [f"{key:<14} {value}" for key, value in header.items()]
    (FIXTURE_DIR / f"{name}.hdr").write_text("\n".join(lines) + "\n", encoding="ascii")

    endian = "<" if byte_order.upper().startswith("I") else ">"
    block = struct.pack(f"{endian}{len(samples)}f", *samples)
    if truncate_bytes:
        block = block[:-truncate_bytes]
    (FIXTURE_DIR / f"{name}.bil").write_bytes(block)


def hill(columns: int, rows: int, void_corner: bool) -> list[float]:
    """A smooth hill. The void corner marks a 3 by 3 block as no data."""
    samples = []
    for row in range(rows):
        for column in range(columns):
            if void_corner and column < 3 and row < 3:
                samples.append(NO_DATA)
            else:
                samples.append(1200.0 + 80.0 * math.sin(column / 6.0) * math.cos(row / 5.0))
    return samples


def write_json(name: str, content: dict) -> None:
    (FIXTURE_DIR / f"{name}.json").write_text(json.dumps(content, indent=1), encoding="utf-8")


def build_gdalinfo(
    driver: str = "HFA",
    size: tuple[int, int] = (40, 30),
    geo_transform: list[float] | None = None,
    bands: list[dict] | None = None,
    with_srs: bool = True,
) -> dict:
    content: dict = {
        "description": "probe.img",
        "driverShortName": driver,
        "driverLongName": "Erdas Imagine Images (.img)",
        "size": list(size),
        "geoTransform": geo_transform or [500000.0, 25.0, 0.0, 4600750.0, 0.0, -25.0],
        "bands": bands
        or [
            {
                "band": 1,
                "block": [64, 64],
                "type": "Float32",
                "colorInterpretation": "Undefined",
                "description": "Layer_1",
                "noDataValue": NO_DATA,
            }
        ],
    }
    if with_srs:
        content["coordinateSystem"] = {
            "wkt": 'PROJCRS["WGS 84 / UTM zone 33N",\n    ID["EPSG",32633]]',
            "dataAxisToSRSAxisMapping": [1, 2],
        }
    return content


def main() -> None:
    FIXTURE_DIR.mkdir(parents=True, exist_ok=True)

    write_grid("hill", 40, 30, hill(40, 30, void_corner=False))
    write_grid("hill_voids", 40, 30, hill(40, 30, void_corner=True))
    write_grid("big_endian", 8, 6, hill(8, 6, void_corner=False), byte_order="M")
    write_grid("truncated", 40, 30, hill(40, 30, void_corner=False), truncate_bytes=400)
    write_grid("wrong_type", 8, 6, hill(8, 6, void_corner=False), extra_header={"PIXELTYPE": "SIGNEDINT"})
    write_grid("two_bands", 8, 6, hill(8, 6, void_corner=False), extra_header={"NBANDS": "2"})

    # The strided grid, as GDAL writes it after -outsize. The georeference follows the new step.
    write_grid(
        "hill_strided",
        20,
        15,
        hill(20, 15, void_corner=False),
        first_sample_x=500025.0,
        first_sample_y=4600725.0,
        step_x=50.0,
        step_y=50.0,
    )

    write_json("gdalinfo_hfa", build_gdalinfo())
    write_json("gdalinfo_no_srs", build_gdalinfo(with_srs=False))
    write_json(
        "gdalinfo_rotated",
        build_gdalinfo(geo_transform=[500000.0, 25.0, 3.0, 4600750.0, -2.0, -25.0]),
    )
    write_json(
        "gdalinfo_rgb",
        build_gdalinfo(
            driver="GTiff",
            bands=[
                {"band": 1, "type": "Byte", "colorInterpretation": "Red", "description": "Red"},
                {"band": 2, "type": "Byte", "colorInterpretation": "Green"},
                {"band": 3, "type": "Byte", "colorInterpretation": "Blue"},
            ],
        ),
    )
    write_json(
        "gdalinfo_float64",
        build_gdalinfo(
            bands=[{"band": 1, "type": "Float64", "noDataValue": "nan", "description": "Layer_1"}]
        ),
    )

    print(f"Wrote the fixtures to {FIXTURE_DIR}")


if __name__ == "__main__":
    main()
