using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace EasyColorPicker
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class ColorAdornmentTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal ITextUndoHistoryRegistry UndoHistoryRegistry = null;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null || buffer == null)
                return null;
            if (buffer != textView.TextBuffer)
                return null;

            return buffer.Properties.GetOrCreateSingletonProperty(
                () => (ITagger<IntraTextAdornmentTag>)new ColorAdornmentTagger(buffer, UndoHistoryRegistry)
            ) as ITagger<T>;
        }
    }
}
