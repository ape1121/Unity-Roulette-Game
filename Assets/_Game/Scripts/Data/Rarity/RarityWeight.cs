using System;
using UnityEngine;

namespace Ape.Data
{
    [Serializable]
    public struct RarityWeight
    {
        public RarityType rarity;
        [Min(0f)] public float weight;
    }
}