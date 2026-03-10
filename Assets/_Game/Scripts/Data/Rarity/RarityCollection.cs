using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RarityCollection", menuName = "CriticalShot/Rarities/Rarity Collection")]
    public class RarityCollection : ScriptableObject
    {
        public RarityData[] Rarities;
    }
}