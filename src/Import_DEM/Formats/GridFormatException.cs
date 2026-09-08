using System;

namespace Import_DEM.Formats
{
    /// <summary>Thrown when the temporary grid does not hold the layout that the plugin asked for.</summary>
    public sealed class GridFormatException : Exception
    {
        public GridFormatException(string message)
            : base(message)
        {
        }
    }
}
