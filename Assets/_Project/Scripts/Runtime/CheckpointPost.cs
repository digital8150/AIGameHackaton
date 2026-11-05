using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Lamp-post checkpoint: lights up (in both senses) once reached.</summary>
    public class CheckpointPost : MonoBehaviour
    {
        public string zone;
        public Renderer glow;
        bool lit;

        void Update()
        {
            var duo = DuoController.I;
            if (!duo || lit) return;
            if (Mathf.Abs(duo.Leader.transform.position.x - transform.position.x) < 0.8f && duo.Leader.Grounded)
            {
                lit = true;
                GameFlow.I.SetCheckpoint(transform.position + Vector3.up * 0.1f, zone);
                if (glow) glow.enabled = true;
                SenseController.I?.EmitEcho(transform.position, 0.8f);
            }
        }
    }
}
