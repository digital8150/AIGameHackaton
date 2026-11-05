using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// One of the pair. When it leads, it is a responsive platformer body (coyote time, jump buffer,
    /// variable jump). When it follows, it walks the leader's recent trail and has no physics.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class Actor : MonoBehaviour
    {
        public Sense sense;                 // Dark = Twilight Prince, Light = Dusk Princess
        public string displayName;

        [Header("Motion")]
        public float runSpeed = 6.2f;
        public float accel = 70f;
        public float airAccel = 45f;
        public float jumpVelocity = 12.2f;
        public float gravity = 3.4f;
        public float fallGravityMul = 1.55f;
        public float jumpCutMul = 0.45f;
        public float coyoteTime = 0.15f;
        public float jumpBuffer = 0.15f;
        public float maxFall = 18f;
        public LayerMask groundMask = ~0;

        [Header("Animation (simple 8-frame set — used when no sheets are assigned)")]
        public Sprite[] frames;             // 0-1 idle, 2-5 run, 6 jump, 7 fall
        public float runFps = 10f;

        [Header("Animation (sprite sheets — override the 8-frame set)")]
        public Sprite[] idleFrames;
        public Sprite[] walkFrames;
        [Tooltip("Whole jump sheet: crouch → rise → apex/fall → land")]
        public Sprite[] jumpFrames;
        [Min(1)] public float idleFps = 14f;
        [Tooltip("Walk fps at full run speed (scales down with speed)")]
        [Min(1)] public float walkFps = 32f;
        [Tooltip("Jump sheet frames shown while rising (first..last); picked by vertical speed")]
        public Vector2Int jumpRise = new Vector2Int(4, 13);
        [Tooltip("Jump sheet frames shown from the apex while falling")]
        public Vector2Int jumpFall = new Vector2Int(14, 20);
        [Tooltip("Jump sheet frames played once on touchdown (skipped while walking)")]
        public Vector2Int jumpLand = new Vector2Int(21, 27);
        [Min(1)] public float landFps = 36f;
        [Tooltip("The sheets are drawn facing left")]
        public bool sheetFacesLeft;
        [Tooltip("Pushing / pulling a box (or straining against one that won't move)")]
        public Sprite[] pushFrames;
        [Tooltip("Pulling a box (walking backwards, hands toward the box). Empty = push frames.")]
        public Sprite[] pullFrames;
        [Tooltip("The pull sheet is drawn facing left (hands reaching left)")]
        public bool pullSheetFacesLeft;
        [Tooltip("Push cycle fps while the box moves at full push speed (slower when straining)")]
        [Min(1)] public float pushFps = 20f;

        [Header("Footsteps")]
        public AudioClip[] footsteps;
        public float stepInterval = 0.28f;
        public float stepEchoStrength = 0.32f;

        public bool IsLeader { get; private set; }
        public bool Grounded { get; private set; }
        public int Facing { get; private set; } = 1;
        public Vector2 Velocity => rb ? rb.linearVelocity : Vector2.zero;
        /// <summary>Run speed multiplier, set each frame by whatever slows the leader (a heavy box).</summary>
        public float SpeedScale { get; set; } = 1f;
        /// <summary>Set by a box every frame the leader is pushing against it.</summary>
        public void MarkPushing() => pushUntil = Time.time + 0.25f;
        /// <summary>Pulling a box that is on <paramref name="boxDir"/> (-1 left, +1 right) of her.</summary>
        public void MarkPulling(int boxDir) { pullUntil = Time.time + 0.25f; pullBoxDir = boxDir; }
        public bool Pulling => Time.time < pullUntil;
        float pullUntil = -9f; int pullBoxDir = 1;
        public bool Pushing => Time.time < pushUntil;
        float pushUntil = -9f, pushPhase;
        /// <summary>The input direction the leader is holding (-1, 0, 1).</summary>
        public int InputDir { get; private set; }
        /// <summary>Automated tests: walk this way instead of reading the stick (NaN = off).</summary>
        public float TestMove { get; set; } = float.NaN;
        /// <summary>Automated tests: hold Interact.</summary>
        public bool TestInteract { get; set; }
        /// <summary>Automated tests: hold jump (full-height jumps with <see cref="RequestJump"/>).</summary>
        public bool TestJumpHeld { get; set; }
        public bool InteractHeld => TestInteract || (GameInput.I != null && GameInput.I.Interact.IsPressed());

        Rigidbody2D rb;
        CapsuleCollider2D col;
        SpriteRenderer sr;
        AudioSource audioSrc;
        float lastGrounded = -9, lastJumpPressed = -9, stepTimer, animTime;
        bool jumpHeld;
        Vector2 followVel;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CapsuleCollider2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
            audioSrc = GetComponent<AudioSource>();
            rb.gravityScale = gravity;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void SetLeader(bool lead)
        {
            IsLeader = lead;
            rb.bodyType = lead ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            col.enabled = lead;
            if (!lead) rb.linearVelocity = Vector2.zero;
            sr.sortingOrder = lead ? 21 : 20;
        }

        public void Teleport(Vector2 p)
        {
            rb.position = p;
            transform.position = p;
            rb.linearVelocity = Vector2.zero;
        }

        /// <summary>Voice "점프": same as pressing jump this frame (still needs ground / coyote time).</summary>
        public void RequestJump() { if (IsLeader) lastJumpPressed = Time.time; }

        // ---------------- leader --------------------------------------------------------------
        void Update()
        {
            if (IsLeader && GameInput.I != null && !GameFlow.InputLocked)
            {
                if (GameInput.I.Jump.WasPressedThisFrame()) lastJumpPressed = Time.time;
                jumpHeld = GameInput.I.Jump.IsPressed() || TestJumpHeld;
            }
            else jumpHeld = IsLeader && TestJumpHeld;
            Animate();
        }

        void FixedUpdate()
        {
            if (!IsLeader) return;
            Grounded = CheckGround();
            if (Grounded) lastGrounded = Time.time;

            float input = !float.IsNaN(TestMove) ? TestMove : (GameInput.I != null && !GameFlow.InputLocked) ? GameInput.I.Move.ReadValue<float>() : 0f;
            if (Mathf.Abs(input) < 0.2f) input = 0;
            InputDir = input > 0 ? 1 : input < 0 ? -1 : 0;
            var v = rb.linearVelocity;
            float target = input * runSpeed * SpeedScale;
            SpeedScale = 1f;   // boxes set it again every FixedUpdate they are being moved
            float a = Grounded ? accel : airAccel;
            v.x = Mathf.MoveTowards(v.x, target, a * Time.fixedDeltaTime);
            if (input != 0) Facing = input > 0 ? 1 : -1;

            if (Time.time - lastJumpPressed <= jumpBuffer && Time.time - lastGrounded <= coyoteTime)
            {
                v.y = jumpVelocity;
                GameEvents.Jumped?.Invoke();
                lastJumpPressed = -9;
                lastGrounded = -9;
                Step(1.4f);
            }

            if (v.y > 0 && !jumpHeld) v.y *= Mathf.Pow(jumpCutMul, Time.fixedDeltaTime * 10f);
            rb.gravityScale = v.y < 0 ? gravity * fallGravityMul : gravity;
            v.y = Mathf.Max(v.y, -maxFall);
            rb.linearVelocity = v;

            if (Grounded && Mathf.Abs(v.x) > 0.5f)
            {
                stepTimer -= Time.fixedDeltaTime;
                if (stepTimer <= 0) { Step(1f); stepTimer = stepInterval; }
            }
        }

        bool CheckGround()
        {
            var b = col.bounds;
            var hit = Physics2D.BoxCast(new Vector2(b.center.x, b.min.y + 0.05f), new Vector2(b.size.x * 0.8f, 0.05f), 0, Vector2.down, 0.08f, groundMask);
            return hit.collider != null && rb.linearVelocity.y <= 0.01f;
        }

        void Step(float mul)
        {
            if (footsteps != null && footsteps.Length > 0 && audioSrc)
                audioSrc.PlayLimited(footsteps[Random.Range(0, footsteps.Length)], 0.55f * mul);
            // only the prince's world is shaped by sound
            if (SenseController.I && SenseController.I.IsDark)
                SenseController.I.EmitEcho((Vector2)transform.position + Vector2.up * 0.1f, stepEchoStrength * mul);
        }

        // ---------------- follower ------------------------------------------------------------
        [Header("Follower")]
        [Tooltip("How softly the follower closes on her path point (s). The path itself carries the jump arcs.")]
        [Range(0.02f, 0.2f)] public float followSmooth = 0.05f;
        [Tooltip("Walk cycle starts above this speed and stops below the lower one (no walk/idle flicker)")]
        public Vector2 followWalkHysteresis = new Vector2(0.35f, 0.8f);

        /// <param name="grounded">The path point is on the ground (the leader walked there), not mid-jump.</param>
        /// <param name="smooth">Smooth time override (a gentle glide when her target jumped, e.g. stepping aside); <=0 = followSmooth.</param>
        public void FollowTo(Vector2 target, bool grounded, int leaderFacing, float dt, float smooth = -1f, float maxSpeed = 30f)
        {
            Vector2 p = rb.position;
            Vector2 next = Vector2.SmoothDamp(p, target, ref followVel, smooth > 0 ? smooth : followSmooth, maxSpeed, dt);
            float inv = 1f / Mathf.Max(dt, 1e-4f);
            float vx = (next.x - p.x) * inv, vy = (next.y - p.y) * inv;
            // low-pass so tiny path corrections don't read as steps
            float k = 1f - Mathf.Exp(-dt * 18f);
            followSpeed = Mathf.Lerp(followSpeed, vx, k);
            followVy = Mathf.Lerp(followVy, vy, k);

            float s = Mathf.Abs(followSpeed);
            if (followWalking ? s < followWalkHysteresis.x : s > followWalkHysteresis.y) followWalking = !followWalking;
            if (followWalking || Mathf.Abs(followSpeed) > 1.5f) Facing = followSpeed > 0 ? 1 : -1;
            else if (s < 0.1f) Facing = leaderFacing;
            // on the ground = following a walked stretch and already (almost) at its height
            Grounded = grounded && Mathf.Abs(target.y - next.y) < 0.12f && Mathf.Abs(vy) < 6f;
            rb.MovePosition(next);
        }

        float followSpeed, followVy;
        bool followWalking;

        public void Face(int dir) { if (dir != 0) Facing = dir > 0 ? 1 : -1; }

        // ---------------- switch (visual only) ------------------------------------------------
        Transform[] visuals; Vector3[] visualBase;
        Vector2 swapFrom; float swapStart = -9f, swapDur, swapHop;

        /// <summary>The body is already at its new spot; its picture slides over from <paramref name="worldFrom"/>.</summary>
        public void SwapVisualFrom(Vector2 worldFrom, float duration, float hop)
        {
            if (visuals == null)
            {
                visuals = new Transform[transform.childCount];
                visualBase = new Vector3[visuals.Length];
                for (int i = 0; i < visuals.Length; i++) { visuals[i] = transform.GetChild(i); visualBase[i] = visuals[i].localPosition; }
            }
            swapFrom = worldFrom - (Vector2)transform.position;
            swapStart = Time.time; swapDur = Mathf.Max(0.01f, duration); swapHop = hop;
            ApplySwap();
        }

        void LateUpdate() { if (visuals != null) ApplySwap(); }

        void ApplySwap()
        {
            float u = Mathf.Clamp01((Time.time - swapStart) / swapDur);
            float e = 1f - (1f - u) * (1f - u) * (1f - u);          // ease-out
            Vector2 off = swapFrom * (1f - e) + Vector2.up * (swapHop * 4f * u * (1f - u));
            if (u >= 1f) off = Vector2.zero;
            Vector3 local = transform.InverseTransformVector(off);
            for (int i = 0; i < visuals.Length; i++) if (visuals[i]) visuals[i].localPosition = visualBase[i] + local;
        }

        // ---------------- animation -----------------------------------------------------------
        bool wasAirborne;
        float landTime = -9f, airStart = -9f, walkPhase;

        static Sprite Pick(Sprite[] set, int i) => set[Mathf.Clamp(i, 0, set.Length - 1)];

        // SRP Batcher: sprites with different textures must not share one custom material (wrong texture is drawn).
        // Each sheet texture gets its own runtime copy of the actor material.
        readonly System.Collections.Generic.Dictionary<Texture, Material> sheetMats = new System.Collections.Generic.Dictionary<Texture, Material>();
        Material baseMat;

        void SetSheetSprite(Sprite s)
        {
            if (sr.sprite == s) return;
            if (!baseMat) baseMat = sr.sharedMaterial;
            if (baseMat && !sheetMats.TryGetValue(s.texture, out var m))
            {
                m = new Material(baseMat) { name = baseMat.name + " (" + s.texture.name + ")" };
                sheetMats[s.texture] = m;
            }
            if (baseMat) sr.sharedMaterial = sheetMats[s.texture];
            sr.sprite = s;
        }

        void OnDestroy()
        {
            foreach (var m in sheetMats.Values) if (m) Destroy(m);
        }

        void Animate()
        {
            float vx = IsLeader ? rb.linearVelocity.x : followSpeed;
            float vy = IsLeader ? rb.linearVelocity.y : followVy;
            bool ground = Grounded;
            bool moving = IsLeader ? Mathf.Abs(vx) > 0.6f : followWalking;
            animTime += Time.deltaTime;

            bool sheets = idleFrames != null && idleFrames.Length > 0 && walkFrames != null && walkFrames.Length > 0
                          && jumpFrames != null && jumpFrames.Length > 0;
            if (sheets)
            {
                // only a real airtime earns a landing — a one-frame bump over a seam must not flash the land frames
                if (!ground) { if (!wasAirborne) airStart = Time.time; wasAirborne = true; }
                else if (wasAirborne) { wasAirborne = false; if (Time.time - airStart > 0.12f) landTime = Time.time; }

                Sprite s;
                if (!ground)
                {
                    if (vy > 0f)
                    {
                        float t = Mathf.Clamp01(1f - vy / Mathf.Max(0.1f, jumpVelocity));
                        s = Pick(jumpFrames, Mathf.RoundToInt(Mathf.Lerp(jumpRise.x, jumpRise.y, t)));
                    }
                    else
                    {
                        float t = Mathf.Clamp01(-vy / Mathf.Max(0.1f, jumpVelocity));
                        s = Pick(jumpFrames, Mathf.RoundToInt(Mathf.Lerp(jumpFall.x, jumpFall.y, t)));
                    }
                }
                else if (Pulling && pullFrames != null && pullFrames.Length > 0)
                {
                    pushPhase += Time.deltaTime * pushFps * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(Mathf.Abs(vx) / 3f));
                    SetSheetSprite(Pick(pullFrames, (int)pushPhase % pullFrames.Length));
                    // she faces the box while walking away from it
                    sr.flipX = pullSheetFacesLeft ? pullBoxDir > 0 : pullBoxDir < 0;
                    return;
                }
                else if (Pushing && pushFrames != null && pushFrames.Length > 0)
                {
                    // the push cycle follows how fast she's actually moving: straining against something that won't
                    // budge (the prince, a wall) is a slow lean, not running in place
                    pushPhase += Time.deltaTime * pushFps * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(Mathf.Abs(vx) / 3f));
                    s = Pick(pushFrames, (int)pushPhase % pushFrames.Length);
                }
                else if (moving)
                {
                    float speed01 = Mathf.Clamp01(Mathf.Abs(vx) / Mathf.Max(0.1f, runSpeed));
                    // accumulate the phase: animTime * (changing rate) would skip many frames whenever the speed changes
                    walkPhase += Time.deltaTime * walkFps * Mathf.Lerp(0.5f, 1f, speed01);
                    s = Pick(walkFrames, (int)walkPhase % walkFrames.Length);
                }
                else
                {
                    int land = (int)((Time.time - landTime) * landFps);
                    s = land <= jumpLand.y - jumpLand.x
                        ? Pick(jumpFrames, jumpLand.x + land)
                        : Pick(idleFrames, (int)(animTime * idleFps) % idleFrames.Length);
                }
                SetSheetSprite(s);
                sr.flipX = sheetFacesLeft ? Facing > 0 : Facing < 0;
                return;
            }

            if (frames == null || frames.Length < 8) return;
            int f;
            if (!ground) f = vy > 0.5f ? 6 : 7;
            else if (Mathf.Abs(vx) > 0.6f) f = 2 + (int)(animTime * runFps) % 4;
            else f = (int)(animTime * 1.6f) % 2;
            sr.sprite = frames[f];
            sr.flipX = Facing < 0;
        }
    }
}
