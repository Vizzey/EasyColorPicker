using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace EasyColorPicker
{
    internal class ColorPickerWindow : Window
    {
        private readonly ITextBuffer _buffer;
        private readonly ITrackingSpan _trackingSpan;
        private readonly ColorFormat _colorFormat;
        private readonly ITextUndoHistory _undoHistory;
        private ITextUndoTransaction _undoTx;

        private ColorGradientSquare _colorSquare;
        private VerticalHueBar _hueBar;

        private Slider _sliderR, _sliderG, _sliderB, _sliderA;
        private CheckBox _checkTransparency;
        private Border _preview;

        private bool _isClosing;
        private bool _suppressEvents;
        private bool _updating;

        private readonly string _funcToken;

        public ColorPickerWindow(Color initialColor,
                                 ColorFormat format,
                                 ITextBuffer buffer,
                                 ITrackingSpan trackingSpan,
                                 string functionToken = null,
                                 ITextUndoHistory undoHistory = null)
        {
            _buffer = buffer;
            _trackingSpan = trackingSpan;
            _colorFormat = format;
            _funcToken = functionToken;

            _undoHistory = undoHistory;

            if (_undoHistory != null)
                _undoTx = _undoHistory.CreateTransaction("Color change");

            Loaded += (sa, e) =>
            {
                Left += 15;
                Top += 10;
            };

            SizeToContent = SizeToContent.WidthAndHeight;

            Title = "EasyColorPicker";
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            Closing += OnWindowClosing;
            Deactivated += OnWindowDeactivated;
            Closed += OnWindowClosed;

            var root = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10)
            };

           
            var (h, s, v) = RgbToHsv(initialColor.R, initialColor.G, initialColor.B);

           
            _colorSquare = new ColorGradientSquare
            {
                Margin = new Thickness(0, 0, 10, 0)
            };

            _hueBar = new VerticalHueBar
            {
                Margin = new Thickness(0, 0, 10, 0),
                Height = 200 
            };

            root.Children.Add(_colorSquare);
            root.Children.Add(_hueBar);

            
            var rightPanel = new StackPanel();

            bool initiallyHasAlpha = (format == ColorFormat.Hex8 || format == ColorFormat.Rgba);
            
            
            _checkTransparency = new CheckBox
            {
                Content = "Transparent Channel",
                IsChecked = initiallyHasAlpha,
                Margin = new Thickness(0, 0, 0, 10)
            };
           
            rightPanel.Children.Add(_checkTransparency);

            _sliderR = CreateSliderWithLabel("R:", initialColor.R, rightPanel);
            _sliderG = CreateSliderWithLabel("G:", initialColor.G, rightPanel);
            _sliderB = CreateSliderWithLabel("B:", initialColor.B, rightPanel);
            _sliderA = CreateSliderWithLabel("A:", initialColor.A, rightPanel);

            _checkTransparency.IsEnabled = true;
            if (format == ColorFormat.WinRgb)
            {
                _checkTransparency.IsChecked = false;
            }

            _preview = new Border
            {
                Width = 50,
                Height = 50,
                Background = new SolidColorBrush(initialColor),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 0)
            };
            rightPanel.Children.Add(_preview);

            root.Children.Add(rightPanel);
            Content = root;

            
            _suppressEvents = true;

            _hueBar.SetHue(h, true);

            _hueBar.RedrawHueBar();

            Color pureHueColor = HsvToColor(h, 1f, 1f);
            _colorSquare.BaseColor = pureHueColor;

            _colorSquare.SetMarkerFromColor(initialColor, true);

            UpdateAlphaVisibility();

            _suppressEvents = false;

            _checkTransparency.Checked += (_, __) => UpdateColor();
            _checkTransparency.Unchecked += (_, __) => UpdateColor();

            _hueBar.HueChanged += hue =>
            {
                if (_suppressEvents) return;

                _suppressEvents = true; 

                Color c = HsvToColor(hue, 1f, 1f);
                _colorSquare.BaseColor = c;

                _colorSquare.RefreshSelectedColor();

                _suppressEvents = false;

                UpdateColor(false);
            };

            _colorSquare.ColorChanged += squareColor =>
            {
                if (_suppressEvents) return;

                UpdateColor(false);
            };

            
            _sliderR.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };
            _sliderG.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };
            _sliderB.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };
            _sliderA.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };

            UpdateColor();
        }

        private void UpdateFromSliders()
        {
            if (_updating) return;

            _updating = true;
            UpdateColor(true);
            _updating = false;
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _isClosing = true;

            try
            {
                if (_undoTx != null)
                {
                    _undoTx.Complete();
                    _undoTx.Dispose();
                    _undoTx = null;
                }
            }
            catch { }
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            if (!_isClosing)
            {
                _isClosing = true;
                SystemCommands.CloseWindow(this);
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            ReFocusMainWindowOrEditor();
        }

        private void ReFocusMainWindowOrEditor()
        {
            try
            {
                var mainWin = Application.Current?.MainWindow;
                if (mainWin != null)
                {
                    mainWin.Activate();
                }
            }
            catch { }
        }

        private Slider CreateSliderWithLabel(string label, byte initialValue, Panel container)
        {
            container.Children.Add(new TextBlock { Text = label });
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                Value = initialValue,
                Width = 200
            };
            container.Children.Add(slider);
            return slider;
        }

        private void UpdateColor(bool fromSliders = false)
        {
            if (_suppressEvents) return;

            _suppressEvents = true;

            bool useAlpha = (_checkTransparency.IsChecked == true);
            byte r, g, b, a;

            if (fromSliders)
            {
                r = (byte)_sliderR.Value;
                g = (byte)_sliderG.Value;
                b = (byte)_sliderB.Value;
                a = useAlpha ? (byte)_sliderA.Value : (byte)255;

                var (h, s, v) = RgbToHsv(r, g, b);

                _hueBar.SetHue(h, true);

                Color pureColor = HsvToColor(h, 1f, 1f);
                _colorSquare.BaseColor = pureColor;

                Color sliderColor = Color.FromArgb(a, r, g, b);
                _colorSquare.SetMarkerFromColor(sliderColor, true);
            }
            else
            {
                Color squareColor = _colorSquare.SelectedColor;
                r = squareColor.R;
                g = squareColor.G;
                b = squareColor.B;
                a = useAlpha ? (byte)_sliderA.Value : (byte)255;

                _sliderR.Value = r;
                _sliderG.Value = g;
                _sliderB.Value = b;
            }

            var currentColor = Color.FromArgb(a, r, g, b);
            _preview.Background = new SolidColorBrush(currentColor);

            string replacement = BuildColorString(r, g, b, a, useAlpha);
            var snap = _buffer.CurrentSnapshot;
            var replaceSpan = _trackingSpan.GetSpan(snap);
            using (var edit = _buffer.CreateEdit())
            {
                edit.Replace(replaceSpan.Start, replaceSpan.Length, replacement);
                edit.Apply();
            }

            UpdateAlphaVisibility();

            _suppressEvents = false;
        }


        private bool ColorsEqual(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }

        private static bool IsAllUpper(string s) => !string.IsNullOrEmpty(s) && s == s.ToUpperInvariant();
        private static bool IsAllLower(string s) => !string.IsNullOrEmpty(s) && s == s.ToLowerInvariant();

        private static string MakeRgbToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "rgb";
            string base3 = token.StartsWith("rgba", StringComparison.OrdinalIgnoreCase)
                ? token.Substring(0, 3)
                : token.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)
                    ? token.Substring(0, 3)
                    : "rgb";

            if (IsAllUpper(token)) return base3.ToUpperInvariant();   // RGB
            if (IsAllLower(token)) return base3.ToLowerInvariant();   // rgb
            return base3;                                             // смешанный — как есть первые 3
        }

        private static string MakeRgbaToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "rgba";
            if (token.StartsWith("rgba", StringComparison.OrdinalIgnoreCase))
                return IsAllUpper(token) ? "RGBA" : IsAllLower(token) ? "rgba" : token; // уже rgba — вернём как было

            string rgb3 = MakeRgbToken(token);
            if (IsAllUpper(token)) return rgb3.ToUpperInvariant() + "A"; // RGB -> RGBA
            if (IsAllLower(token)) return rgb3.ToLowerInvariant() + "a"; // rgb -> rgba
            return rgb3 + "a";                                           // смешанный
        }

        private void UpdateAlphaVisibility()
        {
            if (_sliderA == null) return;
            bool useAlpha = (_checkTransparency.IsChecked == true);
            _sliderA.Visibility = useAlpha ? Visibility.Visible : Visibility.Collapsed;
        }

        private string BuildColorString(byte r, byte g, byte b, byte a, bool useAlpha)
        {
            double alpha01 = Math.Round(a / 255.0, 2, MidpointRounding.AwayFromZero);

            string rgbTok = MakeRgbToken(_funcToken);
            string rgbaTok = MakeRgbaToken(_funcToken);

            switch (_colorFormat)
            {
                case ColorFormat.WinRgb:
                    // Не ломаем WinAPI-макрос: всегда RGB без альфы,
                    // чекбокс визуально кликабельный, но на вывод не влияет.
                    return useAlpha
                       ? $"{rgbaTok}({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})"
                       : $"{rgbTok}({r}, {g}, {b})";

                case ColorFormat.Hex3:
                case ColorFormat.Hex6:
                    return useAlpha
                        ? $"{rgbaTok}({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})"
                        : $"#{r:X2}{g:X2}{b:X2}";

                case ColorFormat.Hex8:
                    return $"{rgbaTok}({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";

                case ColorFormat.Rgb:
                    return useAlpha
                        ? $"{rgbaTok}({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})"
                        : $"{rgbTok}({r}, {g}, {b})";

                case ColorFormat.Rgba:
                    return useAlpha
                        ? $"{rgbaTok}({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})"
                        : $"{rgbTok}({r}, {g}, {b})";

                default:
                    return $"{rgbaTok}({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";
            }
        }


        private Color HsvToColor(float hue, float s, float v)
        {
            float c = s * v;
            float hh = hue / 60f;
            float x = c * (1f - Math.Abs(hh % 2f - 1f));

            float r1, g1, b1;
            if (hh < 1) { r1 = c; g1 = x; b1 = 0; }
            else if (hh < 2) { r1 = x; g1 = c; b1 = 0; }
            else if (hh < 3) { r1 = 0; g1 = c; b1 = x; }
            else if (hh < 4) { r1 = 0; g1 = x; b1 = c; }
            else if (hh < 5) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            float m = v - c;
            float r = r1 + m;
            float g = g1 + m;
            float b = b1 + m;

            return Color.FromArgb(255,
                (byte)(255 * r),
                (byte)(255 * g),
                (byte)(255 * b));
        }

        private (float h, float s, float v) RgbToHsv(byte r, byte g, byte b)
        {
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;

            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            float delta = max - min;

            float h;
            if (Math.Abs(delta) < 0.00001f)
            {
                h = 0;
            }
            else if (Math.Abs(max - rf) < 0.00001f)
            {
                h = 60f * (((gf - bf) / delta) % 6f);
            }
            else if (Math.Abs(max - gf) < 0.00001f)
            {
                h = 60f * ((bf - rf) / delta + 2f);
            }
            else
            {
                h = 60f * ((rf - gf) / delta + 4f);
            }
            if (h < 0) h += 360f;

            float s = (max <= 0) ? 0 : (delta / max);
            float v = max;

            return (h, s, v);
        }
    }
}
