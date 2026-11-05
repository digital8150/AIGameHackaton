using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Lamp-post checkpoint: lights up (in both senses) once reached.</summary>
    public class CheckpointPost : MonoBehaviour
    {
        [Tooltip("Director zone id reported on deaths after this checkpoint.")]
        public string zone;
        public Renderer glow;
        [Tooltip("How close (X) the grounded leader must come to light it.")]
        public float reach = 0.8f;
        [Tooltip("Respawn point, relative to the post.")]
        public Vector2 respawnOffset = new Vector2(0, 0.1f);
        public float echoStrength = 0.8f;
        bool lit;

        void Update()
        {
            var duo = DuoController.I;
            if (!duo || lit) return;
            if (Mathf.Abs(duo.Leader.transform.position.x - transform.position.x) < reach && duo.Leader.Grounded)
            {
                lit = true;
                GameFlow.I.SetCheckpoint((Vector2)transform.position + respawnOffset, zone);
                if (glow) glow.enabled = true;
                SenseController.I?.EmitEcho(transform.position, echoStrength);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.4f, 0.7f);
            var p = transform.position;
            Gizmos.DrawLine(p + new Vector3(-reach, 0), p + new Vector3(reach, 0));
            Gizmos.DrawWireSphere(p + (Vector3)respawnOffset, 0.15f);
        }
#endif
    }
}
