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
        [Tooltip("How softly she starts / stops along the path (s)")]
        [Range(0.02f, 0.4f)] public float followPathSmooth = 0.12f;
        [Tooltip("How softly she steps aside for a leader who stopped on her spot, and steps back (s)")]
        [Range(0.05f, 0.6f)] public float followStepAsideSmooth = 0.2f;
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
        float lastWant, cursorVel;
        Vector2 sideOffset, sideOffsetVel;
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
            ResetTrail();
        }

        void SetLeader(Actor a, bool instant = false)
        {
            Leader = a;
            Follower.SetLeader(false);
            Leader.SetLeader(true);
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
        /// The trail, cursor and smoothing state are kept: the two spots are unchanged, so the path between them
        /// is still valid and nothing about the pair's motion changes except who is who.
        /// </summary>
        public void Switch()
        {
            if (Time.time - lastSwitch < switchCooldown) return;
            lastSwitch = Time.time;
            SwitchCount++;
            var oldLeader = Leader;
            var newLeader = Follower;
            // rendered positions (for the visual cross-over) vs. physics truth (for the teleports):
            // the interpolated transform lags the body by up to one fixed step
            Vector2 leadPos = oldLeader.transform.position, followPos = newLeader.transform.position;
            Vector2 leadBody = oldLeader.BodyPosition, followBody = newLeader.BodyPosition;
            var v = oldLeader.Velocity;

            newLeader.Teleport(leadBody);
            oldLeader.Teleport(followBody);
            newLeader.TradeMotionWith(oldLeader);
            SetLeader(newLeader);
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
            cursor = 0; lastWant = 0; cursorVel = 0;
            sideOffset = Vector2.zero; sideOffsetVel = Vector2.zero;
        }

        void Update()
        {
            if (GameInput.I && !GameFlow.InputLocked && !GameInput.I.LockedLastFrame && !switchLocked && GameInput.I.Switch.WasPressedThisFrame()) Switch();
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

            // the leader is close on the same floor level (walking past her, jumping in place): freeze the cursor.
            // The body target stays on the path, so she never detaches from it.
            Vector2 body = Follower.transform.position;
            bool sameLevel = !float.IsNegativeInfinity(groundY) && Mathf.Abs(groundY - body.y) < 0.6f;
            bool closeBy = Mathf.Abs(body.x - leaderPos.x) < followGap;
            bool hold = closeBy && sameLevel && Follower.Grounded;
            if (hold) { want = cursor; pathRate = 0f; }

            // ease along the path (never backwards, speed bounded): jumps are replayed, not cut across,
            // and starting / stopping ramps instead of snapping
            float maxRate = Mathf.Max(followCatchSpeed, pathRate * 1.1f);
            // lead the goal by the smoothing lag (rate * smooth time) so a steady run does not trail the leader by extra metres
            float goal = Mathf.Max(want + pathRate * followPathSmooth, cursor);
            cursor = Mathf.SmoothDamp(cursor, goal, ref cursorVel, followPathSmooth, maxRate, Time.fixedDeltaTime);
            if (cursorVel < 0) cursorVel = 0;
            if (hold && Follower.Velocity.sqrMagnitude < 0.05f * 0.05f) cursorVel = 0;
            cursor = Mathf.Min(cursor, leaderLen);

            // body target = path point + a smoothed offset (steps aside for a leader who stopped on her spot)
            // aim ahead by the body's own smoothing lag so a steady run does not trail the path point
            float aim = Mathf.Min(cursor + Mathf.Min(pathRate, Leader.Velocity.magnitude) * Follower.followSmooth, leaderLen);
            Vector2 pathPoint = PointAt(aim, leaderPos, leaderLen, out bool onGround);
            if (onGround && GroundBelow(pathPoint, 0.3f, out float gy)) pathPoint.y = gy;
            Vector2 desiredOffset = Vector2.zero;
            if (leaderSettled && cursor >= want - 0.05f && NeedsStepAside(pathPoint, leaderPos))
            {
                desiredOffset = FindSpotBeside(body, leaderPos) - pathPoint; onGround = true;
            }
            sideOffset = Vector2.SmoothDamp(sideOffset, desiredOffset, ref sideOffsetVel, followStepAsideSmooth, 12f, Time.fixedDeltaTime);
            Vector2 target = pathPoint + sideOffset;

            Follower.FollowTo(target, onGround, Leader.Facing, Time.fixedDeltaTime);

            TryShortcut(leaderPos, leaderLen);

            // drop crumbs she has already passed (keep the segment she is on)
            int keep = 0;
            while (keep + 1 < trail.Count && trail[keep + 1].len <= cursor) keep++;
            if (keep > 0) trail.RemoveRange(0, keep);
            if (trail.Count > 800) { trail.RemoveRange(0, trail.Count - 800); cursor = Mathf.Max(cursor, trail[0].len); }
        }

        /// <summary>
        /// The leader turned back toward her on the same floor: the path to the turning point is a detour.
        /// Restart the trail from where she stands, so she simply turns and follows instead of walking
        /// out to the turn first (and gets passed through calmly instead of being thrown to the far side).
        /// </summary>
        void TryShortcut(Vector2 leaderPos, float leaderLen)
        {
            if (!Leader.Grounded || !Follower.Grounded) return;
            Vector2 self = Follower.transform.position;
            float straight = Vector2.Distance(self, leaderPos);
            if (leaderLen - cursor < straight + 0.3f) return;
            if (Mathf.Abs(self.y - leaderPos.y) > 0.3f) return;
            // the floor must be continuous between them (never a shortcut over a pit)
            int n = Mathf.CeilToInt(Mathf.Abs(leaderPos.x - self.x) / 0.25f);
            for (int i = 1; i < n; i++)
            {
                Vector2 q = Vector2.Lerp(self, leaderPos, i / (float)n);
                if (!GroundBelow(q, 0.35f, out float gy) || Mathf.Abs(gy - q.y) > 0.35f) return;
            }
            float v = cursorVel;
            Vector2 keepOff = sideOffset, keepOffVel = sideOffsetVel;
            ResetTrail();
            sideOffset = keepOff; sideOffsetVel = keepOffVel;
            trail.Add(new Crumb { t = Time.time - followDelay, len = straight, p = leaderPos, ground = true });
            lastWant = straight - followGap;
            // keep her pace only if the leader is still heading away from her
            cursorVel = Mathf.Sign(Leader.Velocity.x) == Mathf.Sign(leaderPos.x - self.x) ? v : 0f;
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

        /// <summary>Her path point is unusable (no floor under it) or sits on the leader: she has to stand elsewhere.</summary>
        bool NeedsStepAside(Vector2 pathPoint, Vector2 leaderPos)
        {
            if (!GroundBelow(pathPoint, 0.3f, out _)) return true;
            return Mathf.Abs(pathPoint.x - leaderPos.x) < followGap * 0.5f;
        }

        /// <summary>Never share the leader's column: the farthest spot with floor, on the side she is already on.</summary>
        Vector2 FindSpotBeside(Vector2 body, Vector2 leaderPos)
        {
            float side = Mathf.Abs(body.x - leaderPos.x) > 0.05f ? Mathf.Sign(body.x - leaderPos.x) : -Leader.Facing;
            float baseY = float.IsNegativeInfinity(groundY) ? leaderPos.y : groundY;
            for (float off = followGap; off > 0.01f; off -= 0.1f)
            {
                float x = leaderPos.x + side * off;
                var hit = Down(new Vector2(x, baseY + 0.6f), 1.2f);
                // both feet on it: not balanced on the very edge
                var outer = Down(new Vector2(x + side * edgeMargin, baseY + 0.6f), 1.2f);
                if (hit.collider && outer.collider && Mathf.Abs(outer.point.y - hit.point.y) < 0.3f) return new Vector2(x, hit.point.y);
            }
            return new Vector2(leaderPos.x, baseY);
        }
    }
}
