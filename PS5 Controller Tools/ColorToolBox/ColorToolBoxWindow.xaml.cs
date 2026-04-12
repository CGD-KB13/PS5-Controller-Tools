using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PS5_Controller_Tools.ColorToolBox
{
    public partial class ColorToolBoxWindow : Window
    {
        private const double PaletteSize = 250.0;
        private const double PaletteMarkerSize = 14.0;
        private const double SliderThumbSize = 10.0;
        private const double BubbleHorizontalGap = 6.0;
        private const string HexPrefix = "#";

        private bool _isPaletteDragging;
        private bool _isUpdatingControls;
        private bool _isIntensityBubbleVisible;
        private bool _isSynchronizingSelection;

        private Color _baseColor = Colors.Cyan;
        private Point _paletteSelection = new Point(PaletteSize / 2.0, PaletteSize / 2.0);

        public event EventHandler<Color>? SelectedColorChanged;

        public Color SelectedColor
        {
            get => GetDisplayColor();
            set
            {
                Color selectedColor = Color.FromRgb(value.R, value.G, value.B);

                if (IsLoaded)
                {
                    ApplyDisplayColor(selectedColor);
                    return;
                }

                _baseColor = selectedColor;
            }
        }

        public ColorToolBoxWindow()
        {
            InitializeComponent();
            Loaded += ColorToolBoxWindow_Loaded;
        }

        private void ColorToolBoxWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DrawPalette();
            ApplyDisplayColor(_baseColor);
        }

        private void DrawPalette()
        {
            if (ColorPalette == null)
                return;

            ColorPalette.Children.Clear();

            for (int x = 0; x < (int)PaletteSize; x++)
            {
                double xRatio = x / (PaletteSize - 1.0);

                for (int y = 0; y < (int)PaletteSize; y++)
                {
                    double yRatio = y / (PaletteSize - 1.0);

                    var rect = new Rectangle
                    {
                        Width = 1,
                        Height = 1,
                        Fill = new SolidColorBrush(ColorToolBoxColorHelper.GetPaletteColor(xRatio, yRatio))
                    };

                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    ColorPalette.Children.Add(rect);
                }
            }

            ColorPalette.Children.Add(PaletteSelectionMarker);
            UpdatePaletteMarkerPosition();
        }

        private void Palette_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPaletteDragging = true;
            ColorPalette.CaptureMouse();
            UpdatePaletteSelection(e.GetPosition(ColorPalette));
        }

        private void Palette_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPaletteDragging)
                return;

            UpdatePaletteSelection(e.GetPosition(ColorPalette));
        }

        private void Palette_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPaletteDragging)
                return;

            _isPaletteDragging = false;
            ColorPalette.ReleaseMouseCapture();
            UpdatePaletteSelection(e.GetPosition(ColorPalette));
        }

        private void IntensityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _isSynchronizingSelection)
                return;

            UpdateVisuals();
        }

        private void IntensityHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }

        private void IntensitySlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isIntensityBubbleVisible = true;
            UpdateIntensityBubblePosition();
        }

        private void IntensitySlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HideIntensityBubble();
        }

        private void IntensitySlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            HideIntensityBubble();
        }

        private void HexChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingControls || HexBox == null)
                return;

            string normalizedHex = NormalizeHexText(HexBox.Text);
            ApplySanitizedText(HexBox, normalizedHex, Math.Max(1, normalizedHex.Length));

            if (normalizedHex.Length != 7)
                return;

            try
            {
                if (ColorConverter.ConvertFromString(normalizedHex) is Color color)
                    ApplyDisplayColor(color);
            }
            catch
            {
            }
        }

        private void RgbChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingControls || RBox == null || VBox == null || BBox == null)
                return;

            SanitizeRgbTextBox(RBox);
            SanitizeRgbTextBox(VBox);
            SanitizeRgbTextBox(BBox);

            if (byte.TryParse(RBox.Text, out byte r) &&
                byte.TryParse(VBox.Text, out byte g) &&
                byte.TryParse(BBox.Text, out byte b))
            {
                ApplyDisplayColor(Color.FromRgb(r, g, b));
            }
        }

        private void HexBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = string.IsNullOrWhiteSpace(e.Text) || e.Text.Any(c => !IsHexCharacter(c));
        }

        private void HexBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (e.Key == Key.Back && textBox.SelectionLength == 0 && textBox.CaretIndex <= 1)
            {
                textBox.CaretIndex = 1;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && textBox.SelectionStart == 0)
            {
                textBox.CaretIndex = Math.Max(1, textBox.CaretIndex);
                e.Handled = true;
                return;
            }

            if ((e.Key == Key.Left || e.Key == Key.Home) && textBox.SelectionLength == 0 && textBox.CaretIndex <= 1)
            {
                textBox.CaretIndex = 1;
                e.Handled = true;
            }
        }

        private void HexBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (textBox.SelectionLength == 0 && textBox.CaretIndex < 1)
                textBox.CaretIndex = 1;
        }

        private void HexBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            string mergedText = MergeText(textBox.Text ?? string.Empty, textBox.SelectionStart, textBox.SelectionLength, pastedText);
            string normalizedHex = NormalizeHexText(mergedText);

            ApplySanitizedText(textBox, normalizedHex, Math.Max(1, normalizedHex.Length));
            e.CancelCommand();
        }

        private void RgbBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = string.IsNullOrWhiteSpace(e.Text) || e.Text.Any(c => !char.IsDigit(c));
        }

        private void RgbBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            string mergedText = MergeText(textBox.Text ?? string.Empty, textBox.SelectionStart, textBox.SelectionLength, pastedText);
            string digitsOnly = new string(mergedText.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length > 3)
                digitsOnly = digitsOnly[..3];

            if (digitsOnly.Length > 0 && int.TryParse(digitsOnly, out int value))
                digitsOnly = Math.Clamp(value, 0, 255).ToString(CultureInfo.InvariantCulture);

            ApplySanitizedText(textBox, digitsOnly, digitsOnly.Length);
            e.CancelCommand();
        }

        private void ApplyBaseColor(Color color, bool updatePaletteMarker)
        {
            _baseColor = Color.FromRgb(color.R, color.G, color.B);

            if (updatePaletteMarker)
                UpdatePaletteSelectionFromColor(GetDisplayColor());

            UpdateVisuals();
        }

        private void ApplyDisplayColor(Color displayColor)
        {
            double sliderValue = EstimateSliderValueFromColor(displayColor);
            Color baseColor = CreateBaseColorFromDisplayColor(displayColor, sliderValue);

            _isSynchronizingSelection = true;
            try
            {
                _baseColor = baseColor;

                if (IntensitySlider != null)
                    IntensitySlider.Value = sliderValue;

                UpdatePaletteSelectionFromColor(displayColor);
            }
            finally
            {
                _isSynchronizingSelection = false;
            }

            UpdateVisuals();
        }

        private void UpdatePaletteSelection(Point position)
        {
            double clampedX = Math.Clamp(position.X, 0.0, PaletteSize - 1.0);
            double clampedY = Math.Clamp(position.Y, 0.0, PaletteSize - 1.0);

            _paletteSelection = new Point(clampedX, clampedY);
            UpdatePaletteMarkerPosition();

            double xRatio = clampedX / (PaletteSize - 1.0);
            double yRatio = clampedY / (PaletteSize - 1.0);

            Color paletteColor = ColorToolBoxColorHelper.GetPaletteColor(xRatio, yRatio);
            ApplyDisplayColor(paletteColor);
        }

        private void UpdatePaletteSelectionFromColor(Color color)
        {
            ColorToolBoxColorHelper.ToHSV(color, out double hue, out double saturation, out double value);

            if (saturation < 0.01)
                ColorToolBoxColorHelper.ToHSV(_baseColor, out hue, out _, out _);

            double x = (hue / 360.0) * (PaletteSize - 1.0);
            double yRatio = GetPaletteVerticalRatioFromColor(color);
            double y = yRatio * (PaletteSize - 1.0);

            _paletteSelection = new Point(
                Math.Clamp(x, 0.0, PaletteSize - 1.0),
                Math.Clamp(y, 0.0, PaletteSize - 1.0));

            UpdatePaletteMarkerPosition();
        }

        private void UpdatePaletteMarkerPosition()
        {
            if (PaletteSelectionMarker == null)
                return;

            Canvas.SetLeft(PaletteSelectionMarker, _paletteSelection.X - (PaletteMarkerSize / 2.0));
            Canvas.SetTop(PaletteSelectionMarker, _paletteSelection.Y - (PaletteMarkerSize / 2.0));
            PaletteSelectionMarker.Visibility = Visibility.Visible;
        }

        private void UpdateVisuals()
        {
            if (ColorPreview == null || HexBox == null || RBox == null || VBox == null || BBox == null ||
                IntensityTrack == null || IntensityFill == null || IntensityBubbleText == null || IntensitySlider == null)
            {
                return;
            }

            Color displayColor = GetDisplayColor();
            ColorPreview.Background = new SolidColorBrush(displayColor);
            UpdatePaletteSelectionFromColor(displayColor);

            _isUpdatingControls = true;
            try
            {
                HexBox.Text = $"#{displayColor.R:X2}{displayColor.G:X2}{displayColor.B:X2}";
                RBox.Text = displayColor.R.ToString(CultureInfo.InvariantCulture);
                VBox.Text = displayColor.G.ToString(CultureInfo.InvariantCulture);
                BBox.Text = displayColor.B.ToString(CultureInfo.InvariantCulture);
                HexBox.SelectionStart = HexBox.Text.Length;
            }
            finally
            {
                _isUpdatingControls = false;
            }

            double ratio = GetSliderPercentRatio();
            double fillHeight = IntensityTrack.ActualHeight * ratio;
            if (fillHeight < 0.0)
                fillHeight = 0.0;

            IntensityFill.Height = fillHeight;
            IntensityBubbleText.Text = $"{Math.Round(IntensitySlider.Value):0}%";
            UpdateIntensityBubblePosition();

            SelectedColorChanged?.Invoke(this, displayColor);
        }

        private void UpdateIntensityBubblePosition()
        {
            if (IntensityBubble == null || IntensityBubbleLayer == null || IntensityHost == null || IntensityTrackShell == null || IntensitySlider == null)
                return;

            if (!_isIntensityBubbleVisible)
            {
                IntensityBubble.Visibility = Visibility.Collapsed;
                return;
            }

            if (IntensityBubble.Visibility != Visibility.Visible)
            {
                IntensityBubble.Visibility = Visibility.Hidden;
                IntensityBubble.UpdateLayout();
            }

            double bubbleWidth = IntensityBubble.ActualWidth;
            double bubbleHeight = IntensityBubble.ActualHeight;
            if (bubbleWidth <= 0.0 || bubbleHeight <= 0.0)
            {
                IntensityBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bubbleWidth = IntensityBubble.DesiredSize.Width;
                bubbleHeight = IntensityBubble.DesiredSize.Height;
            }

            double hostWidth = IntensityHost.ActualWidth;
            double hostHeight = IntensityHost.ActualHeight;
            double trackHeight = IntensityTrackShell.ActualHeight;
            if (hostWidth <= 0.0 || hostHeight <= 0.0 || trackHeight <= 0.0)
            {
                IntensityBubble.Visibility = Visibility.Collapsed;
                return;
            }

            double ratio = GetSliderPercentRatio();
            double trackTop = (hostHeight - trackHeight) / 2.0;
            double thumbTravelHeight = Math.Max(0.0, trackHeight - SliderThumbSize);
            double thumbCenterY = trackTop + ((1.0 - ratio) * thumbTravelHeight) + (SliderThumbSize / 2.0);
            double bubbleTop = thumbCenterY - (bubbleHeight / 2.0);
            bubbleTop = Math.Clamp(bubbleTop, 0.0, Math.Max(0.0, hostHeight - bubbleHeight));

            double bubbleLeft = IntensityTrackShell.TranslatePoint(new Point(0.0, 0.0), IntensityHost).X + IntensityTrackShell.ActualWidth + BubbleHorizontalGap;
            bubbleLeft = Math.Clamp(bubbleLeft, 0.0, Math.Max(0.0, hostWidth - bubbleWidth));

            IntensityBubbleLayer.Width = hostWidth;
            IntensityBubbleLayer.Height = hostHeight;
            Canvas.SetLeft(IntensityBubble, bubbleLeft);
            Canvas.SetTop(IntensityBubble, bubbleTop);
            IntensityBubble.Visibility = Visibility.Visible;
        }

        private void HideIntensityBubble()
        {
            _isIntensityBubbleVisible = false;

            if (IntensityBubble != null)
                IntensityBubble.Visibility = Visibility.Collapsed;
        }

        private double GetSliderPercentRatio()
        {
            if (IntensitySlider == null)
                return 0.5;

            double range = IntensitySlider.Maximum - IntensitySlider.Minimum;
            if (range <= 0.0)
                return 0.5;

            double ratio = (IntensitySlider.Value - IntensitySlider.Minimum) / range;
            return Math.Clamp(ratio, 0.0, 1.0);
        }

        private Color GetDisplayColor()
        {
            return ColorToolBoxColorHelper.ApplyIntensityAroundMidpoint(_baseColor, IntensitySlider?.Value ?? 50.0);
        }

        private double EstimateSliderValueFromColor(Color displayColor)
        {
            double yRatio = GetPaletteVerticalRatioFromColor(displayColor);
            double sliderValue = (1.0 - yRatio) * 100.0;
            return Math.Clamp(sliderValue, 0.0, 100.0);
        }

        private double GetPaletteVerticalRatioFromColor(Color color)
        {
            ColorToolBoxColorHelper.ToHSV(color, out _, out double saturation, out double value);

            if (saturation < 0.01)
                return Math.Clamp(1.0 - value, 0.0, 1.0);

            if (value >= 0.999)
                return Math.Clamp(saturation * 0.5, 0.0, 0.5);

            return Math.Clamp(1.0 - (value * 0.5), 0.5, 1.0);
        }

        private Color CreateBaseColorFromDisplayColor(Color displayColor, double sliderValue)
        {
            sliderValue = Math.Clamp(sliderValue, 0.0, 100.0);

            if (Math.Abs(sliderValue - 50.0) < 0.0001)
                return displayColor;

            if (sliderValue < 50.0)
            {
                double ratioToBase = sliderValue / 50.0;
                if (ratioToBase <= 0.0001)
                    return GetHueReferenceColor(displayColor);

                return Color.FromRgb(
                    RecoverDarkenedComponent(displayColor.R, ratioToBase),
                    RecoverDarkenedComponent(displayColor.G, ratioToBase),
                    RecoverDarkenedComponent(displayColor.B, ratioToBase));
            }

            double ratioToWhite = (sliderValue - 50.0) / 50.0;
            double baseWeight = 1.0 - ratioToWhite;
            if (baseWeight <= 0.0001)
                return GetHueReferenceColor(displayColor);

            return Color.FromRgb(
                RecoverLightenedComponent(displayColor.R, ratioToWhite, baseWeight),
                RecoverLightenedComponent(displayColor.G, ratioToWhite, baseWeight),
                RecoverLightenedComponent(displayColor.B, ratioToWhite, baseWeight));
        }

        private Color GetHueReferenceColor(Color color)
        {
            ColorToolBoxColorHelper.ToHSV(color, out double hue, out double saturation, out _);
            if (saturation < 0.01)
                ColorToolBoxColorHelper.ToHSV(_baseColor, out hue, out _, out _);

            return ColorToolBoxColorHelper.FromHSV(hue, 1.0, 1.0);
        }

        private static byte RecoverDarkenedComponent(byte component, double ratioToBase)
        {
            double recovered = component / ratioToBase;
            return (byte)Math.Round(Math.Clamp(recovered, 0.0, 255.0));
        }

        private static byte RecoverLightenedComponent(byte component, double ratioToWhite, double baseWeight)
        {
            double recovered = (component - (255.0 * ratioToWhite)) / baseWeight;
            return (byte)Math.Round(Math.Clamp(recovered, 0.0, 255.0));
        }

        private void SanitizeRgbTextBox(TextBox textBox)
        {
            string digitsOnly = new string((textBox.Text ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digitsOnly.Length > 3)
                digitsOnly = digitsOnly[..3];

            if (digitsOnly.Length > 0 && int.TryParse(digitsOnly, out int value))
                digitsOnly = Math.Clamp(value, 0, 255).ToString(CultureInfo.InvariantCulture);

            ApplySanitizedText(textBox, digitsOnly, digitsOnly.Length);
        }

        private string NormalizeHexText(string? text)
        {
            string hexDigits = new string((text ?? string.Empty)
                .ToUpperInvariant()
                .Where(IsHexCharacter)
                .ToArray());

            if (hexDigits.Length > 6)
                hexDigits = hexDigits[..6];

            return HexPrefix + hexDigits;
        }

        private void ApplySanitizedText(TextBox textBox, string sanitizedText, int caretIndex)
        {
            if (textBox.Text == sanitizedText)
                return;

            _isUpdatingControls = true;
            try
            {
                textBox.Text = sanitizedText;
                textBox.SelectionStart = Math.Clamp(caretIndex, 0, textBox.Text.Length);
                textBox.SelectionLength = 0;
            }
            finally
            {
                _isUpdatingControls = false;
            }
        }

        private static string MergeText(string originalText, int selectionStart, int selectionLength, string insertedText)
        {
            selectionStart = Math.Clamp(selectionStart, 0, originalText.Length);
            selectionLength = Math.Clamp(selectionLength, 0, originalText.Length - selectionStart);
            return originalText.Remove(selectionStart, selectionLength).Insert(selectionStart, insertedText);
        }

        private static bool IsHexCharacter(char character)
        {
            return (character >= '0' && character <= '9') ||
                   (character >= 'A' && character <= 'F') ||
                   (character >= 'a' && character <= 'f');
        }
    }
}
