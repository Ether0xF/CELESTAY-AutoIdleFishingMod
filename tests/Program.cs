// Simulates player-loop ordering and native callbacks without launching Unity or modifying saves.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using Celestay.EnvironmentCheck;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Test(string name, Action<Fixture> action)
    {
        var fixture = new Fixture();
        action(fixture);
        passed++;
        Console.WriteLine("PASS " + name);
    }
    private static void Main()
    {
        Test("Idle bite pulls once and plays the native cue", f =>
        {
            f.Controller.CurrentState = FishingEventController.FishingState.chance;
            f.Tick(); f.Tick();
            Check(f.Controller.CurrentState == FishingEventController.FishingState.pulling, "did not pull");
            Check(f.Observer.Count("AutoPull") == 1 && f.Audio.Plays == 1, "duplicate pull or cue");
        });
        Test("Skill, disabled automation, and missing mark do not pull", f =>
        {
            f.Controller.CurrentState = FishingEventController.FishingState.chance;
            f.Settings.isFishingActionMode = true; f.Tick();
            f.Settings.isFishingActionMode = false; f.Enabled.Value = false; f.Tick();
            f.Enabled.Value = true; f.UI.exclamationMark.activeInHierarchy = false; f.Tick();
            Check(f.Observer.Count("AutoPull") == 0, "guard bypassed");
        });
        Test("Menus block a bite until closed", f =>
        {
            f.Controller.CurrentState = FishingEventController.FishingState.chance;
            f.Menu.activeInHierarchy = true; f.Tick();
            Check(f.Controller.CurrentState == FishingEventController.FishingState.chance, "pulled behind menu");
            f.Menu.activeInHierarchy = false; f.Tick();
            Check(f.Observer.Count("AutoPull") == 1, "did not resume");
        });
        Test("No close during animation or before result is ready", f =>
        {
            f.Controller.CurrentState = FishingEventController.FishingState.pulling; f.Tick();
            f.Controller.CurrentState = FishingEventController.FishingState.resulting; f.Tick();
            f.UI.resultPopupUI.activeInHierarchy = true; f.Tick();
            Check(f.Observer.Count("AutoCloseScheduled") == 0 && f.CallbackOrder.Count == 0, "premature close");
        });
        Test("Result blocker is allowed, delay and full listener chain execute once", f =>
        {
            f.Ready(); f.Tick(); f.Tick(0.999f);
            Check(f.CallbackOrder.Count == 0, "delay shortened");
            f.Tick(1f); f.Tick(2f);
            Check(string.Join(",", f.CallbackOrder) == "close,audio,buttons,tutorial", "missing or duplicate listener");
            Check(f.Observer.Count("AutoCloseInvoked") == 1, "duplicate close");
        });
        Test("Early manual close cancels the old deadline for the next round", f =>
        {
            f.Ready(); f.Tick(); f.CloseManually();
            f.Tick(0.5f); f.Ready(); f.Tick(); f.Tick(1f);
            Check(f.Observer.Count("AutoCloseInvoked") == 0, "stale close entered next round");
            f.Tick(1.5f);
            Check(f.Observer.Count("AutoCloseInvoked") == 1, "next round did not close");
        });
        Test("Settings restart cancels pending result even before the state changes", f =>
        {
            f.Ready(); f.Tick(); f.Auto.Invalidate(f.Controller, "sequence-restart");
            f.Tick(0.8f); f.Tick(1f);
            Check(f.Observer.Count("AutoCloseInvoked") == 0, "old restart deadline survived");
        });
        Test("Other UI blocks result and restarts its display delay", f =>
        {
            f.Ready(); f.Tick(); f.Menu.activeInHierarchy = true; f.Tick(2f);
            Check(f.CallbackOrder.Count == 0, "closed behind menu");
            f.Menu.activeInHierarchy = false; f.Tick(); f.Tick(2.9f);
            Check(f.CallbackOrder.Count == 0, "did not wait after menu");
            f.Tick(3f); Check(f.CallbackOrder.Count == 4, "did not close after menu");
        });
        Test("Mode switch cancels pending result", f =>
        {
            f.Ready(); f.Tick(); f.Settings.isFishingActionMode = true; f.Tick(2f);
            Check(f.CallbackOrder.Count == 0, "acted in Skill mode");
            f.Settings.isFishingActionMode = false; f.Tick(); f.Tick(2.5f);
            Check(f.CallbackOrder.Count == 0, "stale mode deadline survived");
        });
        Test("Inactive UI or controller, unloaded scene and paused game are ignored", f =>
        {
            f.Controller.CurrentState = FishingEventController.FishingState.chance;
            GameUIController.Instance.isActiveAndEnabled = false; f.Tick();
            GameUIController.Instance.isActiveAndEnabled = true;
            f.Controller.isActiveAndEnabled = false; f.Tick(); f.Controller.isActiveAndEnabled = true;
            f.Controller.gameObject.scene.isLoaded = false; f.Tick();
            f.Controller.gameObject.scene.isLoaded = true; Time.timeScale = 0f; f.Tick();
            Check(f.Observer.Count("AutoPull") == 0, "invalid context acted");
        });
        Test("Only an active enabled listener bound to this controller can close", f =>
        {
            f.Ready(); f.Button.onClick.Target = new FishingEventController(); f.Tick(); f.Tick(2f);
            f.Button.onClick.Target = f.Controller; f.Button.onClick.State = UnityEventCallState.Off; f.Tick();
            f.Button.onClick.State = UnityEventCallState.RuntimeOnly; f.Button.Interactable = false; f.Tick();
            Check(f.CallbackOrder.Count == 0, "invalid button invoked");
            f.Button.Interactable = true; f.Tick(); Check(f.CallbackOrder.Count == 4, "valid button ignored");
        });
        Test("Controller replacement cancels the old close deadline", f =>
        {
            f.Ready(); f.Tick(); f.ReplaceController(); f.Ready(); f.Tick(0.8f); f.Tick(1f);
            Check(f.CallbackOrder.Count == 0, "old controller deadline survived");
            f.Tick(1.8f); Check(f.CallbackOrder.Count == 4, "new controller did not close");
        });
        Test("Zero delay still defers one tick", f =>
        {
            f.Delay.Value = 0f; f.Ready(); f.Tick();
            Check(f.CallbackOrder.Count == 0, "closed on scheduling tick");
            f.Tick(); Check(f.CallbackOrder.Count == 4, "zero delay did not close next tick");
        });
        Test("A failing listener disables automation rather than repeating partial actions", f =>
        {
            f.Ready(); int calls = 0;
            f.Button.onClick.Callback = () => { calls++; throw new Exception("listener failed"); };
            f.Tick(); f.Tick(2f); f.Tick(3f);
            Check(calls == 1 && f.Observer.Count("AutoError") == 1, "partial action repeated");
        });
        Console.WriteLine($"{passed} behavior checks passed. Unity integration still requires an in-game run.");
    }

    private sealed class Fixture
    {
        internal FishingEventController Controller;
        internal readonly FishingSettingsWindow Settings = new FishingSettingsWindow();
        internal readonly FishingUIManager UI = new FishingUIManager();
        internal readonly GameObject Menu = new GameObject { activeInHierarchy = false };
        internal readonly Button Button = new Button();
        internal readonly FishingObserver Observer = new FishingObserver();
        internal readonly ConfigEntry<bool> Enabled = new ConfigEntry<bool> { Value = true };
        internal readonly ConfigEntry<float> Delay = new ConfigEntry<float> { Value = 1f };
        internal readonly UnityCustomExtension.Audio.AudioClipDictionary Audio = new UnityCustomExtension.Audio.AudioClipDictionary();
        internal readonly List<string> CallbackOrder = new List<string>();
        internal readonly AutoIdleFishing Auto;
        internal Fixture()
        {
            Time.realtimeSinceStartup = 0f; Time.timeScale = 1f;
            GameUIController.Instance = new GameUIController();
            UI.resultPopupUI.activeInHierarchy = false; UI.resultPopupUI.Buttons.Add(Button);
            UIBlockManager.Instance = new UIBlockManager { uiPanels = new[] { UI.resultPopupUI, Menu } };
            DontDestroyOnLoadSingletonMonoBehaviour<UnityCustomExtension.Audio.AudioClipDictionary>.Instance = Audio;
            Auto = new AutoIdleFishing(Observer, Enabled, Delay); AutoIdleFishing.Current = Auto;
            ReplaceController();
            Button.onClick.Callback = () =>
            {
                CallbackOrder.Add("close"); CloseManually();
                CallbackOrder.Add("audio"); CallbackOrder.Add("buttons"); CallbackOrder.Add("tutorial");
            };
        }
        internal void ReplaceController()
        {
            Controller = new FishingEventController { fishingSettingsWindow = Settings, fishingUIManager = UI };
            Button.onClick.Target = Controller;
        }
        internal void Ready()
        {
            Controller.CurrentState = FishingEventController.FishingState.resulting;
            Controller.resultShown = true; UI.resultPopupUI.activeInHierarchy = true;
        }
        internal void CloseManually()
        {
            Auto.Invalidate(Controller, "native-result-close");
            UI.resultPopupUI.activeInHierarchy = false; Auto.ResultHidden(UI);
            Controller.CurrentState = FishingEventController.FishingState.throwing;
            Auto.StateChanged(Controller); Controller.resultShown = false;
        }
        internal void Tick(float? now = null)
        {
            if (now.HasValue) Time.realtimeSinceStartup = now.Value;
            AutoIdleFishing.Safe(value => value.Tick(Controller));
        }
    }
}

namespace BepInEx.Configuration { public sealed class ConfigEntry<T> { public T Value; } }
namespace UnityEngine
{
    public class Object { }
    public class Component : Object { public GameObject gameObject = new GameObject(); public bool isActiveAndEnabled = true; }
    public sealed class Scene { public bool isLoaded = true; public bool IsValid() => true; }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public Scene scene = new Scene();
        public Transform transform = new Transform();
        public List<Button> Buttons = new List<Button>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Buttons.ToArray() as T[];
    }
    public class Transform { public bool IsChildOf(Transform parent) => false; }
    public class AudioSource : Object { }
    public static class Time { public static float timeScale = 1f; public static float realtimeSinceStartup; }
    public static class Mathf { public static float Clamp(float n, float min, float max) => Math.Min(max, Math.Max(min, n)); }
}
namespace UnityEngine.Events
{
    public enum UnityEventCallState { Off, EditorAndRuntime, RuntimeOnly }
    public sealed class UnityEvent
    {
        public UnityEngine.Object Target;
        public UnityEventCallState State = UnityEventCallState.RuntimeOnly;
        public Action Callback;
        public int GetPersistentEventCount() => 1;
        public UnityEngine.Object GetPersistentTarget(int i) => Target;
        public string GetPersistentMethodName(int i) => "OnResultCloseButton";
        public UnityEventCallState GetPersistentListenerState(int i) => State;
        public void Invoke() => Callback();
    }
}
namespace UnityEngine.UI
{
    public sealed class Button : UnityEngine.Object
    {
        public bool Active = true, Interactable = true;
        public UnityEvent onClick = new UnityEvent();
        public bool IsActive() => Active;
        public bool IsInteractable() => Interactable;
    }
}
namespace UnityCustomExtension.Audio
{
    public sealed class AudioClipDictionary { public int Plays; public void Play(string name, AudioSource source) { Plays++; } }
}
public static class DontDestroyOnLoadSingletonMonoBehaviour<T> { public static T Instance; }
public sealed class FishingSettingsWindow { public bool isFishingActionMode; }
public sealed class FishingUIManager
{
    public GameObject exclamationMark = new GameObject();
    public GameObject resultPopupUI = new GameObject();
}
public sealed class FishingEventController : Component
{
    public enum FishingState { throwing, waiting, chance, pulling, resulting }
    public FishingState CurrentState;
    public FishingSettingsWindow fishingSettingsWindow;
    public FishingUIManager fishingUIManager;
    public AudioSource clickToPullAS = new AudioSource();
    public bool resultShown;
}
public sealed class GameUIController : Component { public static GameUIController Instance; }
public sealed class UIBlockManager
{
    public static UIBlockManager Instance;
    public GameObject[] uiPanels;
    public bool IsAnyUIActive()
    {
        foreach (GameObject panel in uiPanels) if (panel.activeInHierarchy) return true;
        return false;
    }
}
namespace Celestay.EnvironmentCheck
{
    internal sealed class FishingObserver
    {
        private readonly List<string> events = new List<string>();
        internal void Event(string name, string details) => events.Add(name);
        internal int Count(string name) => events.FindAll(value => value == name).Count;
        internal static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
        internal static string PathOf(UnityEngine.Object target) => "fixture";
        internal static string Number(float value) => value.ToString();
    }
}
