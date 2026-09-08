using System.Collections.Generic;
using System.Linq;

namespace Import_DEM
{
    /// <summary>
    /// The raster formats that the plugin offers. GDAL reads every format the same way, so a new
    /// format needs one more entry here and nothing else.
    /// </summary>
    public static class SupportedFormats
    {
        public sealed class RasterFormat
        {
            public RasterFormat(string description, string extension)
            {
                Description = description;
                Extension = extension;
            }

            /// <summary>The name in the import dialog, for example "ERDAS IMAGINE DEM".</summary>
            public string Description { get; }

            /// <summary>The file name extension with no leading point, for example "img".</summary>
            public string Extension { get; }

            public string DialogFilter => $"{Description} (*.{Extension})|*.{Extension}";
        }

        public static IReadOnlyList<RasterFormat> All { get; } = new[]
        {
            new RasterFormat("ERDAS IMAGINE DEM", "img"),
        };

        /// <summary>The filter text of the open file dialog.</summary>
        public static string OpenDialogFilter =>
            string.Join("|", All.Select(format => format.DialogFilter));
    }
}
