using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Scales a piece of player-facing text (sound captions, the guide panel) with the 자막·안내 크기 setting.
    /// Put it on the element's root; its resting scale is remembered and multiplied by <see cref="GameSettings.TextScale"/>.
    /// </summary>
    public class TextScaleTarget : MonoBehaviour
    {
        Vector3 baseScale;
        bool init;

        void Awake() { baseScale = transform.localScale; init = true; Apply(); }
        void OnEnable() { GameSettings.Changed += Apply; if (init) Apply(); }
        void OnDisable() { GameSettings.Changed -= Apply; }
        void Apply() { if (init) transform.localScale = baseScale * GameSettings.TextScale; }
    }
}
