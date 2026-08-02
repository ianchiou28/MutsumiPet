using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MutsumiPet.Models;
using MutsumiPet.Stores;
using MutsumiPet.Support;

namespace MutsumiPet.Views
{
    /// The transparent, borderless, non-activating window the pet lives in.
    /// Mirrors the macOS `PetView`: same 380x450 stage, same 314pt character box,
    /// same timers.
    public sealed class PetWindow : Window
    {
        private const double BaseWidth = 380;
        private const double BaseHeight = 450;
        private const double CharacterSize = 314;
        private const double WalkTimeStep = 1.0 / 60.0;

        private static readonly double[] ScaleChoices = { 0.6, 0.8, 1.0, 1.2, 1.4 };

        private readonly PetStore store;
        private readonly PetWindowController controller;

        private readonly Grid root = new Grid();
        private readonly Image character = new Image();
        private readonly ThoughtBubble bubble = new ThoughtBubble();
        private readonly ContextMenu menu = new ContextMenu();

        private readonly ScaleTransform rootScale = new ScaleTransform(1, 1);
        private readonly ScaleTransform characterFlip = new ScaleTransform(1, 1);
        private readonly TranslateTransform characterOffset = new TranslateTransform();
        private readonly TranslateTransform dropBounce = new TranslateTransform();
        private readonly DropShadowEffect characterShadow = new DropShadowEffect();

        private readonly DispatcherTimer lifestyleTimer;
        private readonly DispatcherTimer activityTimer;
        private readonly DispatcherTimer walkTimer;

        private bool didDrag;
        private Point pressOrigin;

        public PetWindow(PetStore store)
        {
            this.store = store;
            controller = new PetWindowController(this);

            Title = "若叶睦桌宠";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            // A null brush keeps the empty parts of the stage click-through, so only
            // the character itself intercepts the mouse.
            Background = null;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Manual;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Topmost = store.LayerMode == WindowLayerMode.Front;
            FontFamily = ThoughtBubble.UIFont;

            BuildContent();
            ApplyScaleToLayout();
            PlaceRoughly();

            lifestyleTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(18), DispatcherPriority.Normal, OnLifestyleTick, Dispatcher);
            activityTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(0.125), DispatcherPriority.Normal, OnActivityTick, Dispatcher);
            walkTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(WalkTimeStep), DispatcherPriority.Render, OnWalkTick, Dispatcher);
            activityTimer.Stop();
            walkTimer.Stop();

            store.PropertyChanged += OnStoreChanged;
            controller.InteractRequested += OnInteractRequested;
            controller.ToggleBubbleRequested += store.ToggleBubble;
            controller.QuitRequested += Quit;

            SourceInitialized += OnSourceInitialized;
            ContentRendered += OnContentRendered;
            Closed += OnClosed;

            Render();
        }

        private void BuildContent()
        {
            characterShadow.Color = Colors.Black;
            characterShadow.Opacity = 0.16;
            characterShadow.BlurRadius = 20;
            characterShadow.ShadowDepth = 5;
            characterShadow.Direction = 270;
            characterShadow.RenderingBias = RenderingBias.Performance;

            var transforms = new TransformGroup();
            transforms.Children.Add(characterFlip);
            transforms.Children.Add(characterOffset);
            transforms.Children.Add(dropBounce);

            character.Width = CharacterSize;
            character.Height = CharacterSize;
            character.Stretch = Stretch.Uniform;
            character.HorizontalAlignment = HorizontalAlignment.Center;
            character.VerticalAlignment = VerticalAlignment.Bottom;
            character.RenderTransformOrigin = new Point(0.5, 0.5);
            character.RenderTransform = transforms;
            character.Cursor = Cursors.Hand;
            character.ContextMenu = menu;
            RenderOptions.SetBitmapScalingMode(character, BitmapScalingMode.HighQuality);
            System.Windows.Automation.AutomationProperties.SetName(character, "Q版若叶睦桌宠");
            System.Windows.Automation.AutomationProperties.SetHelpText(
                character, "点击与她互动，拖动可移动位置");

            character.MouseLeftButtonDown += OnCharacterMouseDown;
            character.MouseMove += OnCharacterMouseMove;
            character.MouseLeftButtonUp += OnCharacterMouseUp;
            character.ContextMenuOpening += OnContextMenuOpening;

            bubble.MaxWidth = 270;
            bubble.HorizontalAlignment = HorizontalAlignment.Center;
            bubble.VerticalAlignment = VerticalAlignment.Bottom;
            bubble.Margin = new Thickness(0, 0, 0, 326);
            bubble.IsHitTestVisible = false;

            root.Width = BaseWidth;
            root.Height = BaseHeight;
            root.HorizontalAlignment = HorizontalAlignment.Left;
            root.VerticalAlignment = VerticalAlignment.Top;
            // LayoutTransform, not RenderTransform: the window clips its content to
            // the client area before render transforms run, which would crop the
            // stage at any scale below 100%.
            root.LayoutTransform = rootScale;
            root.Children.Add(bubble);
            root.Children.Add(character);

            Content = root;
        }

        // MARK: - Lifecycle

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            controller.Attach(store.LayerMode);
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            // Deliberately not Loaded: the window has not settled on its final size
            // there yet, so the bottom-right anchor would be computed against the
            // wrong dimensions.
            controller.PlaceInitial();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            lifestyleTimer.Stop();
            activityTimer.Stop();
            walkTimer.Stop();
            store.PropertyChanged -= OnStoreChanged;
            controller.Detach();
        }

        private void Quit()
        {
            Application.Current.Shutdown();
        }

        private void OnInteractRequested()
        {
            store.ReactToTap();
        }

        // MARK: - Timers

        private void OnLifestyleTick(object sender, EventArgs e)
        {
            store.LifestyleTick();
        }

        private void OnActivityTick(object sender, EventArgs e)
        {
            if (store.Activity == PetActivity.Idle) return;
            if (store.Activity == PetActivity.Walking) return;
            store.AdvanceActivityFrame();
        }

        private void OnWalkTick(object sender, EventArgs e)
        {
            if (store.Activity != PetActivity.Walking) return;

            WanderStepResult step = controller.AdvanceWandering(store.WanderSpeed, WalkTimeStep);
            switch (step.Kind)
            {
                case WanderStepKind.Moving:
                    store.UpdateWalkingDirection(step.Direction);
                    store.AdvanceWalkingFrame(step.Distance);
                    break;
                case WanderStepKind.Arrived:
                    store.FinishWalking();
                    break;
            }
        }

        // MARK: - Store bindings

        private void OnStoreChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "LayerMode":
                    controller.UpdateLayerMode(store.LayerMode);
                    break;
                case "Scale":
                    ApplyScale();
                    break;
                case "Activity":
                    OnActivityChanged();
                    break;
                case "WanderingEnabled":
                    if (store.WanderingEnabled == false) controller.StopWandering();
                    break;
            }

            Render();
        }

        private void OnActivityChanged()
        {
            bool walking = store.Activity == PetActivity.Walking;
            if (walking)
            {
                controller.BeginWandering();
                walkTimer.Start();
            }
            else
            {
                controller.StopWandering();
                walkTimer.Stop();
            }

            bool sipping = store.Activity == PetActivity.DrinkingTea
                || store.Activity == PetActivity.EatingSnack;
            if (sipping) activityTimer.Start();
            else activityTimer.Stop();
        }

        private void Render()
        {
            character.Source = PetAssets.Character(store.Activity, store.AnimationFrame, store.Pose);

            double horizontal = PetActivities.HorizontalScale(store.Activity, store.WalkingDirection);
            characterFlip.ScaleX = horizontal;

            Vector offset = PetActivities.AlignmentOffset(store.Activity, store.AnimationFrame);
            double baseY = store.Pose == PetPose.Grabbed
                ? -15
                : (store.Mood == PetMood.Curious ? -3 : 0);

            characterOffset.X = offset.X * CharacterSize * horizontal;
            characterOffset.Y = baseY + offset.Y * CharacterSize;

            character.Effect = store.Pose == PetPose.Grabbed ? null : characterShadow;

            if (store.ShowsBubble)
            {
                bubble.Update(store.Message, store.Mood);
                bubble.Visibility = Visibility.Visible;
            }
            else
            {
                bubble.Visibility = Visibility.Collapsed;
            }
        }

        // MARK: - Sizing

        private void ApplyScaleToLayout()
        {
            double scale = store.Scale;
            rootScale.ScaleX = scale;
            rootScale.ScaleY = scale;
            Width = BaseWidth * scale;
            Height = BaseHeight * scale;
            controller.WindowSize = new Size(Width, Height);
        }

        private void ApplyScale()
        {
            double previousBottom = controller.Position.Y + controller.WindowSize.Height;
            ApplyScaleToLayout();
            controller.ResizeKeepingBottomEdge(previousBottom);
        }

        private void PlaceRoughly()
        {
            // Refined by PlaceInitial() once the window has a handle and we know which
            // monitor it landed on; this just avoids a flash in the top-left corner.
            Rect work = SystemParameters.WorkArea;
            Left = work.Right - Width - 28;
            Top = work.Bottom - Height - 24;
        }

        // MARK: - Pointer

        private void OnCharacterMouseDown(object sender, MouseButtonEventArgs e)
        {
            didDrag = false;
            pressOrigin = e.GetPosition(this);
            controller.BeginDrag();
            character.CaptureMouse();
            e.Handled = true;
        }

        private void OnCharacterMouseMove(object sender, MouseEventArgs e)
        {
            if (character.IsMouseCaptured == false) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (didDrag == false)
            {
                Point current = e.GetPosition(this);
                if (Math.Abs(current.X - pressOrigin.X) < 1 && Math.Abs(current.Y - pressOrigin.Y) < 1) return;
                didDrag = true;
            }

            if (store.Pose != PetPose.Grabbed) store.BeginDrag();
            controller.DragWindowToCurrentMouse();
        }

        private void OnCharacterMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (character.IsMouseCaptured == false) return;
            character.ReleaseMouseCapture();
            controller.EndDrag();
            e.Handled = true;

            if (didDrag == false)
            {
                store.ReactToTap();
                return;
            }

            didDrag = false;
            store.EndDrag();
            PlayDropBounce();
        }

        private void PlayDropBounce()
        {
            var bounce = new DoubleAnimationUsingKeyFrames();
            bounce.Duration = TimeSpan.FromSeconds(0.28);
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(
                11, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.08)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(
                -3, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.18)),
                new CubicEase { EasingMode = EasingMode.EaseInOut }));
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(
                0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.28)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            bounce.FillBehavior = FillBehavior.Stop;

            dropBounce.BeginAnimation(TranslateTransform.YProperty, bounce);
        }

        // MARK: - Context menu

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            menu.Items.Clear();
            menu.FontFamily = ThoughtBubble.UIFont;

            menu.Items.Add(Item(store.ShowsBubble ? "隐藏气泡" : "显示气泡", store.ToggleBubble));
            menu.Items.Add(Item(
                store.WanderingEnabled ? "暂停自由活动" : "恢复自由活动",
                delegate { store.SetWanderingEnabled(store.WanderingEnabled == false); }));

            var lifestyle = new MenuItem();
            lifestyle.Header = "生活动作";
            lifestyle.Items.Add(Item("去走走", delegate { store.PerformLifestyle(PetActivity.Walking); }));
            lifestyle.Items.Add(Item("喝茶", delegate { store.PerformLifestyle(PetActivity.DrinkingTea); }));
            lifestyle.Items.Add(Item("吃点心", delegate { store.PerformLifestyle(PetActivity.EatingSnack); }));
            menu.Items.Add(lifestyle);

            menu.Items.Add(Item("让睦说一句", store.SpeakForCurrentState));

            var layers = new MenuItem();
            layers.Header = "窗口层级：" + WindowLayerModes.Title(store.LayerMode);
            foreach (WindowLayerMode mode in WindowLayerModes.AllCases)
            {
                WindowLayerMode captured = mode;
                MenuItem entry = Item(
                    WindowLayerModes.Title(mode),
                    delegate { store.SetLayerMode(captured); });
                entry.IsChecked = store.LayerMode == mode;
                layers.Items.Add(entry);
            }
            menu.Items.Add(layers);

            menu.Items.Add(new Separator());

            var sizes = new MenuItem();
            sizes.Header = "大小：" + Percent(store.Scale);
            foreach (double option in ScaleChoices)
            {
                double captured = option;
                MenuItem entry = Item(Percent(option), delegate { store.SetScale(captured); });
                entry.IsChecked = Math.Abs(store.Scale - option) < 0.01;
                sizes.Items.Add(entry);
            }
            menu.Items.Add(sizes);

            menu.Items.Add(new Separator());
            menu.Items.Add(Item("退出若叶睦桌宠", Quit));
        }

        private static string Percent(double scale)
        {
            return ((int)Math.Round(scale * 100)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static MenuItem Item(string header, Action action)
        {
            var item = new MenuItem();
            item.Header = header;
            item.Click += delegate { action(); };
            return item;
        }
    }
}
