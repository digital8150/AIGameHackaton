using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Settings window: volumes, display mode/resolution, microphone device with a live level meter.
    /// Binds the referenced controls to GameSettings and animates open/close. Layout lives in the prefab.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public CanvasGroup group;
        [Tooltip("Scaled during open/close.")]
        public RectTransform window;

        [Header("Sound")]
        public Slider master;
        public Slider music;
        public Slider sfx;
        [Tooltip("Optional percentage labels next to the sliders.")]
        public Text masterValue, musicValue, sfxValue;

        [Header("Display")]
        public Toggle fullscreen;
        public Dropdown resolution;

        [Header("Voice")]
        public Dropdown micDevice;
        [Tooltip("Pivot-left bar scaled on X by the live microphone level.")]
        public RectTransform micLevel;
        [Range(1f, 40f)] public float micGain = 12f;
        [Tooltip("Shown instead of the device list when no microphone is plugged in.")]
        public string noMicText = "마이크 없음";

        [Header("Close")]
        public Button close;
        [Range(0.05f, 1f)] public float fadeTime = 0.25f;

        public bool IsOpen { get; private set; }
        public event System.Action Closed;

        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        readonly float[] window512 = new float[512];
        AudioClip micClip;
        string micName;
        float openK;
        float level;
        GameObject returnSelection;

        void Awake()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            Bind(master, masterValue, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            Bind(music, musicValue, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            Bind(sfx, sfxValue, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

            if (fullscreen)
            {
                fullscreen.SetIsOnWithoutNotify(GameSettings.Fullscreen);
                fullscreen.onValueChanged.AddListener(v => GameSettings.Fullscreen = v);
            }

            if (resolution) FillResolutions();
            if (micDevice) FillMics();
            if (close) close.onClick.AddListener(Close);

            SetVisible(0f);
            group.blocksRaycasts = group.interactable = false;
        }

        static void Bind(Slider s, Text label, System.Func<float> get, System.Action<float> set)
        {
            if (!s) return;
            s.minValue = 0; s.maxValue = 1;
            s.SetValueWithoutNotify(get());
            if (label) label.text = Mathf.RoundToInt(get() * 100f).ToString();
            s.onValueChanged.AddListener(v =>
            {
                set(v);
                if (label) label.text = Mathf.RoundToInt(v * 100f).ToString();
            });
        }

        void FillResolutions()
        {
            resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1024 && !resolutions.Contains(v)) resolutions.Add(v);
            }
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            resolutions.Sort((a, b) => (b.x * b.y).CompareTo(a.x * a.y));

            var cur = GameSettings.Resolution;
            int index = 0;
            var options = new List<Dropdown.OptionData>();
            for (int i = 0; i < resolutions.Count; i++)
            {
                options.Add(new Dropdown.OptionData($"{resolutions[i].x} × {resolutions[i].y}"));
                if (resolutions[i] == cur) index = i;
            }
            resolution.ClearOptions();
            resolution.AddOptions(options);
            resolution.SetValueWithoutNotify(index);
            resolution.RefreshShownValue();
            resolution.onValueChanged.AddListener(i => GameSettings.Resolution = resolutions[i]);
        }

        void FillMics()
        {
            micDevice.ClearOptions();
            var devices = Microphone.devices;
            if (devices.Length == 0)
            {
                micDevice.AddOptions(new List<string> { noMicText });
                micDevice.interactable = false;
                micDevice.RefreshShownValue();
                return;
            }
            micDevice.AddOptions(new List<string>(devices));
            string want = GameSettings.ResolveMicDevice();
            micDevice.SetValueWithoutNotify(System.Array.IndexOf(devices, want));
            micDevice.RefreshShownValue();
            micDevice.onValueChanged.AddListener(i =>
            {
                GameSettings.MicDevice = Microphone.devices[i];
                if (IsOpen) { StopMic(); StartMic(); }
            });
        }

        public void Open(GameObject returnTo)
        {
            if (IsOpen) return;
            returnSelection = returnTo;
            IsOpen = true;
            group.blocksRaycasts = group.interactable = true;
            if (master && EventSystem.current) EventSystem.current.SetSelectedGameObject(master.gameObject);
            StartMic();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            group.blocksRaycasts = group.interactable = false;
            StopMic();
            if (returnSelection && EventSystem.current) EventSystem.current.SetSelectedGameObject(returnSelection);
            Closed?.Invoke();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            openK = Mathf.MoveTowards(openK, IsOpen ? 1f : 0f, dt / fadeTime);
            SetVisible(openK);

            if (IsOpen)
            {
                var kb = Keyboard.current;
                var pad = Gamepad.current;
                bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
                // a dropdown list open takes Esc first
                if (back && !(resolution && resolution.transform.Find("Dropdown List")) && !(micDevice && micDevice.transform.Find("Dropdown List")))
                    Close();
            }

            MeasureMic(dt);
        }

        void SetVisible(float k)
        {
            float e = k * k * (3f - 2f * k);
            group.alpha = e;
            if (window) window.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, e);
        }

        void StartMic()
        {
            micName = GameSettings.ResolveMicDevice();
            if (micName == null) return;
            micClip = Microphone.Start(micName, true, 1, 16000);
        }

        void StopMic()
        {
            if (micName != null && Microphone.IsRecording(micName)) Microphone.End(micName);
            micClip = null;
            micName = null;
        }

        void MeasureMic(float dt)
        {
            float target = 0f;
            if (micClip && micName != null)
            {
                int pos = Microphone.GetPosition(micName) - window512.Length;
                if (pos >= 0)
                {
                    micClip.GetData(window512, pos);
                    float sum = 0;
                    for (int i = 0; i < window512.Length; i++) sum += window512[i] * window512[i];
                    target = Mathf.Clamp01(Mathf.Sqrt(sum / window512.Length) * micGain);
                }
            }
            level = target > level ? target : Mathf.MoveTowards(level, target, dt * 1.5f);
            if (micLevel) micLevel.localScale = new Vector3(level, 1f, 1f);
        }

        void OnDisable() => StopMic();
    }
}
