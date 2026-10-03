using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Import_DEM.Gdal
{
    /// <summary>
    /// The raster that gdalwarp wrote in the target CRS. The dispose deletes the temporary folder.
    /// </summary>
    public sealed class WarpedRaster : IDisposable
    {
        private readonly string _folder;

        internal WarpedRaster(string folder, string vrtPath)
        {
            _folder = folder;
            VrtPath = vrtPath;
        }

        /// <summary>The path of the VRT file. GDAL reads it like a raster.</summary>
        public string VrtPath { get; }

        public void Dispose()
        {
            GdalWarper.TryDeleteFolder(_folder);
        }
    }

    /// <summary>Translates a raster to another coordinate reference system.</summary>
    public sealed class GdalWarper
    {
        /// <summary>The value of a sample that the translated raster does not cover.</summary>
        public const double NoDataValue = -9999.0;

        private readonly GdalTools _tools;

        public GdalWarper(GdalTools tools)
        {
            _tools = tools;
        }

        /// <summary>
        /// Writes a VRT that describes the raster in the target CRS. A VRT is a small text file,
        /// so no full copy of the raster lands on the disk. GDAL resamples the grid when a later
        /// tool reads the VRT.
        /// </summary>
        /// <param name="sourcePath">The raster to read.</param>
        /// <param name="sourceCrs">The CRS that replaces the CRS of the file, or null to use the file.</param>
        /// <param name="targetCrs">The CRS of the result.</param>
        /// <param name="setNoDataValue">True when the source has no no-data value for the uncovered corners.</param>
        /// <exception cref="GdalNotFoundException">The GDAL folder holds no gdalwarp.</exception>
        public WarpedRaster Warp(string sourcePath, string? sourceCrs, string targetCrs, bool setNoDataValue)
        {
            if (_tools.GdalWarpPath is null)
            {
                throw new GdalNotFoundException(
                    "The plugin did not find the GDAL tool \"gdalwarp\", which translates the raster "
                    + "to the target CRS. It belongs to the same install as \"gdalinfo\".");
            }

            var folder = Path.Combine(Path.GetTempPath(), "Import_DEM_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var vrtPath = Path.Combine(folder, "warped.vrt");

            try
            {
                GdalProcess.Run(
                    _tools.GdalWarpPath,
                    BuildArguments(sourcePath, sourceCrs, targetCrs, setNoDataValue, vrtPath));
            }
            catch
            {
                TryDeleteFolder(folder);
                throw;
            }

            if (!File.Exists(vrtPath))
            {
                TryDeleteFolder(folder);
                throw new GdalFailureException("gdalwarp reported success but wrote no file.");
            }

            return new WarpedRaster(folder, vrtPath);
        }

        /// <summary>Builds the gdalwarp arguments. Bilinear resampling keeps a terrain smooth.</summary>
        public static IReadOnlyList<string> BuildArguments(
            string sourcePath,
            string? sourceCrs,
            string targetCrs,
            bool setNoDataValue,
            string vrtPath)
        {
            var arguments = new List<string> { "-q", "-of", "VRT", "-r", "bilinear" };

            if (!string.IsNullOrWhiteSpace(sourceCrs))
            {
                arguments.Add("-s_srs");
                arguments.Add(sourceCrs.Trim());
            }

            arguments.Add("-t_srs");
            arguments.Add(targetCrs.Trim());

            if (setNoDataValue)
            {
                arguments.Add("-dstnodata");
                arguments.Add(NoDataValue.ToString(CultureInfo.InvariantCulture));
            }

            arguments.Add(sourcePath);
            arguments.Add(vrtPath);
            return arguments;
        }

        internal static void TryDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temporary folder is not a reason to fail an import.
            }
        }
    }
}
