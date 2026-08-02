using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MutsumiPet.Models;

namespace MutsumiPet.Views
{
    /// A `Border` that always renders as a capsule, whatever height the wrapped
    /// dialogue ends up needing.
    internal sealed class CapsuleBorder : Border
    {
        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            CornerRadius = new CornerRadius(info.NewSize.Height / 2);
        }
    }

    /// The speech bubble above the pet, matching the macOS build's capsule plus two
    /// trailing dots.
    public sealed class ThoughtBubble : ContentControl
    {
        internal static readonly FontFamily UIFont = new FontFamily("Segoe UI, Microsoft YaHei UI");

        private static readonly Brush BubbleFill = Frozen(Color.FromArgb(240, 255, 255, 255));
        private static readonly Brush Outline = Frozen(Color.FromRgb(10, 64, 31));
        private static readonly Brush SymbolInk = Frozen(Color.FromRgb(13, 61, 31));
        private static readonly Brush TextInk = Frozen(Color.FromRgb(18, 51, 31));

        private readonly TextBlock symbolText = new TextBlock();
        private readonly TextBlock messageText = new TextBlock();

        public ThoughtBubble()
        {
            symbolText.FontFamily = UIFont;
            symbolText.FontSize = 17;
            symbolText.FontWeight = FontWeights.Black;
            symbolText.Foreground = SymbolInk;
            symbolText.VerticalAlignment = VerticalAlignment.Center;

            messageText.FontFamily = UIFont;
            messageText.FontSize = 15;
            messageText.FontWeight = FontWeights.SemiBold;
            messageText.Foreground = TextInk;
            messageText.TextWrapping = TextWrapping.Wrap;
            messageText.LineHeight = 20;
            messageText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            messageText.MaxHeight = 40;
            messageText.TextTrimming = TextTrimming.CharacterEllipsis;
            messageText.VerticalAlignment = VerticalAlignment.Center;
            messageText.Margin = new Thickness(9, 0, 0, 0);

            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Children.Add(symbolText);
            row.Children.Add(messageText);

            var capsule = new CapsuleBorder();
            capsule.HorizontalAlignment = HorizontalAlignment.Right;
            capsule.Background = BubbleFill;
            capsule.BorderBrush = Outline;
            capsule.BorderThickness = new Thickness(3);
            capsule.Padding = new Thickness(18, 13, 18, 13);
            capsule.Child = row;

            var tail = new StackPanel();
            tail.Orientation = Orientation.Horizontal;
            tail.HorizontalAlignment = HorizontalAlignment.Right;
            // The macOS layout overlaps the tail with the capsule by four points.
            tail.Margin = new Thickness(0, -4, 34, 0);
            tail.Children.Add(Dot(13, 2.5));
            tail.Children.Add(Dot(8, 2, 7));

            var stack = new StackPanel();
            stack.Orientation = Orientation.Vertical;
            stack.Children.Add(capsule);
            stack.Children.Add(tail);

            var shadow = new DropShadowEffect();
            shadow.Color = Colors.Black;
            shadow.Opacity = 0.10;
            shadow.BlurRadius = 16;
            shadow.ShadowDepth = 3;
            shadow.Direction = 270;
            shadow.RenderingBias = RenderingBias.Quality;

            Effect = shadow;
            Content = stack;
        }

        public void Update(string text, PetMood mood)
        {
            symbolText.Text = PetMoods.Symbol(mood);
            messageText.Text = text;
            System.Windows.Automation.AutomationProperties.SetName(this, "若叶睦说：" + text);
        }

        private static Ellipse Dot(double size, double thickness)
        {
            return Dot(size, thickness, 0);
        }

        private static Ellipse Dot(double size, double thickness, double leftMargin)
        {
            var dot = new Ellipse();
            dot.Width = size;
            dot.Height = size;
            dot.Fill = BubbleFill;
            dot.Stroke = Outline;
            dot.StrokeThickness = thickness;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(leftMargin, 0, 0, 0);
            return dot;
        }

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
