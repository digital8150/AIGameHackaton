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
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;
        public float maxFall = 18f;
        public LayerMask groundMask = ~0;

        [Header("Animation")]
        public Sprite[] frames;             // 0-1 idle, 2-5 run, 6 jump, 7 fall
        public float runFps = 10f;

        [Header("Footsteps")]
        public AudioClip[] footsteps;
        public float stepInterval = 0.28f;
        public float stepEchoStrength = 0.32f;

        public bool IsLeader { get; private set; }
        public bool Grounded { get; private set; }
        public int Facing { get; private set; } = 1;
        public Vector2 Velocity => rb ? rb.linearVelocity : Vector2.zero;

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
                jumpHeld = GameInput.I.Jump.IsPressed();
            }
            else jumpHeld = false;
            Animate();
        }

        void FixedUpdate()
        {
            if (!IsLeader) return;
            Grounded = CheckGround();
            if (Grounded) lastGrounded = Time.time;

            float input = (GameInput.I != null && !GameFlow.InputLocked) ? GameInput.I.Move.ReadValue<float>() : 0f;
            if (Mathf.Abs(input) < 0.2f) input = 0;
            var v = rb.linearVelocity;
            float target = input * runSpeed;
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
                audioSrc.PlayOneShot(footsteps[Random.Range(0, footsteps.Length)], 0.55f * mul);
            // only the prince's world is shaped by sound
            if (SenseController.I && SenseController.I.IsDark)
                SenseController.I.EmitEcho((Vector2)transform.position + Vector2.up * 0.1f, stepEchoStrength * mul);
        }

        // ---------------- follower ------------------------------------------------------------
        public void FollowTo(Vector2 target, int leaderFacing, float dt)
        {
            Vector2 p = transform.position;
            Vector2 next = Vector2.SmoothDamp(p, target, ref followVel, 0.06f, 40f, dt);
            float dx = next.x - p.x;
            if (Mathf.Abs(dx) > 0.004f) Facing = dx > 0 ? 1 : -1;
            else Facing = leaderFacing;
            Grounded = Mathf.Abs(next.y - p.y) < 0.01f;
            rb.MovePosition(next);
            followSpeed = dx / Mathf.Max(dt, 1e-4f);
            followVy = (next.y - p.y) / Mathf.Max(dt, 1e-4f);
        }

        float followSpeed, followVy;

        // ---------------- animation -----------------------------------------------------------
        void Animate()
        {
            if (frames == null || frames.Length < 8) return;
            float vx = IsLeader ? rb.linearVelocity.x : followSpeed;
            float vy = IsLeader ? rb.linearVelocity.y : followVy;
            bool ground = IsLeader ? Grounded : Mathf.Abs(vy) < 0.5f;
            animTime += Time.deltaTime;
            int f;
            if (!ground) f = vy > 0.5f ? 6 : 7;
            else if (Mathf.Abs(vx) > 0.6f) f = 2 + (int)(animTime * runFps) % 4;
            else f = (int)(animTime * 1.6f) % 2;
            sr.sprite = frames[f];
            sr.flipX = Facing < 0;
        }
    }
}
