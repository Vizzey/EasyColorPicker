using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasyColorPicker
{
    public class ColorGradientSquare : Canvas
    {
        private const int SIZE = 200;
        private Image _image;
        private Ellipse _marker;
        private bool _isMouseDown;
        private bool _suppressEvents;
        private WriteableBitmap _cachedBitmap;

        public event Action<Color> ColorChanged;

        public Color SelectedColor { get; private set; } = Colors.White;

        private Color _baseColor = Colors.Red;
        public Color BaseColor
        {
            get => _baseColor;
            set
            {
                if (ColorsEqual(_baseColor, value)) return;

                _baseColor = value;
                RedrawGradient();

                RefreshSelectedColor();
            }
        }

        public ColorGradientSquare()
        {
            Width = SIZE;
            Height = SIZE;

            _image = new Image
            {
                Width = SIZE,
                Height = SIZE,
                Stretch = Stretch.None
            };
            Children.Add(_image);

            _marker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            SetLeft(_marker, SIZE / 2 - 5);
            SetTop(_marker, SIZE / 2 - 5); 
            Children.Add(_marker);

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseLeave += (_, __) => _isMouseDown = false;

            RedrawGradient();

            RefreshSelectedColor();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            CaptureMouse();
            UpdateMarker(e.GetPosition(this));
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                UpdateMarker(e.GetPosition(this));
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            ReleaseMouseCapture();
        }

        private void UpdateMarker(Point p)
        {
            double x = Math.Max(0, Math.Min(SIZE, p.X));
            double y = Math.Max(0, Math.Min(SIZE, p.Y));

            SetLeft(_marker, x - _marker.Width / 2.0);
            SetTop(_marker, y - _marker.Height / 2.0);

            SelectedColor = GetColorAtPosition(x, y);

            if (!_suppressEvents)
            {
                ColorChanged?.Invoke(SelectedColor);
            }
        }

        public void RedrawGradient()
        {
            _cachedBitmap = new WriteableBitmap(SIZE, SIZE, 96, 96, PixelFormats.Pbgra32, null);
            byte[] pixels = new byte[SIZE * SIZE * 4];
            int stride = SIZE * 4;
            int offset = 0;

            for (int j = 0; j < SIZE; j++)
            {
                double v = j / (double)(SIZE - 1);
                for (int i = 0; i < SIZE; i++)
                {
                    double u = i / (double)(SIZE - 1);

              
                    double r1 = (1 - u) * 255 + (u) * BaseColor.R;
                    double g1 = (1 - u) * 255 + (u) * BaseColor.G;
                    double b1 = (1 - u) * 255 + (u) * BaseColor.B;

                    double brightness = 1.0 - v;
                    byte rr = (byte)(brightness * r1);
                    byte gg = (byte)(brightness * g1);
                    byte bb = (byte)(brightness * b1);

                    pixels[offset + 0] = bb;
                    pixels[offset + 1] = gg;
                    pixels[offset + 2] = rr;
                    pixels[offset + 3] = 255;
                    offset += 4;
                }
            }

            _cachedBitmap.WritePixels(new Int32Rect(0, 0, SIZE, SIZE), pixels, stride, 0);
            _image.Source = _cachedBitmap;
        }

        private Color GetColorAtPosition(double x, double y)
        {
            int ix = (int)Math.Round(x);
            int iy = (int)Math.Round(y);
            if (ix < 0) ix = 0;
            if (ix >= SIZE) ix = SIZE - 1;
            if (iy < 0) iy = 0;
            if (iy >= SIZE) iy = SIZE - 1;

            return GetColorAt(ix, iy);
        }

        private Color GetColorAt(int x, int y)
        {
            double u = x / (double)(SIZE - 1);
            double v = y / (double)(SIZE - 1);

            double r1 = (1 - u) * 255 + u * BaseColor.R;
            double g1 = (1 - u) * 255 + u * BaseColor.G;
            double b1 = (1 - u) * 255 + u * BaseColor.B;

            double brightness = 1.0 - v;
            byte rr = (byte)(brightness * r1);
            byte gg = (byte)(brightness * g1);
            byte bb = (byte)(brightness * b1);

            return Color.FromArgb(255, rr, gg, bb);
        }

        public void RefreshSelectedColor()
        {
            double mx = GetLeft(_marker) + _marker.Width / 2.0;
            double my = GetTop(_marker) + _marker.Height / 2.0;

            SelectedColor = GetColorAtPosition(mx, my);

            if (!_suppressEvents)
            {
                ColorChanged?.Invoke(SelectedColor);
            }
        }

        /// <summary>
        /// Улучшенный метод для установки маркера по цвету. Работает по HSV модели
        /// для более точного позиционирования.
        /// </summary>
        public void SetMarkerFromColor(Color c, bool suppressEvents = false)
        {
            _suppressEvents = suppressEvents;

            var (h, s, v) = RgbToHsv(c.R, c.G, c.B);

            double x = s * (SIZE - 1);
            double y = (1 - v) * (SIZE - 1);

            SetLeft(_marker, x - _marker.Width / 2);
            SetTop(_marker, y - _marker.Height / 2);
            SelectedColor = c;

            _suppressEvents = false;
        }

        private bool ColorsEqual(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
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
