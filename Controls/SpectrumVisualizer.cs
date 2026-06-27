using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsFormsApp1.Controls
{
    public class SpectrumVisualizer : Control
    {
        private float[] _spectrumData;
        private float[] _peakData;
        private int _barCount = 32;
        private Color _gradientStart = Color.FromArgb(0, 150, 255);
        private Color _gradientEnd = Color.FromArgb(0, 255, 128);
        private Color _peakColor = Color.FromArgb(255, 255, 255);
        private Color _backgroundColor = Color.FromArgb(30, 30, 30);

        public SpectrumVisualizer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            _spectrumData = new float[_barCount];
            _peakData = new float[_barCount];
            Height = 120;
        }

        public void UpdateData(float[] fftData)
        {
            if (fftData == null || fftData.Length == 0) return;

            // Map FFT bins to fewer bars
            _spectrumData = MapFftToBars(fftData, _barCount);

            // Smooth the display with peak hold
            for (int i = 0; i < _barCount; i++)
            {
                if (_spectrumData[i] > _peakData[i])
                    _peakData[i] = _spectrumData[i];
                else
                    _peakData[i] -= 0.02f; // Peak decay

                if (_peakData[i] < 0) _peakData[i] = 0;
            }

            Invalidate();
        }

        private float[] MapFftToBars(float[] fftData, int barCount)
        {
            float[] bars = new float[barCount];
            int binsPerBar = fftData.Length / barCount;

            for (int i = 0; i < barCount; i++)
            {
                float sum = 0;
                for (int j = 0; j < binsPerBar; j++)
                {
                    int index = i * binsPerBar + j;
                    if (index < fftData.Length)
                        sum += fftData[index];
                }
                bars[i] = (sum / binsPerBar) * 10f;

                // Clamp
                if (bars[i] > 1) bars[i] = 1;
                if (bars[i] < 0) bars[i] = 0;
            }
            return bars;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_backgroundColor);

            if (_spectrumData == null || _spectrumData.Length == 0) return;

            int width = ClientSize.Width;
            int height = ClientSize.Height;
            int padding = 2;
            int barWidth = (width - padding * (_barCount + 1)) / _barCount;
            if (barWidth < 1) barWidth = 1;

            using (var linearGradient = new LinearGradientBrush(
                new Point(0, height), new Point(0, 0),
                _gradientStart, _gradientEnd))
            {
                for (int i = 0; i < Math.Min(_spectrumData.Length, _barCount); i++)
                {
                    float barHeight = _spectrumData[i] * height;
                    float peakHeight = _peakData[i] * height;

                    int x = padding + i * (barWidth + padding);
                    int barY = height - (int)barHeight;

                    // Draw bar with rounded top
                    var barRect = new Rectangle(x, barY, barWidth, (int)barHeight);
                    if (barHeight > 2)
                    {
                        g.FillRectangle(linearGradient, barRect);
                    }

                    // Draw peak dot
                    if (peakHeight > 2)
                    {
                        int peakY = height - (int)peakHeight;
                        g.FillEllipse(Brushes.White, x - 1, peakY - 2, barWidth + 2, 4);
                    }
                }
            }
        }

        public void Reset()
        {
            _spectrumData = new float[_barCount];
            _peakData = new float[_barCount];
            Invalidate();
        }
    }
}
