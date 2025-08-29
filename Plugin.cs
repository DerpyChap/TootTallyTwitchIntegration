using BaboonAPI.Hooks.Initializer;
using BaboonAPI.Hooks.Tracks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using TootTallyAccounts;
using TootTallyCore;
using TootTallyCore.APIServices;
using TootTallyCore.Graphics;
using TootTallyCore.Utils.Assets;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyModules;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallySettings;
using TrombLoader.CustomTracks;
using UnityEngine;

namespace TootTallyTwitchIntegration
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallyCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallySettings", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallyAccounts", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "TwitchIntegration.cfg";
        private const string PERSISTENT_TWITCH_CONFIG_NAME = "TootTallyTwitchIntegration.cfg";
        private const string CONFIG_FIELD = "Twitch";
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "Twitch Integration"; set => Name = value; }

        public static TootTallySettingPage settingPage;
        public static TwitchRequestController TwitchRequestController;
        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);
        public static void LogDebug(string msg) => Instance.Logger.LogDebug(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "Twitch", true, "Twitch integration with song requests and more.");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            AssetManager.LoadAssets(Path.Combine(Path.GetDirectoryName(Instance.Info.Location), "Assets"));
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            ToggleRequestPanel = config.Bind(CONFIG_FIELD, "TogglePanel", KeyCode.F8, "Key to toggle the twitch request panel.");
            EnableRequestsCommand = config.Bind(CONFIG_FIELD, "Enable requests command", true, "Allow people to requests songs using !ttr [songID]");
            EnableCurrentSongCommand = config.Bind(CONFIG_FIELD, "Enable current song command", true, "!song command that sends a link to the current song into the chat");
            EnableProfileCommand = config.Bind(CONFIG_FIELD, "Enable profile command", true, "!profile command that links your toottally profile into the chat");
            SubOnlyMode = config.Bind(CONFIG_FIELD, "Sub-only requests", false, "Only allow subscribers to send requests");
            MaxRequestCount = config.Bind(CONFIG_FIELD, "Max Request Count", 50f, "Maximum request count allowed in queue");

            settingPage = TootTallyTwitchLibs.Plugin.SettingPage;
            if (settingPage != null)
            {
                settingPage.AddLabel("TRequestConfigLabel", "Twitch Requests Configs", 40);
                settingPage.AddLabel("ToggleKeybindLabel", "Keybind Toggle Request Panel", 20);
                settingPage.AddDropdown("Toggle Request Panel", ToggleRequestPanel);
                settingPage.AddToggle("Enable Requests Command", EnableRequestsCommand);
                settingPage.AddToggle("Enable Current Songs Command", EnableCurrentSongCommand);
                settingPage.AddToggle("Enable Profile Command", EnableProfileCommand);
                settingPage.AddToggle("Subs Only Requests", SubOnlyMode);
                settingPage.AddSlider("Max Request Count", 0, 200, MaxRequestCount, true);
            }

            TootTallySettings.Plugin.TryAddThunderstoreIconToPageButton(Instance.Info.Location, Name, settingPage);
            ThemeManager.OnThemeRefreshEvents += RequestPanelManager.UpdateTheme;
            TwitchRequestController = TootTallyTwitchLibs.Plugin.Instance.gameObject.AddComponent<TwitchRequestController>();
            _harmony.PatchAll(typeof(RequestPanelManager));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            ThemeManager.OnThemeRefreshEvents -= RequestPanelManager.UpdateTheme;
            GameObject.DestroyImmediate(TwitchRequestController);
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public ConfigEntry<KeyCode> ToggleRequestPanel { get; set; }
        public ConfigEntry<bool> EnableRequestsCommand { get; set; }
        public ConfigEntry<bool> EnableProfileCommand { get; set; }
        public ConfigEntry<bool> EnableCurrentSongCommand { get; set; }
        public ConfigEntry<bool> SubOnlyMode { get; set; }
        public ConfigEntry<float> MaxRequestCount { get; set; }
    }
}