using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace Sts2PathHelper;

internal sealed class MapPlannerController : Node
{
    private sealed class LegendIndicator
    {
        public required NMapLegendItem LegendItem { get; init; }

        public required PanelContainer Root { get; init; }

        public required StyleBoxFlat FrameStyle { get; init; }

        public required ColorRect Accent { get; init; }

        public required MegaLabel CountLabel { get; init; }

        public required MegaLabel PriorityLabel { get; init; }

        public Tween? PulseTween { get; set; }
    }

    private const string ControllerNodeName = "Sts2PathHelper_MapPlanner";

    private const string PointsRootPath = "TheMap/Points";

    private const string LegendItemsPath = "MapLegend/LegendItems";

    private const string LegendIndicatorNodeName = "Sts2PathHelper_LegendIndicator";

    private const float LegendIndicatorWidth = 64f;

    private const float LegendIndicatorHeight = 24f;

    private const float LegendIndicatorIconGap = 1f;

    private static readonly Color PassiveFrameBackgroundColor = new(0.12f, 0.16f, 0.2f, 0.94f);

    private static readonly Color ActiveFrameBackgroundColor = new(0.14f, 0.18f, 0.22f, 0.98f);

    private static readonly Color PassiveFrameBorderColor = new(0.34f, 0.41f, 0.47f, 0.95f);

    private static readonly Color ActiveFrameBorderColor = new(0.48f, 0.56f, 0.62f, 0.98f);

    private static readonly Color PassiveAccentColor = new(0.84f, 0.79f, 0.69f, 0.35f);

    private static readonly Color ActiveAccentColor = new(0.94f, 0.78f, 0.41f, 0.95f);

    private static readonly Color PassiveCountColor = new(0.96f, 0.95f, 0.91f, 0.82f);

    private static readonly Color ActiveCountColor = new(0.99f, 0.97f, 0.91f, 1f);

    private static readonly Color PriorityColor = new(0.96f, 0.82f, 0.45f, 0.98f);

    private readonly Dictionary<MapCoord, NMapPoint> _pointNodes = new();

    private readonly Dictionary<string, int> _routeIndices = new();

    private readonly List<MapPointType> _priorityPointTypes = new();

    private readonly Dictionary<MapPointType, LegendIndicator> _legendIndicators = new();

    private IReadOnlyList<IReadOnlyList<Vector2>>? _lastPlannedStrokes;

    private NMapScreen? _screen;

    private bool _legendSignalsConnected;

    private bool _legendIndicatorRelayoutQueued;

    internal static void AttachTo(NMapScreen screen)
    {
        if (screen.GetNodeOrNull<MapPlannerController>(ControllerNodeName) != null)
        {
            return;
        }

        MapPlannerController controller = new()
        {
            Name = ControllerNodeName
        };
        controller.Initialize(screen);
        screen.AddChild(controller);
        controller.OnAttached();
        Log.Info($"{ModEntry.ModId}: attached route helper controller.", 2);
    }

    internal static MapPlannerController? GetFor(NMapScreen screen)
    {
        return screen.GetNodeOrNull<MapPlannerController>(ControllerNodeName);
    }

    private void Initialize(NMapScreen screen)
    {
        _screen = screen;
    }

    private void OnAttached()
    {
        ConnectLegendSignals();
        EnsureLegendIndicators();
    }

    public void ScheduleRefresh()
    {
        Callable.From(RefreshPreviewNow).CallDeferred();
    }

    public void RefreshPreviewNow()
    {
        ConnectLegendSignals();
        EnsureLegendIndicators();
        RebuildPointCache();
    }

    public void OnLegendItemReleased(string legendItemName)
    {
        if (!TryMapLegendItemToPointType(legendItemName, out MapPointType pointType))
        {
            return;
        }

        bool isRepeatedPriorityClick = _priorityPointTypes.Contains(pointType);
        AddPriority(pointType);

        IReadOnlyList<MapPlannerRoute> routes = MapPlannerService.FindBestRoutes(
            RunManager.Instance.DebugOnlyGetState(),
            _priorityPointTypes);
        if (routes.Count == 0)
        {
            Log.Info($"{ModEntry.ModId}: no routes found for {FormatPrioritySummary()}.", 2);
            return;
        }

        string strategyKey = BuildPriorityKey();
        int nextIndex = _routeIndices.TryGetValue(strategyKey, out int savedIndex)
            ? Mathf.PosMod(savedIndex, routes.Count)
            : 0;
        if (isRepeatedPriorityClick)
        {
            nextIndex = (nextIndex + 1) % routes.Count;
        }

        _routeIndices[strategyKey] = nextIndex;
        MapPlannerRoute route = routes[nextIndex];
        UpdateLegendIndicators(route);
        RequestLegendIndicatorRelayout();
        Log.Info(
            $"{ModEntry.ModId}: legend click '{legendItemName}' -> {FormatPrioritySummary()}, route {nextIndex + 1}/{routes.Count}, counts {FormatCounts(route)}.",
            2);
        DrawRoute(route);
    }

    public void ClearPlanningState()
    {
        _priorityPointTypes.Clear();
        _routeIndices.Clear();
        _lastPlannedStrokes = null;
        ClearLegendIndicators();
    }

    public void ResetForMap()
    {
        ClearPlanningState();
        ScheduleRefresh();
    }

    private void AddPriority(MapPointType pointType)
    {
        if (_priorityPointTypes.Contains(pointType))
        {
            return;
        }

        _priorityPointTypes.Add(pointType);
    }

    private string BuildPriorityKey()
    {
        return string.Join(">", _priorityPointTypes.Select(static pointType => pointType.ToString()));
    }

    private string FormatPrioritySummary()
    {
        return string.Join(" > ", _priorityPointTypes.Select(static pointType => pointType.ToString()));
    }

    private string FormatCounts(MapPlannerRoute route)
    {
        return string.Join(
            ", ",
            _priorityPointTypes.Select(pointType => $"{pointType}={route.GetCount(pointType)}"));
    }

    private void EnsureLegendIndicators()
    {
        if (_screen == null)
        {
            return;
        }

        Control? legendItems = _screen.GetNodeOrNull<Control>(LegendItemsPath);
        if (legendItems == null)
        {
            return;
        }

        foreach (NMapLegendItem item in legendItems.GetChildren().OfType<NMapLegendItem>())
        {
            if (!TryMapLegendItemToPointType(item.Name, out MapPointType pointType) ||
                _legendIndicators.ContainsKey(pointType))
            {
                continue;
            }

            MegaLabel? templateLabel = item.GetNodeOrNull<MegaLabel>("MegaLabel");

            PanelContainer indicatorRoot = new()
            {
                Name = LegendIndicatorNodeName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                Modulate = new Color(1f, 1f, 1f, 0f),
                CustomMinimumSize = new Vector2(LegendIndicatorWidth, LegendIndicatorHeight)
            };
            indicatorRoot.AnchorLeft = 0f;
            indicatorRoot.AnchorRight = 0f;
            indicatorRoot.AnchorTop = 0f;
            indicatorRoot.AnchorBottom = 0f;

            StyleBoxFlat frameStyle = CreateIndicatorFrameStyle();
            indicatorRoot.AddThemeStyleboxOverride("panel", frameStyle);

            MarginContainer contentMargins = new()
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            contentMargins.AddThemeConstantOverride("margin_left", 5);
            contentMargins.AddThemeConstantOverride("margin_right", 5);
            contentMargins.AddThemeConstantOverride("margin_top", 2);
            contentMargins.AddThemeConstantOverride("margin_bottom", 2);

            ColorRect accent = new()
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(2f, 16f),
                Color = PassiveAccentColor
            };

            HBoxContainer rowRoot = new()
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Begin
            };
            rowRoot.AddThemeConstantOverride("separation", 5);

            HBoxContainer textRoot = new()
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Begin
            };
            textRoot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            textRoot.AddThemeConstantOverride("separation", 4);

            MegaLabel priorityLabel = CreateIndicatorLabel(templateLabel, 9, 15, 14f);
            priorityLabel.HorizontalAlignment = HorizontalAlignment.Left;
            priorityLabel.VerticalAlignment = VerticalAlignment.Center;
            priorityLabel.AddThemeColorOverride("font_color", PriorityColor);
            priorityLabel.Text = string.Empty;
            priorityLabel.Visible = false;

            MegaLabel countLabel = CreateIndicatorLabel(templateLabel, 9, 15, 24f);
            countLabel.HorizontalAlignment = HorizontalAlignment.Right;
            countLabel.VerticalAlignment = VerticalAlignment.Center;
            countLabel.AddThemeColorOverride("font_color", PassiveCountColor);
            countLabel.Text = "x0";

            textRoot.AddChild(priorityLabel);
            textRoot.AddChild(countLabel);

            rowRoot.AddChild(accent);
            rowRoot.AddChild(textRoot);
            contentMargins.AddChild(rowRoot);
            indicatorRoot.AddChild(contentMargins);
            legendItems.AddChild(indicatorRoot);

            _legendIndicators[pointType] = new LegendIndicator
            {
                LegendItem = item,
                Root = indicatorRoot,
                FrameStyle = frameStyle,
                Accent = accent,
                CountLabel = countLabel,
                PriorityLabel = priorityLabel
            };
        }

        RelayoutLegendIndicators(legendItems);
        RequestLegendIndicatorRelayout();
        if (_legendIndicators.Count > 0 && _priorityPointTypes.Count == 0)
        {
            ClearLegendIndicators();
        }
    }

    private void ClearLegendIndicators()
    {
        foreach (LegendIndicator indicator in _legendIndicators.Values)
        {
            indicator.PulseTween?.Kill();
            indicator.Root.Visible = false;
            indicator.Root.Scale = Vector2.One;
            indicator.Root.Modulate = new Color(1f, 1f, 1f, 0f);
            indicator.FrameStyle.BgColor = PassiveFrameBackgroundColor;
            indicator.FrameStyle.BorderColor = PassiveFrameBorderColor;
            indicator.Accent.Color = PassiveAccentColor;
            ApplyLabelColor(indicator.CountLabel, PassiveCountColor);
            indicator.CountLabel.SetTextAutoSize("x0");
            ApplyLabelColor(indicator.PriorityLabel, PriorityColor);
            indicator.PriorityLabel.SetTextAutoSize(string.Empty);
            indicator.PriorityLabel.Visible = false;
        }
    }

    private void UpdateLegendIndicators(MapPlannerRoute route)
    {
        EnsureLegendIndicators();
        Control? legendItems = _screen?.GetNodeOrNull<Control>(LegendItemsPath);
        if (legendItems != null)
        {
            RelayoutLegendIndicators(legendItems);
        }
        RequestLegendIndicatorRelayout();

        foreach ((MapPointType pointType, LegendIndicator indicator) in _legendIndicators)
        {
            int count = route.GetCount(pointType);
            int priorityIndex = _priorityPointTypes.IndexOf(pointType);
            bool isPriorityPoint = priorityIndex >= 0;

            indicator.Root.Visible = true;
            indicator.FrameStyle.BgColor = isPriorityPoint
                ? ActiveFrameBackgroundColor
                : PassiveFrameBackgroundColor;
            indicator.FrameStyle.BorderColor = isPriorityPoint
                ? ActiveFrameBorderColor
                : PassiveFrameBorderColor;
            indicator.CountLabel.SetTextAutoSize($"x{count}");
            ApplyLabelColor(
                indicator.CountLabel,
                isPriorityPoint ? ActiveCountColor : PassiveCountColor);

            indicator.PriorityLabel.Visible = isPriorityPoint;
            indicator.PriorityLabel.SetTextAutoSize(isPriorityPoint ? FormatPriorityRank(priorityIndex) : string.Empty);
            indicator.Accent.Color = isPriorityPoint ? ActiveAccentColor : PassiveAccentColor;

            AnimateLegendIndicator(indicator, isPriorityPoint);
        }
    }

    private static StyleBoxFlat CreateIndicatorFrameStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PassiveFrameBackgroundColor,
            BorderColor = PassiveFrameBorderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusBottomLeft = 5,
            ShadowColor = new Color(0f, 0f, 0f, 0.24f),
            ShadowSize = 1
        };
    }

    private void RequestLegendIndicatorRelayout()
    {
        if (_legendIndicatorRelayoutQueued || !IsInsideTree())
        {
            return;
        }

        _legendIndicatorRelayoutQueued = true;
        _ = RelayoutLegendIndicatorsNextFrameAsync();
    }

    private async Task RelayoutLegendIndicatorsNextFrameAsync()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        catch
        {
        }

        _legendIndicatorRelayoutQueued = false;
        if (!IsInsideTree() || _screen == null)
        {
            return;
        }

        Control? legendItems = _screen.GetNodeOrNull<Control>(LegendItemsPath);
        if (legendItems == null)
        {
            return;
        }

        RelayoutLegendIndicators(legendItems);
    }

    private void RelayoutLegendIndicators(Control legendItems)
    {
        foreach (LegendIndicator indicator in _legendIndicators.Values)
        {
            PositionLegendIndicator(indicator.Root, legendItems, indicator.LegendItem);
        }
    }

    private static void PositionLegendIndicator(
        PanelContainer indicatorRoot,
        Control legendItems,
        NMapLegendItem item)
    {
        float rightEdge = legendItems.Size.X > 0f
            ? Mathf.Round(legendItems.Size.X - LegendIndicatorIconGap)
            : Mathf.Round(item.Position.X + item.Size.X + LegendIndicatorWidth);
        float rowCenterY = item.Size.Y > 0f
            ? Mathf.Round(item.Position.Y + item.Size.Y * 0.5f)
            : Mathf.Round(item.Position.Y + LegendIndicatorHeight * 0.5f);
        float halfHeight = LegendIndicatorHeight * 0.5f;

        indicatorRoot.OffsetRight = rightEdge;
        indicatorRoot.OffsetLeft = rightEdge - LegendIndicatorWidth;
        indicatorRoot.OffsetTop = rowCenterY - halfHeight;
        indicatorRoot.OffsetBottom = rowCenterY + halfHeight;
    }

    private static MegaLabel CreateIndicatorLabel(
        MegaLabel? templateLabel,
        int minFontSize,
        int maxFontSize,
        float minWidth)
    {
        MegaLabel label = new()
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = minFontSize,
            MaxFontSize = maxFontSize,
            ClipText = false,
            CustomMinimumSize = new Vector2(minWidth, 12f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
        };
        if (templateLabel != null)
        {
            label.Theme = templateLabel.Theme;
            label.ThemeTypeVariation = templateLabel.ThemeTypeVariation;
            label.LabelSettings = templateLabel.LabelSettings;
            label.AutowrapMode = templateLabel.AutowrapMode;
        }

        return label;
    }

    private static void ApplyLabelColor(MegaLabel label, Color color)
    {
        label.RemoveThemeColorOverride("font_color");
        label.AddThemeColorOverride("font_color", color);
    }

    private static string FormatPriorityRank(int priorityIndex)
    {
        return priorityIndex switch
        {
            0 => "I",
            1 => "II",
            2 => "III",
            3 => "IV",
            4 => "V",
            5 => "VI",
            _ => (priorityIndex + 1).ToString()
        };
    }

    private static void AnimateLegendIndicator(LegendIndicator indicator, bool isPriorityPoint)
    {
        indicator.PulseTween?.Kill();
        indicator.Root.Scale = isPriorityPoint ? new Vector2(0.97f, 0.97f) : new Vector2(0.985f, 0.985f);
        indicator.Root.Modulate = new Color(1f, 1f, 1f, isPriorityPoint ? 0.88f : 0.72f);

        Tween tween = indicator.Root.CreateTween().SetParallel();
        tween.TweenProperty(indicator.Root, "scale", Vector2.One, isPriorityPoint ? 0.18 : 0.12)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(indicator.Root, "modulate", Colors.White, isPriorityPoint ? 0.18 : 0.12)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        indicator.PulseTween = tween;
    }

    private void ConnectLegendSignals()
    {
        if (_screen == null || _legendSignalsConnected)
        {
            return;
        }

        Control? legendItems = _screen.GetNodeOrNull<Control>(LegendItemsPath);
        if (legendItems == null)
        {
            return;
        }

        foreach (NMapLegendItem item in legendItems.GetChildren().OfType<NMapLegendItem>())
        {
            string legendItemName = item.Name;
            item.Released += _ => OnLegendItemReleased(legendItemName);
        }

        _legendSignalsConnected = true;
        Log.Info($"{ModEntry.ModId}: connected legend item callbacks.", 2);
    }

    private void DrawRoute(MapPlannerRoute route)
    {
        if (_screen == null || route.Points.Count <= 1)
        {
            return;
        }

        RebuildPointCache();
        NMapDrawings drawings = _screen.Drawings;

        Dictionary<MapCoord, Vector2> centers = new();
        Dictionary<MapCoord, float> radii = new();
        foreach (MapPoint point in route.Points)
        {
            if (TryGetDrawingPoint(point.coord, out Vector2 drawingPoint, out float radius))
            {
                centers[point.coord] = drawingPoint;
                radii[point.coord] = radius;
            }
        }

        if (centers.Count <= 1)
        {
            return;
        }

        List<IReadOnlyList<Vector2>> strokes = BuildStrokes(route, centers, radii);
        if (strokes.Count == 0)
        {
            return;
        }

        RedrawPlannedRoute(drawings, strokes);
    }

    private void RedrawPlannedRoute(NMapDrawings drawings, IReadOnlyList<IReadOnlyList<Vector2>> strokes)
    {
        if (_lastPlannedStrokes != null)
        {
            foreach (IReadOnlyList<Vector2> stroke in _lastPlannedStrokes)
            {
                ReplayRoute(drawings, stroke, DrawingMode.Erasing);
            }
        }

        foreach (IReadOnlyList<Vector2> stroke in strokes)
        {
            ReplayRoute(drawings, stroke, DrawingMode.Drawing);
        }

        _lastPlannedStrokes = strokes.Select(static stroke => (IReadOnlyList<Vector2>)stroke.ToArray()).ToArray();
    }

    private static void ReplayRoute(NMapDrawings drawings, IReadOnlyList<Vector2> drawPoints, DrawingMode drawingMode)
    {
        if (drawPoints.Count <= 1)
        {
            return;
        }

        drawings.BeginLineLocal(drawPoints[0], drawingMode);
        for (int index = 1; index < drawPoints.Count; index++)
        {
            drawings.UpdateCurrentLinePositionLocal(drawPoints[index]);
        }

        drawings.StopLineLocal();
    }

    private void RebuildPointCache()
    {
        _pointNodes.Clear();
        if (_screen == null)
        {
            return;
        }

        Control? pointsRoot = _screen.GetNodeOrNull<Control>(PointsRootPath);
        if (pointsRoot == null)
        {
            return;
        }

        foreach (NMapPoint pointNode in pointsRoot.GetChildren().OfType<NMapPoint>())
        {
            _pointNodes[pointNode.Point.coord] = pointNode;
        }
    }

    private List<IReadOnlyList<Vector2>> BuildStrokes(
        MapPlannerRoute route,
        IReadOnlyDictionary<MapCoord, Vector2> centers,
        IReadOnlyDictionary<MapCoord, float> radii)
    {
        List<IReadOnlyList<Vector2>> strokes = new();

        for (int index = 0; index < route.Points.Count - 1; index++)
        {
            MapPoint fromPoint = route.Points[index];
            MapPoint toPoint = route.Points[index + 1];
            if (!centers.TryGetValue(fromPoint.coord, out Vector2 fromCenter) ||
                !centers.TryGetValue(toPoint.coord, out Vector2 toCenter))
            {
                continue;
            }

            float fromRadius = index == 0 ? 0f : radii[fromPoint.coord];
            float toRadius = radii[toPoint.coord];
            Vector2 direction = toCenter - fromCenter;
            if (direction.LengthSquared() < 1f)
            {
                continue;
            }

            Vector2 normal = direction.Normalized();
            Vector2 lineStart = fromCenter + normal * fromRadius;
            Vector2 lineEnd = toCenter - normal * toRadius;
            strokes.Add(new[] { lineStart, lineEnd });
        }

        for (int index = 1; index < route.Points.Count; index++)
        {
            MapPoint point = route.Points[index];
            if (!centers.TryGetValue(point.coord, out Vector2 center) ||
                !radii.TryGetValue(point.coord, out float radius))
            {
                continue;
            }

            strokes.Add(BuildCircleStroke(center, radius));
        }

        return strokes;
    }

    private static IReadOnlyList<Vector2> BuildCircleStroke(Vector2 center, float radius)
    {
        const int segments = 16;
        List<Vector2> stroke = new(segments + 1);
        for (int index = 0; index <= segments; index++)
        {
            float angle = Mathf.Tau * index / segments;
            stroke.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        return stroke;
    }

    private bool TryGetDrawingPoint(MapCoord coord, out Vector2 drawingPoint, out float radius)
    {
        drawingPoint = Vector2.Zero;
        radius = 28f;
        if (_screen == null || !_pointNodes.TryGetValue(coord, out NMapPoint? pointNode))
        {
            return false;
        }

        Vector2 localCenter = pointNode.PivotOffset;
        if (localCenter == Vector2.Zero)
        {
            localCenter = pointNode.Size * 0.5f;
        }

        Vector2 globalPoint = pointNode.GetGlobalTransform() * localCenter;
        drawingPoint = _screen.Drawings.GetGlobalTransform().AffineInverse() * globalPoint;
        radius = pointNode.Size.X > 0f || pointNode.Size.Y > 0f
            ? Mathf.Max(Mathf.Max(pointNode.Size.X, pointNode.Size.Y) * 0.52f, 34f)
            : 34f;
        return true;
    }

    private static bool TryMapLegendItemToPointType(string legendItemName, out MapPointType pointType)
    {
        pointType = legendItemName switch
        {
            "UnknownLegendItem" => MapPointType.Unknown,
            "MerchantLegendItem" => MapPointType.Shop,
            "TreasureLegendItem" => MapPointType.Treasure,
            "RestSiteLegendItem" => MapPointType.RestSite,
            "EnemyLegendItem" => MapPointType.Monster,
            "EliteLegendItem" => MapPointType.Elite,
            _ => MapPointType.Unassigned
        };

        return pointType != MapPointType.Unassigned;
    }
}
