using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace EasyColorPicker
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class ColorAdornmentTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new ColorAdornmentTagger(buffer) as ITagger<T>;
        }
    }
}
