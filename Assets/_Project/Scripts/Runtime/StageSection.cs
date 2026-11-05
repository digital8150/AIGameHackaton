using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// One section of a stage (기획 5단계: 떨어지면 해당 구간의 시작점으로 돌아가고, 발판과 스위치도 처음 상태로).
    /// Standing in it makes its start the respawn point; falling while in it puts its puzzles back.
    /// The section spans x ∈ [this.x, this.x + width] (blue band in the Scene view).
    /// </summary>
    public class StageSection : MonoBehaviour
    {
        [Header("Area")]
        [Min(1f)] public float width = 30f;
        [Tooltip("Respawn point for this section. Empty = this object's position.")]
        public Transform respawn;
        [Tooltip("Director zone id")]
        public string zone = "section";

        [Header("Reset on fall")]
        public bool resetOnFall = true;
        [Tooltip("Things to put back (switches, gates, bridges, stairs, bells). Empty = every resettable inside the area.")]
        public MonoBehaviour[] resettables;

        Vector2 StartPoint => respawn ? (Vector2)respawn.position : (Vector2)transform.position;
        bool Contains(float x) => x >= transform.position.x && x <= transform.position.x + width;

        readonly List<IResettable> items = new List<IResettable>();

        void Start()
        {
            // subscribe after GameFlow.Awake has cleared the events
            GameEvents.Died += OnDied;
            if (resettables != null && resettables.Length > 0)
            {
                foreach (var m in resettables) if (m is IResettable r) items.Add(r);
            }
            else
            {
                foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (m is IResettable r && Contains(m.transform.position.x)) items.Add(r);
            }
        }

        void OnDestroy() { GameEvents.Died -= OnDied; }

        void Update()
        {
            var duo = DuoController.I;
            var flow = GameFlow.I;
            if (!duo || !flow || flow.Cleared) return;
            var leader = duo.Leader;
            if (leader.Grounded && Contains(leader.transform.position.x) && (flow.Checkpoint - StartPoint).sqrMagnitude > 0.01f)
                flow.SetCheckpoint(StartPoint, zone);
        }

        void OnDied(string _)
        {
            if (!resetOnFall || !DuoController.I || !Contains(DuoController.I.Leader.transform.position.x)) return;
            StartCoroutine(ResetSoon());
        }

        IEnumerator ResetSoon()
        {
            // behind the fade
            yield return new WaitForSeconds(GameFlow.I ? GameFlow.I.respawnFadeOut : 0.3f);
            foreach (var r in items) r.ResetState();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var p = transform.position;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
            Gizmos.DrawLine(p + new Vector3(0, -4), p + new Vector3(0, 20));
            Gizmos.DrawLine(p + new Vector3(width, -4), p + new Vector3(width, 20));
            Gizmos.DrawLine(p + new Vector3(0, -4), p + new Vector3(width, -4));
            Gizmos.color = new Color(0.5f, 1f, 0.6f);
            Gizmos.DrawWireSphere(StartPoint, 0.35f);
            UnityEditor.Handles.Label(p + new Vector3(0.3f, 19.5f), zone);
        }
#endif
    }
}
