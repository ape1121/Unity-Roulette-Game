using System;
using System.Collections.Generic;

namespace Ape.Profile
{
    [Serializable]
    public struct SaveData
    {
        public int Level;
        public int Cash;
        public int Gold;
        public List<RewardInventoryEntry> Inventory;
    }
}
