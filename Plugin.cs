﻿using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using JetBrains.Annotations;
using ServerSync;
using Settlers.Managers;
using Shipwright.Solution;
using CraftingTable = ItemManager.CraftingTable;

namespace Shipwright
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ShipwrightPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Shipwright";
        internal const string ModVersion = "1.0.1";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource ShipwrightLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<int> _materialAmount = null!;
        public static ConfigEntry<string> _material = null!;
        public static ConfigEntry<float> _repairAmount = null!;
        public static ConfigEntry<float> _staminaCost = null!;
        public static ConfigEntry<float> _repairDuration = null!;
        public static ConfigEntry<Toggle> _usePlaceEffects = null!;
        public static ConfigEntry<Toggle> _canDeconstruct = null!;
        public static ConfigEntry<float> _deconstructDuration = null!;
        public static ConfigEntry<Toggle> _useDurability = null!;
        
        public static ConfigEntry<Toggle> _useShipCustomize = null!;
        public static ConfigEntry<Toggle> _useShipTent = null!;
        public static ConfigEntry<Toggle> _useTraderLamp = null!;
        public static ConfigEntry<Toggle> _useStorage = null!;
        public static ConfigEntry<Toggle> _useShields = null!;

        public static bool m_balrondShipyardInstalled;

        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            
            _material = config("2 - Settings", "Material", "Wood", "Set the material requirements to repair while on water");
            _materialAmount = config("2 - Settings", "Material Amount", 1, new ConfigDescription("Set the amount of material needed to repair ship, multiplied by ship health", new AcceptableValueRange<int>(0, 999)));
            _repairAmount = config("2 - Settings", "Repair Amount", 0.1f, new ConfigDescription("Set the health percentage amount for each repair", new AcceptableValueRange<float>(0f, 1f)));
            _staminaCost = config("2 - Settings", "Stamina Cost", 5f, new ConfigDescription("Set the amount of stamina needed to repair once", new AcceptableValueRange<float>(0f, 50f)));
            _repairDuration = config("2 - Settings", "Repair Duration", 1f, new ConfigDescription("Set the duration to load repair hammer, in seconds, multiplied by the quality of the tool", new AcceptableValueRange<float>(1f, 101f)));
            _usePlaceEffects = config("2 - Settings", "Repair Effects", Toggle.On, "If on, upon repair, effects are triggered");
            _canDeconstruct = config("2 - Settings", "Can Deconstruct", Toggle.Off, "If on, using secondary attack, player can deconstruct");
            _deconstructDuration = config("2 - Settings", "Deconstruct Duration", 10f, new ConfigDescription("Set the duration to deconstruct, in seconds, multiplied by quality of the tool", new AcceptableValueRange<float>(1f, 101f)));
            _useDurability = config("2 - Settings", "Use Durability", Toggle.On, "If on, each use of tool uses durability");

            _useShipCustomize = config("3 - Longship", "Extra Visuals", Toggle.Off,
                "If on, viking ship will have extra visuals enabled");
            _useShipCustomize.SettingChanged += (sender, args) =>
            {
                foreach (var ship in ShipCustomize.m_instances)
                {
                    ship.SetCustomize(_useShipCustomize.Value is Toggle.On);
                }
            };

            _useShipTent = config("3 - Longship", "Use Tent", Toggle.Off, "If on, tent is enabled");
            _useShipTent.SettingChanged += (sender, args) =>
            {
                foreach (var ship in ShipCustomize.m_instances)
                {
                    ship.SetTent(_useShipTent.Value is Toggle.On);
                }
            };
            _useTraderLamp = config("3 - Longship", "Use Lamp", Toggle.Off, "If on, lamp is enabled");
            _useTraderLamp.SettingChanged += (sender, args) =>
            {
                foreach (var ship in ShipCustomize.m_instances)
                {
                    ship.SetLamp(_useTraderLamp.Value is Toggle.On);
                }
            };
            _useStorage = config("3 - Longship", "Use Storage", Toggle.Off, "If on, storage is enabled");
            _useStorage.SettingChanged += (sender, args) =>
            {
                foreach (var ship in ShipCustomize.m_instances)
                {
                    ship.SetStorage(_useStorage.Value is Toggle.On);
                }
            };
            _useShields = config("3 - Longship", "Use Shields", Toggle.Off, "If on, shields are enabled");
            _useShields.SettingChanged += (sender, args) =>
            {
                foreach (var ship in ShipCustomize.m_instances)
                {
                    ship.SetShields(_useShields.Value is Toggle.On);
                }
            };
        }
        public void Awake()
        {
            InitConfigs();
            Localizer.Load();
            Item HammerBucket = new Item("hammerbucketbundle", "HammerBucket");
            HammerBucket.Name.English("Shipwright Hammer");
            HammerBucket.Description.English("Repair ships without the need of a workstation");
            HammerBucket.RequiredItems.Add("Wood", 10);
            HammerBucket.RequiredItems.Add("Tin", 5);
            HammerBucket.RequiredUpgradeItems.Add("Wood", 5);
            HammerBucket.RequiredUpgradeItems.Add("Tin", 1);
            HammerBucket.CraftAmount = 1;
            HammerBucket.Crafting.Add(CraftingTable.Forge, 1);
            HammerBucket.triggerEffects = new() { "sfx_build_hammer_metal" };

            if (Chainloader.PluginInfos.ContainsKey("balrond.astafaraios.BalrondShipyard"))
            {
                ShipwrightLogger.LogInfo("Balrond Shipyard Installed, disabling extra visuals");
                m_balrondShipyardInstalled = true;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void Update()
        {
            Repair.UpdateHammerDraw();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ShipwrightLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ShipwrightLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ShipwrightLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions


        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion
    }
}