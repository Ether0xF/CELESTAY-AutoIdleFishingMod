using System;
using BepInEx.Configuration;
using UnityCustomExtension.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Celestay.EnvironmentCheck
{
    internal sealed class AutoIdleFishing
    {
        internal static AutoIdleFishing Current;
        private readonly FishingObserver observer;
        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<float> closeDelay;
        private FishingEventController controller;
        private GameObject pendingPopup;
        private bool pendingClose;
        private bool closeAttempted;
        private float closeAt;
        private bool faulted;

        internal AutoIdleFishing(FishingObserver observer, ConfigEntry<bool> enabled, ConfigEntry<float> closeDelay)
        {
            this.observer = observer;
            this.enabled = enabled;
            this.closeDelay = closeDelay;
        }

        // Never propagate an automation error through a Harmony hook or retry a partial action.
        internal static void Safe(Action<AutoIdleFishing> action)
        {
            var current = Current;
            if (current == null || current.faulted) return;
            try { action(current); }
            catch (Exception exception)
            {
                current.faulted = true;
                current.pendingClose = false;
                try { current.observer.Event("AutoError", $"Automation disabled for this session: {exception}"); }
                catch { }
            }
        }

        internal void Invalidate(FishingEventController value, string reason)
        {
            if (value == controller) Cancel(reason);
        }

        internal void StateChanged(FishingEventController value)
        {
            if (value == controller && value.CurrentState != FishingEventController.FishingState.resulting)
                Cancel("state=" + value.CurrentState);
        }

        internal void ResultHidden(FishingUIManager value)
        {
            if (controller != null && FishingObserver.Field<FishingUIManager>(controller, "fishingUIManager") == value)
                Cancel("result-hidden");
        }

        private void Cancel(string reason)
        {
            bool hadPending = pendingClose;
            pendingClose = false;
            pendingPopup = null;
            closeAttempted = false;
            if (hadPending) observer.Event("AutoCloseCancelled", "reason=" + reason);
        }

        internal void Tick(FishingEventController value)
        {
            if (controller != value)
            {
                Cancel("controller-changed");
                controller = value;
            }
            if (!enabled.Value || value == null || !value.isActiveAndEnabled ||
                !value.gameObject.scene.IsValid() || !value.gameObject.scene.isLoaded ||
                GameUIController.Instance == null || !GameUIController.Instance.isActiveAndEnabled ||
                Time.timeScale <= 0f)
            {
                Cancel("disabled-or-context-unavailable");
                return;
            }
            var settings = FishingObserver.Field<FishingSettingsWindow>(value, "fishingSettingsWindow");
            var ui = FishingObserver.Field<FishingUIManager>(value, "fishingUIManager");
            var blocks = UIBlockManager.Instance;
            if (settings == null || ui == null || blocks == null || settings.isFishingActionMode)
            {
                Cancel("skill-or-context-unavailable");
                return;
            }

            if (value.CurrentState == FishingEventController.FishingState.chance)
            {
                Cancel("chance");
                var mark = FishingObserver.Field<GameObject>(ui, "exclamationMark");
                if (mark == null || !mark.activeInHierarchy || blocks.IsAnyUIActive()) return;
                observer.Event("AutoBiteDetected", $"controller={FishingObserver.PathOf(value)}");
                // Match Idle Update: preserve the running fishing coroutine and its reward path.
                value.CurrentState = FishingEventController.FishingState.pulling;
                var audio = FishingObserver.Field<AudioSource>(value, "clickToPullAS");
                var clips = DontDestroyOnLoadSingletonMonoBehaviour<AudioClipDictionary>.Instance;
                if (audio != null && clips != null) clips.Play("openCraft", audio);
                observer.Event("AutoPull", "chance->pulling; native coroutine continues");
                return;
            }

            if (value.CurrentState != FishingEventController.FishingState.resulting)
            {
                Cancel("state=" + value.CurrentState);
                return;
            }
            var popup = FishingObserver.Field<GameObject>(ui, "resultPopupUI");
            if (popup == null || !popup.activeInHierarchy || !FishingObserver.Field<bool>(value, "resultShown"))
            {
                Cancel("result-not-ready");
                return;
            }
            if (HasOtherBlocker(blocks, popup))
            {
                Cancel("other-ui-blocks-result");
                return;
            }
            if (closeAttempted) return;
            if (!pendingClose || pendingPopup != popup)
            {
                pendingPopup = popup;
                pendingClose = true;
                float delay = closeDelay.Value;
                if (float.IsNaN(delay) || float.IsInfinity(delay)) delay = 1f;
                delay = Mathf.Clamp(delay, 0f, 60f);
                closeAt = Time.realtimeSinceStartup + delay;
                observer.Event("AutoCloseScheduled", $"delaySeconds={FishingObserver.Number(delay)},popup={FishingObserver.PathOf(popup)}");
                // Always defer at least one frame, even with a configured zero delay.
                return;
            }
            if (Time.realtimeSinceStartup < closeAt) return;

            Button button = FindCloseButton(popup, value);
            if (button == null) return;
            // Consume before invoking listeners; synchronous close hooks may clear the pending action.
            pendingClose = false;
            closeAttempted = true;
            observer.Event("AutoCloseInvoked", $"button={FishingObserver.PathOf(button)}");
            button.onClick.Invoke();
            observer.Event("AutoCloseCompleted", $"state={value.CurrentState},popupActive={(popup != null && popup.activeInHierarchy)}");
        }

        private static bool HasOtherBlocker(UIBlockManager blocks, GameObject popup)
        {
            GameObject[] panels = FishingObserver.Field<GameObject[]>(blocks, "uiPanels");
            if (panels == null) return true;
            foreach (GameObject panel in panels)
            {
                if (panel == null || !panel.activeInHierarchy) continue;
                // An ancestor of the result may also contain menus; do not exempt ancestors.
                if (panel == popup || panel.transform.IsChildOf(popup.transform)) continue;
                return true;
            }
            return false;
        }

        private static Button FindCloseButton(GameObject popup, FishingEventController value)
        {
            foreach (Button button in popup.GetComponentsInChildren<Button>(includeInactive: false))
            {
                if (!button.IsActive() || !button.IsInteractable()) continue;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == value &&
                        button.onClick.GetPersistentMethodName(i) == "OnResultCloseButton" &&
                        button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off)
                        return button;
                }
            }
            return null;
        }
    }
}
