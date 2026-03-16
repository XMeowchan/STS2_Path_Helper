using MegaCrit.Sts2.Core.Map;

namespace Sts2PathHelper;

internal sealed class MapPlannerRoute
{
    public MapPlannerRoute(IReadOnlyList<MapPoint> points, MapPointType targetPointType, int targetCount, string signature)
    {
        Points = points;
        TargetPointType = targetPointType;
        TargetCount = targetCount;
        Signature = signature;
    }

    public IReadOnlyList<MapPoint> Points { get; }

    public MapPointType TargetPointType { get; }

    public int TargetCount { get; }

    public string Signature { get; }
}
