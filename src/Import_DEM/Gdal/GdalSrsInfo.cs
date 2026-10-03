using System;
using System.Globalization;

namespace Import_DEM.Gdal
{
    /// <summary>Runs "gdalsrsinfo" to find the EPSG code of a coordinate reference system.</summary>
    public static class GdalSrsInfo
    {
        private const string EpsgPrefix = "EPSG:";

        /// <summary>
        /// Finds the EPSG code of the CRS of a file. Returns null when the tool is absent, when
        /// the file states no CRS, or when no code matches. The detection only fills a default
        /// value, so a failure of the tool is not an error of the import.
        /// </summary>
        public static string? FindEpsgCode(GdalTools tools, string filePath)
        {
            if (tools.GdalSrsInfoPath is null)
                return null;

            try
            {
                return ParseEpsgCode(GdalProcess.Run(tools.GdalSrsInfoPath, new[] { "-e", "-o", "epsg", filePath }));
            }
            catch (GdalFailureException)
            {
                // gdalsrsinfo stops with an error for a file that states no CRS.
                return null;
            }
        }

        /// <summary>
        /// Reads the first code of the "gdalsrsinfo -e -o epsg" output. GDAL writes "EPSG:-1"
        /// when no code matches.
        /// </summary>
        public static string? ParseEpsgCode(string output)
        {
            foreach (var line in output.Split('\n'))
            {
                var text = line.Trim();
                if (!text.StartsWith(EpsgPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isNumber = int.TryParse(
                    text.Substring(EpsgPrefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var code);

                return isNumber && code > 0
                    ? EpsgPrefix + code.ToString(CultureInfo.InvariantCulture)
                    : null;
            }

            return null;
        }
    }
}
