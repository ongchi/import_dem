using Import_DEM.Gdal;
using Rhino;
using Rhino.Geometry;

namespace Import_DEM.Import
{
    /// <summary>How the samples become a surface.</summary>
    public enum SurfaceType
    {
        /// <summary>The surface passes through every sample. Exact, but the solve is heavy.</summary>
        Interpolated,

        /// <summary>The samples become the control points. Fast, and the surface stays near the terrain.</summary>
        Approximated,
    }

    /// <summary>What the import does with a sample that holds no elevation.</summary>
    public enum NoDataMode
    {
        /// <summary>Replace each void by the mean of its valid neighbours.</summary>
        Fill,

        /// <summary>Replace each void by one elevation.</summary>
        Constant,

        /// <summary>Build no surface for a tile that holds a void.</summary>
        SkipTile,
    }

    /// <summary>The user settings of one import operation.</summary>
    public sealed class ImportOptions
    {
        /// <summary>The default tile side of the interpolated mode, in samples.</summary>
        public const int DefaultInterpolatedPatchSize = 200;

        /// <summary>The default tile side of the approximated mode, in samples.</summary>
        public const int DefaultApproximatedPatchSize = 500;

        /// <summary>The layer that receives the surfaces.</summary>
        public string LayerName { get; set; } = string.Empty;

        /// <summary>The band that holds the elevation. GDAL counts from 1.</summary>
        public int Band { get; set; } = 1;

        public SurfaceType SurfaceType { get; set; } = SurfaceType.Interpolated;

        /// <summary>Keep one sample in every N. A stride of 1 keeps every sample.</summary>
        public int Stride { get; set; } = 1;

        /// <summary>How GDAL combines the source samples when the stride reduces the grid.</summary>
        public ResamplingMethod Resampling { get; set; } = ResamplingMethod.Nearest;

        /// <summary>The largest tile side in samples.</summary>
        public int MaxPatchSize { get; set; } = DefaultInterpolatedPatchSize;

        public NoDataMode NoDataMode { get; set; } = NoDataMode.Fill;

        /// <summary>The elevation of a void when <see cref="NoDataMode"/> is Constant.</summary>
        public double NoDataElevation { get; set; }

        /// <summary>A factor on Z, for a vertical exaggeration.</summary>
        public double ElevationScale { get; set; } = 1.0;

        /// <summary>
        /// The factor that converts the elevation unit to the horizontal unit, for example
        /// 0.3048 for an elevation in feet on a grid in metres.
        /// </summary>
        public double ElevationUnitFactor { get; set; } = 1.0;

        /// <summary>
        /// The coordinate reference system of the source. An empty text means the CRS that the
        /// file states.
        /// </summary>
        public string SourceCrs { get; set; } = string.Empty;

        /// <summary>
        /// The coordinate reference system of the result. An empty text means no translation.
        /// </summary>
        public string TargetCrs { get; set; } = string.Empty;

        /// <summary>
        /// The EPSG code that the plugin detected in the file. While <see cref="SourceCrs"/> holds
        /// this text, the import reads the CRS from the file itself, because the code is a match
        /// and not a proof.
        /// </summary>
        public string DetectedSourceCrs { get; set; } = string.Empty;

        /// <summary>True when the import translates the data to <see cref="TargetCrs"/>.</summary>
        public bool TranslatesCrs => !string.IsNullOrWhiteSpace(TargetCrs);

        /// <summary>
        /// The source CRS that replaces the CRS of the file, or null when the file states it.
        /// </summary>
        public string? SourceCrsOverride => OverrideFor(SourceCrs);

        /// <summary>The override that a source CRS text gives. See <see cref="SourceCrsOverride"/>.</summary>
        public string? OverrideFor(string? sourceCrs)
        {
            var text = sourceCrs?.Trim() ?? string.Empty;
            var isTheDetectedCode = string.Equals(text, DetectedSourceCrs.Trim(), System.StringComparison.OrdinalIgnoreCase);
            return text.Length == 0 || isTheDetectedCode ? null : text;
        }

        /// <summary>
        /// The unit of the source coordinates when the model space receives the data. The import
        /// scales X, Y and Z from this unit to the model unit of the document.
        /// </summary>
        public UnitSystem ModelUnits { get; set; } = UnitChoice.SameAsDocument;

        /// <summary>
        /// The unit of the source coordinates when a layout receives the data.
        /// The command imports into the model space only, so this unit scales nothing yet.
        /// </summary>
        public UnitSystem LayoutUnits { get; set; } = UnitChoice.SameAsDocument;

        /// <summary>True to move the data by <see cref="Offset"/>.</summary>
        public bool ApplyOffset { get; set; }

        /// <summary>The translation added to every coordinate.</summary>
        public Vector3d Offset { get; set; } = Vector3d.Zero;

        /// <summary>True to group the tiles of one import.</summary>
        public bool GroupTiles { get; set; } = true;

        /// <summary>
        /// The factor that turns a sample into a Z coordinate in the source unit. The model unit
        /// scale then applies to X, Y and Z alike.
        /// </summary>
        public double ElevationFactor => ElevationScale * ElevationUnitFactor;

        /// <summary>The factor from <see cref="ModelUnits"/> to the model unit of the document.</summary>
        public double ModelScale(RhinoDoc doc) =>
            UnitChoice.ScaleTo(ModelUnits, doc?.ModelUnitSystem ?? UnitChoice.SameAsDocument);

        /// <summary>The factor from <see cref="LayoutUnits"/> to the layout unit of the document.</summary>
        public double LayoutScale(RhinoDoc doc) =>
            UnitChoice.ScaleTo(LayoutUnits, doc?.PageUnitSystem ?? UnitChoice.SameAsDocument);

        /// <summary>The sample budget that the surface type can carry.</summary>
        public long SampleBudget =>
            SurfaceType == SurfaceType.Interpolated
                ? Grid.GridStride.InterpolatedSampleBudget
                : Grid.GridStride.ApproximatedSampleBudget;

        /// <summary>The default tile side of the surface type.</summary>
        public static int DefaultPatchSize(SurfaceType surfaceType) =>
            surfaceType == SurfaceType.Interpolated ? DefaultInterpolatedPatchSize : DefaultApproximatedPatchSize;
    }
}
