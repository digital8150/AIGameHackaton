using System.Collections.Generic;
using System.Threading.Tasks;

namespace DuskTwilight
{
    /// <summary>
    /// The accessibility director's two questions (기획서 "AI 활용"), asked to Jev in one call:
    ///   1) 플레이어가 놓치는 신호는? [sound, visual, timing, none]
    ///   2) 개입 강도는? [none, subtle, strong]
    /// It judges "which signal is being missed" only — it never diagnoses the player.
    /// Returns null when Jev is unavailable so the caller falls back to rules.
    /// </summary>
    public class JevDirectorBrain
    {
        static readonly Dictionary<string, JevClient.Choice> Questions = new Dictionary<string, JevClient.Choice>
        {
            ["missed"] = new JevClient.Choice
            {
                instructions = "A player of a two-character puzzle platformer is struggling in the current zone. " +
                               "The prince perceives the world only through sound (echoes reveal hidden things); the princess only through sight (light signals, timing, inscriptions). " +
                               "Based on the play telemetry, which kind of in-game signal is the player most likely missing? Judge the signal, not the player.",
                criteria = new Dictionary<string, string>
                {
                    ["sound"] = "They rarely use the prince's hearing/echo although this zone's clue is audible (hidden stones, hidden mechanisms).",
                    ["visual"] = "They stay in the prince's dark although this zone's clue is visible only to the princess (light signals, inscriptions).",
                    ["timing"] = "They see the right signal but keep acting outside the window it shows.",
                    ["none"] = "They are progressing or have not struggled enough to need help."
                }
            },
            ["intensity"] = new JevClient.Choice
            {
                instructions = "How strongly should the game adapt to help, without taking the puzzle away?",
                criteria = new Dictionary<string, string>
                {
                    ["none"] = "Do nothing yet.",
                    ["subtle"] = "A gentle boost of the missed signal and a soft hint.",
                    ["strong"] = "A clear boost: repeated failures or a long time stuck."
                }
            }
        };

        public async Task<DirectorDecision?> DecideAsync(DirectorObservation o)
        {
            if (!JevClient.HasKey) return null;
            var state = new
            {
                current_sense = o.mode == Sense.Dark ? "prince (sound)" : "princess (light)",
                zone = o.zone,
                zone_clue_sense = o.clueSense,
                seconds_in_zone = (int)o.timeInZone,
                share_of_time_in_prince_sense = System.Math.Round(o.darkFraction, 2),
                falls_in_zone = o.deathsInZone,
                switches_in_zone = o.switchesInZone,
                echo_calls_in_zone = o.echoCallsInZone,
                failed_attempts = o.failsInZone,
                wrong_lever_pulls = o.leverFails,
                mistimed_pulls = o.timingFails,
                trigger = o.trigger
            };
            var answers = await JevClient.AskAsync(state, Questions);
            if (answers == null || !answers.TryGetValue("missed", out var m)) return null;
            answers.TryGetValue("intensity", out var i);
            return new DirectorDecision
            {
                missed = m.choice switch { "sound" => MissedSignal.Sound, "visual" => MissedSignal.Visual, "timing" => MissedSignal.Timing, _ => MissedSignal.None },
                intensity = i.choice switch { "strong" => Intervention.Strong, "subtle" => Intervention.Subtle, _ => Intervention.None },
                confidence = m.confidence,
                source = "Jev"
            };
        }
    }
}
