using System;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace DuskTwilight
{
    public static class GameEvents
    {
        public static Action<Sense> Switched;
        public static Action<string> Died;              // zone id
        public static Action<string> ZoneEntered;       // zone id
        public static Action<string, bool> PuzzleAttempt; // puzzle id, success
        public static Action Cleared;
        public static Action Jumped;
        public static Action<string> Hint;               // the twilight voice whispering (narration)
        public static Action<string> Notice;             // the game quietly adjusted itself (assist chip)
        public static Action<string> Caption;            // what a sound looks like when you cannot hear it

        public static void Reset()
        {
            Switched = null; Died = null; ZoneEntered = null; PuzzleAttempt = null; Cleared = null; Jumped = null; Hint = null; Notice = null; Caption = null;
        }
    }

    /// <summary>Checkpoints, falling, respawn, clear. Owns the input lock used during fades.</summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I { get; private set; }
        public static bool InputLocked => fadeLock || ModalOpen;
        /// <summary>A modal panel (e.g. the word altar) is taking input.</summary>
        public static bool ModalOpen { get; set; }
        static bool fadeLock;

        [Header("Chapter")]
        [Tooltip("Title card: label · name, then the line.")]
        public string chapterLabel = "제3장";
        public string chapterName = "황혼";
        public string chapterLine = "어둠 속의 왕자, 빛 속의 공주";
        [Tooltip("Scene loaded after the clear card. Empty = this is the last chapter.")]
        public string nextScene = "";
        public string clearTitle = "황혼에서 만나다";
        public string clearLine = "서로의 감각으로, 문을 열었다";

        [Header("Stage")]
        public Transform startPoint;
        [Tooltip("Falling below this world Y respawns at the last checkpoint (red line in the Scene view).")]
        public float killY = -4f;

        [Header("Pacing (seconds)")]
        public float introFade = 2.0f;
        public float introInputLock = 0.8f;
        public float respawnFadeOut = 0.35f;
        public float respawnFadeIn = 0.45f;
        [Tooltip("How long the clear card stays before the next chapter can be started.")]
        public float clearHold = 2.5f;

        [Header("Audio")]
        public AudioClip fallClip;
        public AudioClip checkpointClip;

        public Vector2 Checkpoint { get; private set; }
        public string CurrentZone { get; set; } = "start";
        public int Deaths { get; private set; }
        public float StartTime { get; private set; }
        public bool Cleared { get; private set; }

        AudioSource sfx;
        bool respawning;

        void Awake()
        {
            I = this;
            GameEvents.Reset();
            fadeLock = false;
            ModalOpen = false;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void Start()
        {
            Checkpoint = startPoint ? (Vector2)startPoint.position : Vector2.zero;
            DuoController.I.PlaceAt(Checkpoint);
            StartTime = Time.time;
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            fadeLock = true;
            HUD.I?.Fade(1f, 0f, introFade);
            HUD.I?.ShowTitle(chapterLabel, chapterName, chapterLine);
            yield return new WaitForSeconds(introInputLock);
            fadeLock = false;
        }

        public void SetCheckpoint(Vector2 p, string zone)
        {
            if ((p - Checkpoint).sqrMagnitude < 0.01f) return;
            Checkpoint = p;
            CurrentZone = zone;
            if (checkpointClip) sfx.PlayOneShot(checkpointClip, 0.5f);
        }

        void Update()
        {
            var leader = DuoController.I ? DuoController.I.Leader : null;
            if (!respawning && !Cleared && leader && leader.transform.position.y < killY)
                StartCoroutine(Respawn());
        }

        /// <summary>Hazards (water, thorns): fall back to the last checkpoint as if the leader dropped below killY.</summary>
        public void Fall()
        {
            if (!respawning && !Cleared) StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            respawning = true;
            fadeLock = true;
            Deaths++;
            GameEvents.Died?.Invoke(CurrentZone);
            if (fallClip) sfx.PlayOneShot(fallClip, 0.7f);
            CameraRig.I?.Kick(0.3f);
            HUD.I?.Fade(0f, 1f, respawnFadeOut);
            yield return new WaitForSeconds(respawnFadeOut + 0.05f);
            DuoController.I.PlaceAt(Checkpoint);
            CameraRig.I?.Snap();
            HUD.I?.Fade(1f, 0f, respawnFadeIn);
            yield return new WaitForSeconds(0.2f);
            fadeLock = false;
            respawning = false;
        }

        public void Clear()
        {
            if (Cleared) return;
            Cleared = true;
            fadeLock = true;
            GameEvents.Cleared?.Invoke();
            StartCoroutine(Continue());
        }

        IEnumerator Continue()
        {
            // let the clear card breathe, then any jump/interact moves on to the next chapter
            yield return new WaitForSeconds(clearHold);
            HUD.I?.ShowContinue(!string.IsNullOrEmpty(nextScene));
            if (string.IsNullOrEmpty(nextScene)) yield break;
            while (!(GameInput.I.Jump.WasPressedThisFrame() || GameInput.I.Interact.WasPressedThisFrame())) yield return null;
            HUD.I?.Fade(0f, 1f, 0.8f);
            yield return new WaitForSeconds(0.9f);
            SceneManager.LoadScene(nextScene);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.8f);
            Gizmos.DrawLine(new Vector3(-50, killY), new Vector3(400, killY));
            if (startPoint)
            {
                Gizmos.color = new Color(0.5f, 1f, 0.6f);
                Gizmos.DrawWireSphere(startPoint.position, 0.3f);
            }
        }
#endif
    }
}
