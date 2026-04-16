using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DataCenterCrosshair.Patches;

/// <summary>
/// Shifts the pointer label when Unity assigns <see cref="RectTransform.anchoredPosition"/>.
/// Uses a <b>prefix</b> with <c>ref Vector2 value</c> so the offset is applied to the same write the game intended
/// (no second setter call, no re-entrancy).
/// </summary>
[HarmonyPatch]
public static class PointerLabelAnchoredHarmony
{
    /// <summary>Assign in <see cref="Mod.OnUpdate"/> before game UI runs so setter patches see a non-null target.</summary>
    public static RectTransform Target { get; internal set; }

    /// <summary>Il2CppInterop can produce different managed wrappers for the same native <see cref="RectTransform"/>; instance id matches setter <paramref name="__instance"/> reliably.</summary>
    public static int TargetInstanceId { get; internal set; } = int.MinValue;

    private static MethodBase TargetMethod()
    {
        return AccessTools.PropertySetter(typeof(RectTransform), nameof(RectTransform.anchoredPosition));
    }

    [HarmonyPrefix]
    public static void Prefix(RectTransform __instance, ref Vector2 value)
    {
        if (TargetInstanceId == int.MinValue)
            return;

        if (__instance.GetInstanceID() != TargetInstanceId)
            return;

        if (!CrosshairSettings.PointerTextEnabled)
            return;

        var o = new Vector2(CrosshairSettings.PointerTextOffsetX, CrosshairSettings.PointerTextOffsetY);
        if (o.sqrMagnitude < 1e-12f)
            return;

        value += o;
    }

    internal static void ClearWrittenCache()
    {
        TargetInstanceId = int.MinValue;
    }
}
