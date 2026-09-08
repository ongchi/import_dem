using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Import_DEM.Gdal
{
    /// <summary>The paths of the two GDAL command line tools that the plugin runs.</summary>
    public sealed class GdalTools
    {
        /// <summary>The folders that hold GDAL in a usual install. Searched after the PATH.</summary>
        private static readonly string[] KnownFolders =
        {
            "/run/current-system/sw/bin",   // Nix
            "/opt/homebrew/bin",            // Homebrew on Apple silicon
            "/usr/local/bin",               // Homebrew on Intel
            "/opt/local/bin",               // MacPorts
            "/Library/Frameworks/GDAL.framework/Programs",
            "/usr/bin",
            @"C:\OSGeo4W\bin",
            @"C:\Program Files\GDAL",
        };

        private GdalTools(string gdalInfoPath, string gdalTranslatePath)
        {
            GdalInfoPath = gdalInfoPath;
            GdalTranslatePath = gdalTranslatePath;
        }

        public string GdalInfoPath { get; }

        public string GdalTranslatePath { get; }

        /// <summary>
        /// Finds the GDAL tools. The search order is the folder of <paramref name="preferredFolder"/>,
        /// then the PATH, then the usual install folders.
        /// </summary>
        /// <exception cref="GdalNotFoundException">No folder holds both tools.</exception>
        public static GdalTools Find(string? preferredFolder = null)
        {
            var gdalInfoName = ExecutableName("gdalinfo");
            var gdalTranslateName = ExecutableName("gdal_translate");

            foreach (var folder in SearchFolders(preferredFolder))
            {
                var gdalInfoPath = Path.Combine(folder, gdalInfoName);
                var gdalTranslatePath = Path.Combine(folder, gdalTranslateName);

                if (File.Exists(gdalInfoPath) && File.Exists(gdalTranslatePath))
                    return new GdalTools(gdalInfoPath, gdalTranslatePath);
            }

            throw new GdalNotFoundException(
                "The plugin did not find the GDAL tools \"gdalinfo\" and \"gdal_translate\". "
                + "Install GDAL, for example with \"brew install gdal\". "
                + "An install in an unusual folder needs the folder, which the ImportDEM command asks for.");
        }

        private static IEnumerable<string> SearchFolders(string? preferredFolder)
        {
            if (!string.IsNullOrWhiteSpace(preferredFolder))
                yield return preferredFolder.Trim();

            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVariable))
            {
                foreach (var folder in pathVariable.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(folder))
                        yield return folder;
                }
            }

            foreach (var folder in KnownFolders)
                yield return folder;
        }

        private static string ExecutableName(string name)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? name + ".exe" : name;
        }
    }
}
