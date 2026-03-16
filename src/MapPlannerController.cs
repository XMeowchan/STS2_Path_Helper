using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2PathHelper;

internal sealed class MapPlannerController : Node
{
    private const string ControllerNodeName = "Sts2PathHelper_MapPlanner";

    private const string PointsRootPath = "TheMap/Points";

    private const string LegendItemsPath = "MapLegend/LegendItems";

    private readonly Dictionary<MapCoord, NMapPoint> _pointNodes = new();

    private readonly Dictionary<MapPointType, int> _routeIndices = new();

    private IReadOnlyList<IReadOnlyList<Vector2>>? _lastPlannedStrokes;

    private NMapScreen? _screen;

    private bool _legendSignalsConnected;

    private MapPointType _selectedPointType = MapPointType.Unassigned;

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
    }

    public void ScheduleRefresh()
    {
        Callable.From(RefreshPreviewNow).CallDeferred();
    }

    public void RefreshPreviewNow()
    {
        ConnectLegendSignals();
        if (_selectedPointType == MapPointType.Unassigned)
        {
            return;
        }

        DrawSelectedRoute(resetCycleOnMissing: false);
    }

    public void OnLegendItemReleased(string legendItemName)
    {
        if (!TryMapLegendItemToPointType(legendItemName, out MapPointType pointType))
        {
            return;
        }

        bool isRepeatedClick = _selectedPointType == pointType;
        _selectedPointType = pointType;

        IReadOnlyList<MapPlannerRoute> routes = MapPlannerService.FindBestRoutes(
            RunManager.Instance.DebugOnlyGetState(),
            pointType);
        if (routes.Count == 0)
        {
            Log.Info($"{ModEntry.ModId}: no routes found for {pointType}.", 2);
            return;
        }

        int nextIndex = 0;
        if (isRepeatedClick && _routeIndices.TryGetValue(pointType, out int currentIndex))
        {
            nextIndex = (currentIndex + 1) % routes.Count;
        }

        _routeIndices[pointType] = nextIndex;
        Log.Info($"{ModEntry.ModId}: legend click '{legendItemName}' -> {pointType}, route {nextIndex + 1}/{routes.Count}.", 2);
        DrawRoute(routes[nextIndex]);
    }

    private void DrawSelectedRoute(bool resetCycleOnMissing)
    {
        IReadOnlyList<MapPlannerRoute> routes = MapPlannerService.FindBestRoutes(
            RunManager.Instance.DebugOnlyGetState(),
            _selectedPointType);
        if (routes.Count == 0)
        {
            if (resetCycleOnMissing)
            {
                _routeIndices.Remove(_selectedPointType);
            }

            return;
        }

        int index = _routeIndices.TryGetValue(_selectedPointType, out int savedIndex)
            ? Mathf.PosMod(savedIndex, routes.Count)
            : 0;
        _routeIndices[_selectedPointType] = index;
        DrawRoute(routes[index]);
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
            ? Mathf.Max(pointNode.Size.X, pointNode.Size.Y) * 0.38f
            : 28f;
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
