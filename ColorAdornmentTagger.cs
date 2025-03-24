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

   
        private static readonly Regex _colorRegex = new Regex(
            @"#[0-9A-Fa-f]{3}(?![0-9A-Fa-f])" +     // #abc
            @"|#[0-9A-Fa-f]{6}(?![0-9A-Fa-f])" +     // #aabbcc
            @"|#[0-9A-Fa-f]{8}(?![0-9A-Fa-f])" +     // #aabbccdd
            @"|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}(?:\s*,\s*(?:\d*\.?\d+))?\s*\)",
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

            var snapshot = spans[0].Snapshot;
            string text = snapshot.GetText();

            foreach (Match match in _colorRegex.Matches(text))
            {
                var colorSpan = new SnapshotSpan(snapshot, match.Index, match.Length);
                var trackingSpan = snapshot.CreateTrackingSpan(colorSpan, SpanTrackingMode.EdgeInclusive);

                string original = match.Value;
                ColorFormat format = DetermineColorFormat(original);
                Color? parsedColor = ParseColor(original);
                if (parsedColor == null || format == ColorFormat.Unknown)
                    continue;

                var color = parsedColor.Value;

                var insertPosition = new SnapshotSpan(snapshot, match.Index, 0);
                var square = new Border
                {
                    Width = 12,
                    Height = 12,
                    Background = new SolidColorBrush(color),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, -2, 4, 0),
                    Cursor = Cursors.Hand
                };

                square.MouseLeftButtonDown += (s, e) =>
                {
                    Point mousePosDip = Mouse.GetPosition(null);

                    var pickerWindow = new ColorPickerWindow(color, format, _buffer, trackingSpan)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = mousePosDip.X,
                        Top = mousePosDip.Y
                    };

                    pickerWindow.Show();
                };

                yield return new TagSpan<IntraTextAdornmentTag>(
                    insertPosition,
                    new ColorAdornmentTag(square, (t, elem) => { })
                );
            }
        }

        /// <summary>
        /// Определяем формат: #abc, #aabbcc, #aabbccdd, rgb(...), rgba(...).
        /// Иначе Unknown.
        /// </summary>
        private ColorFormat DetermineColorFormat(string original)
        {
            if (original.StartsWith("#"))
            {
                if (original.Length == 4) return ColorFormat.Hex3; // #abc
                if (original.Length == 7) return ColorFormat.Hex6; // #aabbcc
                if (original.Length == 9) return ColorFormat.Hex8; // #aabbccdd
                return ColorFormat.Unknown;
            }
            if (original.StartsWith("rgba", StringComparison.OrdinalIgnoreCase))
                return ColorFormat.Rgba;
            if (original.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
                return ColorFormat.Rgb;
            return ColorFormat.Unknown;
        }

        private Color? ParseColor(string value)
        {
            if (value.StartsWith("#"))
            {
                string hex = value.Substring(1);
                byte r, g, b, a = 255;
                if (hex.Length == 3)
                {
                    r = byte.Parse(new string(hex[0], 2), NumberStyles.HexNumber);
                    g = byte.Parse(new string(hex[1], 2), NumberStyles.HexNumber);
                    b = byte.Parse(new string(hex[2], 2), NumberStyles.HexNumber);
                }
                else if (hex.Length == 6)
                {
                    r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                }
                else if (hex.Length == 8)
                {
                    r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                }
                else
                {
                    return null;
                }
                return Color.FromArgb(a, r, g, b);
            }
            else if (value.Trim().StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var parts = Regex.Matches(value, @"\d*\.?\d+");
                if (parts.Count >= 3)
                {
                    byte r = byte.Parse(parts[0].Value, CultureInfo.InvariantCulture);
                    byte g = byte.Parse(parts[1].Value, CultureInfo.InvariantCulture);
                    byte b = byte.Parse(parts[2].Value, CultureInfo.InvariantCulture);
                    byte a = 255;
                    if (parts.Count == 4)
                    {
                        double alpha = double.Parse(parts[3].Value, CultureInfo.InvariantCulture);
                        a = (byte)((alpha <= 1) ? alpha * 255 : alpha);
                    }
                    return Color.FromArgb(a, r, g, b);
                }
            }
            return null;
        }
    }
}
