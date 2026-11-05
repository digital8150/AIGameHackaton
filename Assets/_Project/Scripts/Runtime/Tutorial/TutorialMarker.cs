using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Raises ReachMarker once the leader stands past this point (on the ground).</summary>
    public class TutorialMarker : MonoBehaviour
    {
        [Tooltip("Matches TutorialObjective.markerId (goal = ReachMarker).")]
        public string markerId;
        [Tooltip("Empty = the TutorialSignals in the scene.")]
        public TutorialSignals signals;
        [Tooltip("Leader must be within this height of the marker.")]
        public float heightTolerance = 2f;
        bool fired;

        void Awake() { if (!signals) signals = FindAnyObjectByType<TutorialSignals>(); }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 1f, 0.7f, 0.8f);
            var p = transform.position;
            Gizmos.DrawLine(p + Vector3.down * heightTolerance, p + Vector3.up * heightTolerance);
            UnityEditor.Handles.Label(p + Vector3.up * (heightTolerance + 0.3f), markerId);
        }
#endif

        void Update()
        {
            var duo = DuoController.I;
            if (fired || !duo || !signals) return;
            var p = duo.Leader.transform.position;
            if (p.x >= transform.position.x && duo.Leader.Grounded && Mathf.Abs(p.y - transform.position.y) < heightTolerance)
            {
                fired = true;
                signals.Raise(TutorialGoal.ReachMarker, markerId);
            }
        }
    }
}
