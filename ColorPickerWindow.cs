using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;

namespace EasyColorPicker
{
    internal class ColorPickerWindow : Window
    {
        private readonly ITextBuffer _buffer;
        private readonly ITrackingSpan _trackingSpan;
        private readonly ColorFormat _colorFormat;

        private ColorGradientSquare _colorSquare;
        private VerticalHueBar _hueBar;

        private Slider _sliderR, _sliderG, _sliderB, _sliderA;
        private CheckBox _checkTransparency;
        private Border _preview;

        private bool _isClosing;
        private bool _suppressEvents;
        private bool _updating;

        public ColorPickerWindow(Color initialColor,
                                 ColorFormat format,
                                 ITextBuffer buffer,
                                 ITrackingSpan trackingSpan)
        {
            _buffer = buffer;
            _trackingSpan = trackingSpan;
            _colorFormat = format;

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

            //
            // 1) ВАЖНО: Сначала вычисляем HSV из исходного цвета
            //
            var (h, s, v) = RgbToHsv(initialColor.R, initialColor.G, initialColor.B);

            //
            // 2) Создаем компоненты
            //
            _colorSquare = new ColorGradientSquare
            {
                Margin = new Thickness(0, 0, 10, 0)
            };

            _hueBar = new VerticalHueBar
            {
                Margin = new Thickness(0, 0, 10, 0),
                Height = 200 // фиксированная высота для наглядности
            };

            // Добавляем панели
            root.Children.Add(_colorSquare);
            root.Children.Add(_hueBar);

            //
            // 3) Справа - панель со слайдерами и предпросмотром
            //
            var rightPanel = new StackPanel();

            bool initiallyHasAlpha = (format == ColorFormat.Hex8 || format == ColorFormat.Rgba);
            _checkTransparency = new CheckBox
            {
                Content = "Transparent Channel",
                IsChecked = initiallyHasAlpha,
                Margin = new Thickness(0, 0, 0, 10)
            };
            rightPanel.Children.Add(_checkTransparency);

            // Создаем слайдеры с исходными значениями R,G,B,A
            _sliderR = CreateSliderWithLabel("R:", initialColor.R, rightPanel);
            _sliderG = CreateSliderWithLabel("G:", initialColor.G, rightPanel);
            _sliderB = CreateSliderWithLabel("B:", initialColor.B, rightPanel);
            _sliderA = CreateSliderWithLabel("A:", initialColor.A, rightPanel);

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

            //
            // 4) Инициализируем цветовые компоненты В ПРАВИЛЬНОМ ПОРЯДКЕ
            // Но с подавлением событий, чтобы избежать циклических вызовов
            //
            _suppressEvents = true;

            // Сначала устанавливаем Hue в панели оттенков
            _hueBar.SetHue(h, true);

            // Перерисовываем панель оттенков
            _hueBar.RedrawHueBar();

            // ВАЖНО: Сначала устанавливаем чистый цвет для базового оттенка h
            Color pureHueColor = HsvToColor(h, 1f, 1f);
            _colorSquare.BaseColor = pureHueColor;

            // Только после установки базового цвета устанавливаем маркер
            // на квадрате в соответствии с s,v
            _colorSquare.SetMarkerFromColor(initialColor, true);

            // Настраиваем видимость альфа-канала
            UpdateAlphaVisibility();

            _suppressEvents = false;

            // Настраиваем события после инициализации компонентов
            _checkTransparency.Checked += (_, __) => UpdateColor();
            _checkTransparency.Unchecked += (_, __) => UpdateColor();

            // Определяем, когда ввод пользователя через HueBar влияет на квадрат
            _hueBar.HueChanged += hue =>
            {
                if (_suppressEvents) return;

                _suppressEvents = true; // Избегаем циклических вызовов

                // Берём "чистый" цвет (s=1,v=1) для данного hue
                Color c = HsvToColor(hue, 1f, 1f);
                _colorSquare.BaseColor = c;

                // Обновляем только цвет, не трогая слайдеры (это сделает RefreshSelectedColor)
                _colorSquare.RefreshSelectedColor();

                _suppressEvents = false;

                // Обновляем слайдеры и интерфейс
                UpdateColor(false);
            };

            // Определяем, когда изменения в квадрате влияют на слайдеры
            _colorSquare.ColorChanged += squareColor =>
            {
                if (_suppressEvents) return;

                UpdateColor(false);
            };

            // События слайдеров - важно использовать throttling для предотвращения частых обновлений
            _sliderR.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };
            _sliderG.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };
            _sliderB.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };
            _sliderA.ValueChanged += (_, __) => { if (!_suppressEvents) UpdateFromSliders(); };

            // Финальное обновление цвета
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
                // Получаем значения из слайдеров
                r = (byte)_sliderR.Value;
                g = (byte)_sliderG.Value;
                b = (byte)_sliderB.Value;
                a = useAlpha ? (byte)_sliderA.Value : (byte)255;

                // Обновляем положение маркера в соответствии с новым цветом
                var (h, s, v) = RgbToHsv(r, g, b);

                // Устанавливаем положение hue-бара без вызова события
                _hueBar.SetHue(h, true);

                // Обновляем BaseColor квадрата  
                Color pureColor = HsvToColor(h, 1f, 1f);
                _colorSquare.BaseColor = pureColor;

                // Обновляем маркер на квадрате
                Color sliderColor = Color.FromArgb(a, r, g, b);
                _colorSquare.SetMarkerFromColor(sliderColor, true);
            }
            else
            {
                // Получаем цвет из квадрата, если метод вызван из-за изменений в квадрате или hue-баре
                Color squareColor = _colorSquare.SelectedColor;
                r = squareColor.R;
                g = squareColor.G;
                b = squareColor.B;
                a = useAlpha ? (byte)_sliderA.Value : (byte)255;

                // Обновляем слайдеры из цвета квадрата
                _sliderR.Value = r;
                _sliderG.Value = g;
                _sliderB.Value = b;
            }

            // Всегда обновляем превью и текстовый буфер
            var currentColor = Color.FromArgb(a, r, g, b);
            _preview.Background = new SolidColorBrush(currentColor);

            // Подстановка в текстовый буфер, если нужно:
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

        private void UpdateAlphaVisibility()
        {
            bool useAlpha = (_checkTransparency.IsChecked == true);
            _sliderA.Visibility = useAlpha ? Visibility.Visible : Visibility.Collapsed;
        }

        private string BuildColorString(byte r, byte g, byte b, byte a, bool useAlpha)
        {
            double alpha01 = Math.Round(a / 255.0, 2);

            switch (_colorFormat)
            {
                case ColorFormat.Hex3:
                    if (useAlpha)
                        return $"rgba({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";
                    else
                        return $"#{r:X2}{g:X2}{b:X2}";

                case ColorFormat.Hex6:
                    if (useAlpha)
                        return $"rgba({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";
                    else
                        return $"#{r:X2}{g:X2}{b:X2}";

                case ColorFormat.Hex8:
                    return $"rgba({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";

                case ColorFormat.Rgb:
                    if (useAlpha)
                        return $"rgba({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";
                    else
                        return $"rgb({r}, {g}, {b})";

                case ColorFormat.Rgba:
                    if (!useAlpha)
                        return $"rgb({r}, {g}, {b})";
                    else
                        return $"rgba({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";

                default:
                    return $"rgba({r}, {g}, {b}, {alpha01.ToString(CultureInfo.InvariantCulture)})";
            }
        }

        // -- Функция для перевода Hue, Saturation, Value -> Color
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

        // -- Функция для перевода Color(R,G,B) -> (h,s,v)
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
                h = 0; // hue не определён, пусть будет 0
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
