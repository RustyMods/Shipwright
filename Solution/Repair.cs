﻿using HarmonyLib;
using UnityEngine;

namespace Shipwright.Solution;

public static class Repair
{
    public static bool m_isSecondary;
    
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    private static class Humanoid_StartAttack_Patch
    {
        private static bool Prefix(Humanoid __instance, bool secondaryAttack, ref bool __result)
        {
            if (__instance is not Player player) return true;
            ItemDrop.ItemData? toolItem = player.m_rightItem;
            if (toolItem == null) return true;
            m_isSecondary = secondaryAttack;
            if (!IsCorrectTool(toolItem))
            {
                ResetDrawTime(toolItem);
                return true;
            }
            __result = false;
            if (secondaryAttack && ShipwrightPlugin._canDeconstruct.Value is ShipwrightPlugin.Toggle.Off) return false;
            Piece? hoveringPiece = player.m_hoveringPiece;
            if (hoveringPiece == null)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_missinghoverpiece");
                return false;
            }
            if (!hoveringPiece.TryGetComponent(out WearNTear component)) return false;
            // component.m_nview.ClaimOwnership();
            var currentHealth = component.m_nview.GetZDO().GetFloat(ZDOVars.s_health, component.m_health);

            if (!secondaryAttack)
            {
                if (currentHealth >= component.m_health)
                {
                    ResetDrawTime(toolItem);
                    return false;
                }
                
                if (!CanRepair(player))
                {
                    ResetDrawTime(toolItem);
                    return false;
                }
            }
            if (!IsLoaded(toolItem)) return false;
            
            if (!secondaryAttack)
            {
                if (!UseMaterial(player)) return false;
                var repairAmount = component.m_health * ShipwrightPlugin._repairAmount.Value;
                var newHealth = Mathf.Clamp(currentHealth + repairAmount, 1f, component.m_health);
                RepairAmount(component, newHealth);
                player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_shiphealth: " + $" {(int)newHealth}/{(int)component.m_health}"));
            }
            else
            {
                component.Destroy();
            }
            
            UseTool(hoveringPiece, player, toolItem);
            __result = true;
            return false;
        }
    }
    
    public static void UpdateHammerDraw()
    {
        if (!Player.m_localPlayer) return;
        var toolItem = Player.m_localPlayer.m_rightItem;
        if (toolItem == null) return;
        if (!IsCorrectTool(toolItem)) return;
        if (Player.m_localPlayer is { m_attack: false, m_attackHold: false, m_secondaryAttack: false, m_secondaryAttackHold: false }) ResetDrawTime(toolItem);
    }
    
    private static bool IsCorrectTool(ItemDrop.ItemData toolItem) => toolItem.m_shared.m_name == "$item_hammerbucket";
    public static float GetRepairDrawPercentage(ItemDrop.ItemData tool, bool secondary) => Mathf.Clamp01(tool.m_shared.m_attack.m_attackDrawPercentage /
        (secondary ? ShipwrightPlugin._deconstructDuration.Value : ShipwrightPlugin._repairDuration.Value));
    public static void ResetDrawTime(ItemDrop.ItemData tool) => tool.m_shared.m_attack.m_attackDrawPercentage = 0f;

    private static bool CanRepair(Player player)
    {
        if (!HasMaterial(player)) return false;
        if (!player.HaveStamina(ShipwrightPlugin._staminaCost.Value)) return false;
        if (player.InAttack() && player.HaveQueuedChain()) return false;
        if (player.InDodge() || !player.CanMove() || player.IsKnockedBack() || player.IsStaggering() || player.InMinorAction()) return false;

        return true;
    }

    private static bool HasMaterial(Player player)
    {
        if (ShipwrightPlugin._materialAmount.Value == 0) return true;
        ItemDrop mat = GetUseMaterial();
        var name = mat.m_itemData.m_shared.m_name;
        if (!player.GetInventory().HaveItem(name))
        {
            player.Message(MessageHud.MessageType.Center, "$msg_missingmat: " + $" {name}");
            return false;
        }

        var playerAmount = player.GetInventory().CountItems(name);
        if (playerAmount < ShipwrightPlugin._materialAmount.Value)
        {
            player.Message(MessageHud.MessageType.Center, "$msg_missingmat: " + $" {ShipwrightPlugin._materialAmount.Value}x {name}");
            return false;
        }

        return true;
    }

    private static bool UseMaterial(Player player)
    {
        if (ShipwrightPlugin._materialAmount.Value == 0) return true;
        if (!HasMaterial(player)) return false;
        ItemDrop mat = GetUseMaterial();
        var name = mat.m_itemData.m_shared.m_name;
        player.GetInventory().RemoveItem(name, ShipwrightPlugin._materialAmount.Value);
        return true;
    }

    private static ItemDrop GetUseMaterial()
    {
        GameObject material = ZNetScene.instance.GetPrefab(ShipwrightPlugin._material.Value);
        var mat = !material ? ZNetScene.instance.GetPrefab("Wood") : material;
        return mat.GetComponent<ItemDrop>();
    }

    private static void UseTool(Piece hoveringPiece, Player player, ItemDrop.ItemData toolItem)
    {
        player.FaceLookDirection();
        player.m_zanim.SetTrigger(toolItem.m_shared.m_attack.m_attackAnimation);

        var transform = hoveringPiece.transform;
        if (ShipwrightPlugin._usePlaceEffects.Value is ShipwrightPlugin.Toggle.On) hoveringPiece.m_placeEffect.Create(transform.position, transform.rotation);
        player.UseStamina(ShipwrightPlugin._staminaCost.Value, true);
        var transform1 = player.transform;
        toolItem.m_shared.m_triggerEffect.Create(transform1.position, transform1.rotation);
        if (ShipwrightPlugin._useDurability.Value is ShipwrightPlugin.Toggle.Off) return;
        toolItem.m_durability -= toolItem.m_shared.m_useDurabilityDrain;
    }

    private static void RepairAmount(WearNTear component, float amount)
    {
        component.m_nview.GetZDO().Set(ZDOVars.s_health, amount);
        component.m_nview.InvokeRPC(nameof(WearNTear.RPC_HealthChanged), amount);
    }

    private static bool IsLoaded(ItemDrop.ItemData tool)
    {
        tool.m_shared.m_attack.m_attackDrawPercentage += Time.deltaTime * tool.m_quality;
        if (GetRepairDrawPercentage(tool, m_isSecondary) < 1f) return false;
        tool.m_shared.m_attack.m_attackDrawPercentage = 0f;
        return true;
    }
}