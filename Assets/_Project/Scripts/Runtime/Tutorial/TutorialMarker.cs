using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Raises ReachMarker once the leader stands past this point (on the ground).</summary>
    public class TutorialMarker : MonoBehaviour
    {
        public string markerId;
        public TutorialSignals signals;
        bool fired;

        void Update()
        {
            var duo = DuoController.I;
            if (fired || !duo || !signals) return;
            var p = duo.Leader.transform.position;
            if (p.x >= transform.position.x && duo.Leader.Grounded && Mathf.Abs(p.y - transform.position.y) < 2f)
            {
                fired = true;
                signals.Raise(TutorialGoal.ReachMarker, markerId);
            }
        }
    }
}
