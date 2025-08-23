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
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace EasyColorPicker
{
    internal sealed class ColorAdornmentTagger : ITagger<IntraTextAdornmentTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextUndoHistoryRegistry _undoHistoryRegistry;

        private static readonly Regex _colorRegex = new Regex(
             @"#[0-9A-Fa-f]{3}(?![0-9A-Fa-f])" +                 // #abc
             @"|#[0-9A-Fa-f]{6}(?![0-9A-Fa-f])" +               // #aabbcc
             @"|#[0-9A-Fa-f]{8}(?![0-9A-Fa-f])" +               // #aabbccdd
             @"|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}(?:\s*,\s*\d*\.?\d+)?\s*\)" + // rgb/rgba(...)
             @"|RGB\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\)" +                        // WinAPI RGB(...)
             @"|new\s+(?:UnityEngine\.)?Color\s*\(\s*(?:[+-]?\d*\.?\d+f?\s*(?:,\s*[+-]?\d*\.?\d+f?\s*){2,3})?\)",
             RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public ColorAdornmentTagger(ITextBuffer buffer, ITextUndoHistoryRegistry undoHistoryRegistry)
        {
            _buffer = buffer;
            _undoHistoryRegistry = undoHistoryRegistry ?? throw new ArgumentNullException(nameof(undoHistoryRegistry));
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
                    string funcToken = ExtractFunctionToken(original);

                    ITextUndoHistory history = _undoHistoryRegistry.GetHistory(_buffer)
                        ?? _undoHistoryRegistry.RegisterHistory(_buffer);

                    var pickerWindow = new ColorPickerWindow(color, format, _buffer, trackingSpan, funcToken, history)
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

        private static string ExtractFunctionToken(string original)
        {
            int p = original.IndexOf('(');
            if (p > 0)
            {
                var t = original.Substring(0, p);
                // сохраняем как есть: rgb / rgba / RGB / RGBA / смешанный регистр
                if (t.Equals("rgb", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("rgba", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("RGB", StringComparison.Ordinal) ||
                    t.Equals("RGBA", StringComparison.Ordinal))
                    return t;

                if (Regex.IsMatch(original, @"^\s*new\s+(?:UnityEngine\.)?Color\s*\(", RegexOptions.IgnoreCase))
                {
                    var nums = Regex.Matches(original, @"[+-]?\d*\.?\d+f?", RegexOptions.IgnoreCase);
                    if (nums.Count == 0) return "UNITY0";
                    return nums.Count >= 4 ? "UNITY4" : "UNITY3";
                }
            }
            return null;
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

            if (Regex.IsMatch(original, @"^\s*new\s+(?:UnityEngine\.)?Color\s*\(", RegexOptions.IgnoreCase))
                return ColorFormat.UnityColor;
            if (original.StartsWith("RGB(", StringComparison.Ordinal))
                return ColorFormat.WinRgb;
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
            else if (Regex.IsMatch(value, @"^\s*new\s+(?:UnityEngine\.)?Color\s*\(", RegexOptions.IgnoreCase))
            {
                var parts = Regex.Matches(value, @"[+-]?\d*\.?\d+f?", RegexOptions.IgnoreCase);
                if (parts.Count == 0)
                {
                    return Color.FromArgb(0, 0, 0, 0);
                }
                if (parts.Count >= 3)
                {
                    Func<string, double> to01 = s =>
                    {
                        var v = s.EndsWith("f", StringComparison.OrdinalIgnoreCase) ? s.Substring(0, s.Length - 1) : s;
                        double d = double.Parse(v, CultureInfo.InvariantCulture);
                        if (d < 0) d = 0; if (d > 1) d = 1;
                        return d;
                    };
                    double rf = to01(parts[0].Value);
                    double gf = to01(parts[1].Value);
                    double bf = to01(parts[2].Value);
                    double af = (parts.Count >= 4) ? to01(parts[3].Value) : 1.0;
                    return Color.FromArgb(
                        (byte)Math.Round(af * 255),
                        (byte)Math.Round(rf * 255),
                        (byte)Math.Round(gf * 255),
                        (byte)Math.Round(bf * 255));
                }
            }

            return null;
        }
    }
}
