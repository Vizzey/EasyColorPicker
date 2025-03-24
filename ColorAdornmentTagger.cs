// File: ColorAdornmentTagger.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace EasyColorPicker
{
    internal sealed class ColorAdornmentTagger : ITagger<IntraTextAdornmentTag>
    {
        private readonly ITextBuffer _buffer;

        // Поддерживаем: #RGB, #RGBA, #RRGGBB, #RRGGBBAA, rgb(...), rgba(...)
        private static readonly Regex _colorRegex = new Regex(
            @"#(?:[0-9A-Fa-f]{3,4}|[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?)|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}(?:\s*,\s*(?:\d*\.?\d+))?\s*\)",
            RegexOptions.Compiled);

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public ColorAdornmentTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            var snapshot = e.After;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            ITextSnapshot snapshot = spans[0].Snapshot;
            string text = snapshot.GetText();

            foreach (Match match in _colorRegex.Matches(text))
            {
                var insertPosition = new SnapshotSpan(snapshot, new Span(match.Index, 0));
                Color? parsed = ParseColor(match.Value);
                if (parsed == null)
                    continue;

                var square = new Border
                {
                    Width = 12,
                    Height = 12,
                    Background = new SolidColorBrush(parsed.Value),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, -10, 4, 0),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = Transform.Identity,
                    Cursor = Cursors.Hand
                };

                square.MouseEnter += (s, e) =>
                {
                    square.BorderBrush = Brushes.Yellow;
                    square.RenderTransform = new ScaleTransform(1.2, 1.2);
                };
                square.MouseLeave += (s, e) =>
                {
                    square.BorderBrush = Brushes.White;
                    square.RenderTransform = Transform.Identity;
                };

                AdornmentRemovedCallback removedCallback = (tag, element) => { };
                yield return new TagSpan<IntraTextAdornmentTag>(insertPosition, new ColorAdornmentTag(square, removedCallback));
            }
        }

        private Color? ParseColor(string value)
        {
            if (value.StartsWith("#"))
            {
                string hex = value.Substring(1);
                byte a = 255, r, g, b;

                if (hex.Length == 3)
                {
                    r = Convert.ToByte(new string(hex[0], 2), 16);
                    g = Convert.ToByte(new string(hex[1], 2), 16);
                    b = Convert.ToByte(new string(hex[2], 2), 16);
                }
                else if (hex.Length == 4)
                {
                    r = Convert.ToByte(new string(hex[0], 2), 16);
                    g = Convert.ToByte(new string(hex[1], 2), 16);
                    b = Convert.ToByte(new string(hex[2], 2), 16);
                    a = Convert.ToByte(new string(hex[3], 2), 16);
                }
                else if (hex.Length == 6 || hex.Length == 8)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                    if (hex.Length == 8)
                        a = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else return null;

                return Color.FromArgb(a, r, g, b);
            }

            // rgb(...) или rgba(...)
            var parts = Regex.Matches(value, @"\d*\.?\d+");
            if (parts.Count >= 3)
            {
                byte r = Byte.Parse(parts[0].Value, CultureInfo.InvariantCulture);
                byte g = Byte.Parse(parts[1].Value, CultureInfo.InvariantCulture);
                byte b = Byte.Parse(parts[2].Value, CultureInfo.InvariantCulture);
                byte a = 255;

                if (parts.Count == 4)
                {
                    double alpha = Double.Parse(parts[3].Value, CultureInfo.InvariantCulture);
                    a = (byte)(alpha <= 1 ? alpha * 255 : alpha);
                }

                return Color.FromArgb(a, r, g, b);
            }

            return null;
        }
    }
}
