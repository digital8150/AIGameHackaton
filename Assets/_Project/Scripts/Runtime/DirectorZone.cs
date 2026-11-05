using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Marks the stretch of level the director reasons about.</summary>
    public class DirectorZone : MonoBehaviour
    {
        public string zoneId;
        [Tooltip("Which sense holds this zone's clue: sound | visual | timing")]
        public string clueSense = "sound";
        public float minX, maxX;

        public bool Contains(float x) => x >= minX && x <= maxX;
    }
}
