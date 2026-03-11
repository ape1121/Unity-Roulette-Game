using System.Collections.Generic;
using System;
using Ape.Core;
using UnityEngine;

namespace Ape.Profile
{
    public sealed class ProfileManager : IManager
    {
        private const string SaveKey = "CriticalShot.SaveData";

        private SaveData _currentData;

        public event Action<SaveData> DataChanged;

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
                PublishDataChanged();
                return _currentData;
            }

            _currentData = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            EnsureDataInitialized();
            PublishDataChanged();
            return _currentData;
        }

        public void Save()
        {
            EnsureDataInitialized();
            string json = JsonUtility.ToJson(_currentData);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
            PublishDataChanged();
        }

        public bool CanAffordCash(int amount)
        {
            return _currentData.Cash >= Mathf.Max(0, amount);
        }

        public bool CanAffordGold(int amount)
        {
            return _currentData.Gold >= Mathf.Max(0, amount);
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

        public void AddInventoryReward(string rewardId, int amount, bool saveImmediately = true)
        {
            EnsureDataInitialized();

            if (!AddInventoryInternal(rewardId, amount))
                return;

            if (saveImmediately)
                Save();
        }

        public int GetInventoryRewardCount(string rewardId)
        {
            EnsureDataInitialized();

            if (string.IsNullOrWhiteSpace(rewardId))
                return 0;

            for (int i = 0; i < _currentData.Inventory.Count; i++)
            {
                if (_currentData.Inventory[i].RewardId == rewardId)
                    return Mathf.Max(0, _currentData.Inventory[i].Amount);
            }

            return 0;
        }

        public bool TrySpendInventoryReward(string rewardId, int amount, bool saveImmediately = true)
        {
            EnsureDataInitialized();

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            int resolvedAmount = Mathf.Max(0, amount);
            if (resolvedAmount == 0)
                return true;

            for (int i = 0; i < _currentData.Inventory.Count; i++)
            {
                if (_currentData.Inventory[i].RewardId != rewardId)
                    continue;

                RewardInventoryEntry entry = _currentData.Inventory[i];
                if (entry.Amount < resolvedAmount)
                    return false;

                entry.Amount -= resolvedAmount;

                if (entry.Amount > 0)
                    _currentData.Inventory[i] = entry;
                else
                    _currentData.Inventory.RemoveAt(i);

                if (saveImmediately)
                    Save();

                return true;
            }

            return false;
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

        public bool TrySpendGold(int amount, bool saveImmediately = true)
        {
            int resolvedAmount = Mathf.Max(0, amount);
            if (_currentData.Gold < resolvedAmount)
                return false;

            _currentData.Gold -= resolvedAmount;

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
            else
                PublishDataChanged();
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

        private bool AddInventoryInternal(string rewardId, int amount)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            int resolvedAmount = Mathf.Max(0, amount);
            if (resolvedAmount == 0)
                return false;

            for (int i = 0; i < _currentData.Inventory.Count; i++)
            {
                if (_currentData.Inventory[i].RewardId != rewardId)
                    continue;

                RewardInventoryEntry entry = _currentData.Inventory[i];
                entry.Amount += resolvedAmount;
                _currentData.Inventory[i] = entry;
                return true;
            }

            _currentData.Inventory.Add(new RewardInventoryEntry(rewardId, resolvedAmount));
            return true;
        }

        private void PublishDataChanged()
        {
            DataChanged?.Invoke(_currentData);
        }
    }
}
