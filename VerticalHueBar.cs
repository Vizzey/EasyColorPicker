using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasyColorPicker
{
    public class VerticalHueBar : Canvas
    {
        private const int BAR_WIDTH = 20;

        private Image _image;
        private Rectangle _marker;
        private bool _isMouseDown;
        private bool _suppressEvents;

        private float _hue;
        public float Hue
        {
            get { return _hue; }
            private set { _hue = value; }
        }

        public event Action<float> HueChanged;

        public VerticalHueBar()
        {
            Width = BAR_WIDTH;

            Loaded += (s, e) => RedrawHueBar();
            SizeChanged += (s, e) => RedrawHueBar();

            _image = new Image { Width = BAR_WIDTH, Stretch = Stretch.Fill };
            Children.Add(_image);

            _marker = new Rectangle
            {
                Width = BAR_WIDTH * 1.25,
                Height = 3,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                RadiusX = 2,
                RadiusY = 2
            };
            double leftPos = (BAR_WIDTH - _marker.Width) / 2;
            SetLeft(_marker, leftPos);
            SetTop(_marker, -_marker.Height / 2);
            Children.Add(_marker);

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseLeave += (_, __) => _isMouseDown = false;
        }

        public void RedrawHueBar()
        {
            if (ActualHeight <= 0) return;

            int h = (int)ActualHeight;
            int w = (int)Width;

            
            if (_image.Source == null || ((WriteableBitmap)_image.Source).PixelHeight != h)
            {
                var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
                byte[] pixels = new byte[w * h * 4];
                int stride = w * 4;

                int offset = 0;
                for (int y = 0; y < h; y++)
                {
                    float hueVal = (float)(y / (double)(h - 1) * 360.0f);
                    Color c = HsvToColor(hueVal, 1f, 1f);

                    for (int x = 0; x < w; x++)
                    {
                        pixels[offset + 0] = c.B;
                        pixels[offset + 1] = c.G;
                        pixels[offset + 2] = c.R;
                        pixels[offset + 3] = 255;
                        offset += 4;
                    }
                }

                wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                _image.Source = wb;
                _image.Height = h;
            }

            SetMarkerToHue(Hue);
        }

        public void SetHue(float hue, bool suppressEvents = false)
        {
            _suppressEvents = suppressEvents;
            if (hue < 0) hue = 0;
            if (hue > 360) hue = 360;
            Hue = hue;

            SetMarkerToHue(Hue);
            _suppressEvents = false;
        }

        private void SetMarkerToHue(float hue)
        {
            if (ActualHeight > 0)
            {
                double y = hue / 360.0 * (ActualHeight - 1);
                SetTop(_marker, y - _marker.Height / 2);
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            CaptureMouse();
            UpdateMarker(e.GetPosition(this));
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                UpdateMarker(e.GetPosition(this));
            }
        }

        private void UpdateMarker(Point p)
        {
            double y = Math.Max(0, Math.Min(ActualHeight, p.Y));
            SetTop(_marker, y - _marker.Height / 2);

            float newHue = (float)(y / (ActualHeight - 1) * 360.0);
            if (newHue < 0) newHue = 0;
            if (newHue > 360) newHue = 360;
            Hue = newHue;

            if (!_suppressEvents)
            {
                HueChanged?.Invoke(Hue);
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
            float rr = r1 + m;
            float gg = g1 + m;
            float bb = b1 + m;

            return Color.FromArgb(255,
                (byte)(255f * rr),
                (byte)(255f * gg),
                (byte)(255f * bb));
        }
    }
}
