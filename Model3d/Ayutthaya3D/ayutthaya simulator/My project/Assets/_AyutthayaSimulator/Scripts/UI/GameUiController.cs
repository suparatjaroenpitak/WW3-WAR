using AyutthayaSimulator.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AyutthayaSimulator
{
    public sealed class GameUiController : MonoBehaviour
    {
        public PlayerController Player;
        public MissionManager Missions;
        public VehicleController Vehicle;
        public WorldMetadata World;
        public Text Prompt;
        public Text Mission;
        public Text Credits;
        public Text VehicleHud;
        public Text Status;
        public GameObject PausePanel;
        public RectTransform PlayerDot;
        public bool Paused { get; private set; }

        public static GameUiController Create(Transform parent)
        {
            var root = new GameObject("GameUi");
            root.transform.SetParent(parent, false);
            var ui = root.AddComponent<GameUiController>();
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var events = new GameObject("EventSystem");
            events.transform.SetParent(root.transform, false);
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            ui.Prompt = MakeText(canvasObject.transform, "Prompt", new Vector2(0.5f, 0.62f), 28, TextAnchor.MiddleCenter);
            ui.Mission = MakeText(canvasObject.transform, "Mission", new Vector2(0.02f, 0.98f), 24, TextAnchor.UpperLeft);
            ui.Credits = MakeText(canvasObject.transform, "Credits", new Vector2(0.02f, 0.02f), 18, TextAnchor.LowerLeft);
            ui.VehicleHud = MakeText(canvasObject.transform, "VehicleHud", new Vector2(0.98f, 0.08f), 24, TextAnchor.LowerRight);
            ui.Status = MakeText(canvasObject.transform, "Status", new Vector2(0.98f, 0.98f), 18, TextAnchor.UpperRight);
            ui.PlayerDot = MakeDot(canvasObject.transform);
            ui.PausePanel = MakePause(canvasObject.transform, ui);
            ui.PausePanel.SetActive(false);
            return ui;
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (PausePanel != null) PausePanel.SetActive(paused);
        }

        public void Resume() { if (Player != null) Player.SetPaused(false); }
        public void Save() { if (Player != null && Player.Saves != null) Player.Saves.Save(Player, Player.Cycle, Player.Weather, Player.Missions); }
        public void Load() { if (Player != null && Player.Saves != null) Player.Saves.Load(Player, Player.Cycle, Player.Weather, Player.Missions); }
        public void ToggleLanguage() { if (Player != null) Player.State.Language = Player.State.Language == "th" ? "en" : "th"; }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            var language = Player != null && Player.State != null ? Player.State.Language : "th";
            if (Prompt != null)
            {
                var detector = FindFirstObjectByType<InteractionDetector>();
                Prompt.text = detector != null ? detector.CurrentPrompt : "";
            }
            if (Mission != null)
            {
                var active = Missions != null ? Missions.Active() : null;
                Mission.text = active == null
                    ? LocalizationCatalog.Get(language, "no_mission")
                    : LocalizationCatalog.Get(language, "mission") + ": " + active.Id;
            }
            if (Credits != null)
            {
                var attribution = World != null ? World.Attribution : LocalizationCatalog.Get(language, "credits");
                Credits.text = attribution + "\n" + LocalizationCatalog.Get(language, "terrain_placeholder");
            }
            if (VehicleHud != null)
            {
                var driving = Player != null && Player.State.Mode == PlayerMode.Driving && Vehicle != null && Vehicle.Engine != null;
                VehicleHud.text = driving
                    ? Mathf.RoundToInt(Vehicle.Engine.CurrentSpeedKph) + " km/h\n" + LocalizationCatalog.Get(language, "exit_vehicle")
                    : "";
            }
            if (Status != null && Player != null)
            {
                var clock = Player.Cycle != null ? Player.Cycle.ClockText + (Player.Cycle.Paused ? "  ||" : "") : "";
                var weather = Player.Weather != null ? LocalizationCatalog.Get(language, "weather_" + Player.Weather.Current.ToString().ToLowerInvariant()) : "";
                Status.text = clock + "  |  " + weather + "\n" + LocalizationCatalog.Get(language, "environment_controls") + "\n" + (Player.Saves != null ? Player.Saves.LastStatus : "");
            }
            if (PlayerDot != null && Player != null)
            {
                var normalized = new Vector2(Mathf.Clamp01(Player.transform.position.x / 500f), Mathf.Clamp01(Player.transform.position.z / 500f));
                PlayerDot.anchorMin = PlayerDot.anchorMax = normalized;
                PlayerDot.anchoredPosition = Vector2.zero;
            }
        }

        static Text MakeText(Transform parent, string name, Vector2 anchor, int size, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = align == TextAnchor.UpperLeft || align == TextAnchor.LowerLeft ? new Vector2(0f, align == TextAnchor.UpperLeft ? 1f : 0f) : new Vector2(0.5f, 0.5f);
            if (align == TextAnchor.UpperRight || align == TextAnchor.LowerRight) rect.pivot = new Vector2(1f, align == TextAnchor.UpperRight ? 1f : 0f);
            rect.sizeDelta = new Vector2(760f, 160f);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        static RectTransform MakeDot(Transform parent)
        {
            var map = new GameObject("SchematicMinimap");
            map.transform.SetParent(parent, false);
            var mapRect = map.AddComponent<RectTransform>();
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.86f, 0.72f);
            mapRect.sizeDelta = new Vector2(180f, 180f);
            var image = map.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);
            var dot = new GameObject("PlayerDot");
            dot.transform.SetParent(map.transform, false);
            var rect = dot.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(8f, 8f);
            var dotImage = dot.AddComponent<Image>();
            dotImage.color = new Color(1f, 0.85f, 0.2f, 1f);
            return rect;
        }

        static GameObject MakePause(Transform parent, GameUiController ui)
        {
            var panel = new GameObject("PauseMenu");
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            AddButton(panel.transform, "Resume", new Vector2(0f, 80f), ui.Resume);
            AddButton(panel.transform, "Save", new Vector2(0f, 20f), ui.Save);
            AddButton(panel.transform, "Load", new Vector2(0f, -40f), ui.Load);
            AddButton(panel.transform, "Language", new Vector2(0f, -100f), ui.ToggleLanguage);
            AddButton(panel.transform, "Quit", new Vector2(0f, -160f), ui.QuitGame);
            return panel;
        }

        static void AddButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(280f, 46f);
            rect.anchoredPosition = position;
            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.17f, 0.2f, 0.95f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(action);
            var text = MakeText(go.transform, "Label", new Vector2(0.5f, 0.5f), 22, TextAnchor.MiddleCenter);
            text.text = label;
            text.rectTransform.sizeDelta = new Vector2(260f, 40f);
        }
    }
}
