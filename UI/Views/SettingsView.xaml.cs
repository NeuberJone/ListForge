using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class SettingsView : UserControl
{
    private const int MaxForgeParticlesPerLayer = 60;

    private readonly Random _forgeRandom = new();
    private readonly Dictionary<TextBox, ForgeSettingsSparkState> _forgeSparkStates = [];
    private MainViewModel? _vm;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void SetViewModel(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;

        // ---- Display ----
        SldEditorFontSize.SetBinding(Slider.ValueProperty,
            new Binding(nameof(vm.EditorFontSize)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtEditorFontSize.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(vm.EditorFontSize)) { Source = vm, StringFormat = "{0:0}px" });

        // ---- Output ----
        ChkUseDefaultOutputDir.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.UseDefaultOutputDir)) { Source = vm, Mode = BindingMode.TwoWay });
        TxtOutputDir.SetBinding(TextBox.TextProperty,
            new Binding(nameof(vm.OutputDir)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        ChkUseDefaultListName.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.UseDefaultListName)) { Source = vm, Mode = BindingMode.TwoWay });
        TxtDefaultListName.SetBinding(TextBox.TextProperty,
            new Binding(nameof(vm.DefaultListName)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        CmbDefaultCase.ItemsSource = vm.CaseLabels;
        CmbDefaultCase.SetBinding(ComboBox.SelectedItemProperty,
            new Binding(nameof(vm.DefaultCaseLabel)) { Source = vm, Mode = BindingMode.TwoWay });

        TxtDefaultSeparator.SetBinding(TextBox.TextProperty,
            new Binding(nameof(vm.DefaultSeparator)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        ChkAllowOutputEditing.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.AllowOutputEditing)) { Source = vm, Mode = BindingMode.TwoWay });

        // ---- Advanced export ----
        CmbAdvancedSaveMode.ItemsSource = vm.AdvancedSaveModeLabels;
        CmbAdvancedSaveMode.SetBinding(ComboBox.SelectedItemProperty,
            new Binding(nameof(vm.AdvancedSaveModeLabel)) { Source = vm, Mode = BindingMode.TwoWay });

        // ---- Theme ----
        CmbTheme.ItemsSource = vm.ThemeNames;
        CmbTheme.SetBinding(ComboBox.SelectedItemProperty,
            new Binding(nameof(vm.ThemeName)) { Source = vm, Mode = BindingMode.TwoWay });

        // ---- Size groups ----
        var male = vm.SizeGroupBindings["male"];
        TxtMaleBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(male.BaseSizes)) { Source = male, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtMalePre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(male.Prefixes)) { Source = male, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtMaleSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(male.Suffixes)) { Source = male, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var female = vm.SizeGroupBindings["female"];
        TxtFemaleBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(female.BaseSizes)) { Source = female, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtFemalePre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(female.Prefixes)) { Source = female, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtFemaleSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(female.Suffixes)) { Source = female, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var child = vm.SizeGroupBindings["child"];
        TxtChildBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(child.BaseSizes)) { Source = child, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtChildPre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(child.Prefixes)) { Source = child, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtChildSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(child.Suffixes)) { Source = child, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var sock = vm.SizeGroupBindings["sock"];
        TxtSockBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(sock.BaseSizes)) { Source = sock, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtSockPre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(sock.Prefixes)) { Source = sock, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtSockSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(sock.Suffixes)) { Source = sock, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        // ---- Size summary ----
        TxtSizeSummary.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(vm.SizeSummary)) { Source = vm });

        RegisterForgeSparkFields();
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(SettingsTextBox_TextChanged));
    }

    private void RegisterForgeSparkFields()
    {
        _forgeSparkStates.Clear();
        foreach (var textBox in FindVisualChildren<TextBox>(this))
            _forgeSparkStates[textBox] = new ForgeSettingsSparkState(textBox.Text.Length);
    }

    private void SettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_vm == null || !_vm.ForgeModeEnabled || !_vm.ForgeSparksEnabled)
            return;

        if (e.OriginalSource is not TextBox textBox || textBox.IsReadOnly || !textBox.IsEnabled)
            return;

        if (!_forgeSparkStates.TryGetValue(textBox, out var state))
        {
            state = new ForgeSettingsSparkState(Math.Max(0, textBox.Text.Length - 1));
            _forgeSparkStates[textBox] = state;
        }

        var textLength = textBox.Text.Length;
        if (textLength <= state.LastTextLength)
        {
            state.LastTextLength = textLength;
            return;
        }

        state.LastTextLength = textLength;

        var now = DateTime.UtcNow;
        if ((now - state.LastSparkUtc).TotalMilliseconds < 85)
            return;

        state.LastSparkUtc = now;
        PulseTextBoxGlow(textBox);
        SpawnSettingsSparks(textBox);
    }

    private void SpawnSettingsSparks(TextBox textBox, int count = 3)
    {
        if (ForgeSettingsSparks.ActualWidth <= 0 || ForgeSettingsSparks.ActualHeight <= 0)
            return;

        var origin = GetCaretSparkOrigin(textBox);
        for (var i = 0; i < count; i++)
        {
            AddSpark(origin.X, origin.Y);
            if (i == 0 && count > 3)
                AddEmber(origin.X, origin.Y);
        }
    }

    private void PulseTextBoxGlow(TextBox textBox)
    {
        var glow = new DropShadowEffect
        {
            Color = Color.FromRgb(255, 135, 35),
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0,
        };
        textBox.Effect = glow;

        var animation = PulseOpacityAnimation(0.0, 0.28, 200);
        animation.Completed += (_, _) =>
        {
            if (ReferenceEquals(textBox.Effect, glow))
                textBox.Effect = null;
        };
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, animation);
    }

    private Point GetCaretSparkOrigin(TextBox textBox)
    {
        var caretRect = textBox.GetRectFromCharacterIndex(textBox.CaretIndex, true);
        var origin = caretRect.IsEmpty
            ? new Point(textBox.ActualWidth - 18, Math.Max(10, textBox.ActualHeight * 0.5))
            : new Point(caretRect.Right, caretRect.Top + caretRect.Height * 0.45);

        var point = textBox.TranslatePoint(origin, ForgeSettingsSparks);
        return new Point(
            Math.Clamp(point.X, 8, Math.Max(8, ForgeSettingsSparks.ActualWidth - 16)),
            Math.Clamp(point.Y, 8, Math.Max(8, ForgeSettingsSparks.ActualHeight - 16)));
    }

    private void AddSpark(double originX, double originY)
    {
        TrimForgeLayer();

        var spark = new Rectangle
        {
            Width = _forgeRandom.NextDouble() * 7 + 5,
            Height = 1.4,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(_forgeRandom.Next(0, 3) switch
            {
                0 => Color.FromRgb(255, 229, 132),
                1 => Color.FromRgb(255, 165, 54),
                _ => Color.FromRgb(239, 82, 28),
            }),
            Opacity = 0.95,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 128, 24),
                BlurRadius = 4,
                ShadowDepth = 0,
                Opacity = 0.5,
            },
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new RotateTransform(_forgeRandom.Next(-26, 27)),
        };

        Canvas.SetLeft(spark, originX);
        Canvas.SetTop(spark, originY);
        ForgeSettingsSparks.Children.Add(spark);

        var direction = (_forgeRandom.NextDouble() * Math.PI * 1.55) - (Math.PI * 0.75);
        var distance = _forgeRandom.NextDouble() * 22 + 14;
        var duration = TimeSpan.FromMilliseconds(_forgeRandom.Next(180, 320));

        var storyboard = new Storyboard();
        storyboard.Children.Add(SparkAnimation(spark, Canvas.LeftProperty, originX + Math.Cos(direction) * distance, duration));
        storyboard.Children.Add(SparkAnimation(spark, Canvas.TopProperty, originY + Math.Sin(direction) * distance - 8, duration));
        storyboard.Children.Add(SparkAnimation(spark, OpacityProperty, 0.0, duration));
        BeginStoryboardWithCleanup(storyboard, spark, 620);
    }

    private void AddEmber(double originX, double originY)
    {
        TrimForgeLayer();

        var size = _forgeRandom.NextDouble() * 2.5 + 2;
        var ember = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new RadialGradientBrush(Color.FromRgb(255, 235, 170), Color.FromRgb(239, 82, 28)),
            Opacity = 0.55,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 110, 24),
                BlurRadius = 5,
                ShadowDepth = 0,
                Opacity = 0.42,
            },
        };

        Canvas.SetLeft(ember, originX);
        Canvas.SetTop(ember, originY);
        ForgeSettingsSparks.Children.Add(ember);

        var duration = TimeSpan.FromMilliseconds(_forgeRandom.Next(360, 560));
        var storyboard = new Storyboard();
        storyboard.Children.Add(SparkAnimation(ember, Canvas.LeftProperty, originX + (_forgeRandom.NextDouble() * 34 - 17), duration));
        storyboard.Children.Add(SparkAnimation(ember, Canvas.TopProperty, originY - (_forgeRandom.NextDouble() * 42 + 22), duration));
        storyboard.Children.Add(SparkAnimation(ember, OpacityProperty, 0.0, duration));
        BeginStoryboardWithCleanup(storyboard, ember, 760);
    }

    private void BeginStoryboardWithCleanup(Storyboard storyboard, UIElement element, int cleanupMilliseconds)
    {
        var removed = false;
        void Remove()
        {
            if (removed)
                return;

            removed = true;
            element.BeginAnimation(OpacityProperty, null);
            ForgeSettingsSparks.Children.Remove(element);
        }

        storyboard.Completed += (_, _) => Remove();
        storyboard.Begin();

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(cleanupMilliseconds),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Remove();
        };
        timer.Start();
    }

    private void TrimForgeLayer()
    {
        while (ForgeSettingsSparks.Children.Count >= MaxForgeParticlesPerLayer)
            ForgeSettingsSparks.Children.RemoveAt(0);
    }

    private static DoubleAnimation SparkAnimation(
        System.Windows.DependencyObject target,
        System.Windows.DependencyProperty property,
        double to,
        TimeSpan duration,
        bool autoReverse = false)
    {
        var animation = new DoubleAnimation(to, duration)
        {
            AutoReverse = autoReverse,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new System.Windows.PropertyPath(property));
        return animation;
    }

    private static DoubleAnimation PulseOpacityAnimation(double from, double to, double milliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent)
        where T : System.Windows.DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
                yield return typed;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private sealed class ForgeSettingsSparkState(int lastTextLength)
    {
        public int LastTextLength { get; set; } = lastTextLength;
        public DateTime LastSparkUtc { get; set; } = DateTime.MinValue;
    }
}
