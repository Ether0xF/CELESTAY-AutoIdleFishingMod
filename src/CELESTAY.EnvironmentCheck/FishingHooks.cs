using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Celestay.EnvironmentCheck
{
    internal static class FishingHooks
    {
        internal static void Install(Harmony harmony)
        {
            Patch(harmony, AccessTools.PropertySetter(typeof(FishingEventController), "CurrentState"), null, nameof(StateChanged));
            Patch(harmony, typeof(FishingEventController), "Start", null, nameof(ControllerStarted));
            Patch(harmony, typeof(FishingEventController), "Update", nameof(IdleInput), null);
            Patch(harmony, typeof(FishingEventController), "RestartCoroutine", nameof(SequenceRestart), null);
            Patch(harmony, typeof(FishingEventController), "StartPullingFromActionGame", nameof(SkillPull), null);
            Patch(harmony, typeof(FishingEventController), "OnResultCloseButton", nameof(ResultCloseBefore), nameof(ResultCloseAfter));
            Patch(harmony, typeof(FishingSettingsWindow), "OnClickApply", null, nameof(SettingsApplied));
            Patch(harmony, typeof(FishingUIManager), "ShowExclamationMark", null, nameof(MarkShown));
            Patch(harmony, typeof(FishingUIManager), "ShowResult", null, nameof(ResultShown));
            Patch(harmony, typeof(FishingUIManager), "HideResultPopup", null, nameof(ResultHidden));
            Patch(harmony, typeof(FishingRewardHandler), "AddResource", nameof(AwardBefore), nameof(AwardAfter));
            Patch(harmony, typeof(CraftResource), "AddResource", nameof(ResourceBefore), nameof(ResourceAfter));
            Patch(harmony, typeof(CraftResource), "ConsumeFood", nameof(ResourceBefore), nameof(ResourceAfter));
            Patch(harmony, typeof(CraftResource), "ConsumeResource", nameof(ResourceBefore), nameof(ResourceAfter));
            foreach (string method in new[] { "SaveFish", "SaveMemory", "SaveRecord" })
                Patch(harmony, typeof(SaveDataManager), method, null, nameof(CollectionSaved));
            Patch(harmony, typeof(SaveDataManager), "SaveFishingCount", null, nameof(CountSaved));
            Patch(harmony, typeof(GameUIController), "DisableAllButtons", null, nameof(MainButtonsChanged));
            Patch(harmony, typeof(GameUIController), "EnableAllButtons", null, nameof(MainButtonsChanged));
            Patch(harmony, typeof(GameUIController), "ActivateAll", null, nameof(MainVisibilityChanged));
            Patch(harmony, typeof(GameUIController), "DeactivateAll", null, nameof(MainVisibilityChanged));
            Patch(harmony, typeof(TutorialController), "OnClickStartButton", nameof(TutorialRequested), nameof(TutorialReturned));
            Patch(harmony, typeof(Button), "Press", nameof(ButtonPressed), null);
        }

        private static void Patch(Harmony harmony, Type type, string method, string prefix, string postfix)
            => Patch(harmony, AccessTools.Method(type, method), prefix, postfix);

        private static void Patch(Harmony harmony, MethodBase target, string prefix, string postfix)
        {
            if (target == null) throw new MissingMethodException("An observation hook target could not be found.");
            harmony.Patch(target, prefix: prefix == null ? null : new HarmonyMethod(typeof(FishingHooks), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(FishingHooks), postfix));
        }

        private static void StateChanged(FishingEventController __instance)
        {
            AutoIdleFishing.Safe(value => value.StateChanged(__instance));
            FishingObserver.Safe(value => value.Observe(__instance, "state-setter"));
        }
        private static void ControllerStarted(FishingEventController __instance)
            => FishingObserver.Safe(value => value.Observe(__instance, "Start-complete"));
        private static void IdleInput(FishingEventController __instance)
        {
            FishingObserver.Safe(value =>
            {
                var settings = FishingObserver.Field<FishingSettingsWindow>(__instance, "fishingSettingsWindow");
                if (settings != null && !settings.isFishingActionMode && Input.GetMouseButtonDown(0))
                {
                    value.Observe(__instance, "before-idle-click");
                    value.Event("IdleClick", $"state={__instance.CurrentState},mouse={Input.mousePosition},blocked={(UIBlockManager.Instance == null ? "<unavailable>" : UIBlockManager.Instance.IsAnyUIActive().ToString())}");
                }
            });
        }
        private static void SequenceRestart(FishingEventController __instance)
        {
            AutoIdleFishing.Safe(value => value.Invalidate(__instance, "sequence-restart"));
            FishingObserver.Safe(value => { value.Observe(__instance, "before-restart"); value.Event("SequenceRestart", $"from={__instance.CurrentState}"); });
        }
        private static void SkillPull(FishingEventController __instance)
            => FishingObserver.Safe(value => { value.Observe(__instance, "before-skill-pull"); value.Event("SkillPullRequested", $"from={__instance.CurrentState}"); });
        private static void ResultCloseBefore(FishingEventController __instance)
        {
            AutoIdleFishing.Safe(value => value.Invalidate(__instance, "native-result-close"));
            FishingObserver.Safe(value => { value.Observe(__instance, "before-result-close"); value.Event("ResultCloseBegin", $"state={__instance.CurrentState}"); });
        }
        private static void ResultCloseAfter(FishingEventController __instance)
            => FishingObserver.Safe(value => { value.Observe(__instance, "after-result-close"); value.Event("ResultCloseEnd", $"state={__instance.CurrentState}"); });
        private static void SettingsApplied(FishingSettingsWindow __instance)
            => FishingObserver.Safe(value => { value.Event("SettingsApplyReturned", $"mode={(__instance.isFishingActionMode ? "Skill" : "Idle")},windowActive={__instance.gameObject.activeInHierarchy}"); value.Observe(FishingObserver.Field<FishingEventController>(__instance, "fishingEventController"), "settings-apply-returned"); });
        private static void MarkShown(FishingUIManager __instance)
            => FishingObserver.Safe(value => value.UIEvent(__instance, "BiteMarkShown"));
        private static void ResultShown(FishingUIManager __instance, FishedItem __0)
            => FishingObserver.Safe(value => value.UIEvent(__instance, "ResultShown", __0));
        private static void ResultHidden(FishingUIManager __instance)
        {
            AutoIdleFishing.Safe(value => value.ResultHidden(__instance));
            FishingObserver.Safe(value => value.UIEvent(__instance, "ResultHidden"));
        }
        private static void AwardBefore(FishedItem __0)
            => FishingObserver.Safe(value => value.Event("AwardBegin", $"item=({FishingObserver.Item(__0)}),resources=({FishingObserver.Resources(CraftResource.Instance)})"));
        private static void AwardAfter(FishedItem __0)
            => FishingObserver.Safe(value => value.Event("AwardEnd", $"item=({FishingObserver.Item(__0)}),resources=({FishingObserver.Resources(CraftResource.Instance)})"));
        private static void ResourceBefore(CraftResource __instance, out string __state)
        {
            string before = "<unavailable>";
            FishingObserver.Safe(value => before = FishingObserver.Resources(__instance));
            __state = before;
        }
        private static void ResourceAfter(CraftResource __instance, MethodBase __originalMethod, string __state)
            => FishingObserver.Safe(value => value.Event("ResourceChange", $"method={__originalMethod.Name},before=({__state}),after=({FishingObserver.Resources(__instance)})"));
        private static void CollectionSaved(string __0, MethodBase __originalMethod)
            => FishingObserver.Safe(value => value.Event("CollectionSaved", $"method={__originalMethod.Name},id={__0}"));
        private static void CountSaved(int __0)
            => FishingObserver.Safe(value => value.Event("FishingCountSaved", $"count={__0}"));
        private static void MainButtonsChanged(GameUIController __instance, MethodBase __originalMethod)
            => FishingObserver.Safe(value => { value.Event("MainButtonsChanged", $"method={__originalMethod.Name},object={FishingObserver.PathOf(__instance)}"); value.CurrentSnapshot(__originalMethod.Name); });
        private static void MainVisibilityChanged(GameUIController __instance, MethodBase __originalMethod)
            => FishingObserver.Safe(value => { value.Event("MainUIVisibility", $"method={__originalMethod.Name},object={FishingObserver.PathOf(__instance)}"); value.CurrentSnapshot(__originalMethod.Name); });
        private static void TutorialRequested(TutorialController __instance)
            => FishingObserver.Safe(value => value.Event("TutorialRequested", $"id={__instance.tutorialId},object={FishingObserver.PathOf(__instance)},alreadyCompleted={(SaveDataManager.Instance == null ? "<unavailable>" : SaveDataManager.Instance.LoadTutorial(__instance.tutorialId).ToString())},isMultiple={FishingObserver.Field<bool>(__instance, "isMultiple")}"));
        private static void TutorialReturned(TutorialController __instance)
            => FishingObserver.Safe(value => { var panel = FishingObserver.Field<GameObject>(__instance, "tutorialPanel"); value.Event("TutorialStartReturned", $"id={__instance.tutorialId},panel={FishingObserver.PathOf(panel)},active={(panel != null && panel.activeInHierarchy)}"); value.CurrentSnapshot("tutorial-start-returned"); });
        private static void ButtonPressed(Button __instance)
            => FishingObserver.Safe(value =>
            {
                if (FishingObserver.IsResultButton(__instance))
                    value.Event("ResultButtonPressed", $"button={FishingObserver.PathOf(__instance)},active={__instance.IsActive()},interactable={__instance.IsInteractable()}");
            });
    }
}
