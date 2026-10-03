using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Import_DEM.Import
{
    /// <summary>Counts and messages collected during one import operation.</summary>
    public sealed class ImportReport
    {
        private readonly List<string> _warnings = new();

        /// <summary>The surfaces that the import added to the document.</summary>
        public int SurfaceCount { get; set; }

        /// <summary>The tiles that the grid was split into.</summary>
        public int TileCount { get; set; }

        /// <summary>The tiles that hold a void and were left out.</summary>
        public int SkippedTileCount { get; set; }

        /// <summary>The tiles that Rhino could not turn into a surface.</summary>
        public int FailedTileCount { get; set; }

        /// <summary>The samples that held no elevation.</summary>
        public int VoidCount { get; set; }

        public int Columns { get; set; }

        public int Rows { get; set; }

        public int Stride { get; set; } = 1;

        /// <summary>The factor from the source unit to the model unit of the document.</summary>
        public double ModelScale { get; set; } = 1.0;

        /// <summary>The factor from the source unit to the layout unit of the document.</summary>
        public double LayoutScale { get; set; } = 1.0;

        /// <summary>The CRS that the import translated from. Empty when no translation ran.</summary>
        public string SourceCrs { get; set; } = string.Empty;

        /// <summary>The CRS that the import translated to. Empty when no translation ran.</summary>
        public string TargetCrs { get; set; } = string.Empty;

        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>Adds a warning. Each message text is kept one time only.</summary>
        public void AddWarning(string message)
        {
            if (!_warnings.Contains(message))
                _warnings.Add(message);
        }

        public string ToSummary(string fileName)
        {
            var text = new StringBuilder();
            text.Append(CultureInfo.CurrentCulture, $"{fileName}: {Columns} x {Rows} samples");

            if (Stride > 1)
                text.Append(CultureInfo.CurrentCulture, $" (1 sample in {Stride})");

            text.Append(CultureInfo.CurrentCulture, $", {SurfaceCount} surface{(SurfaceCount == 1 ? string.Empty : "s")}.");

            if (VoidCount > 0)
                text.Append(CultureInfo.CurrentCulture, $" {VoidCount} samples held no elevation.");
            if (SkippedTileCount > 0)
                text.Append(CultureInfo.CurrentCulture, $" {SkippedTileCount} tiles with a void were skipped.");
            if (FailedTileCount > 0)
                text.Append(CultureInfo.CurrentCulture, $" {FailedTileCount} tiles gave no surface.");

            return text.ToString();
        }

        /// <summary>The CRS line, or null when no translation ran.</summary>
        public string? ToCrsText()
        {
            return TargetCrs.Length == 0
                ? null
                : $"CRS: the import translated the coordinates from {SourceCrs} to {TargetCrs}.";
        }

        /// <summary>
        /// The unit line, or null when both units leave the coordinates as they are.
        /// </summary>
        public string? ToUnitText()
        {
            if (ModelScale == 1.0 && LayoutScale == 1.0)
                return null;

            var text = new StringBuilder();
            text.Append(CultureInfo.CurrentCulture, $"Model units: the import scaled the coordinates by {ModelScale:R}.");

            if (LayoutScale != 1.0)
                text.Append(CultureInfo.CurrentCulture, $" Layout units: the layout factor is {LayoutScale:R}, and no layout received data.");

            return text.ToString();
        }
    }
}
