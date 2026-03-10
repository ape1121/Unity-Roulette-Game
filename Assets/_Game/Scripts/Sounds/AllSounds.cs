using UnityEngine;

namespace Ape.Sounds
{
    [CreateAssetMenu(fileName = "AllSounds", menuName = "CriticalShot/Sounds/AllSounds", order = 1)]
    public class AllSounds : ScriptableObject
    {
        public Sound[] sounds;
    }
}
