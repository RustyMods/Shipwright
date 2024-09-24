using HarmonyLib;
using UnityEngine;

namespace Shipwright.Solution;

public static class HoverPiece
{
    private static bool IsCorrectTool(ItemDrop.ItemData toolItem) => toolItem.m_shared.m_name == "$item_hammerbucket";

    [HarmonyPatch(typeof(Player), nameof(Player.UpdateWearNTearHover))]
    private static class Player_UpdateWearNTearHover_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (!__instance || __instance.InPlaceMode()) return;
            ItemDrop.ItemData? tool = __instance.m_rightItem;
            if (tool == null) return;
            if (!IsCorrectTool(tool)) return;

            __instance.m_hoveringPiece = null;
            if (!Physics.Raycast(GameCamera.instance.transform.position, GameCamera.instance.transform.forward,
                    out RaycastHit hitInfo, 50f, __instance.m_removeRayMask) ||
                Vector3.Distance(__instance.m_eye.position, hitInfo.point) >=
                (double)__instance.m_maxPlaceDistance)
                return;
            Piece componentInParent = hitInfo.collider.GetComponentInParent<Piece>();
            if (!componentInParent) return;
            
            if (!componentInParent.m_waterPiece) return;
            __instance.m_hoveringPiece = componentInParent;
            if (!componentInParent.TryGetComponent(out WearNTear component)) return;
            component.Highlight();
        }
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
    private static class Hud_UpdateCrossHair_Patch
    {
        private static void Postfix(Hud __instance, Player player)
        {
            var toolItem = player.m_rightItem;
            if (toolItem == null) return;
            if (!IsCorrectTool(toolItem)) return;

            Piece hoveringPiece = player.m_hoveringPiece;
            if (hoveringPiece == null)
            {
                __instance.m_pieceHealthRoot.gameObject.SetActive(false);
                __instance.m_crosshairBow.gameObject.SetActive(false);
                Repair.ResetDrawTime(toolItem);
                return;
            }

            if (!hoveringPiece.TryGetComponent(out WearNTear component))
            {
                __instance.m_pieceHealthRoot.gameObject.SetActive(false);
                __instance.m_crosshairBow.gameObject.SetActive(false);
                Repair.ResetDrawTime(toolItem);
                return;
            }
            
            __instance.m_pieceHealthRoot.gameObject.SetActive(true);
            __instance.m_pieceHealthBar.SetValue(component.GetHealthPercentage());
            var drawPercentage = Repair.GetRepairDrawPercentage(toolItem, Repair.m_isSecondary);
            float num = Mathf.Lerp(1f, 0.15f, drawPercentage);
            __instance.m_crosshairBow.gameObject.SetActive(true);
            __instance.m_crosshairBow.transform.localScale = new Vector3(num, num, num);
            __instance.m_crosshairBow.color = Color.Lerp(Color.clear, Color.yellow, drawPercentage);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    private static class Container_GetHoverText_Patch
    {
        private static void Postfix(ref string __result)
        {
            if (!Player.m_localPlayer) return;
            var tool = Player.m_localPlayer.m_rightItem;
            if (tool == null) return;
            if (!IsCorrectTool(tool)) return;
            __result = "";
        }
    }
    
    [HarmonyPatch(typeof(Chair), nameof(Chair.GetHoverText))]
    private static class Chair_GetHoverText_Patch
    {
        private static void Postfix(ref string __result)
        {
            if (!Player.m_localPlayer) return;
            var tool = Player.m_localPlayer.m_rightItem;
            if (tool == null) return;
            if (!IsCorrectTool(tool)) return;
            __result = "";
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    private static class Container_Interact_Patch
    {
        private static bool Prefix()
        {
            if (!Player.m_localPlayer) return true;
            var tool = Player.m_localPlayer.m_rightItem;
            if (tool == null) return true;
            return !IsCorrectTool(tool);
        }
    }
    [HarmonyPatch(typeof(Chair), nameof(Chair.Interact))]
    private static class Chair_Interact_Patch
    {
        private static bool Prefix()
        {
            if (!Player.m_localPlayer) return true;
            var tool = Player.m_localPlayer.m_rightItem;
            if (tool == null) return true;
            return !IsCorrectTool(tool);
        }
    }
    
}