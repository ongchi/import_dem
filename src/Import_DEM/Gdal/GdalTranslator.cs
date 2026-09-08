using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Import_DEM.Gdal
{
    /// <summary>How GDAL combines the source samples when it reduces the grid.</summary>
    public enum ResamplingMethod
    {
        /// <summary>Take the nearest source sample. Keeps the sample values of the file.</summary>
        Nearest,

        /// <summary>Take the mean of the source samples. Gives a smoother surface.</summary>
        Average,
    }

    /// <summary>
    /// The temporary grid that gdal_translate wrote. The dispose deletes the whole temporary
    /// folder, which holds the raw block and the header, the projection and the statistics files
    /// that GDAL writes beside it.
    /// </summary>
    public sealed class GridConversion : IDisposable
    {
        private readonly string _folder;

        internal GridConversion(string folder, string gridPath)
        {
            _folder = folder;
            GridPath = gridPath;
        }

        /// <summary>The path of the raw sample block. The header sits beside it with the .hdr extension.</summary>
        public string GridPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_folder))
                    Directory.Delete(_folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A locked temporary file is not a reason to fail an import that already succeeded.
            }
        }
    }

    /// <summary>Converts one band of a raster into a plain grid that the plugin can read.</summary>
    public sealed class GdalTranslator
    {
        /// <summary>
        /// The EHdr format refuses Float64, so the intermediate holds Float32. The loss is about
        /// 0.0001 m at an elevation of 1000 m, far below the accuracy of any DEM.
        /// </summary>
        private const string OutputDataType = "Float32";

        private readonly GdalTools _tools;

        public GdalTranslator(GdalTools tools)
        {
            _tools = tools;
        }

        /// <summary>
        /// Writes one band into a temporary ESRI .hdr grid. GDAL does the reduction, so a large
        /// file never lands on the disk at full size, and GDAL updates the header georeference to
        /// match the reduced grid.
        /// </summary>
        /// <param name="sourcePath">The raster to read.</param>
        /// <param name="band">The band number. GDAL counts from 1.</param>
        /// <param name="width">The sample count of the result in the X direction.</param>
        /// <param name="height">The sample count of the result in the Y direction.</param>
        /// <param name="resampling">How GDAL combines the source samples.</param>
        public GridConversion Convert(string sourcePath, int band, int width, int height, ResamplingMethod resampling)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    $"The target grid is {width} by {height} samples. A surface needs at least 2 samples in each direction.");

            var folder = CreateTemporaryFolder();
            var gridPath = Path.Combine(folder, "grid.bil");

            try
            {
                GdalProcess.Run(_tools.GdalTranslatePath, BuildArguments(sourcePath, band, width, height, resampling, gridPath));
            }
            catch
            {
                TryDeleteFolder(folder);
                throw;
            }

            if (!File.Exists(gridPath))
            {
                TryDeleteFolder(folder);
                throw new GdalFailureException("gdal_translate reported success but wrote no grid file.");
            }

            return new GridConversion(folder, gridPath);
        }

        private static IReadOnlyList<string> BuildArguments(
            string sourcePath,
            int band,
            int width,
            int height,
            ResamplingMethod resampling,
            string gridPath)
        {
            return new[]
            {
                "-q",
                "-of", "EHdr",
                "-ot", OutputDataType,
                "-b", band.ToString(CultureInfo.InvariantCulture),
                "-outsize", width.ToString(CultureInfo.InvariantCulture), height.ToString(CultureInfo.InvariantCulture),
                "-r", resampling == ResamplingMethod.Average ? "average" : "nearest",
                sourcePath,
                gridPath,
            };
        }

        private static string CreateTemporaryFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "Import_DEM_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void TryDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The first error is the one that matters. A leftover temporary folder is not.
            }
        }
    }
}
