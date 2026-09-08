using System;
using System.Collections.Generic;

namespace Import_DEM.Gdal
{
    /// <summary>One band of a raster, as "gdalinfo -json" reports it.</summary>
    public sealed class GdalBandInfo
    {
        public GdalBandInfo(int index, string dataType, double? noDataValue, string? description)
        {
            Index = index;
            DataType = dataType;
            NoDataValue = noDataValue;
            Description = description;
        }

        /// <summary>The band number. GDAL counts from 1.</summary>
        public int Index { get; }

        /// <summary>The GDAL data type name, for example "Float32" or "Int16".</summary>
        public string DataType { get; }

        public double? NoDataValue { get; }

        public string? Description { get; }

        /// <summary>True when the samples do not fit a Float32 without a loss of accuracy.</summary>
        public bool NeedsMoreThanFloat32 =>
            string.Equals(DataType, "Float64", StringComparison.OrdinalIgnoreCase);

        /// <summary>The label of the band in the options dialog.</summary>
        public string Label =>
            string.IsNullOrWhiteSpace(Description)
                ? $"Band {Index} ({DataType})"
                : $"Band {Index} — {Description} ({DataType})";
    }

    /// <summary>The metadata of one raster file.</summary>
    public sealed class GdalInfo
    {
        public GdalInfo(
            string filePath,
            string driverShortName,
            int width,
            int height,
            IReadOnlyList<double> geoTransform,
            string? coordinateSystemWkt,
            IReadOnlyList<GdalBandInfo> bands)
        {
            FilePath = filePath;
            DriverShortName = driverShortName;
            Width = width;
            Height = height;
            GeoTransform = geoTransform;
            CoordinateSystemWkt = coordinateSystemWkt;
            Bands = bands;
        }

        public string FilePath { get; }

        public string DriverShortName { get; }

        /// <summary>The sample count in the X direction.</summary>
        public int Width { get; }

        /// <summary>The sample count in the Y direction.</summary>
        public int Height { get; }

        /// <summary>
        /// The six GDAL geo transform coefficients:
        /// [0] the X of the upper left corner, [1] the X cell size, [2] the X rotation,
        /// [3] the Y of the upper left corner, [4] the Y rotation, [5] the Y cell size, normally negative.
        /// </summary>
        public IReadOnlyList<double> GeoTransform { get; }

        public string? CoordinateSystemWkt { get; }

        public IReadOnlyList<GdalBandInfo> Bands { get; }

        public long SampleCount => (long)Width * Height;

        /// <summary>
        /// True when the grid follows the X and the Y axis. A rotated or sheared raster has no
        /// axis aligned grid of points, so the plugin cannot build a surface from it.
        /// </summary>
        public bool IsAxisAligned =>
            GeoTransform.Count == 6 && GeoTransform[2] == 0.0 && GeoTransform[4] == 0.0;

        public GdalBandInfo? FindBand(int index)
        {
            foreach (var band in Bands)
            {
                if (band.Index == index)
                    return band;
            }

            return null;
        }
    }
}
