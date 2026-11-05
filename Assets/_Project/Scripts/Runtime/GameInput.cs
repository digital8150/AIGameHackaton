using UnityEngine;
using UnityEngine.InputSystem;

namespace DuskTwilight
{
    /// <summary>All bindings in one place (see Docs/InGame_Decisions.md §3). Built in code so the scene has no asset dependency.</summary>
    public class GameInput : MonoBehaviour
    {
        public static GameInput I { get; private set; }

        public InputAction Move { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Switch { get; private set; }
        public InputAction Echo { get; private set; }
        public InputAction Restart { get; private set; }
        public InputAction MicHold { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction DebugOverlay { get; private set; }
        public InputAction DirectorToggle { get; private set; }

        void Awake()
        {
            I = this;
            Move = new InputAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick/x");
            Move.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/dpad/left").With("Positive", "<Gamepad>/dpad/right");

            Jump = Button("Jump", "<Keyboard>/space", "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/buttonSouth");
            Switch = Button("Switch", "<Keyboard>/tab", "<Keyboard>/q", "<Gamepad>/buttonNorth");
            Echo = Button("Echo", "<Keyboard>/r", "<Gamepad>/buttonWest");
            MicHold = Button("MicHold", "<Keyboard>/v", "<Gamepad>/leftShoulder");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonEast");
            Restart = Button("Restart", "<Keyboard>/backspace", "<Gamepad>/select");
            DebugOverlay = Button("Debug", "<Keyboard>/f1");
            DirectorToggle = Button("DirectorToggle", "<Keyboard>/f2");

            foreach (var a in new[] { Move, Jump, Switch, Echo, MicHold, Interact, Restart, DebugOverlay, DirectorToggle }) a.Enable();
        }

        static InputAction Button(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        void OnDestroy()
        {
            foreach (var a in new[] { Move, Jump, Switch, Echo, MicHold, Interact, Restart, DebugOverlay, DirectorToggle }) a?.Dispose();
            if (I == this) I = null;
        }
    }
}
