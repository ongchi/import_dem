# Import_DEM — Digital Elevation Model import for Rhino 8

A Rhino 8 plugin that reads a Digital Elevation Model and builds NURBS surfaces from the
elevation grid. The plugin adds the format to **File > Import** and gives the command
`ImportDEM` to scripts.

The first release reads ERDAS IMAGINE `.img` files.

## Requirement: GDAL

The plugin reads the raster with the GDAL command line tools `gdalinfo` and
`gdal_translate`. The CRS options also use `gdalwarp` and `gdalsrsinfo` of the same
install. Install GDAL before the first import:

```bash
brew install gdal
```

The plugin searches the `PATH` and the usual install folders. An install in an unusual
folder needs the folder, which the `ImportDEM` command asks for one time and then
remembers.

## Build

The build needs the .NET SDK 8. The plugin targets `net7.0`, the plugin framework of Rhino 8.

```bash
./build.sh            # build in Release and run the unit tests
./build.sh Debug      # build in Debug
```

The build writes `src/Import_DEM/bin/Release/net7.0/Import_DEM.rhp`.

Set the `DOTNET` variable when the SDK is not on the PATH:

```bash
DOTNET=/run/current-system/sw/bin/dotnet ./build.sh
```

## Install

```bash
./tools/install_mac.sh
```

The script copies `Import_DEM.rhp` into the plugin folder of the current user:

```
~/Library/Application Support/McNeel/Rhinoceros/MacPlugIns/
```

Restart Rhino 8 after the copy. Rhino reads that folder at startup.

The plugin is one file. It needs no companion file, because RhinoCommon is part of the
Rhino process.

## Use

**From the import dialog:** select **File > Import**, then select an `.img` file.
The options dialog opens before the import.

**From the command line:** run `ImportDEM`. The dash form `-ImportDEM` runs without the
dialog and asks for the path and the options on the command line, which lets a script set
every option.

### Options

| Option | Function |
| --- | --- |
| Layer | The layer that receives the surfaces. The default is the file name. |
| Band | The band that holds the elevation. The default is band 1. |
| Surface | `Interpolated` passes through every sample. `Approximated` takes the samples as control points. |
| Stride | Keep one sample in each N. The default brings the grid under the sample budget. |
| Resampling | `Nearest` keeps the sample values. `Average` gives a smoother surface. |
| MaxPatchSize | The largest tile side in samples. |
| NoData | `Fill`, `Constant` or `SkipTile`. See below. |
| NoDataElevation | The elevation for the `Constant` mode. |
| ElevationScale | A factor on Z, for a vertical exaggeration. |
| ElevationUnitFactor | Converts the elevation unit to the horizontal unit, for example 0.3048 for feet in a metre grid. |
| SourceCRS | The coordinate reference system of the file. The default is the CRS that the plugin detects in the file. See [Coordinate reference system](#coordinate-reference-system). |
| TargetCRS | The coordinate reference system of the result. The default is empty, which translates nothing. |
| ModelUnits | The unit of the source coordinates. The import scales X, Y and Z to the model unit of the document. |
| LayoutUnits | The unit of the source coordinates for a layout. See [Units](#units). |
| MoveToOrigin | Moves the data near the world origin. |
| OffsetX, OffsetY | The translation added to every coordinate. |
| GroupTiles | Groups the surfaces of one import. |

## How the import works

```
.img  --gdalinfo -json-->  metadata  -->  options dialog
      --gdal_translate -of EHdr -->  temporary grid  -->  samples
      -->  no data fill  -->  tiles  -->  one NURBS surface for each tile
```

GDAL writes a temporary ESRI `.hdr` grid, which is a raw block of samples and a short text
header. The plugin reads that grid and deletes the temporary folder at the end of the run.

The stride runs inside GDAL with `-outsize`, so a large file never lands on the disk at
full size. GDAL rewrites the header georeference to match the reduced grid, so the surface
still covers the whole raster.

### Surface type

| Type | Result |
| --- | --- |
| `Interpolated` | The surface passes through every sample. Exact, but the solve is heavy, so the sample budget is 250000. |
| `Approximated` | The samples become the control points. Fast and light, and the surface stays near the terrain. The sample budget is 1000000. |

The U direction of the surface follows the columns of the raster and the V direction
follows the rows.

### Tiles

One NURBS surface with a very large control point grid is slow in Rhino, so the import
splits the grid into tiles of at most `MaxPatchSize` samples in each direction. Neighbour
tiles share one row or one column of samples, so the surfaces meet edge to edge with no
gap. The default `MaxPatchSize` is 200 for the interpolated mode and 500 for the
approximated mode.

The dialog shows the sampled grid size, the point count and the surface count before the
import runs, so a slow run can be made smaller first.

### Samples with no elevation

A DEM holds a no data value in voids. A NURBS surface is a full grid of points, so a void
needs an elevation:

| Mode | Action |
| --- | --- |
| `Fill` (default) | Replace each void by the mean of its valid neighbours. The fill runs in passes, so the values grow inward until the void is closed. |
| `Constant` | Replace each void by one elevation. |
| `SkipTile` | Build no surface for a tile that holds a void. |

The report counts the voids and the skipped tiles.

## Coordinate reference system

`SourceCRS` (from) and `TargetCRS` (to) translate the raster from one coordinate reference
system to another. Both options accept each text that GDAL accepts: an EPSG code such as
`EPSG:25833`, a WKT, or a PROJ string.

- **SourceCRS** — the plugin detects the CRS of the file and runs `gdalsrsinfo` to find its
  EPSG code. The code is the default value. When no code matches, the field is empty and
  shows the CRS name of the file, and the import uses the CRS of the file. A text that you
  type replaces the CRS of the file.
- **TargetCRS** — empty by default. An empty target translates nothing, and `SourceCRS`
  then has no effect. On the command line, the text `None` clears a CRS.

The EPSG code is a match, not a proof. While `SourceCRS` holds the detected code, the
import therefore reads the CRS from the file itself.

`gdalwarp` does the translation. It writes a small VRT file that describes the raster in
the target CRS, and `gdal_translate` then reads that VRT. The translation resamples the
grid with the bilinear method, so the grid size and the sample values can change. The
dialog has an **Apply CRS** button, which shows the new grid size, the new estimate and
the new offset before the import runs.

The order of the transforms is:

```
document point = translate(source point) x model unit scale + offset
```

- The translation is horizontal only. The elevation keeps its value.
- `ModelUnits` states the unit of the **target** CRS when a translation runs.
- The corners of a translated raster can hold no data. The `NoData` mode handles them.
- A geographic target CRS gives X and Y in degrees. The import permits it and writes a
  warning.
- A translated raster is always north up, so a target CRS also makes a rotated raster
  importable.
- A file that states no CRS needs a `SourceCRS` text.

## Units

A DEM states its coordinates in the unit of its coordinate system, and the plugin does not
read that unit from the file. `ModelUnits` states it. The import then scales X, Y and Z
from that unit to the model unit of the document. The default is "Same as the document",
which keeps the coordinates as they are.

The elevation takes the same scale as the horizontal coordinates, after
`ElevationScale` and `ElevationUnitFactor`. The whole Z factor is therefore:

```
Z = sample x ElevationScale x ElevationUnitFactor x model unit scale
```

Use `ElevationUnitFactor` when the elevation unit differs from the horizontal unit of the
source, for example feet of height on a grid in metres. Use `ModelUnits` when the source
unit differs from the document unit.

The order of the transforms is the scale first, then the offset:

```
document point = source point x model unit scale + offset
```

The offset comes last because the document keeps it in document units. The dialog and the
command line therefore move the proposed offset when the model unit changes, unless an
earlier import already fixed the offset.

`LayoutUnits` states the unit for a layout, and its factor goes to the layout unit of the
document. **The command imports into the model space only, so this option scales no
geometry today.** The report prints its factor. The option becomes live when the plugin
can import into a layout.

## Coordinates far from the origin

Rhino loses accuracy when geometry sits far from the world origin, and projected
coordinate systems such as UTM give coordinates of millions of units. When the center of
the raster is more than 100000 units from the origin, the plugin proposes an offset that
moves the data near the origin. The offset is rounded to the nearest 1000.

The plugin writes the offset it applied into the document user text with the key
`Import_DEM.Offset`. The next import into the same document reuses that offset, so every
file lands in the same place. When that key is absent, the plugin reads the key
`Import_SHP.Offset` of the shapefile import plugin, so a DEM and a shapefile of the same
area land together.

The coordinate system text goes on the import layer as user text with the key
`Import_DEM.Projection`. It is the text of the source, or the text of the target CRS when
a translation ran.

## Tests

```bash
python3 tools/make_fixtures.py     # write the unit test fixtures, no GDAL needed
./build.sh                         # build and run the unit tests
```

The unit tests cover the grid reader, the `gdalinfo` parser, the `gdalwarp` arguments, the
CRS detection, the void fill and the tile split. They need no GDAL and no Rhino.

The surface construction needs RhinoCommon, so a second test runs inside Rhino:

```bash
python3 tools/make_sample_dem.py   # write the sample .img files, needs GDAL
```

Then open `tools/rhino_import_test.py` in the Rhino Script Editor (**Tools > Script
Editor**) and press Run, or run it from a shell while Rhino is open:

```bash
"/Applications/Rhino 8.app/Contents/Resources/bin/rhinocode" script \
    "$(pwd)/tools/rhino_import_test.py"
```

The script writes `rhino_test_report.txt`. It makes its own headless document for each
case, so it changes no open model.

## Limits

- The plugin needs GDAL on the machine. It does not read the raster by itself.
- The first release reads `.img` only. GeoTIFF comes later.
- The elevation passes through a 32 bit intermediate, because the ESRI `.hdr` format holds
  no 64 bit samples. The loss is about 0.0001 m at an elevation of 1000 m. The import
  writes a warning when the source band holds 64 bit elevations.
- A rotated or sheared raster is refused, because its samples do not sit on a grid that
  follows the X and the Y axis. Set a `TargetCRS`, or use `gdalwarp` to write a north up
  copy.
- The CRS translation is horizontal only. It does not translate the elevation between
  vertical datums.
- The document does not remember the target CRS. Each import states it.
- The plugin imports, it does not export.
- The plugin imports one band as a height field. It does not import the image colours.
- A void makes a filled surface, not a hole. Trimmed surfaces are out of scope.
