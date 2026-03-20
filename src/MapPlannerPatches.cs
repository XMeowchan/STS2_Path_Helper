using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace Sts2PathHelper;

[HarmonyPatch]
internal static class MapPlannerPatches
{
    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Ready))]
    [HarmonyPostfix]
    private static void MapScreenReadyPostfix(NMapScreen __instance)
    {
        MapPlannerController.AttachTo(__instance);
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
    [HarmonyPostfix]
    private static void MapScreenSetMapPostfix(NMapScreen __instance)
    {
        MapPlannerController.GetFor(__instance)?.ResetForMap();
    }

    [HarmonyPatch(typeof(NMapScreen), "OnClearMapDrawingButtonPressed")]
    [HarmonyPostfix]
    private static void MapScreenClearMapDrawingButtonPressedPostfix(NMapScreen __instance)
    {
        MapPlannerController.GetFor(__instance)?.ClearPlanningState();
    }
}
