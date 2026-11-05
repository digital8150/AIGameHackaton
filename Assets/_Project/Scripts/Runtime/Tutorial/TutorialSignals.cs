using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Adapts what the player actually does into tutorial goal signals.</summary>
    public class TutorialSignals : MonoBehaviour
    {
        public float walkUnit = 1f;

        public event System.Action<TutorialGoal, string, int> Raised;

        float walked;
        float lastX;
        bool hasLast;

        void Start()
        {
            GameEvents.Jumped += () => Raise(TutorialGoal.Jump);
            GameEvents.Switched += s => Raise(s == Sense.Dark ? TutorialGoal.SwitchToPrince : TutorialGoal.SwitchToPrincess);
            GameEvents.PuzzleAttempt += (id, ok) =>
            {
                if (id == "echo") Raise(TutorialGoal.Echo);
                if (id == "gate" && ok) Raise(TutorialGoal.OpenGate);
            };
            GameEvents.Cleared += () => Raise(TutorialGoal.ReachDoor);
        }

        void Update()
        {
            var duo = DuoController.I;
            if (!duo || GameFlow.InputLocked) { hasLast = false; return; }
            float x = duo.Leader.transform.position.x;
            float input = GameInput.I ? GameInput.I.Move.ReadValue<float>() : 0;
            if (hasLast && Mathf.Abs(input) > 0.2f) walked += Mathf.Abs(x - lastX);
            lastX = x;
            hasLast = true;
            while (walked >= walkUnit) { walked -= walkUnit; Raise(TutorialGoal.Walk); }
        }

        public void Raise(TutorialGoal goal, string id = null, int amount = 1) => Raised?.Invoke(goal, id, amount);
    }
}
