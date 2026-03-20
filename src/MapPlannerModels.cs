using MegaCrit.Sts2.Core.Map;

namespace Sts2PathHelper;

internal sealed class MapPlannerRoute
{
    public MapPlannerRoute(
        IReadOnlyList<MapPoint> points,
        IReadOnlyDictionary<MapPointType, int> pointTypeCounts,
        string signature)
    {
        Points = points;
        PointTypeCounts = pointTypeCounts;
        Signature = signature;
    }

    public IReadOnlyList<MapPoint> Points { get; }

    public IReadOnlyDictionary<MapPointType, int> PointTypeCounts { get; }

    public string Signature { get; }

    public int GetCount(MapPointType pointType)
    {
        return PointTypeCounts.TryGetValue(pointType, out int count) ? count : 0;
    }
}
