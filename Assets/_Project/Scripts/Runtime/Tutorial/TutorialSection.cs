using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Player actions a tutorial objective can wait for.</summary>
    public enum TutorialGoal
    {
        Walk,             // per unit walked under the player's own input
        Jump,
        SwitchToPrince,
        SwitchToPrincess,
        Echo,             // the prince called out (button or voice)
        ReachMarker,      // walked past a TutorialMarker with matching id
        OpenGate,         // pulled the wired lever on gold
        ReachDoor,        // the chapter's door
        Speak,            // a spoken (or chosen) word took hold on a gold object / floating stone
    }

    /// <summary>One objective row: which action, how many times, how it reads.</summary>
    [System.Serializable]
    public class TutorialObjective
    {
        public string text;
        [Tooltip("Key chips drawn before the text, e.g. ←, →, Space, Tab")]
        public string[] keys;
        public TutorialGoal goal;
        [Tooltip("ReachMarker only")]
        public string markerId;
        [Min(1)] public int target = 1;
        public bool showCount;
    }

    /// <summary>One tutorial step: title, objectives that complete in any order, an optional tip, and what it opens.</summary>
    [System.Serializable]
    public class TutorialSection
    {
        public string title;
        public TutorialObjective[] objectives;
        [TextArea] public string tip;
        [Tooltip("Veils lifted when this section begins (the next stretch of the path).")]
        public TutorialVeil[] opens;
        [Tooltip("Allow switching senses from this section on.")]
        public bool allowSwitch;
    }
}
