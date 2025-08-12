using System.Windows;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

namespace EasyColorPicker
{
    internal enum ColorFormat
    {
        Hex3,
        Hex6,
        Hex8,
        Rgb,
        Rgba,
        WinRgb,
        Unknown
    }

    internal class ColorAdornmentTag : IntraTextAdornmentTag
    {
        public ColorAdornmentTag(UIElement adornment, AdornmentRemovedCallback removedCallback)
            : base(adornment, removedCallback)
        {
        }
    }
}
