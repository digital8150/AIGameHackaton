using UnityEngine;

namespace DuskTwilight
{
    /// <summary>The goal lantern: when both arrive, the stage clears.</summary>
    public class ReunionGoal : MonoBehaviour
    {
        public float radius = 1.2f;
        void Update()
        {
            var duo = DuoController.I;
            if (!duo || GameFlow.I.Cleared) return;
            if (Vector2.Distance(duo.Leader.transform.position, transform.position) < radius && duo.Leader.Grounded)
            {
                SenseController.I?.EmitEcho(transform.position, 2f);
                GameFlow.I.Clear();
            }
        }
    }
}
