using UnityEngine;

namespace DuskTwilight
{
    /// <summary>The goal lantern: when both arrive, the stage clears.</summary>
    public class ReunionGoal : MonoBehaviour
    {
        [Tooltip("Distance from the door at which the grounded leader clears the chapter.")]
        public float radius = 1.2f;
        public float echoStrength = 2f;

        void Update()
        {
            var duo = DuoController.I;
            if (!duo || GameFlow.I.Cleared) return;
            if (Vector2.Distance(duo.Leader.transform.position, transform.position) < radius && duo.Leader.Grounded)
            {
                SenseController.I?.EmitEcho(transform.position, echoStrength);
                GameFlow.I.Clear();
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
