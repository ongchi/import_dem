using System.Globalization;
using System.IO;
using System.Linq;
using Import_DEM.Gdal;
using Import_DEM.Grid;

namespace Import_DEM.Import
{
    /// <summary>The counts that one set of options produces. The dialog shows them before the run.</summary>
    public readonly struct ImportEstimate
    {
        public ImportEstimate(int columns, int rows, int tileCount)
        {
            Columns = columns;
            Rows = rows;
            TileCount = tileCount;
        }

        public int Columns { get; }

        public int Rows { get; }

        public int TileCount { get; }

        public long PointCount => (long)Columns * Rows;

        public string ToText()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} x {1} samples, {2:N0} points, {3} surface{4}",
                Columns,
                Rows,
                PointCount,
                TileCount,
                TileCount == 1 ? string.Empty : "s");
        }
    }

    /// <summary>What the plugin knows about one raster file before the import runs.</summary>
    public sealed class ImageSummary : System.IDisposable
    {
        private WarpedRaster? _warp;
        private string? _appliedSourceCrs;
        private string _appliedTargetCrs = string.Empty;

        private ImageSummary(GdalTools tools, GdalInfo sourceInfo, DetectedCrs detectedCrs)
        {
            Tools = tools;
            SourceInfo = sourceInfo;
            Info = sourceInfo;
            DetectedCrs = detectedCrs;
        }

        public GdalTools Tools { get; }

        /// <summary>The metadata of the file on the disk.</summary>
        public GdalInfo SourceInfo { get; }

        /// <summary>
        /// The metadata of the raster that the import reads. It is the file itself, or the
        /// translated raster after <see cref="ApplyCrs"/> set a target CRS.
        /// </summary>
        public GdalInfo Info { get; private set; }

        /// <summary>The CRS that the file states.</summary>
        public DetectedCrs DetectedCrs { get; }

        /// <summary>The path that GDAL reads: the file, or the translated raster.</summary>
        public string FilePath => Info.FilePath;

        /// <summary>The path of the file on the disk.</summary>
        public string SourceFilePath => SourceInfo.FilePath;

        /// <summary>True when <see cref="Info"/> describes the raster in a target CRS.</summary>
        public bool IsTranslated => _warp is not null;

        /// <summary>Finds GDAL and reads the metadata of the file.</summary>
        public static ImageSummary Read(string filePath, string? gdalFolder = null)
        {
            var tools = GdalTools.Find(gdalFolder);
            var info = GdalInfoReader.Read(tools, filePath);

            var detectedCrs = info.CoordinateSystemWkt is null
                ? DetectedCrs.None
                : new DetectedCrs(GdalSrsInfo.FindEpsgCode(tools, filePath), info.CoordinateSystemWkt);

            return new ImageSummary(tools, info, detectedCrs);
        }

        /// <summary>
        /// Translates the raster to a target CRS, so that <see cref="Info"/> describes the result.
        /// An empty target goes back to the file itself. Returns true when <see cref="Info"/> changed.
        /// </summary>
        /// <param name="sourceCrsOverride">The CRS that replaces the CRS of the file, or null to use the file.</param>
        /// <param name="targetCrs">The CRS of the result, or an empty text for no translation.</param>
        /// <exception cref="GdalFailureException">GDAL refused a CRS, or the source CRS is unknown.</exception>
        public bool ApplyCrs(string? sourceCrsOverride, string? targetCrs)
        {
            var target = targetCrs?.Trim() ?? string.Empty;
            var source = target.Length == 0 || string.IsNullOrWhiteSpace(sourceCrsOverride)
                ? null
                : sourceCrsOverride.Trim();

            if (source == _appliedSourceCrs && target == _appliedTargetCrs)
                return false;

            WarpedRaster? warp = null;
            var info = SourceInfo;

            if (target.Length > 0)
            {
                if (source is null && !DetectedCrs.IsKnown)
                {
                    throw new GdalFailureException(
                        "The file states no coordinate reference system. Enter the source CRS.");
                }

                var needsNoDataValue = SourceInfo.Bands.Any(band => band.NoDataValue is null);
                warp = new GdalWarper(Tools).Warp(SourceFilePath, source, target, needsNoDataValue);

                try
                {
                    info = GdalInfoReader.Read(Tools, warp.VrtPath);
                }
                catch
                {
                    warp.Dispose();
                    throw;
                }
            }

            _warp?.Dispose();
            _warp = warp;
            _appliedSourceCrs = source;
            _appliedTargetCrs = target;
            Info = info;
            return true;
        }

        /// <summary>Deletes the temporary files of the translated raster.</summary>
        public void Dispose()
        {
            _warp?.Dispose();
            _warp = null;
        }

        /// <summary>The layer name that an import of this file uses by default.</summary>
        public string DefaultLayerName()
        {
            var name = Path.GetFileNameWithoutExtension(SourceFilePath);
            return string.IsNullOrWhiteSpace(name) ? "DEM" : name;
        }

        /// <summary>The grid size and the surface count that the options produce.</summary>
        public ImportEstimate Estimate(ImportOptions options)
        {
            var columns = GridStride.SampledLength(Info.Width, options.Stride);
            var rows = GridStride.SampledLength(Info.Height, options.Stride);

            return new ImportEstimate(columns, rows, GridTiler.CountTiles(columns, rows, options.MaxPatchSize));
        }
    }
}
