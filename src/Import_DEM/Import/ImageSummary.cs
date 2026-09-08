using System.Globalization;
using System.IO;
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
    public sealed class ImageSummary
    {
        private ImageSummary(GdalTools tools, GdalInfo info)
        {
            Tools = tools;
            Info = info;
        }

        public GdalTools Tools { get; }

        public GdalInfo Info { get; }

        public string FilePath => Info.FilePath;

        /// <summary>Finds GDAL and reads the metadata of the file.</summary>
        public static ImageSummary Read(string filePath, string? gdalFolder = null)
        {
            var tools = GdalTools.Find(gdalFolder);
            return new ImageSummary(tools, GdalInfoReader.Read(tools, filePath));
        }

        /// <summary>The layer name that an import of this file uses by default.</summary>
        public string DefaultLayerName()
        {
            var name = Path.GetFileNameWithoutExtension(FilePath);
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
