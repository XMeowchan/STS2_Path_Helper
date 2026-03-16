using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2PathHelper;

internal static class MapPlannerService
{
    public static IReadOnlyList<MapPlannerRoute> FindBestRoutes(RunState? runState, MapPointType targetPointType)
    {
        if (runState == null || targetPointType is MapPointType.Unassigned or MapPointType.Ancient or MapPointType.Boss)
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
            Explore(child, targetPointType, path, 0, allRoutes);
        }

        if (allRoutes.Count == 0)
        {
            return Array.Empty<MapPlannerRoute>();
        }

        int maxCount = allRoutes.Max(static route => route.TargetCount);
        return allRoutes
            .Where(route => route.TargetCount == maxCount)
            .OrderBy(route => route.Signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Explore(
        MapPoint point,
        MapPointType targetPointType,
        IList<MapPoint> path,
        int currentCount,
        ICollection<MapPlannerRoute> routes)
    {
        path.Add(point);
        int updatedCount = currentCount + (point.PointType == targetPointType ? 1 : 0);

        if (point.Children.Count == 0)
        {
            List<MapPoint> points = new(path);
            routes.Add(new MapPlannerRoute(points, targetPointType, updatedCount, BuildSignature(points)));
        }
        else
        {
            foreach (MapPoint child in point.Children.OrderBy(static child => child.coord.col))
            {
                Explore(child, targetPointType, path, updatedCount, routes);
            }
        }

        path.RemoveAt(path.Count - 1);
    }

    private static string BuildSignature(IEnumerable<MapPoint> points)
    {
        return string.Join(
            "->",
            points.Select(static point => $"{point.coord.col:D2}:{point.coord.row:D2}"));
    }
}
