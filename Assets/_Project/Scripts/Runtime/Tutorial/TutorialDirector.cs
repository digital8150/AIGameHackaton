using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Runs chapter 0: sections of objectives that complete in any order; when every objective of
    /// a section is done the next one begins (structure after the Towerdefense/ProjectAdventure tutorials).
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public TutorialSection[] sections;
        public TutorialSignals signals;
        public float sectionGapSeconds = 1.1f;
        public float firstSectionDelay = 2.8f; // let the chapter title card play first
        public AudioClip tickClip, sectionClip;

        public int SectionIndex { get; private set; } = -1;
        public TutorialSection Current => SectionIndex >= 0 && SectionIndex < sections.Length ? sections[SectionIndex] : null;
        public event System.Action<int, int, TutorialSection> SectionStarted;   // index, total, section
        public event System.Action<int, int, int, bool> ObjectiveChanged;       // objective, progress, target, done
        public event System.Action<int> SectionCompleted;
        public event System.Action Completed;

        int[] progress;
        bool transitioning = true;
        AudioSource sfx;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            foreach (var s in sections)
                if (s.opens != null) foreach (var v in s.opens) if (v) v.Close();
        }

        void OnEnable() { if (signals) signals.Raised += OnRaised; }
        void OnDisable() { if (signals) signals.Raised -= OnRaised; }

        IEnumerator Start()
        {
            if (DuoController.I) DuoController.I.switchLocked = true;
            yield return new WaitForSeconds(firstSectionDelay);
            Begin(0);
        }

        void Begin(int index)
        {
            SectionIndex = index;
            var section = Current;
            progress = new int[section.objectives.Length];
            if (section.opens != null) foreach (var v in section.opens) if (v) v.Open();
            if (section.allowSwitch && DuoController.I) DuoController.I.switchLocked = false;
            transitioning = false;
            if (sectionClip && index > 0) sfx.PlayLimited(sectionClip, 0.5f);
            SectionStarted?.Invoke(index, sections.Length, section);
            for (int k = 0; k < progress.Length; k++)
                ObjectiveChanged?.Invoke(k, 0, section.objectives[k].target, false);
        }

        void OnRaised(TutorialGoal goal, string id, int amount)
        {
            if (transitioning || Current == null) return;
            var objectives = Current.objectives;
            bool allDone = true;
            for (int k = 0; k < objectives.Length; k++)
            {
                var o = objectives[k];
                bool match = o.goal == goal && (goal != TutorialGoal.ReachMarker || o.markerId == id);
                if (match && progress[k] < o.target)
                {
                    progress[k] = Mathf.Min(o.target, progress[k] + amount);
                    bool done = progress[k] >= o.target;
                    if (done && tickClip) sfx.PlayLimited(tickClip, 0.45f);
                    ObjectiveChanged?.Invoke(k, progress[k], o.target, done);
                }
                if (progress[k] < o.target) allDone = false;
            }
            if (!allDone) return;
            transitioning = true;
            SectionCompleted?.Invoke(SectionIndex);
            StartCoroutine(Advance());
        }

        IEnumerator Advance()
        {
            yield return new WaitForSeconds(sectionGapSeconds);
            if (SectionIndex + 1 < sections.Length) Begin(SectionIndex + 1);
            else
            {
                SectionIndex = sections.Length;
                Completed?.Invoke();
            }
        }
    }
}
