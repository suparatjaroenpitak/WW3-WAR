using UnityEngine;

namespace AyutthayaSimulator
{
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerController : MonoBehaviour
    {
        public PlayerInputHandler Input;
        public PlayerMovement Movement;
        public FirstPersonCamera Look;
        public PlayerInteraction Interaction;
        public PlayerState State { get; private set; }
        public VehicleController CurrentVehicle { get; private set; }
        public MissionManager Missions;
        public SaveManager Saves;
        public WeatherManager Weather;
        public DayNightCycle Cycle;
        public GameUiController Ui;

        CharacterController body;
        Camera viewCamera;
        AudioSource steps;
        AudioClip stepClip;
        float nextStep;

        void Awake()
        {
            State = new PlayerState();
            Input = GetComponent<PlayerInputHandler>();
            Movement = GetComponent<PlayerMovement>();
            Look = GetComponent<FirstPersonCamera>();
            Interaction = GetComponent<PlayerInteraction>();
            body = GetComponent<CharacterController>();
            viewCamera = GetComponentInChildren<Camera>();
            steps = gameObject.AddComponent<AudioSource>();
            steps.playOnAwake = false;
            steps.spatialBlend = 0f;
            stepClip = ProceduralTone.Step();
        }

        void Start()
        {
            LockCursor(true);
        }

        void Update()
        {
            var frame = Input.Read(State.Paused, Cursor.lockState == CursorLockMode.Locked);
            if (frame.Pause) SetPaused(!State.Paused);
            if (frame.ToggleLanguage) State.Language = State.Language == "th" ? "en" : "th";
            if (frame.Save && Saves != null) Saves.Save(this, Cycle, Weather, Missions);
            if (frame.Load && Saves != null) Saves.Load(this, Cycle, Weather, Missions);
            if (frame.ToggleWeather && Weather != null) Weather.Toggle();
            if (frame.Screenshot) CaptureShot();
            if (State.Paused) return;

            if (State.Mode == PlayerMode.OnFoot)
            {
                Look.Tick(frame, State);
                Movement.Tick(frame, State);
                Interaction.Tick(frame, State);
                TickSteps();
                if (Missions != null) Missions.NotifyPosition(transform.position, false, "");
            }
            else
            {
                Movement.Tick(frame, State);
            }
        }

        public void NotifyInteracted(string id)
        {
            if (Missions != null) Missions.NotifyPosition(transform.position, CurrentVehicle != null, id);
        }

        public void BeginDriving(VehicleController vehicle, Transform seat)
        {
            CurrentVehicle = vehicle;
            State.Mode = PlayerMode.Driving;
            if (body != null) body.enabled = false;
            if (viewCamera != null) viewCamera.enabled = false;
            var listener = viewCamera != null ? viewCamera.GetComponent<AudioListener>() : null;
            if (listener != null) listener.enabled = false;
            if (seat != null)
            {
                transform.SetParent(seat, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        public void EndDriving(Vector3 exitPosition, float yaw)
        {
            transform.SetParent(null, true);
            CurrentVehicle = null;
            State.Mode = PlayerMode.OnFoot;
            Movement.Teleport(exitPosition, yaw);
            if (viewCamera != null) viewCamera.enabled = true;
            var listener = viewCamera != null ? viewCamera.GetComponent<AudioListener>() : null;
            if (listener != null) listener.enabled = true;
        }

        public void SetPaused(bool paused)
        {
            State.Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            LockCursor(!paused);
            if (Ui != null) Ui.SetPaused(paused);
        }

        void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void TickSteps()
        {
            if (!State.Grounded || State.Velocity.magnitude < 0.4f) return;
            if (Time.time < nextStep) return;
            nextStep = Time.time + (State.Running ? 0.34f : 0.5f);
            steps.PlayOneShot(stepClip, 0.35f);
        }

        void CaptureShot()
        {
            var path = System.IO.Path.Combine(Application.persistentDataPath, "ayutthaya-shot-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path);
        }

        void OnDestroy()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }
}
