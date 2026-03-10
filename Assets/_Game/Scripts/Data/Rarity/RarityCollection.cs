using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RarityCollection", menuName = "Critical Shot/Rarities/Rarity Collection")]
    public class RarityCollection : ScriptableObject
    {
        public RarityData[] Rarities;
    }
}