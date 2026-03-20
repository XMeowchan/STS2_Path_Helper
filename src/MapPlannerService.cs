using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2PathHelper;

internal static class MapPlannerService
{
    public static IReadOnlyList<MapPlannerRoute> FindBestRoutes(RunState? runState, MapPointType targetPointType)
    {
        return FindBestRoutes(runState, new[] { targetPointType });
    }

    public static IReadOnlyList<MapPlannerRoute> FindBestRoutes(
        RunState? runState,
        IReadOnlyList<MapPointType> prioritizedPointTypes)
    {
        MapPointType[] normalizedPriority = NormalizePriority(prioritizedPointTypes);
        if (runState == null || normalizedPriority.Length == 0)
        {
            return Array.Empty<MapPlannerRoute>();
        }

        MapPoint startPoint = runState.CurrentMapPoint ?? runState.Map.StartingMapPoint;
        if (startPoint.Children.Count == 0)
        {
            return Array.Empty<MapPlannerRoute>();
        }

        List<MapPlannerRoute> allRoutes = new();
        List<MapPoint> path = new() { startPoint };
        foreach (MapPoint child in startPoint.Children.OrderBy(static point => point.coord.col))
        {
            Explore(child, path, allRoutes);
        }

        if (allRoutes.Count == 0)
        {
            return Array.Empty<MapPlannerRoute>();
        }

        MapPlannerRoute bestRoute = allRoutes[0];
        foreach (MapPlannerRoute route in allRoutes.Skip(1))
        {
            if (ComparePriority(route, bestRoute, normalizedPriority) > 0)
            {
                bestRoute = route;
            }
        }

        return allRoutes
            .Where(route => ComparePriority(route, bestRoute, normalizedPriority) == 0)
            .OrderBy(route => route.Signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Explore(
        MapPoint point,
        IList<MapPoint> path,
        ICollection<MapPlannerRoute> routes)
    {
        path.Add(point);

        if (point.Children.Count == 0)
        {
            List<MapPoint> points = new(path);
            routes.Add(new MapPlannerRoute(points, BuildPointTypeCounts(points), BuildSignature(points)));
        }
        else
        {
            foreach (MapPoint child in point.Children.OrderBy(static child => child.coord.col))
            {
                Explore(child, path, routes);
            }
        }

        path.RemoveAt(path.Count - 1);
    }

    private static int ComparePriority(
        MapPlannerRoute left,
        MapPlannerRoute right,
        IReadOnlyList<MapPointType> prioritizedPointTypes)
    {
        foreach (MapPointType pointType in prioritizedPointTypes)
        {
            int comparison = left.GetCount(pointType).CompareTo(right.GetCount(pointType));
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static IReadOnlyDictionary<MapPointType, int> BuildPointTypeCounts(IReadOnlyList<MapPoint> points)
    {
        Dictionary<MapPointType, int> counts = new();
        foreach (MapPoint point in points.Skip(1))
        {
            counts.TryGetValue(point.PointType, out int currentCount);
            counts[point.PointType] = currentCount + 1;
        }

        return counts;
    }

    private static MapPointType[] NormalizePriority(IEnumerable<MapPointType> prioritizedPointTypes)
    {
        HashSet<MapPointType> seen = new();
        List<MapPointType> normalized = new();
        foreach (MapPointType pointType in prioritizedPointTypes)
        {
            if (!IsSupportedPriority(pointType) || !seen.Add(pointType))
            {
                continue;
            }

            normalized.Add(pointType);
        }

        return normalized.ToArray();
    }

    private static bool IsSupportedPriority(MapPointType pointType)
    {
        return pointType is not MapPointType.Unassigned and not MapPointType.Ancient and not MapPointType.Boss;
    }

    private static string BuildSignature(IEnumerable<MapPoint> points)
    {
        return string.Join(
            "->",
            points.Select(static point => $"{point.coord.col:D2}:{point.coord.row:D2}"));
    }
}
