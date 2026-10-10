using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Celestay.EnvironmentCheck
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EnvironmentCheckPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.celestay.environmentcheck";
        public const string PluginName = "CELESTAY Environment Check";
        public const string PluginVersion = "0.3.0";

        private bool initialized;
        private bool firstUpdateLogged;
        private Harmony harmony;
        private FishingObserver observer;
        private AutoIdleFishing automation;

        private void Awake()
        {
            try
            {
                Logger.LogInfo($"Plugin: {PluginName} v{PluginVersion} (GUID: {PluginGuid})");
                Logger.LogInfo($"Plugin DLL: {typeof(EnvironmentCheckPlugin).Assembly.Location}");
                Logger.LogInfo($"Unity version: {Application.unityVersion}");
                Logger.LogInfo($"Unity platform: {Application.platform}");
                Logger.LogInfo($"Process bitness: {IntPtr.Size * 8}-bit");
                observer = new FishingObserver(Logger);
                FishingObserver.Current = observer;
                harmony = new Harmony(PluginGuid + ".observation");
                FishingHooks.Install(harmony);
                var autoEnabled = Config.Bind("AutoIdleFishing", "Enabled", true,
                    "Automatically pull in Idle mode and close the native result popup. Skill mode is untouched.");
                var closeDelay = Config.Bind("AutoIdleFishing", "ResultCloseDelaySeconds", 1f,
                    new BepInEx.Configuration.ConfigDescription("Delay after the native result popup appears (after animation and rewards).",
                        new BepInEx.Configuration.AcceptableValueRange<float>(0f, 60f)));
                automation = new AutoIdleFishing(observer, autoEnabled, closeDelay);
                AutoIdleFishing.Current = automation;
                observer.Event("ObservationReady", "Observation hooks installed.");
                observer.Event("AutoReady", $"enabled={autoEnabled.Value},resultCloseDelaySeconds={closeDelay.Value}; Idle mode only");
                Logger.LogInfo("Plugin initialization complete.");
                initialized = true;
            }
            catch (Exception exception)
            {
                Logger.LogError($"Plugin initialization failed:\n{exception}");
                harmony?.UnpatchSelf();
                FishingObserver.Current = null;
                AutoIdleFishing.Current = null;
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (initialized)
            {
                FishingObserver.Safe(value => value.Poll());
                AutoIdleFishing.Safe(value => value.Tick(observer.Controller));
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            FishingObserver.Safe(value => value.Event("Focus", $"focused={hasFocus}"));
        }

        private void OnApplicationPause(bool paused)
        {
            FishingObserver.Safe(value => value.Event("Pause", $"paused={paused}"));
        }

        private void OnDestroy()
        {
            if (FishingObserver.Current == observer)
                FishingObserver.Current = null;
            if (AutoIdleFishing.Current == automation)
                AutoIdleFishing.Current = null;
            harmony?.UnpatchSelf();
        }

        private void Update()
        {
            if (!initialized || firstUpdateLogged)
                return;

            // Mark the callback before logging so an error cannot cause repeated output.
            firstUpdateLogged = true;
            try
            {
                Logger.LogInfo("First Update callback received.");
            }
            catch (Exception exception)
            {
                Logger.LogError($"First Update callback logging failed:\n{exception}");
            }
        }
    }
}
