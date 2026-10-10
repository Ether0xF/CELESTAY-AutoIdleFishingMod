using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace Celestay.EnvironmentCheck
{
    internal sealed class FishingObserver
    {
        internal static FishingObserver Current;
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private readonly ManualLogSource logger;
        private readonly string session = Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly HashSet<string> reportedErrors = new HashSet<string>();
        private readonly Dictionary<int, string> buttonSnapshots = new Dictionary<int, string>();
        private FishingEventController controller;
        private string controllerPath;
        private string lastState;
        private string lastSnapshot;
        private float nextPoll;
        private float nextDiscovery;
        private int sequence;

        internal FishingObserver(ManualLogSource logger) { this.logger = logger; }
        internal FishingEventController Controller => controller;

        // Hook failures must not escape into the game's original methods.
        internal static void Safe(Action<FishingObserver> action)
        {
            FishingObserver current = Current;
            if (current == null) return;
            try { action(current); }
            catch (Exception exception)
            {
                string key = exception.GetType().FullName + ":" + exception.Message;
                if (current.reportedErrors.Add(key))
                {
                    try { current.logger.LogError($"[FishingTrace] ObserverError (repeats suppressed):\n{exception}"); }
                    catch { /* Logging failure must not affect gameplay. */ }
                }
            }
        }

        internal void Event(string name, string details)
        {
            logger.LogInfo($"[FishingTrace session={session} seq={++sequence} frame={Time.frameCount} t={Number(Time.realtimeSinceStartup)}] {name}: {details}");
        }

        internal static T Field<T>(object target, string name)
        {
            if (target == null || (target is UnityEngine.Object unityObject && unityObject == null))
                return default(T);
            Type type = target.GetType();
            string key = type.FullName + "." + name;
            if (!Fields.TryGetValue(key, out FieldInfo field))
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) throw new MissingFieldException(type.FullName, name);
                Fields.Add(key, field);
            }
            return (T)field.GetValue(target);
        }

        internal static string Number(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        internal static string PathOf(UnityEngine.Object target)
        {
            if (target == null) return "<none>";
            Transform transform = target is GameObject gameObject ? gameObject.transform : (target as Component)?.transform;
            if (transform == null) return target.name + "#" + target.GetInstanceID();
            var parts = new List<string>();
            Transform cursor = transform;
            while (cursor != null)
            {
                parts.Add(cursor.name + "[" + cursor.GetSiblingIndex() + "]");
                cursor = cursor.parent;
            }
            parts.Reverse();
            return transform.gameObject.scene.name + ":/" + string.Join("/", parts) + "#" + target.GetInstanceID();
        }

        internal static string Item(FishedItem item)
        {
            return item == null ? "<none>" : $"id={item.ID},genre={item.genre},food={item.foodAmount},wood={item.woodAmount},steel={item.steelAmount}";
        }

        internal static string Resources(CraftResource value)
        {
            return value == null ? "<unavailable>" : $"food={value.GetResourceAmount(CraftResource.ResourceType.Food)},wood={value.GetResourceAmount(CraftResource.ResourceType.Wood)},steel={value.GetResourceAmount(CraftResource.ResourceType.Steel)}";
        }

        internal void Observe(FishingEventController value, string reason)
        {
            if (value == null) return;
            if (controller != value)
            {
                controller = value;
                controllerPath = PathOf(value);
                lastState = null;
                lastSnapshot = null;
                buttonSnapshots.Clear();
                Event("ControllerBound", $"reason={reason},object={controllerPath},resources=({Resources(CraftResource.Instance)})");
            }
            string state = value.CurrentState.ToString();
            if (lastState != state)
            {
                Event("State", $"{lastState ?? "<initial>"}->{state},reason={reason},controller={controllerPath},count={Field<int>(value, "fishingCount")}");
                lastState = state;
            }
            Snapshot(reason);
        }

        internal void Poll()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + 0.25f;
            if (controller == null)
            {
                if (controllerPath != null)
                {
                    Event("ControllerLost", controllerPath);
                    controllerPath = null;
                    lastSnapshot = null;
                }
                if (now < nextDiscovery) return;
                nextDiscovery = now + 1f;
                FishingEventController[] found = UnityEngine.Object.FindObjectsByType<FishingEventController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (found.Length > 0) Observe(found[0], "discovery");
            }
            else Observe(controller, "poll");
        }

        internal void CurrentSnapshot(string reason)
        {
            if (controller != null) Observe(controller, reason);
        }

        private void Snapshot(string reason)
        {
            if (controller == null) return;
            var settings = Field<FishingSettingsWindow>(controller, "fishingSettingsWindow");
            var ui = Field<FishingUIManager>(controller, "fishingUIManager");
            var slider = Field<FishingUI_Slider>(controller, "fishingUI_Slider");
            var reel = Field<Reel>(controller, "reel");
            var rates = Field<FishingRateCalculator>(controller, "fishingRateCalculator");
            var popup = Field<GameObject>(ui, "resultPopupUI");
            var mark = Field<GameObject>(ui, "exclamationMark");
            var blocks = UIBlockManager.Instance;
            var main = GameUIController.Instance;
            string mode = settings == null ? "<unavailable>" : (settings.isFishingActionMode ? "Skill" : "Idle");
            string panels = ActivePanels(blocks, popup);
            string buttons = MainButtons(main);
            string signature = $"controllerActive={controller.isActiveAndEnabled},state={controller.CurrentState},mode={mode},depth={(slider == null ? "<none>" : slider.Depth.ToString())},lure={Field<int>(rates, "currentLureID")},reelRate={(reel == null ? "<none>" : Number(reel.GetCurrentReelRate()))},timer={Field<bool>(controller, "isWaitingTimerActive")},waitSeconds={Number(Field<float>(controller, "fishingWaitTime"))},resultShown={Field<bool>(controller, "resultShown")},heldItem=({Item(Field<FishedItem>(controller, "currentFishedItem"))}),popup=({ObjectState(popup)}),mark=({ObjectState(mark)}),blocked={(blocks == null ? "<unavailable>" : blocks.IsAnyUIActive().ToString())},activeBlockUI=[{panels}],blockOverlay=({ObjectState(Field<GameObject>(blocks, "blockPanel"))}),mainUI=({ObjectState(main == null ? null : main.gameObject)}),mainButtons=[{buttons}],focused={Application.isFocused},timeScale={Number(Time.timeScale)}";
            if (signature != lastSnapshot)
            {
                lastSnapshot = signature;
                float remaining = Field<bool>(controller, "isWaitingTimerActive") ? Mathf.Max(0f, Field<float>(controller, "waitEndTime") - Time.time) : 0f;
                Event("Snapshot", $"reason={reason},remainingSeconds={Number(remaining)},{signature}");
            }
            ResultButtons(popup);
        }

        private static string ObjectState(GameObject value)
        {
            return value == null ? "<none>" : $"path={PathOf(value)},active={value.activeInHierarchy},self={value.activeSelf}";
        }

        private static string ActivePanels(UIBlockManager value, GameObject popup)
        {
            var paths = new List<string>();
            GameObject[] panels = Field<GameObject[]>(value, "uiPanels");
            if (panels != null)
            {
                foreach (GameObject panel in panels)
                {
                    if (panel == null || !panel.activeInHierarchy) continue;
                    string relation = "other";
                    if (popup != null)
                    {
                        if (panel == popup) relation = "result";
                        else if (panel.transform.IsChildOf(popup.transform)) relation = "inside-result";
                        else if (popup.transform.IsChildOf(panel.transform)) relation = "contains-result";
                    }
                    paths.Add(PathOf(panel) + ":" + relation);
                }
            }
            paths.Sort(StringComparer.Ordinal);
            return string.Join(";", paths);
        }

        private static string MainButtons(GameUIController value)
        {
            if (value == null) return "<unavailable>";
            var results = new List<string>();
            foreach (string name in new[] { "menuBtn", "albumBtn", "questBtn", "craftBtn", "soundBtn", "boxBtn", "poolBtn", "fishSettingsBtn" })
            {
                Button button = Field<Button>(value, name);
                results.Add(name + "=" + (button == null ? "<none>" : $"active:{button.gameObject.activeInHierarchy}/enabled:{button.enabled}/interactable:{button.interactable}/effective:{button.IsInteractable()}"));
            }
            return string.Join(";", results);
        }

        internal static bool IsResultButton(Button button)
        {
            if (button == null) return false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) is FishingEventController && button.onClick.GetPersistentMethodName(i) == "OnResultCloseButton")
                    return true;
            }
            return false;
        }

        private void ResultButtons(GameObject popup)
        {
            if (popup == null) return;
            foreach (Button button in popup.GetComponentsInChildren<Button>(includeInactive: true))
            {
                if (!IsResultButton(button)) continue;
                var calls = new List<string>();
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                    calls.Add($"{i}:{(target == null ? "<missing>" : target.GetType().FullName)}.{button.onClick.GetPersistentMethodName(i)}@{PathOf(target)}:state={button.onClick.GetPersistentListenerState(i)}:matchesController={target == controller}");
                }
                string signature = $"path={PathOf(button)},active={button.gameObject.activeInHierarchy},enabled={button.enabled},interactable={button.interactable},effective={button.IsInteractable()},persistentCalls=[{string.Join(";", calls)}]";
                int id = button.GetInstanceID();
                if (!buttonSnapshots.TryGetValue(id, out string previous) || previous != signature)
                {
                    buttonSnapshots[id] = signature;
                    Event("ResultButton", signature);
                }
            }
        }

        internal void UIEvent(FishingUIManager value, string name, FishedItem item = null)
        {
            Event(name, $"ui={PathOf(value)},item=({Item(item)})");
            if (controller != null && Field<FishingUIManager>(controller, "fishingUIManager") == value)
                Observe(controller, name);
        }
    }
}
