using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Animates a title menu button: hover/selection eases the plate shader (_Hover, _Shine, _Press),
    /// warms the label and nudges the scale. Mouse and keyboard/gamepad selection look the same.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TitleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int PressId = Shader.PropertyToID("_Press");
        static readonly int ShineId = Shader.PropertyToID("_Shine");

        [Tooltip("Plate with the TitleButton shader (UIShaderSize gives it its own material).")]
        public UIShaderSize plate;
        public Text label;

        [Header("Look")]
        public Color labelIdle = new Color(0.86f, 0.80f, 0.72f, 0.9f);
        public Color labelHover = new Color(1f, 0.93f, 0.78f, 1f);
        [Range(1f, 1.2f)] public float hoverScale = 1.05f;
        [Range(1f, 30f)] public float ease = 10f;
        [Tooltip("Seconds for the light sweep to cross the plate after hover starts.")]
        [Range(0.1f, 2f)] public float shineTime = 0.55f;

        [Header("Sound")]
        public AudioSource sfx;
        public AudioClip hoverClip;
        public AudioClip clickClip;

        bool pointerOver, selected;
        float hover, press, shine = 1f;

        bool Active => pointerOver || selected;

        void Awake()
        {
            if (!plate) plate = GetComponentInChildren<UIShaderSize>();
            if (!label) label = GetComponentInChildren<Text>();
            GetComponent<Button>().onClick.AddListener(() => { press = 1f; Play(clickClip); });
        }

        void OnDisable()
        {
            pointerOver = selected = false;
            hover = press = 0f;
            shine = 1f;
            Push();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hover = Mathf.Lerp(hover, Active ? 1f : 0f, 1f - Mathf.Exp(-ease * dt));
            press = Mathf.MoveTowards(press, 0f, dt * 2.5f);
            if (shine < 1f) shine = Mathf.Min(1f, shine + dt / shineTime);
            Push();
        }

        void Push()
        {
            var m = plate ? plate.Material : null;
            if (m)
            {
                m.SetFloat(HoverId, hover);
                m.SetFloat(PressId, press);
                m.SetFloat(ShineId, shine);
            }
            if (label) label.color = Color.Lerp(labelIdle, labelHover, hover);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, hover);
        }

        void Enter()
        {
            if (Active) return;
            shine = 0f;
            Play(hoverClip);
        }

        void Play(AudioClip clip)
        {
            if (sfx && clip && isActiveAndEnabled) sfx.PlayLimited(clip);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            Enter();
            pointerOver = true;
            // keep mouse and keyboard selection in one place
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData e) => pointerOver = false;

        public void OnSelect(BaseEventData e)
        {
            Enter();
            selected = true;
        }

        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerDown(PointerEventData e) => press = 1f;
        public void OnPointerUp(PointerEventData e) { }
        public void OnSubmit(BaseEventData e) => press = 1f;
    }
}
