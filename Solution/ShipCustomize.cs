using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Shipwright.Solution;

public class ShipCustomize : MonoBehaviour
{
    public static readonly List<ShipCustomize> m_instances = new();

    private GameObject m_customize = null!;
    private GameObject m_shipTentBeam = null!;
    private GameObject m_shipTent = null!;
    private GameObject m_shipTentHolders1 = null!;
    private GameObject m_shipTentHolders2 = null!;
    private GameObject m_traderLamp = null!;
    private GameObject m_storage = null!;
    private readonly List<GameObject> m_crates = new();
    private readonly List<GameObject> m_shields = new();
    public void Awake()
    {
        var objects = transform.Find("ship/visual/Customize");
        if (objects == null) return;
        m_customize = objects.gameObject;
        m_shipTentBeam = objects.Find("ShipTen2_beam").gameObject;
        m_shipTent = objects.Find("ShipTen2 (1)").gameObject;
        m_shipTentHolders1 = objects.Find("ShipTentHolders").gameObject;
        m_shipTentHolders2 = objects.Find("ShipTentHolders (1)").gameObject;
        m_traderLamp = objects.Find("TraderLamp").gameObject;
        m_storage = objects.Find("storage").gameObject;
        foreach (Transform obj in m_storage.transform)
        {
            if (obj.name.StartsWith("Shield"))
            {
                m_shields.Add(obj.gameObject);
            }
            else
            {
                m_crates.Add(obj.gameObject);
            }
        }
        DisableAll();
        
        m_instances.Add(this);
        
        SetCustomize(ShipwrightPlugin._useShipCustomize.Value is ShipwrightPlugin.Toggle.On);
        SetTent(ShipwrightPlugin._useShipTent.Value is ShipwrightPlugin.Toggle.On);
        SetLamp(ShipwrightPlugin._useTraderLamp.Value is ShipwrightPlugin.Toggle.On);
        SetStorage(ShipwrightPlugin._useStorage.Value is ShipwrightPlugin.Toggle.On);
        SetShields(ShipwrightPlugin._useShields.Value is ShipwrightPlugin.Toggle.On);
    }

    public void SetCustomize(bool enable) => m_customize.SetActive(enable);

    public void SetTent(bool enable)
    {
        m_shipTentBeam.SetActive(enable);
        m_shipTent.SetActive(enable);
        m_shipTentHolders1.SetActive(enable);
        m_shipTentHolders2.SetActive(enable);
    }

    public void SetLamp(bool enable) => m_traderLamp.SetActive(enable);

    public void SetStorage(bool enable)
    {
        foreach (var item in m_crates) item.SetActive(enable);
    }

    public void SetShields(bool enable)
    {
        foreach (var item in m_shields) item.SetActive(enable);
    }

    public void DisableAll()
    {
        SetTent(false);
        SetLamp(false);
        SetStorage(false);
        SetShields(false);
    }

    public void OnDestroy() => m_instances.Remove(this);
    
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;
            var vikingShip = __instance.GetPrefab("VikingShip");
            if (!vikingShip) return;
            vikingShip.AddComponent<ShipCustomize>();
        }
    }
}