using System.Windows;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

namespace EasyColorPicker
{
    internal class ColorAdornmentTag : IntraTextAdornmentTag
    {
        public ColorAdornmentTag(UIElement adornment, AdornmentRemovedCallback removedCallback)
            : base(adornment, removedCallback)
        {
        }
    }
}
