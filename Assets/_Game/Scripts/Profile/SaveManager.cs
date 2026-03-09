using System.Collections.Generic;
using Ape.Core;
using UnityEngine;

namespace Ape.Profile
{
    public sealed class ProfileManager : IManager
    {
        private const string SaveKey = "CriticalShot.SaveData";

        private SaveData _currentData;

        public SaveData CurrentData => _currentData;
        public IReadOnlyList<RewardInventoryEntry> Inventory => _currentData.Inventory;

        public void Initialize()
        {
            Load();
        }

        public SaveData Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                _currentData = CreateDefaultData();
                return _currentData;
            }

            _currentData = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            EnsureDataInitialized();
            return _currentData;
        }

        public void Save()
        {
            EnsureDataInitialized();
            string json = JsonUtility.ToJson(_currentData);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        public bool CanAffordCash(int amount)
        {
            return _currentData.Cash >= Mathf.Max(0, amount);
        }

        public void AddCash(int amount, bool saveImmediately = true)
        {
            int resolvedAmount = Mathf.Max(0, amount);
            if (resolvedAmount == 0)
                return;

            _currentData.Cash += resolvedAmount;

            if (saveImmediately)
                Save();
        }

        public void AddGold(int amount, bool saveImmediately = true)
        {
            int resolvedAmount = Mathf.Max(0, amount);
            if (resolvedAmount == 0)
                return;

            _currentData.Gold += resolvedAmount;

            if (saveImmediately)
                Save();
        }

        public bool TrySpendCash(int amount, bool saveImmediately = true)
        {
            int resolvedAmount = Mathf.Max(0, amount);
            if (_currentData.Cash < resolvedAmount)
                return false;

            _currentData.Cash -= resolvedAmount;

            if (saveImmediately)
                Save();

            return true;
        }

        public void ApplyBankedRewards(int cash, int gold, IReadOnlyList<RewardInventoryEntry> inventoryRewards, bool saveImmediately = true)
        {
            EnsureDataInitialized();

            _currentData.Cash += Mathf.Max(0, cash);
            _currentData.Gold += Mathf.Max(0, gold);

            if (inventoryRewards != null)
            {
                for (int i = 0; i < inventoryRewards.Count; i++)
                    AddInventoryInternal(inventoryRewards[i].RewardId, inventoryRewards[i].Amount);
            }

            if (saveImmediately)
                Save();
        }

        public void SetData(SaveData saveData, bool saveImmediately = true)
        {
            _currentData = saveData;
            EnsureDataInitialized();

            if (saveImmediately)
                Save();
        }

        public void Reset()
        {
            _currentData = CreateDefaultData();
            Save();
        }

        private static SaveData CreateDefaultData()
        {
            return new SaveData
            {
                Level = 0,
                Cash = 0,
                Gold = 0,
                Inventory = new List<RewardInventoryEntry>()
            };
        }

        private void EnsureDataInitialized()
        {
            if (_currentData.Inventory == null)
                _currentData.Inventory = new List<RewardInventoryEntry>();
        }

        private void AddInventoryInternal(string rewardId, int amount)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
                return;

            int resolvedAmount = Mathf.Max(0, amount);
            if (resolvedAmount == 0)
                return;

            for (int i = 0; i < _currentData.Inventory.Count; i++)
            {
                if (_currentData.Inventory[i].RewardId != rewardId)
                    continue;

                RewardInventoryEntry entry = _currentData.Inventory[i];
                entry.Amount += resolvedAmount;
                _currentData.Inventory[i] = entry;
                return;
            }

            _currentData.Inventory.Add(new RewardInventoryEntry(rewardId, resolvedAmount));
        }
    }
}
