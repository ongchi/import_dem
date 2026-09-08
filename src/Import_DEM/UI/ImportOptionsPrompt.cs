using System.Linq;
using Import_DEM.Gdal;
using Import_DEM.Grid;
using Import_DEM.Import;
using Rhino;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Import_DEM.UI
{
    /// <summary>Collects the import options on the command line. Scripts can set every option.</summary>
    public static class ImportOptionsPrompt
    {
        private static readonly string[] SurfaceTypeNames = { "Interpolated", "Approximated" };
        private static readonly string[] ResamplingNames = { "Nearest", "Average" };
        private static readonly string[] NoDataNames = { "Fill", "Constant", "SkipTile" };

        /// <summary>Asks for the options. Returns false when the user cancels.</summary>
        public static bool TryPrompt(ImageSummary summary, ImportOptions options)
        {
            var bandNames = summary.Info.Bands.Select(band => band.Index.ToString()).ToArray();
            var bandIndex = System.Math.Max(0, System.Array.IndexOf(bandNames, options.Band.ToString()));

            var stride = new OptionInteger(options.Stride, 1, 10000);
            var maxPatchSize = new OptionInteger(options.MaxPatchSize, GridTiler.MinimumPatchSize, 5000);
            var noDataElevation = new OptionDouble(options.NoDataElevation);
            var elevationScale = new OptionDouble(options.ElevationScale, 0.000001, 100000.0);
            var elevationUnitFactor = new OptionDouble(options.ElevationUnitFactor, 0.000001, 100000.0);
            var offsetX = new OptionDouble(options.Offset.X);
            var offsetY = new OptionDouble(options.Offset.Y);
            var applyOffset = new OptionToggle(options.ApplyOffset, "No", "Yes");
            var groupTiles = new OptionToggle(options.GroupTiles, "No", "Yes");

            var getOption = new GetOption();
            getOption.AcceptNothing(true);

            while (true)
            {
                options.Stride = stride.CurrentValue;
                options.MaxPatchSize = maxPatchSize.CurrentValue;
                var estimate = summary.Estimate(options);

                getOption.SetCommandPrompt($"DEM import options. {estimate.ToText()}. Press Enter to import");
                getOption.ClearCommandOptions();

                var surfaceOption = getOption.AddOptionList("Surface", SurfaceTypeNames, (int)options.SurfaceType);
                var bandOption = bandNames.Length > 1 ? getOption.AddOptionList("Band", bandNames, bandIndex) : -1;
                var strideOption = getOption.AddOptionInteger("Stride", ref stride);
                var resamplingOption = getOption.AddOptionList("Resampling", ResamplingNames, (int)options.Resampling);
                var patchOption = getOption.AddOptionInteger("MaxPatchSize", ref maxPatchSize);
                var noDataOption = getOption.AddOptionList("NoData", NoDataNames, (int)options.NoDataMode);
                var noDataElevationOption = options.NoDataMode == NoDataMode.Constant
                    ? getOption.AddOptionDouble("NoDataElevation", ref noDataElevation)
                    : -1;
                var scaleOption = getOption.AddOptionDouble("ElevationScale", ref elevationScale);
                var unitOption = getOption.AddOptionDouble("ElevationUnitFactor", ref elevationUnitFactor);
                var layerOption = getOption.AddOption("Layer");
                var offsetOption = getOption.AddOptionToggle("MoveToOrigin", ref applyOffset);
                var offsetXOption = applyOffset.CurrentValue ? getOption.AddOptionDouble("OffsetX", ref offsetX) : -1;
                var offsetYOption = applyOffset.CurrentValue ? getOption.AddOptionDouble("OffsetY", ref offsetY) : -1;
                var groupOption = getOption.AddOptionToggle("GroupTiles", ref groupTiles);

                var result = getOption.Get();

                if (result == GetResult.Nothing)
                    break;

                if (result != GetResult.Option)
                    return false;

                var chosen = getOption.OptionIndex();

                if (chosen == surfaceOption)
                {
                    options.SurfaceType = (SurfaceType)getOption.Option().CurrentListOptionIndex;

                    // The two surface types carry a different sample count, so the defaults follow.
                    ImportOptionsResolver.ApplySurfaceTypeDefaults(summary, options);
                    stride = new OptionInteger(options.Stride, 1, 10000);
                    maxPatchSize = new OptionInteger(options.MaxPatchSize, GridTiler.MinimumPatchSize, 5000);
                }
                else if (chosen == bandOption)
                {
                    bandIndex = getOption.Option().CurrentListOptionIndex;
                    options.Band = summary.Info.Bands[bandIndex].Index;
                }
                else if (chosen == resamplingOption)
                {
                    options.Resampling = (ResamplingMethod)getOption.Option().CurrentListOptionIndex;
                }
                else if (chosen == noDataOption)
                {
                    options.NoDataMode = (NoDataMode)getOption.Option().CurrentListOptionIndex;
                }
                else if (chosen == layerOption)
                {
                    var layerName = options.LayerName;
                    if (RhinoGet.GetString("Layer name", true, ref layerName) != Rhino.Commands.Result.Success)
                        continue;
                    if (!string.IsNullOrWhiteSpace(layerName))
                        options.LayerName = layerName.Trim();
                }
                else if (chosen == strideOption || chosen == patchOption || chosen == noDataElevationOption
                         || chosen == scaleOption || chosen == unitOption || chosen == offsetOption
                         || chosen == offsetXOption || chosen == offsetYOption || chosen == groupOption)
                {
                    // The option objects already hold the new value.
                }
            }

            options.Stride = stride.CurrentValue;
            options.MaxPatchSize = maxPatchSize.CurrentValue;
            options.NoDataElevation = noDataElevation.CurrentValue;
            options.ElevationScale = elevationScale.CurrentValue;
            options.ElevationUnitFactor = elevationUnitFactor.CurrentValue;
            options.GroupTiles = groupTiles.CurrentValue;
            options.ApplyOffset = applyOffset.CurrentValue;
            options.Offset = applyOffset.CurrentValue
                ? new Vector3d(offsetX.CurrentValue, offsetY.CurrentValue, 0.0)
                : Vector3d.Zero;

            return true;
        }
    }
}
