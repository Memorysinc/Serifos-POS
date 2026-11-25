using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using FirewallControl.App.Services;

namespace FirewallControl.App.Views;

/// <summary>
/// Pantalla de presentación animada de inicio para Sefiros POS:
/// muestra el aura prismática, traza progresivamente la "S" con un cometa luminoso siguiendo el recorrido,
/// enciende un destello especular sobre la letra, dispara una estrella protagonista con onda expansiva al llegar al final
/// acompañada de estrellas astroide Gemini alrededor y desaparece suavemente con audio relajante.
/// </summary>
public partial class SefirosSplashWindow : Window
{
    private const string PropScaleX = "(UIElement.RenderTransform).(ScaleTransform.ScaleX)";
    private const string PropScaleY = "(UIElement.RenderTransform).(ScaleTransform.ScaleY)";
    private const string PropTranslateX = "(UIElement.RenderTransform).(TranslateTransform.X)";
    private const string PropTranslateY = "(UIElement.RenderTransform).(TranslateTransform.Y)";
    private const string PropGroupScaleX = "(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)";
    private const string PropGroupScaleY = "(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)";
    private const string PropGroupAngle = "(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)";
    private const string PropGroupTranslateX = "(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.X)";
    private const string PropGroupTranslateY = "(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)";

    private Storyboard? _introStoryboard;
    private Storyboard? _exitStoryboard;
    private IDisposable? _startupSoundHandle;
    private bool _started;
    private bool _readyToTransitionRaised;
    private bool _completedRaised;
    private bool _isClosing;

    public event Action? PresentationReadyToTransition;
    public event Action? PresentationCompleted;

    public bool IsPresentationCompleted => _completedRaised;

    public SefirosSplashWindow()
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);

        Loaded += OnLoaded;
        MouseLeftButtonDown += (_, _) => SkipPresentation();
        PreviewKeyDown += OnWindowPreviewKeyDown;
        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Calcula la longitud euclidiana total de una geometría vectorial aplanada.
    /// Se utiliza para sincronizar StrokeDashArray y StrokeDashOffset en unidades de StrokeThickness.
    /// </summary>
    public static double ComputePathLength(Geometry? geometry)
    {
        if (geometry == null)
            return 0d;

        var flattened = geometry.GetFlattenedPathGeometry(0.15, ToleranceType.Absolute);
        double total = 0d;

        foreach (var figure in flattened.Figures)
        {
            Point current = figure.StartPoint;
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        total += (line.Point - current).Length;
                        current = line.Point;
                        break;
                    case PolyLineSegment poly:
                        foreach (Point pt in poly.Points)
                        {
                            total += (pt - current).Length;
                            current = pt;
                        }
                        break;
                }
            }

            if (figure.IsClosed)
                total += (figure.StartPoint - current).Length;
        }

        return total;
    }

    /// <summary>
    /// Permite posicionar la línea de tiempo del storyboard principal en un instante exacto para pruebas o captura visual.
    /// </summary>
    public void SeekIntroForVerification(TimeSpan offset)
    {
        if (_introStoryboard == null)
            return;

        _introStoryboard.Pause(this);
        _introStoryboard.SeekAlignedToLastTick(this, offset, TimeSeekOrigin.BeginTime);
        UpdateLayout();
    }

    public void SkipPresentation()
    {
        if (_completedRaised || _isClosing)
            return;

        StopAndDisposeStartupSound();

        if (!IsLoaded)
        {
            RaiseReadyToTransitionOnce();
            RaiseCompletedOnce();
            return;
        }

        _introStoryboard?.Stop(this);
        BeginSmoothExit(durationMs: 110);
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.Space)
        {
            e.Handled = true;
            SkipPresentation();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started || _completedRaised || _isClosing)
            return;
        _started = true;

        StartIntroAnimation();
    }

    private void StartIntroAnimation()
    {
        double rawLen = Math.Max(ComputePathLength(PathSStroke.Data), 180d);

        var sb = new Storyboard();

        // 1. Entrada del emblema, aura prismática y filete superior (todos apuntando al FrameworkElement dueño)
        var cardFade = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(cardFade, RootStage);
        Storyboard.SetTargetProperty(cardFade, new PropertyPath(OpacityProperty));
        sb.Children.Add(cardFade);

        var topBarAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(1250))
        {
            BeginTime = TimeSpan.FromMilliseconds(60),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(topBarAnim, TopAccentBar);
        Storyboard.SetTargetProperty(topBarAnim, new PropertyPath(PropScaleX));
        sb.Children.Add(topBarAnim);

        var squircleFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(squircleFade, SquircleBadge);
        Storyboard.SetTargetProperty(squircleFade, new PropertyPath(OpacityProperty));
        sb.Children.Add(squircleFade);

        AddSquircleScaleBounce(sb);
        AddPrismAuraAnimation(sb);

        var guideFade = new DoubleAnimationUsingKeyFrames();
        guideFade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(80)));
        guideFade.KeyFrames.Add(new EasingDoubleKeyFrame(0.11, TimeSpan.FromMilliseconds(340), new CubicEase { EasingMode = EasingMode.EaseOut }));
        guideFade.KeyFrames.Add(new EasingDoubleKeyFrame(0.05, TimeSpan.FromMilliseconds(1350), new CubicEase { EasingMode = EasingMode.EaseOut }));
        Storyboard.SetTarget(guideFade, PathSGuide);
        Storyboard.SetTargetProperty(guideFade, new PropertyPath(OpacityProperty));
        sb.Children.Add(guideFade);

        // 2. Trazado progresivo sincronizado de la "S" + cometa luminoso siguiendo el recorrido (140ms -> 1360ms)
        var drawBegin = TimeSpan.FromMilliseconds(140);
        var drawDuration = TimeSpan.FromMilliseconds(1220);

        ConfigureProgressiveStrokeLayer(sb, PathSGlowOuter, rawLen, drawBegin, drawDuration, targetOpacity: 0.16, fadePeakBeginMs: 170, settleOpacity: 0.11);
        ConfigureProgressiveStrokeLayer(sb, PathSGlowInner, rawLen, drawBegin, drawDuration, targetOpacity: 0.24, fadePeakBeginMs: 160, settleOpacity: 0.16);
        ConfigureProgressiveStrokeLayer(sb, PathSStroke, rawLen, drawBegin, drawDuration, targetOpacity: 1.0, fadePeakBeginMs: 140, settleOpacity: 1.0);
        ConfigureProgressiveStrokeLayer(sb, PathSWarmBloom, rawLen, drawBegin, drawDuration, targetOpacity: 0.94, fadePeakBeginMs: 140, settleOpacity: 0.94);

        AddCometPathAnimation(sb, drawBegin, drawDuration);

        // 3. Destello especular visible recorriendo la letra S completada (1080ms -> 1720ms)
        ConfigureSheenSweep(sb, PathSSheen, rawLen, TimeSpan.FromMilliseconds(1080), TimeSpan.FromMilliseconds(640));

        // 4. Estrella protagonista con onda expansiva al final de la S (1290ms -> 2350ms) + estrellas astroide alrededor
        AddEndStarBurst(sb);
        AddWingedGeminiSparkles(sb);

        // 5. Título, subtítulo y barra de progreso
        var titleOpacity = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(460))
        {
            BeginTime = TimeSpan.FromMilliseconds(460),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(titleOpacity, TxtSplashTitle);
        Storyboard.SetTargetProperty(titleOpacity, new PropertyPath(OpacityProperty));
        sb.Children.Add(titleOpacity);

        var titleSlide = new DoubleAnimation(10.0, 0.0, TimeSpan.FromMilliseconds(480))
        {
            BeginTime = TimeSpan.FromMilliseconds(460),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(titleSlide, TxtSplashTitle);
        Storyboard.SetTargetProperty(titleSlide, new PropertyPath(PropTranslateY));
        sb.Children.Add(titleSlide);

        var subtitleOpacity = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(440))
        {
            BeginTime = TimeSpan.FromMilliseconds(680),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(subtitleOpacity, TxtSplashSubtitle);
        Storyboard.SetTargetProperty(subtitleOpacity, new PropertyPath(OpacityProperty));
        sb.Children.Add(subtitleOpacity);

        var progressAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(2200))
        {
            BeginTime = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(progressAnim, ProgressFill);
        Storyboard.SetTargetProperty(progressAnim, new PropertyPath(PropScaleX));
        sb.Children.Add(progressAnim);

        sb.Completed += (_, _) => BeginSmoothExit(durationMs: 480);
        _introStoryboard = sb;
        _startupSoundHandle = SefirosStartupSoundService.PlayStartupChime();
        sb.Begin(this, isControllable: true);
    }

    private void AddSquircleScaleBounce(Storyboard sb)
    {
        foreach (string propPath in new[] { PropScaleX, PropScaleY })
        {
            var kf = new DoubleAnimationUsingKeyFrames();
            kf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.86, TimeSpan.Zero));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, TimeSpan.FromMilliseconds(360), new CubicEase { EasingMode = EasingMode.EaseOut }));
            kf.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, TimeSpan.FromMilliseconds(1260)));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, TimeSpan.FromMilliseconds(1450), new CubicEase { EasingMode = EasingMode.EaseOut }));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, TimeSpan.FromMilliseconds(1680), new CubicEase { EasingMode = EasingMode.EaseInOut }));
            Storyboard.SetTarget(kf, SquircleBadge);
            Storyboard.SetTargetProperty(kf, new PropertyPath(propPath));
            sb.Children.Add(kf);
        }
    }

    private void AddPrismAuraAnimation(Storyboard sb)
    {
        var op = new DoubleAnimationUsingKeyFrames();
        op.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        op.KeyFrames.Add(new EasingDoubleKeyFrame(0.82, TimeSpan.FromMilliseconds(520), new CubicEase { EasingMode = EasingMode.EaseOut }));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.90, TimeSpan.FromMilliseconds(1560)));
        op.KeyFrames.Add(new EasingDoubleKeyFrame(0.55, TimeSpan.FromMilliseconds(2360), new CubicEase { EasingMode = EasingMode.EaseOut }));
        Storyboard.SetTarget(op, PrismAura);
        Storyboard.SetTargetProperty(op, new PropertyPath(OpacityProperty));
        sb.Children.Add(op);

        foreach (string propPath in new[] { PropGroupScaleX, PropGroupScaleY })
        {
            var sc = new DoubleAnimationUsingKeyFrames();
            sc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.76, TimeSpan.Zero));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(1.04, TimeSpan.FromMilliseconds(520), new CubicEase { EasingMode = EasingMode.EaseOut }));
            sc.KeyFrames.Add(new LinearDoubleKeyFrame(1.04, TimeSpan.FromMilliseconds(1260)));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(1.14, TimeSpan.FromMilliseconds(1480), new CubicEase { EasingMode = EasingMode.EaseOut }));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(1.02, TimeSpan.FromMilliseconds(2200), new CubicEase { EasingMode = EasingMode.EaseInOut }));
            Storyboard.SetTarget(sc, PrismAura);
            Storyboard.SetTargetProperty(sc, new PropertyPath(propPath));
            sb.Children.Add(sc);
        }

        var rot = new DoubleAnimation(0.0, 110.0, TimeSpan.FromMilliseconds(2360));
        Storyboard.SetTarget(rot, PrismAura);
        Storyboard.SetTargetProperty(rot, new PropertyPath(PropGroupAngle));
        sb.Children.Add(rot);
    }

    private static void ConfigureProgressiveStrokeLayer(
        Storyboard sb,
        Path path,
        double rawLength,
        TimeSpan beginTime,
        TimeSpan duration,
        double targetOpacity,
        int fadePeakBeginMs,
        double settleOpacity)
    {
        double thickness = Math.Max(path.StrokeThickness, 1.0);
        double dashUnits = (rawLength / thickness) + 0.5;

        path.StrokeDashArray = new DoubleCollection { dashUnits, dashUnits + 4.0 };
        path.StrokeDashOffset = dashUnits;

        var dashAnim = new DoubleAnimation(dashUnits, 0.0, duration)
        {
            BeginTime = beginTime,
            AccelerationRatio = 0.45,
            DecelerationRatio = 0.45
        };
        Storyboard.SetTarget(dashAnim, path);
        Storyboard.SetTargetProperty(dashAnim, new PropertyPath(Shape.StrokeDashOffsetProperty));
        sb.Children.Add(dashAnim);

        var opKf = new DoubleAnimationUsingKeyFrames();
        opKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        opKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(fadePeakBeginMs)));
        opKf.KeyFrames.Add(new EasingDoubleKeyFrame(targetOpacity, TimeSpan.FromMilliseconds(fadePeakBeginMs + 75), new CubicEase { EasingMode = EasingMode.EaseOut }));
        if (Math.Abs(settleOpacity - targetOpacity) > 0.001)
        {
            opKf.KeyFrames.Add(new EasingDoubleKeyFrame(settleOpacity, TimeSpan.FromMilliseconds(1550), new CubicEase { EasingMode = EasingMode.EaseOut }));
        }
        Storyboard.SetTarget(opKf, path);
        Storyboard.SetTargetProperty(opKf, new PropertyPath(OpacityProperty));
        sb.Children.Add(opKf);
    }

    private void AddCometPathAnimation(Storyboard sb, TimeSpan beginTime, TimeSpan duration)
    {
        if (PathSStroke.Data == null)
            return;

        var pathGeom = PathSStroke.Data.GetFlattenedPathGeometry(0.15, ToleranceType.Absolute);
        pathGeom.Freeze();

        var animX = new DoubleAnimationUsingPath
        {
            PathGeometry = pathGeom,
            Source = PathAnimationSource.X,
            BeginTime = beginTime,
            Duration = new Duration(duration),
            AccelerationRatio = 0.45,
            DecelerationRatio = 0.45
        };
        Storyboard.SetTarget(animX, CometTip);
        Storyboard.SetTargetProperty(animX, new PropertyPath(PropTranslateX));
        sb.Children.Add(animX);

        var animY = new DoubleAnimationUsingPath
        {
            PathGeometry = pathGeom,
            Source = PathAnimationSource.Y,
            BeginTime = beginTime,
            Duration = new Duration(duration),
            AccelerationRatio = 0.45,
            DecelerationRatio = 0.45
        };
        Storyboard.SetTarget(animY, CometTip);
        Storyboard.SetTargetProperty(animY, new PropertyPath(PropTranslateY));
        sb.Children.Add(animY);

        var cometOp = new DoubleAnimationUsingKeyFrames();
        cometOp.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        cometOp.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, beginTime));
        cometOp.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, beginTime + TimeSpan.FromMilliseconds(100), new CubicEase { EasingMode = EasingMode.EaseOut }));
        cometOp.KeyFrames.Add(new LinearDoubleKeyFrame(0.98, beginTime + duration - TimeSpan.FromMilliseconds(90)));
        cometOp.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, beginTime + duration + TimeSpan.FromMilliseconds(40), new CubicEase { EasingMode = EasingMode.EaseOut }));
        Storyboard.SetTarget(cometOp, CometTip);
        Storyboard.SetTargetProperty(cometOp, new PropertyPath(OpacityProperty));
        sb.Children.Add(cometOp);
    }

    private void AddEndStarBurst(Storyboard sb)
    {
        const int starStartMs = 1290;
        const int starPeakMs = 1560;
        const int starEndMs = 2340;

        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var groupOp = new DoubleAnimationUsingKeyFrames();
        groupOp.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        groupOp.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(starStartMs)));
        groupOp.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(starStartMs + 140), easeOut));
        groupOp.KeyFrames.Add(new LinearDoubleKeyFrame(0.96, TimeSpan.FromMilliseconds(starPeakMs + 220)));
        groupOp.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(starEndMs), easeInOut));
        Storyboard.SetTarget(groupOp, EndStarGroup);
        Storyboard.SetTargetProperty(groupOp, new PropertyPath(OpacityProperty));
        sb.Children.Add(groupOp);

        // Onda expansiva luminosa
        var rippleOp = new DoubleAnimationUsingKeyFrames();
        rippleOp.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        rippleOp.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.92, TimeSpan.FromMilliseconds(starStartMs + 20)));
        rippleOp.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(starStartMs + 760), easeOut));
        Storyboard.SetTarget(rippleOp, StarRipple);
        Storyboard.SetTargetProperty(rippleOp, new PropertyPath(OpacityProperty));
        sb.Children.Add(rippleOp);

        foreach (string propPath in new[] { PropScaleX, PropScaleY })
        {
            var ripSc = new DoubleAnimationUsingKeyFrames();
            ripSc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.20, TimeSpan.Zero));
            ripSc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.20, TimeSpan.FromMilliseconds(starStartMs + 20)));
            ripSc.KeyFrames.Add(new EasingDoubleKeyFrame(2.55, TimeSpan.FromMilliseconds(starStartMs + 760), easeOut));
            Storyboard.SetTarget(ripSc, StarRipple);
            Storyboard.SetTargetProperty(ripSc, new PropertyPath(propPath));
            sb.Children.Add(ripSc);

            var haloSc = new DoubleAnimationUsingKeyFrames();
            haloSc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.25, TimeSpan.Zero));
            haloSc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.25, TimeSpan.FromMilliseconds(starStartMs)));
            haloSc.KeyFrames.Add(new EasingDoubleKeyFrame(1.40, TimeSpan.FromMilliseconds(starPeakMs), easeOut));
            haloSc.KeyFrames.Add(new EasingDoubleKeyFrame(0.20, TimeSpan.FromMilliseconds(starEndMs), easeInOut));
            Storyboard.SetTarget(haloSc, StarHalo);
            Storyboard.SetTargetProperty(haloSc, new PropertyPath(propPath));
            sb.Children.Add(haloSc);
        }

        // Estrella astroide principal en la punta final
        foreach (string propPath in new[] { PropGroupScaleX, PropGroupScaleY })
        {
            var sc = new DoubleAnimationUsingKeyFrames();
            sc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, TimeSpan.Zero));
            sc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, TimeSpan.FromMilliseconds(starStartMs)));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(1.26, TimeSpan.FromMilliseconds(starPeakMs), easeOut));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(0.10, TimeSpan.FromMilliseconds(starEndMs), easeInOut));
            Storyboard.SetTarget(sc, StarAstroid);
            Storyboard.SetTargetProperty(sc, new PropertyPath(propPath));
            sb.Children.Add(sc);
        }

        var astRot = new DoubleAnimationUsingKeyFrames();
        astRot.KeyFrames.Add(new DiscreteDoubleKeyFrame(-24.0, TimeSpan.Zero));
        astRot.KeyFrames.Add(new DiscreteDoubleKeyFrame(-24.0, TimeSpan.FromMilliseconds(starStartMs)));
        astRot.KeyFrames.Add(new EasingDoubleKeyFrame(44.0, TimeSpan.FromMilliseconds(starEndMs), easeOut));
        Storyboard.SetTarget(astRot, StarAstroid);
        Storyboard.SetTargetProperty(astRot, new PropertyPath(PropGroupAngle));
        sb.Children.Add(astRot);

        // Cruz de destello acompañando a la estrella principal
        foreach (string propPath in new[] { PropGroupScaleX, PropGroupScaleY })
        {
            var sc = new DoubleAnimationUsingKeyFrames();
            sc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, TimeSpan.Zero));
            sc.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, TimeSpan.FromMilliseconds(starStartMs)));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(1.36, TimeSpan.FromMilliseconds(starPeakMs), easeOut));
            sc.KeyFrames.Add(new EasingDoubleKeyFrame(0.10, TimeSpan.FromMilliseconds(starEndMs), easeInOut));
            Storyboard.SetTarget(sc, StarCross);
            Storyboard.SetTargetProperty(sc, new PropertyPath(propPath));
            sb.Children.Add(sc);
        }

        var crossRot = new DoubleAnimationUsingKeyFrames();
        crossRot.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        crossRot.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(starStartMs)));
        crossRot.KeyFrames.Add(new EasingDoubleKeyFrame(52.0, TimeSpan.FromMilliseconds(starEndMs), easeOut));
        Storyboard.SetTarget(crossRot, StarCross);
        Storyboard.SetTargetProperty(crossRot, new PropertyPath(PropGroupAngle));
        sb.Children.Add(crossRot);
    }

    private static void ConfigureSheenSweep(Storyboard sb, Path sheenPath, double rawLength, TimeSpan beginTime, TimeSpan duration)
    {
        double thickness = Math.Max(sheenPath.StrokeThickness, 1.0);
        double totalUnits = rawLength / thickness;
        const double sheenDash = 3.2;
        double sheenGap = totalUnits + 14.0;

        sheenPath.StrokeDashArray = new DoubleCollection { sheenDash, sheenGap };
        sheenPath.StrokeDashOffset = 4.0;

        var sweep = new DoubleAnimation(4.0, -(totalUnits + 2.0), duration)
        {
            BeginTime = beginTime,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(sweep, sheenPath);
        Storyboard.SetTargetProperty(sweep, new PropertyPath(Shape.StrokeDashOffsetProperty));
        sb.Children.Add(sweep);

        var op = new DoubleAnimationUsingKeyFrames();
        op.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
        op.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, beginTime));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.92, beginTime + TimeSpan.FromMilliseconds(160)));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.76, beginTime + TimeSpan.FromMilliseconds(420)));
        op.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, beginTime + duration, new CubicEase { EasingMode = EasingMode.EaseOut }));
        Storyboard.SetTarget(op, sheenPath);
        Storyboard.SetTargetProperty(op, new PropertyPath(OpacityProperty));
        sb.Children.Add(op);
    }

    private void AddWingedGeminiSparkles(Storyboard sb)
    {
        var specs = new List<(Path Target, int BeginMs, double StartAngle, double PeakScale, double WingShiftX, double WingShiftY)>
        {
            (SparkleGemini1,  680, -24.0, 1.26, -10.0,  6.0),
            (SparkleGemini2, 1020,  22.0, 1.28, -10.0,  0.0),
            (SparkleGemini3, 1340, -20.0, 1.24,  -8.0, -6.0),
            (SparkleGemini4,  840,  24.0, 1.26,  10.0, -6.0),
            (SparkleGemini5, 1180, -22.0, 1.26,  10.0,  2.0),
            (SparkleGemini6, 1420,  18.0, 1.22,   8.0,  6.0)
        };

        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

        foreach (var (target, beginMs, startAngle, peakScale, shiftX, shiftY) in specs)
        {
            foreach (string propPath in new[] { PropGroupScaleX, PropGroupScaleY })
            {
                var scaleKf = new DoubleAnimationUsingKeyFrames();
                scaleKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
                scaleKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(beginMs)));
                scaleKf.KeyFrames.Add(new EasingDoubleKeyFrame(peakScale, TimeSpan.FromMilliseconds(beginMs + 260), easeOut));
                scaleKf.KeyFrames.Add(new EasingDoubleKeyFrame(0.95, TimeSpan.FromMilliseconds(beginMs + 520), easeInOut));
                scaleKf.KeyFrames.Add(new EasingDoubleKeyFrame(0.10, TimeSpan.FromMilliseconds(2340), easeInOut));
                Storyboard.SetTarget(scaleKf, target);
                Storyboard.SetTargetProperty(scaleKf, new PropertyPath(propPath));
                sb.Children.Add(scaleKf);
            }

            var rotKf = new DoubleAnimationUsingKeyFrames();
            rotKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(startAngle, TimeSpan.Zero));
            rotKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(startAngle, TimeSpan.FromMilliseconds(beginMs)));
            rotKf.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(beginMs + 560), easeOut));
            Storyboard.SetTarget(rotKf, target);
            Storyboard.SetTargetProperty(rotKf, new PropertyPath(PropGroupAngle));
            sb.Children.Add(rotKf);

            var txKf = new DoubleAnimationUsingKeyFrames();
            txKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(shiftX, TimeSpan.Zero));
            txKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(shiftX, TimeSpan.FromMilliseconds(beginMs)));
            txKf.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(beginMs + 340), easeOut));
            Storyboard.SetTarget(txKf, target);
            Storyboard.SetTargetProperty(txKf, new PropertyPath(PropGroupTranslateX));
            sb.Children.Add(txKf);

            var tyKf = new DoubleAnimationUsingKeyFrames();
            tyKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(shiftY, TimeSpan.Zero));
            tyKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(shiftY, TimeSpan.FromMilliseconds(beginMs)));
            tyKf.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(beginMs + 340), easeOut));
            Storyboard.SetTarget(tyKf, target);
            Storyboard.SetTargetProperty(tyKf, new PropertyPath(PropGroupTranslateY));
            sb.Children.Add(tyKf);

            var opKf = new DoubleAnimationUsingKeyFrames();
            opKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.Zero));
            opKf.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(beginMs)));
            opKf.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(beginMs + 190), easeOut));
            opKf.KeyFrames.Add(new LinearDoubleKeyFrame(0.92, TimeSpan.FromMilliseconds(Math.Max(beginMs + 420, 1780))));
            opKf.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(2340), easeInOut));
            Storyboard.SetTarget(opKf, target);
            Storyboard.SetTargetProperty(opKf, new PropertyPath(OpacityProperty));
            sb.Children.Add(opKf);
        }
    }

    private void BeginSmoothExit(int durationMs)
    {
        if (_exitStoryboard != null || _completedRaised || _isClosing)
            return;

        RaiseReadyToTransitionOnce();

        var exitSb = new Storyboard();

        var fadeOut = new DoubleAnimation(RootStage.Opacity, 0.0, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(fadeOut, RootStage);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        exitSb.Children.Add(fadeOut);

        foreach (string propPath in new[] { PropGroupScaleX, PropGroupScaleY })
        {
            var breathe = new DoubleAnimation(1.0, 1.035, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(breathe, RootStage);
            Storyboard.SetTargetProperty(breathe, new PropertyPath(propPath));
            exitSb.Children.Add(breathe);
        }

        exitSb.Completed += (_, _) => RaiseCompletedOnce();
        _exitStoryboard = exitSb;
        exitSb.Begin(this, isControllable: true);
    }

    private void RaiseReadyToTransitionOnce()
    {
        if (_readyToTransitionRaised)
            return;
        _readyToTransitionRaised = true;
        PresentationReadyToTransition?.Invoke();
    }

    private void RaiseCompletedOnce()
    {
        if (_completedRaised)
            return;
        _completedRaised = true;
        RaiseReadyToTransitionOnce();
        PresentationCompleted?.Invoke();
    }

    private void StopAndDisposeStartupSound()
    {
        try { _startupSoundHandle?.Dispose(); } catch { }
        _startupSoundHandle = null;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _introStoryboard?.Stop(this);
        _exitStoryboard?.Stop(this);
        StopAndDisposeStartupSound();
        RaiseCompletedOnce();
    }
}
