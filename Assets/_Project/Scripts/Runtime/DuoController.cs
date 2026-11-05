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
        [Tooltip("Minimum time the follower trails the leader's path (seconds)")]
        public float followDelay = 0.26f;
        [Tooltip("Distance along the leader's path the follower keeps")]
        public float followGap = 0.85f;
        [Tooltip("Fastest the follower moves along the path when catching up (m/s) — finishing a jump the leader already landed, closing the gap")]
        public float followCatchSpeed = 9f;
        [Tooltip("Looking for a spot beside the leader on a narrow ledge: keep this much floor past her feet")]
        [Range(0f, 0.4f)] public float edgeMargin = 0.15f;
        public float switchCooldown = 0.35f;
        public AudioClip switchClip;

        [Header("Switch")]
        [Tooltip("The controlled body never moves on a switch: the new leader takes the old leader's exact spot and the two only trade places visually over this time.")]
        [Range(0.05f, 0.6f)] public float swapVisualTime = 0.22f;
        [Tooltip("Small hop of the swap arc (m)")]
        [Range(0f, 1f)] public float swapHop = 0.25f;

        public Actor Leader { get; private set; }
        public Actor Follower => Leader == prince ? princess : prince;
        public int SwitchCount { get; private set; }

        // breadcrumbs of where the leader actually was, with absolute path length and whether they stood on ground
        struct Crumb { public float t, len; public Vector2 p; public bool ground; }
        readonly List<Crumb> trail = new List<Crumb>();
        float cursor;          // follower's absolute path length along the trail
        float lastWant;
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
            ResetTrail();
            if (SenseController.I)
            {
                SenseController.I.Listener = Leader.transform;
                SenseController.I.SetSense(a.sense, Leader.transform.position, instant);
            }
        }

        /// <summary>
        /// Swap who leads. The controlled position is deterministic: the new leader takes the old leader's exact
        /// spot and momentum, the old leader steps into the follower's spot, and the trade is only shown as a
        /// short visual cross-over. A follower standing over a gap can never turn a switch into a fall.
        /// </summary>
        public void Switch()
        {
            if (Time.time - lastSwitch < switchCooldown) return;
            lastSwitch = Time.time;
            SwitchCount++;
            var oldLeader = Leader;
            var newLeader = Follower;
            Vector2 leadPos = oldLeader.transform.position, followPos = newLeader.transform.position;
            var v = oldLeader.Velocity;
            int facing = oldLeader.Facing;

            newLeader.Teleport(leadPos);
            oldLeader.Teleport(followPos);
            SetLeader(newLeader);
            newLeader.Face(facing);
            // hand over momentum so the swap never feels like a stop
            newLeader.GetComponent<Rigidbody2D>().linearVelocity = v;
            newLeader.SwapVisualFrom(followPos, swapVisualTime, swapHop);
            oldLeader.SwapVisualFrom(leadPos, swapVisualTime, swapHop * 0.6f);

            if (switchClip) sfx.PlayLimited(switchClip, 0.6f);
            CameraRig.I?.Kick(0.12f);
            GameEvents.Switched?.Invoke(newLeader.sense);
        }

        public void PlaceAt(Vector2 p)
        {
            Leader.Teleport(p);
            Follower.Teleport(p + Vector2.left * followGap * Leader.Facing);
            groundY = p.y;
            ResetTrail();
        }

        /// <summary>Something solid landed where the follower stood: she steps to the leader's side and starts a fresh trail.</summary>
        public void RegroupFollower()
        {
            Follower.Teleport((Vector2)Leader.transform.position);
            ResetTrail();
        }

        /// <summary>Trail starts at the follower: the path she walks is "from where I am, along where the leader goes".</summary>
        void ResetTrail()
        {
            trail.Clear();
            Vector2 f = Follower.transform.position;
            trail.Add(new Crumb { t = Time.time - followDelay, len = 0, p = f, ground = GroundBelow(f, 0.35f, out _) });
            cursor = 0; lastWant = 0;
        }

        void Update()
        {
            if (GameInput.I && !GameFlow.InputLocked && !switchLocked && GameInput.I.Switch.WasPressedThisFrame()) Switch();
        }

        void FixedUpdate()
        {
            Vector2 leaderPos = Leader.transform.position;
            if (Leader.Grounded) groundY = leaderPos.y;

            // breadcrumbs by distance: the follower only ever walks where the leader actually was
            var last = trail[trail.Count - 1];
            float d = Vector2.Distance(last.p, leaderPos);
            if (d > 0.06f)
            {
                trail.Add(new Crumb { t = Time.time, len = last.len + d, p = leaderPos, ground = Leader.Grounded });
                last = trail[trail.Count - 1];
            }
            float leaderLen = last.len + Vector2.Distance(last.p, leaderPos);

            // where she wants to be: followGap behind along the path, and at least followDelay behind in time
            float want = leaderLen - followGap;
            for (int i = trail.Count - 1; i >= 0; i--)
                if (Time.time - trail[i].t >= followDelay) { want = Mathf.Min(want, trail[i].len); break; }
            // replay the path as fast as the leader actually travelled it (a jump arc is long and quick)
            float pathRate = Mathf.Clamp((want - lastWant) / Time.fixedDeltaTime, 0f, 30f);
            lastWant = want;

            // the leader has stopped on ground but her spot is mid-air (a jump onto a narrow ledge):
            // finish the jump along the leader's arc and land where the leader landed
            bool leaderSettled = Leader.Grounded && Mathf.Abs(Leader.Velocity.x) < 0.5f;
            if (leaderSettled && !GroundAt(Mathf.Max(want, cursor), leaderPos, leaderLen))
            {
                float land = leaderLen;
                for (int i = 0; i < trail.Count; i++)
                    if (trail[i].len >= Mathf.Max(want, cursor) && trail[i].ground) { land = trail[i].len; break; }
                want = Mathf.Max(want, land);
            }

            // move along the path at a bounded speed (never backwards): jumps are replayed, not cut across
            if (want > cursor) cursor = Mathf.Min(want, cursor + Mathf.Max(followCatchSpeed, pathRate * 1.1f) * Time.fixedDeltaTime);
            cursor = Mathf.Min(cursor, leaderLen);

            Vector2 target = PointAt(cursor, leaderPos, leaderLen, out bool onGround);
            bool caughtUp = cursor >= want - 0.02f;
            target = Settle(target, leaderPos, ref onGround, caughtUp && leaderSettled);
            Follower.FollowTo(target, onGround, Leader.Facing, Time.fixedDeltaTime);

            // drop crumbs she has already passed (keep the segment she is on)
            int keep = 0;
            while (keep + 1 < trail.Count && trail[keep + 1].len <= cursor) keep++;
            if (keep > 0) trail.RemoveRange(0, keep);
            if (trail.Count > 800) { trail.RemoveRange(0, trail.Count - 800); cursor = Mathf.Max(cursor, trail[0].len); }
        }

        Vector2 PointAt(float len, Vector2 leaderPos, float leaderLen, out bool ground)
        {
            for (int i = 1; i < trail.Count; i++)
            {
                if (trail[i].len < len) continue;
                var a = trail[i - 1]; var b = trail[i];
                float u = b.len > a.len ? Mathf.Clamp01((len - a.len) / (b.len - a.len)) : 1f;
                ground = u < 0.5f ? a.ground : b.ground;
                return Vector2.Lerp(a.p, b.p, u);
            }
            var lastC = trail[trail.Count - 1];
            float k = leaderLen > lastC.len ? (len - lastC.len) / (leaderLen - lastC.len) : 1f;
            ground = lastC.ground && Leader.Grounded;
            return Vector2.Lerp(lastC.p, leaderPos, Mathf.Clamp01(k));
        }

        bool GroundAt(float len, Vector2 leaderPos, float leaderLen)
        {
            PointAt(len, leaderPos, leaderLen, out bool g);
            return g;
        }

        /// <summary>The leader's feet height the last time they stood on ground.</summary>
        float groundY = float.NegativeInfinity;

        // never from inside a wall / ledge: a ray that starts in a collider would "hit" at its own origin
        RaycastHit2D Down(Vector2 from, float dist)
        {
            bool q = Physics2D.queriesStartInColliders;
            Physics2D.queriesStartInColliders = false;
            var hit = Physics2D.Raycast(from, Vector2.down, dist, Leader.groundMask);
            Physics2D.queriesStartInColliders = q;
            return hit;
        }

        bool GroundBelow(Vector2 p, float tolerance, out float y)
        {
            var hit = Down(p + Vector2.up * 0.5f, 0.5f + tolerance);
            y = hit.collider ? hit.point.y : p.y;
            return hit.collider;
        }

        /// <summary>
        /// Keeps the follower beside the leader with feet on the ground. Without this, a jump in place
        /// leaves breadcrumbs straight above the leader's head and the follower ends up standing on them,
        /// and a leader resting on a narrow ledge leaves the follower hanging in the air behind them.
        /// </summary>
        Vector2 Settle(Vector2 target, Vector2 leaderPos, ref bool onGround, bool restingBeside)
        {
            Vector2 self = Follower.transform.position;
            // walked crumbs: pin feet to the ground under them (tiny offsets only — never lifted onto a ledge)
            if (onGround && GroundBelow(target, 0.3f, out float gy)) target.y = gy;

            bool besideLeader = Mathf.Abs(target.x - leaderPos.x) < followGap * 0.75f;
            // a leader resting on ground with nothing under her spot (narrow pillar, ledge edge): find real ground beside them
            bool hovering = restingBeside && !GroundBelow(target, 0.3f, out _);
            if (!besideLeader && !hovering) return target;
            if (!Leader.Grounded && !besideLeader) return target;   // mid-jump over a gap is fine

            // never share the leader's column: hold the gap on the side we are already on, on the leader's ground
            float side = Mathf.Abs(self.x - leaderPos.x) > 0.05f ? Mathf.Sign(self.x - leaderPos.x) : -Leader.Facing;
            float baseY = float.IsNegativeInfinity(groundY) ? leaderPos.y : groundY;

            // farthest spot (up to followGap) on that side with floor at about the leader's height;
            // a narrow ledge simply puts her closer — right behind the leader if it has to
            for (float off = followGap; off > 0.01f; off -= 0.1f)
            {
                float x = leaderPos.x + side * off;
                var hit = Down(new Vector2(x, baseY + 0.6f), 1.2f);
                // both feet on it: not balanced on the very edge
                var outer = Down(new Vector2(x + side * edgeMargin, baseY + 0.6f), 1.2f);
                if (hit.collider && outer.collider && Mathf.Abs(outer.point.y - hit.point.y) < 0.3f) { onGround = true; return new Vector2(x, hit.point.y); }
            }
            if (!Leader.Grounded) return target;   // jumping over nothing: keep to the path
            onGround = true;
            return new Vector2(leaderPos.x, baseY);
        }
    }
}
