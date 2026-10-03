#! python 3
"""Runs the import pipeline inside Rhino and writes the result to a report file.

Open this file in the Rhino Script Editor (Tools > Script Editor) and press Run.
Build the plugin in Release first, because the script loads the built assembly, and run
tools/make_sample_dem.py first, because the script imports the sample files.

The script imports every sample into the active document and checks the surface count,
the corner positions, the elevations, the void handling, the tiling and the offset. It
deletes the objects of the active document before each case, so run it in an empty model.
"""

import os
import shutil
import sys
import traceback

ROOT_DIR = "/Users/chii/Documents/Projects/Rhino3D/import_dem"
PLUGIN_PATH = os.path.join(ROOT_DIR, "src", "Import_DEM", "bin", "Release", "net7.0", "Import_DEM.rhp")
SAMPLE_DIR = os.path.join(ROOT_DIR, "tests", "samples")
REPORT_PATH = os.path.join(ROOT_DIR, "rhino_test_report.txt")

# The sample grids come from tools/make_sample_dem.py.
CELL_SIZE = 25.0
FIRST_SAMPLE_X = 500012.5
FIRST_SAMPLE_Y = 4600737.5
HILL_COLUMNS = 40
HILL_ROWS = 30

RESULTS = []
OPEN_DOCUMENTS = []


def write_report(lines):
    with open(REPORT_PATH, "w", encoding="utf-8") as report_file:
        report_file.write("\n".join(lines))


try:
    import clr

    # pythonnet resolves assemblies by the .dll extension, so load a copy of the plugin.
    _assembly_copy = os.path.join(ROOT_DIR, "src", "Import_DEM", "bin", "Release", "net7.0", "Import_DEM.dll")
    shutil.copyfile(PLUGIN_PATH, _assembly_copy)
    clr.AddReference(_assembly_copy)

    import Rhino
    from Rhino.Geometry import Vector3d
    from Import_DEM.Gdal import ResamplingMethod
    from Import_DEM.Grid import GridTiler
    from Import_DEM.Import import (
        ElevationImporter,
        ImageSummary,
        ImportOptionsResolver,
        NoDataMode,
        SurfaceType,
        UnitChoice,
    )
except Exception:
    write_report(["FAIL  load the plugin assembly", traceback.format_exc()])
    raise


def check(name, condition, detail=""):
    RESULTS.append(("PASS" if condition else "FAIL", name, detail))


def close_to(actual, expected, tolerance=1e-6):
    return abs(actual - expected) <= tolerance


def sample(path):
    return os.path.join(SAMPLE_DIR, path)


def new_document():
    """
    Gives each case its own empty document. Rhino runs this script with no document open,
    so the script makes a headless one. The layers and the document user text of one case
    then never reach the next case.
    """
    doc = Rhino.RhinoDoc.CreateHeadless(None)
    OPEN_DOCUMENTS.append(doc)
    return doc


def read_summary(path):
    return ImageSummary.Read(path, "")


def import_file(doc, path, configure=None):
    """Reads the file, applies the option changes and runs the import."""
    summary = read_summary(path)
    options = ImportOptionsResolver.CreateDefaults(doc, summary)
    if configure is not None:
        configure(options)

    report = ElevationImporter().Import(doc, summary, options)
    return summary, options, report


def surfaces_of(doc):
    """
    The NURBS surfaces of the document. Rhino stores one surface as a Brep of one face, so
    the check reads the surface back through the face.
    """
    surfaces = []

    for obj in doc.Objects:
        geometry = obj.Geometry
        if isinstance(geometry, Rhino.Geometry.Brep):
            for face in geometry.Faces:
                surfaces.append(face.UnderlyingSurface().ToNurbsSurface())
        elif isinstance(geometry, Rhino.Geometry.Surface):
            surfaces.append(geometry.ToNurbsSurface())

    return surfaces


def corner_point(surface, u_at_end, v_at_end):
    """The surface point at one corner of the parameter space."""
    u_domain = surface.Domain(0)
    v_domain = surface.Domain(1)
    u = u_domain.T1 if u_at_end else u_domain.T0
    v = v_domain.T1 if v_at_end else v_domain.T0
    return surface.PointAt(u, v)


# ---------------------------------------------------------------------------
# The metadata that gdalinfo reports.
# ---------------------------------------------------------------------------
def case_metadata():
    summary = read_summary(sample("hill.img"))
    info = summary.Info

    check("metadata: driver is HFA", info.DriverShortName == "HFA", info.DriverShortName)
    check("metadata: size is 40 x 30", info.Width == HILL_COLUMNS and info.Height == HILL_ROWS)
    check("metadata: raster is axis aligned", info.IsAxisAligned)
    check("metadata: one band of Float32", info.Bands.Count == 1 and info.Bands[0].DataType == "Float32")
    check("metadata: no data value is -9999", close_to(info.Bands[0].NoDataValue, -9999.0, 1e-6))
    check("metadata: coordinate system text is present",
          info.CoordinateSystemWkt is not None and "UTM zone 33N" in info.CoordinateSystemWkt)
    check("metadata: default layer name is the file name", summary.DefaultLayerName() == "hill")


# ---------------------------------------------------------------------------
# The point order of the surface grid. A wrong order would transpose the terrain.
# ---------------------------------------------------------------------------
def case_point_order():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.SurfaceType = SurfaceType.Interpolated
        options.Stride = 1
        options.MaxPatchSize = 1000

    summary, options, report = import_file(doc, sample("hill.img"), configure)

    check("point order: one surface", report.SurfaceCount == 1, f"{report.SurfaceCount} surfaces")
    surfaces = surfaces_of(doc)
    if not surfaces:
        check("point order: surface exists", False)
        return

    surface = surfaces[0]

    check("point order: U counts the columns", surface.Points.CountU == HILL_COLUMNS,
          f"CountU {surface.Points.CountU}")
    check("point order: V counts the rows", surface.Points.CountV == HILL_ROWS,
          f"CountV {surface.Points.CountV}")

    # The first sample of the file sits at the U start and the V start.
    start = corner_point(surface, False, False)
    check("point order: U0 V0 is the first sample",
          close_to(start.X, FIRST_SAMPLE_X, 1e-3) and close_to(start.Y, FIRST_SAMPLE_Y, 1e-3),
          f"({start.X}, {start.Y})")

    # The U end is the last column, so X grows and Y does not change.
    u_end = corner_point(surface, True, False)
    expected_x = FIRST_SAMPLE_X + (HILL_COLUMNS - 1) * CELL_SIZE
    check("point order: the U direction follows the columns",
          close_to(u_end.X, expected_x, 1e-3) and close_to(u_end.Y, FIRST_SAMPLE_Y, 1e-3),
          f"({u_end.X}, {u_end.Y}), expected ({expected_x}, {FIRST_SAMPLE_Y})")

    # The V end is the last row, so Y falls and X does not change.
    v_end = corner_point(surface, False, True)
    expected_y = FIRST_SAMPLE_Y - (HILL_ROWS - 1) * CELL_SIZE
    check("point order: the V direction follows the rows",
          close_to(v_end.X, FIRST_SAMPLE_X, 1e-3) and close_to(v_end.Y, expected_y, 1e-3),
          f"({v_end.X}, {v_end.Y}), expected ({FIRST_SAMPLE_X}, {expected_y})")


# ---------------------------------------------------------------------------
# The interpolated surface touches the samples.
# ---------------------------------------------------------------------------
def case_interpolated_elevation():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.SurfaceType = SurfaceType.Interpolated
        options.Stride = 1
        options.MaxPatchSize = 1000

    import_file(doc, sample("hill.img"), configure)
    surfaces = surfaces_of(doc)
    if not surfaces:
        check("interpolated: surface exists", False)
        return

    # The generator writes 1200 + 80 * sin(column / 6) * cos(row / 5), so the first sample is 1200.
    start = corner_point(surfaces[0], False, False)
    check("interpolated: the surface passes through the first sample", close_to(start.Z, 1200.0, 1e-2),
          f"Z {start.Z}")


# ---------------------------------------------------------------------------
# The approximated surface takes the samples as control points.
# ---------------------------------------------------------------------------
def case_approximated_control_points():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.SurfaceType = SurfaceType.Approximated
        options.Stride = 1
        options.MaxPatchSize = 1000

    summary, options, report = import_file(doc, sample("hill.img"), configure)
    surfaces = surfaces_of(doc)
    if not surfaces:
        check("approximated: surface exists", False)
        return

    surface = surfaces[0]
    check("approximated: one control point for each sample",
          surface.Points.CountU == HILL_COLUMNS and surface.Points.CountV == HILL_ROWS,
          f"{surface.Points.CountU} x {surface.Points.CountV}")

    control_point = surface.Points.GetControlPoint(0, 0)
    check("approximated: the first control point is the first sample",
          close_to(control_point.X, FIRST_SAMPLE_X, 1e-3) and close_to(control_point.Z, 1200.0, 1e-2),
          f"({control_point.X}, {control_point.Y}, {control_point.Z})")


# ---------------------------------------------------------------------------
# The elevation factors.
# ---------------------------------------------------------------------------
def case_elevation_scale():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.ElevationScale = 2.0
        options.ElevationUnitFactor = 0.5
        options.Stride = 1
        options.MaxPatchSize = 1000

    import_file(doc, sample("hill.img"), configure)
    surfaces = surfaces_of(doc)
    if not surfaces:
        check("elevation: surface exists", False)
        return

    # The two factors multiply, so 2.0 and 0.5 leave the elevation unchanged.
    start = corner_point(surfaces[0], False, False)
    check("elevation: the scale and the unit factor multiply", close_to(start.Z, 1200.0, 1e-2), f"Z {start.Z}")


# ---------------------------------------------------------------------------
# The model unit that scales the source coordinates.
# ---------------------------------------------------------------------------
def case_model_units():
    check("units: the scale of the same unit is one",
          UnitChoice.ScaleTo(Rhino.UnitSystem.Meters, Rhino.UnitSystem.Meters) == 1.0)
    check("units: an unstated unit gives the scale one",
          UnitChoice.ScaleTo(UnitChoice.SameAsDocument, Rhino.UnitSystem.Meters) == 1.0)
    check("units: meters to millimeters gives one thousand",
          close_to(UnitChoice.ScaleTo(Rhino.UnitSystem.Meters, Rhino.UnitSystem.Millimeters), 1000.0, 1e-9))
    check("units: feet to meters gives the foot length",
          close_to(UnitChoice.ScaleTo(Rhino.UnitSystem.Feet, Rhino.UnitSystem.Meters), 0.3048, 1e-9))

    doc = new_document()
    doc.AdjustModelUnitSystem(Rhino.UnitSystem.Meters, False)

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.ModelUnits = Rhino.UnitSystem.Feet
        options.Stride = 1
        options.MaxPatchSize = 1000

    _, _, report = import_file(doc, sample("hill.img"), configure)
    check("units: the report holds the model scale", close_to(report.ModelScale, 0.3048, 1e-9),
          f"scale={report.ModelScale}")

    surfaces = surfaces_of(doc)
    if not surfaces:
        check("units: surface exists", False)
        return

    start = corner_point(surfaces[0], False, False)
    check("units: x and y scaled from feet to meters",
          close_to(start.X, FIRST_SAMPLE_X * 0.3048, 1e-3) and close_to(start.Y, FIRST_SAMPLE_Y * 0.3048, 1e-3),
          f"({start.X}, {start.Y})")
    check("units: the elevation takes the same scale", close_to(start.Z, 1200.0 * 0.3048, 1e-2), f"Z {start.Z}")


def case_model_units_default():
    """The default leaves the coordinates as they are."""
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.Stride = 1
        options.MaxPatchSize = 1000

    _, _, report = import_file(doc, sample("hill.img"), configure)
    check("units: the default changes nothing", report.ModelScale == 1.0, f"scale={report.ModelScale}")
    check("units: the report prints no unit line by default", report.ToUnitText() is None)


# ---------------------------------------------------------------------------
# The offset that moves the data near the world origin.
# ---------------------------------------------------------------------------
def case_offset():
    doc = new_document()
    summary, options, report = import_file(doc, sample("hill.img"))

    check("offset: the import proposes an offset for UTM coordinates", options.ApplyOffset)
    check("offset: the offset is rounded to the nearest 1000",
          close_to(options.Offset.X, -501000.0) and close_to(options.Offset.Y, -4600000.0),
          f"({options.Offset.X}, {options.Offset.Y})")

    surfaces = surfaces_of(doc)
    if not surfaces:
        check("offset: surface exists", False)
        return

    start = corner_point(surfaces[0], False, False)
    check("offset: the surface sits near the world origin",
          close_to(start.X, FIRST_SAMPLE_X - 501000.0, 1e-3) and close_to(start.Y, FIRST_SAMPLE_Y - 4600000.0, 1e-3),
          f"({start.X}, {start.Y})")

    stored = doc.Strings.GetValue("Import_DEM.Offset")
    check("offset: the import stores the offset in the document", stored is not None and "-501000" in stored, str(stored))


def case_offset_reuse():
    """A second import into the same document reuses the offset of the first import."""
    doc = new_document()
    doc.Strings.SetString("Import_SHP.Offset", "-1000,-2000,0")

    summary = read_summary(sample("hill.img"))
    options = ImportOptionsResolver.CreateDefaults(doc, summary)

    check("offset: the shapefile offset of the document wins",
          options.ApplyOffset and close_to(options.Offset.X, -1000.0) and close_to(options.Offset.Y, -2000.0),
          f"({options.Offset.X}, {options.Offset.Y})")


# ---------------------------------------------------------------------------
# The coordinate system text.
# ---------------------------------------------------------------------------
def case_projection_user_text():
    doc = new_document()
    import_file(doc, sample("hill.img"))

    layer_index = doc.Layers.FindByFullPath("hill", -1)
    check("projection: the import adds the layer", layer_index >= 0, f"index {layer_index}")
    if layer_index < 0:
        return

    stored = doc.Layers[layer_index].GetUserString("Import_DEM.Projection")
    check("projection: the layer holds the coordinate system text",
          stored is not None and "UTM zone 33N" in stored)


# ---------------------------------------------------------------------------
# The coordinate reference system translation.
# ---------------------------------------------------------------------------
def case_crs_detection():
    summary = read_summary(sample("hill.img"))
    check("crs: the summary detects the EPSG code", summary.DetectedCrs.EpsgCode == "EPSG:32633",
          str(summary.DetectedCrs.EpsgCode))

    options = ImportOptionsResolver.CreateDefaults(new_document(), summary)
    check("crs: the source CRS defaults to the detected code", options.SourceCrs == "EPSG:32633", options.SourceCrs)
    check("crs: the default translates nothing", not options.TranslatesCrs)


def case_crs_translation():
    """ETRS89 / UTM 33N sits a few decimeters at most from WGS 84 / UTM 33N, so the grid stays."""
    doc = new_document()

    def configure(options):
        options.TargetCrs = "EPSG:25833"
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.Stride = 1
        options.MaxPatchSize = 1000

    summary, _, report = import_file(doc, sample("hill.img"), configure)
    try:
        check("crs: the summary describes the translated raster", summary.IsTranslated)
        check("crs: the report holds the two CRS",
              report.SourceCrs == "EPSG:32633" and report.TargetCrs == "EPSG:25833",
              f"{report.SourceCrs} -> {report.TargetCrs}")
        check("crs: the report prints the CRS line", report.ToCrsText() is not None)

        surfaces = surfaces_of(doc)
        if not surfaces:
            check("crs: surface exists", False)
            return

        start = corner_point(surfaces[0], False, False)
        check("crs: the surface keeps its place between two near CRS",
              close_to(start.X, FIRST_SAMPLE_X, 1.0) and close_to(start.Y, FIRST_SAMPLE_Y, 1.0),
              f"({start.X}, {start.Y})")
        check("crs: the elevation keeps its value", close_to(start.Z, 1200.0, 1.0), f"Z {start.Z}")

        layer_index = doc.Layers.FindByFullPath("hill", -1)
        stored = doc.Layers[layer_index].GetUserString("Import_DEM.Projection") if layer_index >= 0 else None
        check("crs: the layer holds the target CRS text", stored is not None and "ETRS89" in stored, str(stored)[:60])
    finally:
        summary.Dispose()


def case_crs_geographic_target():
    doc = new_document()

    def configure(options):
        options.TargetCrs = "EPSG:4326"
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero

    summary, _, report = import_file(doc, sample("hill.img"), configure)
    try:
        check("crs: a geographic target gives a warning",
              any("geographic" in warning for warning in report.Warnings), str(list(report.Warnings)))

        surfaces = surfaces_of(doc)
        if not surfaces:
            check("crs: geographic surface exists", False)
            return

        start = corner_point(surfaces[0], False, False)
        check("crs: x and y are in degrees", close_to(start.X, 15.0, 0.1) and close_to(start.Y, 41.55, 0.1),
              f"({start.X}, {start.Y})")
    finally:
        summary.Dispose()


def case_crs_no_default_translation():
    doc = new_document()
    summary, _, report = import_file(doc, sample("hill.img"))
    check("crs: the default import is not translated", not summary.IsTranslated)
    check("crs: the report prints no CRS line by default", report.ToCrsText() is None)


def case_crs_refused():
    doc = new_document()

    def configure(options):
        options.TargetCrs = "EPSG:999999"

    try:
        import_file(doc, sample("hill.img"), configure)
        check("crs: a target that GDAL does not know is refused", False, "the import did not fail")
    except Exception as error:
        check("crs: a target that GDAL does not know is refused", "gdalwarp" in str(error), str(error)[:120])


# ---------------------------------------------------------------------------
# The voids.
# ---------------------------------------------------------------------------
def case_void_fill():
    doc = new_document()

    def configure(options):
        options.NoDataMode = NoDataMode.Fill
        options.Stride = 1
        options.MaxPatchSize = 1000

    summary, options, report = import_file(doc, sample("hill_voids.img"), configure)

    check("voids: the fill counts the 9 voids", report.VoidCount == 9, f"{report.VoidCount} voids")
    check("voids: the fill still builds the surface", report.SurfaceCount == 1, f"{report.SurfaceCount} surfaces")

    surfaces = surfaces_of(doc)
    if surfaces:
        start = corner_point(surfaces[0], False, False)
        check("voids: the filled corner holds a real elevation",
              1000.0 < start.Z < 1400.0, f"Z {start.Z}")


def case_void_constant():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.NoDataMode = NoDataMode.Constant
        options.NoDataElevation = 0.0
        options.Stride = 1
        options.MaxPatchSize = 1000

    summary, options, report = import_file(doc, sample("hill_voids.img"), configure)
    surfaces = surfaces_of(doc)
    if not surfaces:
        check("voids: the constant mode builds a surface", False)
        return

    start = corner_point(surfaces[0], False, False)
    check("voids: the constant mode writes the given elevation", close_to(start.Z, 0.0, 1e-2), f"Z {start.Z}")


def case_void_skip_tile():
    doc = new_document()

    def configure(options):
        options.NoDataMode = NoDataMode.SkipTile
        options.Stride = 1
        options.MaxPatchSize = 1000

    summary, options, report = import_file(doc, sample("hill_voids.img"), configure)

    check("voids: the skip mode leaves the tile out", report.SkippedTileCount == 1, f"{report.SkippedTileCount} skipped")
    check("voids: the skip mode adds no surface", report.SurfaceCount == 0, f"{report.SurfaceCount} surfaces")


# ---------------------------------------------------------------------------
# The tiles.
# ---------------------------------------------------------------------------
def case_tiling():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.Stride = 1
        options.MaxPatchSize = 100

    summary, options, report = import_file(doc, sample("large.img"), configure)

    expected_tiles = GridTiler.CountTiles(400, 300, 100)
    check("tiles: the import builds one surface for each tile",
          report.SurfaceCount == expected_tiles and report.TileCount == expected_tiles,
          f"{report.SurfaceCount} surfaces, {expected_tiles} expected")

    surfaces = surfaces_of(doc)
    if not surfaces:
        return

    # The tiles together cover the whole raster.
    box = surfaces[0].GetBoundingBox(True)
    for surface in surfaces[1:]:
        box.Union(surface.GetBoundingBox(True))

    expected_min_x = FIRST_SAMPLE_X
    expected_max_x = FIRST_SAMPLE_X + 399 * CELL_SIZE
    check("tiles: the surfaces cover the whole raster",
          close_to(box.Min.X, expected_min_x, 1e-2) and close_to(box.Max.X, expected_max_x, 1e-2),
          f"X from {box.Min.X} to {box.Max.X}, expected {expected_min_x} to {expected_max_x}")


def case_tile_edges_meet():
    """Neighbour tiles share a row of samples, so their edges sit on the same points."""
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.SurfaceType = SurfaceType.Approximated
        options.Stride = 1
        options.MaxPatchSize = 20

    import_file(doc, sample("hill.img"), configure)
    surfaces = surfaces_of(doc)

    if len(surfaces) < 2:
        check("tiles: the grid splits into more than one surface", False, f"{len(surfaces)} surfaces")
        return

    # Take the tiles of the top row, in order from the left, then compare each shared edge.
    boxes = [surface.GetBoundingBox(True) for surface in surfaces]
    top_y = max(box.Max.Y for box in boxes)
    top_row = sorted([box for box in boxes if close_to(box.Max.Y, top_y, 1e-3)], key=lambda box: box.Min.X)

    if len(top_row) < 2:
        check("tiles: the top row holds more than one tile", False, f"{len(top_row)} tiles")
        return

    # A shared column means the right edge of one tile sits on the left edge of the next.
    gaps = [top_row[index + 1].Min.X - top_row[index].Max.X for index in range(len(top_row) - 1)]
    largest_gap = max(abs(gap) for gap in gaps)

    check("tiles: the neighbour surfaces meet on their shared edge", largest_gap < 1e-3,
          f"{len(top_row)} tiles, largest gap {largest_gap}")


# ---------------------------------------------------------------------------
# The stride.
# ---------------------------------------------------------------------------
def case_stride():
    doc = new_document()

    def configure(options):
        options.ApplyOffset = False
        options.Offset = Vector3d.Zero
        options.SurfaceType = SurfaceType.Approximated
        options.Stride = 2
        options.Resampling = ResamplingMethod.Nearest
        options.MaxPatchSize = 1000

    summary, options, report = import_file(doc, sample("hill.img"), configure)

    check("stride: a stride of 2 halves the grid",
          report.Columns == 20 and report.Rows == 15, f"{report.Columns} x {report.Rows}")

    surfaces = surfaces_of(doc)
    if not surfaces:
        return

    # GDAL rewrites the georeference, so the surface still covers the whole raster.
    box = surfaces[0].GetBoundingBox(True)
    check("stride: the reduced grid still covers the raster",
          box.Max.X - box.Min.X > 900.0, f"width {box.Max.X - box.Min.X}")


# ---------------------------------------------------------------------------
# The grouping.
# ---------------------------------------------------------------------------
def case_group():
    doc = new_document()
    group_count_before = doc.Groups.Count

    def configure(options):
        options.GroupTiles = True
        options.Stride = 1
        options.MaxPatchSize = 20

    import_file(doc, sample("hill.img"), configure)

    check("group: the import adds one group for the tiles", doc.Groups.Count == group_count_before + 1,
          f"{doc.Groups.Count - group_count_before} groups added")


# ---------------------------------------------------------------------------
# The failures.
# ---------------------------------------------------------------------------
def case_missing_band():
    doc = new_document()

    def configure(options):
        options.Band = 5

    try:
        import_file(doc, sample("hill.img"), configure)
        check("failure: a band that does not exist is refused", False, "the import did not fail")
    except Exception as error:
        check("failure: a band that does not exist is refused", "band 5 does not exist" in str(error), str(error))


CASES = [
    case_metadata,
    case_point_order,
    case_interpolated_elevation,
    case_approximated_control_points,
    case_elevation_scale,
    case_model_units,
    case_model_units_default,
    case_offset,
    case_offset_reuse,
    case_projection_user_text,
    case_crs_detection,
    case_crs_translation,
    case_crs_geographic_target,
    case_crs_no_default_translation,
    case_crs_refused,
    case_void_fill,
    case_void_constant,
    case_void_skip_tile,
    case_tiling,
    case_tile_edges_meet,
    case_stride,
    case_group,
    case_missing_band,
]


def main():
    if not os.path.isdir(SAMPLE_DIR):
        write_report([f"FAIL  the sample folder {SAMPLE_DIR} does not exist. Run tools/make_sample_dem.py."])
        return

    for case in CASES:
        try:
            case()
        except Exception:
            RESULTS.append(("FAIL", case.__name__, traceback.format_exc()))

    for document in OPEN_DOCUMENTS:
        document.Dispose()

    lines = []
    for status, name, detail in RESULTS:
        lines.append(f"{status}  {name}" + (f"  [{detail}]" if detail else ""))

    passed = sum(1 for status, _, _ in RESULTS if status == "PASS")
    lines.append("")
    lines.append(f"{passed} of {len(RESULTS)} checks passed.")

    write_report(lines)
    print("\n".join(lines))


main()
