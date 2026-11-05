using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// The two always travel together. One button swaps who leads — and with it, whose senses render the world.
    /// </summary>
    public class DuoController : MonoBehaviour
    {
        public static DuoController I { get; private set; }

        public Actor prince;
        public Actor princess;
        public float followDelay = 0.26f;
        public float followGap = 0.85f;
        public float switchCooldown = 0.35f;
        public AudioClip switchClip;

        public Actor Leader { get; private set; }
        public Actor Follower => Leader == prince ? princess : prince;
        public int SwitchCount { get; private set; }

        readonly List<(float t, Vector2 p)> trail = new List<(float, Vector2)>();
        float lastSwitch = -9;
        AudioSource sfx;

        void Awake()
        {
            I = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            Leader = startSense == Sense.Dark ? prince : princess; // GameFlow.Start may place us before our own Start
        }

        [Tooltip("Who leads when the chapter starts (chapter 1 begins in the prince's dark).")]
        public Sense startSense = Sense.Light;
        [Tooltip("Tutorial can hold the switch until the player is taught it.")]
        public bool switchLocked;

        void Start()
        {
            SetLeader(Leader, instant: true);
        }

        void SetLeader(Actor a, bool instant = false)
        {
            Leader = a;
            Follower.SetLeader(false);
            Leader.SetLeader(true);
            trail.Clear();
            trail.Add((Time.time, Follower.transform.position));
            if (SenseController.I)
            {
                SenseController.I.Listener = Leader.transform;
                SenseController.I.SetSense(a.sense, Leader.transform.position, instant);
            }
        }

        public void Switch()
        {
            if (Time.time - lastSwitch < switchCooldown) return;
            lastSwitch = Time.time;
            SwitchCount++;
            var newLeader = Follower;
            // hand over momentum so the swap never feels like a stop
            var v = Leader.Velocity;
            SetLeader(newLeader);
            newLeader.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(v.x, Mathf.Max(0, v.y) * 0.5f);
            if (switchClip) sfx.PlayOneShot(switchClip, 0.6f);
            CameraRig.I?.Kick(0.12f);
            GameEvents.Switched?.Invoke(newLeader.sense);
        }

        public void PlaceAt(Vector2 p)
        {
            Leader.Teleport(p);
            Follower.Teleport(p + Vector2.left * followGap * Leader.Facing);
            trail.Clear();
        }

        void Update()
        {
            if (GameInput.I && !GameFlow.InputLocked && !switchLocked && GameInput.I.Switch.WasPressedThisFrame()) Switch();
        }

        void FixedUpdate()
        {
            // breadcrumbs by distance: the follower only ever stands where the leader actually stood
            Vector2 leaderPos = Leader.transform.position;
            if (trail.Count == 0 || Vector2.Distance(trail[trail.Count - 1].p, leaderPos) > 0.06f)
                trail.Add((Time.time, leaderPos));

            // walk back along the path until we are followGap behind the leader
            // until the leader has walked followGap away, the follower stays put
            Vector2 target = Follower.transform.position;
            float acc = 0;
            int idx = -1;
            for (int i = trail.Count - 1; i > 0; i--)
            {
                acc += Vector2.Distance(trail[i].p, trail[i - 1].p);
                if (acc >= followGap) { idx = i - 1; target = trail[idx].p; break; }
            }
            // also respect a minimum time delay so the pair reads as two people, not one rigid body
            while (idx > 0 && Time.time - trail[idx].t < followDelay) { idx--; target = trail[idx].p; }
            if (idx > 1) trail.RemoveRange(0, idx - 1);
            if (trail.Count > 600) trail.RemoveRange(0, trail.Count - 600);

            Follower.FollowTo(Settle(target, leaderPos), Leader.Facing, Time.fixedDeltaTime);
        }

        /// <summary>
        /// Keeps the follower beside the leader with feet on the ground. Without this, a jump in place
        /// leaves breadcrumbs straight above the leader's head and the follower ends up standing on them.
        /// </summary>
        Vector2 Settle(Vector2 target, Vector2 leaderPos)
        {
            Vector2 self = Follower.transform.position;
            // 1) never share the leader's column: hold the gap on the side we are already on
            if (Mathf.Abs(target.x - leaderPos.x) < followGap * 0.75f)
            {
                float side = Mathf.Abs(self.x - leaderPos.x) > 0.05f ? Mathf.Sign(self.x - leaderPos.x) : -Leader.Facing;
                target.x = leaderPos.x + side * followGap;
                target.y = Mathf.Max(target.y, leaderPos.y);
            }
            // 2) feet on solid ground (actors are excluded from the mask, so the leader is never ground)
            var hit = Physics2D.Raycast(new Vector2(target.x, target.y + 0.6f), Vector2.down, 3f, Leader.groundMask);
            if (hit.collider) return new Vector2(target.x, hit.point.y);
            // 3) nothing below: mid-jump over a gap is fine; standing still over a pit is not
            if (!Leader.Grounded) return target;
            var under = Physics2D.Raycast(self + Vector2.up * 0.6f, Vector2.down, 3f, Leader.groundMask);
            return under.collider ? new Vector2(self.x, under.point.y) : target;
        }
    }
}
