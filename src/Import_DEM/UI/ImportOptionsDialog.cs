using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Import_DEM.Gdal;
using Import_DEM.Grid;
using Import_DEM.Import;
using Rhino.Geometry;
using Rhino.UI;

namespace Import_DEM.UI
{
    /// <summary>The dialog that collects the import options in interactive mode.</summary>
    public sealed class ImportOptionsDialog : Dialog<bool>
    {
        private readonly ImageSummary _summary;
        private readonly ImportOptions _options;

        private readonly TextBox _layerName = new();
        private readonly DropDown _band = new();
        private readonly DropDown _surfaceType = new();
        private readonly NumericStepper _stride = new() { MinValue = 1, MaxValue = 10000, DecimalPlaces = 0 };
        private readonly DropDown _resampling = new();
        private readonly NumericStepper _maxPatchSize = new() { MinValue = GridTiler.MinimumPatchSize, MaxValue = 5000, DecimalPlaces = 0 };
        private readonly DropDown _noDataMode = new();
        private readonly NumericStepper _noDataElevation = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6, MinValue = double.MinValue, MaxValue = double.MaxValue };
        private readonly NumericStepper _elevationScale = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6, MinValue = 0.000001, MaxValue = 100000 };
        private readonly NumericStepper _elevationUnitFactor = new() { DecimalPlaces = 4, MaximumDecimalPlaces = 8, MinValue = 0.000001, MaxValue = 100000 };
        private readonly CheckBox _applyOffset = new();
        private readonly NumericStepper _offsetX = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6, MinValue = double.MinValue, MaxValue = double.MaxValue };
        private readonly NumericStepper _offsetY = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6, MinValue = double.MinValue, MaxValue = double.MaxValue };
        private readonly CheckBox _groupTiles = new();
        private readonly Label _estimate = new();

        private ImportOptionsDialog(ImageSummary summary, ImportOptions options)
        {
            _summary = summary;
            _options = options;

            Title = "Import DEM";
            Padding = new Padding(10);
            Resizable = false;
            Result = false;

            BuildControls();
            Content = BuildLayout();
            UpdateEnabledState();
            UpdateEstimate();
        }

        /// <summary>Shows the dialog and writes the user choices into <paramref name="options"/>.</summary>
        public static bool Show(ImageSummary summary, ImportOptions options)
        {
            var dialog = new ImportOptionsDialog(summary, options);
            return dialog.ShowModal(RhinoEtoApp.MainWindowForDocument(Rhino.RhinoDoc.ActiveDoc));
        }

        private void BuildControls()
        {
            _layerName.Text = _options.LayerName;

            foreach (var band in _summary.Info.Bands)
                _band.Items.Add(new ListItem { Text = band.Label, Key = band.Index.ToString() });
            _band.SelectedIndex = 0;

            _surfaceType.Items.Add(new ListItem { Text = "Through the samples (exact, slow)", Key = nameof(SurfaceType.Interpolated) });
            _surfaceType.Items.Add(new ListItem { Text = "From the samples as control points (fast)", Key = nameof(SurfaceType.Approximated) });
            _surfaceType.SelectedIndex = (int)_options.SurfaceType;
            _surfaceType.SelectedIndexChanged += (_, _) => OnSurfaceTypeChanged();

            _stride.Value = _options.Stride;
            _stride.ValueChanged += (_, _) => UpdateEstimate();

            _resampling.Items.Add(new ListItem { Text = "Nearest sample", Key = nameof(ResamplingMethod.Nearest) });
            _resampling.Items.Add(new ListItem { Text = "Mean of the samples", Key = nameof(ResamplingMethod.Average) });
            _resampling.SelectedIndex = (int)_options.Resampling;

            _maxPatchSize.Value = _options.MaxPatchSize;
            _maxPatchSize.ValueChanged += (_, _) => UpdateEstimate();

            _noDataMode.Items.Add(new ListItem { Text = "Fill from the neighbours", Key = nameof(NoDataMode.Fill) });
            _noDataMode.Items.Add(new ListItem { Text = "Use one elevation", Key = nameof(NoDataMode.Constant) });
            _noDataMode.Items.Add(new ListItem { Text = "Skip the tile", Key = nameof(NoDataMode.SkipTile) });
            _noDataMode.SelectedIndex = (int)_options.NoDataMode;
            _noDataMode.SelectedIndexChanged += (_, _) => UpdateEnabledState();

            _noDataElevation.Value = _options.NoDataElevation;
            _elevationScale.Value = _options.ElevationScale;
            _elevationUnitFactor.Value = _options.ElevationUnitFactor;

            _applyOffset.Text = "Move the data near the world origin";
            _applyOffset.Checked = _options.ApplyOffset;
            _applyOffset.CheckedChanged += (_, _) => UpdateEnabledState();

            var center = OriginOffset.CenterOf(_summary.Info);
            var offset = _options.ApplyOffset ? _options.Offset : OriginOffset.Suggest(center.X, center.Y);
            _offsetX.Value = offset.X;
            _offsetY.Value = offset.Y;

            _groupTiles.Text = "Group the surfaces of this import";
            _groupTiles.Checked = _options.GroupTiles;
        }

        private Control BuildLayout()
        {
            var layout = new DynamicLayout { DefaultSpacing = new Size(6, 6) };

            layout.AddRow(new Label { Text = "File" }, new Label { Text = FileDescription() });
            layout.AddRow(new Label { Text = "Layer" }, _layerName);
            layout.AddRow(new Label { Text = "Elevation band" }, _band);
            layout.AddRow(new Label { Text = "Surface" }, _surfaceType);
            layout.AddRow(new Label { Text = "Keep 1 sample in" }, _stride);
            layout.AddRow(new Label { Text = "Reduce by" }, _resampling);
            layout.AddRow(new Label { Text = "Largest tile side" }, _maxPatchSize);
            layout.AddRow(new Label { Text = "Result" }, _estimate);
            layout.AddRow(new Label { Text = "Samples with no elevation" }, _noDataMode);
            layout.AddRow(new Label { Text = "Their elevation" }, _noDataElevation);
            layout.AddRow(new Label { Text = "Elevation scale" }, _elevationScale);
            layout.AddRow(new Label { Text = "Elevation unit factor" }, _elevationUnitFactor);
            layout.AddRow(new Label(), _applyOffset);
            layout.AddRow(new Label { Text = "Offset X" }, _offsetX);
            layout.AddRow(new Label { Text = "Offset Y" }, _offsetY);
            layout.AddRow(new Label(), _groupTiles);

            var importButton = new Button { Text = "Import" };
            importButton.Click += (_, _) => Accept();
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Click += (_, _) => Close(false);

            DefaultButton = importButton;
            AbortButton = cancelButton;

            layout.AddSeparateRow(null, importButton, cancelButton);
            return layout;
        }

        private string FileDescription()
        {
            return $"{_summary.Info.Width} x {_summary.Info.Height} samples, {_summary.Info.DriverShortName}";
        }

        /// <summary>The stride and the tile size follow the surface type, because the budgets differ.</summary>
        private void OnSurfaceTypeChanged()
        {
            _options.SurfaceType = SelectedSurfaceType();
            ImportOptionsResolver.ApplySurfaceTypeDefaults(_summary, _options);

            _stride.Value = _options.Stride;
            _maxPatchSize.Value = _options.MaxPatchSize;
            UpdateEstimate();
        }

        /// <summary>Shows the counts that the current options produce, before the run starts.</summary>
        private void UpdateEstimate()
        {
            var preview = new ImportOptions
            {
                Stride = (int)_stride.Value,
                MaxPatchSize = (int)_maxPatchSize.Value,
            };

            _estimate.Text = _summary.Estimate(preview).ToText();
        }

        private void UpdateEnabledState()
        {
            _noDataElevation.Enabled = SelectedNoDataMode() == NoDataMode.Constant;

            var offsetEnabled = _applyOffset.Checked == true;
            _offsetX.Enabled = offsetEnabled;
            _offsetY.Enabled = offsetEnabled;
        }

        private SurfaceType SelectedSurfaceType() =>
            (SurfaceType)(_surfaceType.SelectedIndex < 0 ? 0 : _surfaceType.SelectedIndex);

        private NoDataMode SelectedNoDataMode() =>
            (NoDataMode)(_noDataMode.SelectedIndex < 0 ? 0 : _noDataMode.SelectedIndex);

        private void Accept()
        {
            _options.LayerName = _layerName.Text;
            _options.Band = SelectedBand();
            _options.SurfaceType = SelectedSurfaceType();
            _options.Stride = (int)_stride.Value;
            _options.Resampling = (ResamplingMethod)(_resampling.SelectedIndex < 0 ? 0 : _resampling.SelectedIndex);
            _options.MaxPatchSize = (int)_maxPatchSize.Value;
            _options.NoDataMode = SelectedNoDataMode();
            _options.NoDataElevation = _noDataElevation.Value;
            _options.ElevationScale = _elevationScale.Value;
            _options.ElevationUnitFactor = _elevationUnitFactor.Value;
            _options.GroupTiles = _groupTiles.Checked == true;
            _options.ApplyOffset = _applyOffset.Checked == true;
            _options.Offset = _options.ApplyOffset ? new Vector3d(_offsetX.Value, _offsetY.Value, 0.0) : Vector3d.Zero;

            Close(true);
        }

        private int SelectedBand()
        {
            var item = _band.SelectedIndex >= 0 ? _band.Items.ElementAtOrDefault(_band.SelectedIndex) : null;
            return item is not null && int.TryParse(item.Key, out var index) ? index : _summary.Info.Bands[0].Index;
        }
    }
}
