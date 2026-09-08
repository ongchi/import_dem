using Eto.Forms;
using Rhino.UI;

namespace Import_DEM.Import
{
    /// <summary>
    /// Remembers the folder that holds the GDAL tools. The plugin searches the usual folders by
    /// itself, so this setting matters only for an install in an unusual place.
    /// </summary>
    public static class GdalLocation
    {
        /// <summary>The key of the folder in the plugin settings.</summary>
        public const string SettingKey = "GdalFolder";

        /// <summary>The folder that the user set, or an empty text for the automatic search.</summary>
        public static string Folder
        {
            get => Import_DEMPlugIn.Instance?.Settings.GetString(SettingKey, string.Empty) ?? string.Empty;
            set => Import_DEMPlugIn.Instance?.Settings.SetString(SettingKey, value);
        }

        /// <summary>
        /// Asks the user for the folder and remembers it. Returns false when the user cancels.
        /// </summary>
        public static bool TryAskForFolder()
        {
            using var dialog = new SelectFolderDialog
            {
                Title = "Select the folder that holds gdalinfo and gdal_translate",
            };

            if (dialog.ShowDialog(RhinoEtoApp.MainWindowForDocument(Rhino.RhinoDoc.ActiveDoc)) != DialogResult.Ok)
                return false;

            Folder = dialog.Directory ?? string.Empty;
            return !string.IsNullOrWhiteSpace(Folder);
        }
    }
}
