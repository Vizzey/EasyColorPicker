using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace EasyColorPicker
{
    internal static class ColorAdornmentLayerDefinition
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("ColorAdornment")]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
        public static AdornmentLayerDefinition ColorAdornmentLayer;
    }
}
