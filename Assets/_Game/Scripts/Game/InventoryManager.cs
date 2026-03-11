using System.Collections.Generic;
using Ape.Data;
using Ape.Profile;
using UnityEngine;

namespace Ape.Game
{
    public sealed class InventoryManager
    {
        private readonly RunRewardLedger _pendingLedger = new RunRewardLedger();
        private readonly ContinueRewardSnapshot _continueSnapshot = new ContinueRewardSnapshot();

        private GameConfig _config;
        private ProfileManager _profile;
        private RewardManager _rewardManager;

        public CaseManager Cases { get; } = new CaseManager();
        public IReadOnlyList<ResolvedReward> PendingInventoryRewards => _pendingLedger.InventoryRewards;
        public int PendingCash => _pendingLedger.PendingCash;
        public int PendingGold => _pendingLedger.PendingGold;
        public int PendingInventoryRewardCount => _pendingLedger.PendingInventoryRewardCount;
        public int PendingInventoryRewardKinds => _pendingLedger.PendingInventoryRewardKinds;
        public int SavedCash => _profile != null ? _profile.CurrentData.Cash : 0;
        public int SavedGold => _profile != null ? _profile.CurrentData.Gold : 0;
        public int ContinueZone => _continueSnapshot.Zone;
        public bool HasContinueSnapshot => _continueSnapshot.HasValue;

        public void Configure(GameConfig config, ProfileManager profile, RewardManager rewardManager)
        {
            _config = config;
            _profile = profile;
            _rewardManager = rewardManager;
            Cases.Configure(config, profile, rewardManager);
        }

        public void Initialize()
        {
            ResetState();
            Cases.Initialize();
        }

        public void ResetState()
        {
            ResetRunState();
            Cases.ResetState();
        }

        public void ResetRunState()
        {
            _pendingLedger.Clear();
            _continueSnapshot.Clear();
        }

        public void Shutdown()
        {
            Cases.Shutdown();
            _rewardManager = null;
            _profile = null;
            _config = null;
            ResetRunState();
        }

        public bool CanContinue(int continueCost)
        {
            return _continueSnapshot.HasValue
                && _profile != null
                && _profile.CanAffordCash(Mathf.Max(0, continueCost));
        }

        public bool TrySpendContinueCost(int continueCost)
        {
            return _profile != null && _profile.TrySpendCash(Mathf.Max(0, continueCost));
        }

        public bool TryPayBuyIn(int buyInCost)
        {
            int resolvedBuyInCost = Mathf.Max(0, buyInCost);
            if (resolvedBuyInCost == 0)
                return true;

            EnsureProfile();
            return _profile.TrySpendCash(resolvedBuyInCost);
        }

        public void AddPendingReward(ResolvedReward reward)
        {
            if (!reward.HasReward || reward.Amount <= 0)
                return;

            _pendingLedger.AddReward(reward);
        }

        public void ClearPendingRewards()
        {
            _pendingLedger.Clear();
        }

        public void BankPendingRewards()
        {
            EnsureRewardManager();
            _rewardManager.GrantRewards(PendingCash, PendingGold, _pendingLedger.InventoryRewards);
            _pendingLedger.Clear();
        }

        public void CaptureContinueSnapshot(int zone)
        {
            _continueSnapshot.Capture(zone, _pendingLedger);
        }

        public void RestoreContinueSnapshot()
        {
            _pendingLedger.Restore(_continueSnapshot.PendingCash, _continueSnapshot.PendingGold, _continueSnapshot.InventoryRewards);
        }

        public void ClearContinueSnapshot()
        {
            _continueSnapshot.Clear();
        }

        public void GetPendingRewards(List<InventoryRewardEntry> destination)
        {
            destination?.Clear();

            if (destination == null)
                return;

            IReadOnlyList<ResolvedReward> rewards = _pendingLedger.InventoryRewards;
            if (rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                ResolvedReward reward = rewards[i];
                if (!reward.HasReward || !reward.IsInventoryReward || reward.Amount <= 0)
                    continue;

                destination.Add(new InventoryRewardEntry(reward, InventoryRewardAction.None));
            }

            destination.Sort(CompareRewards);
        }

        public void GetBankedRewards(List<InventoryRewardEntry> destination)
        {
            destination?.Clear();

            if (destination == null || _profile == null)
                return;

            IReadOnlyList<RewardInventoryEntry> inventory = _profile.Inventory;

            if (_config == null || inventory == null)
                return;

            bool casePresentationActive = Cases.IsPresentationActive;

            for (int i = 0; i < inventory.Count; i++)
            {
                RewardInventoryEntry entry = inventory[i];
                if (entry.Amount <= 0)
                    continue;

                if (!_config.TryGetReward(entry.RewardId, out RewardData rewardData) || rewardData == null)
                {
                    Debug.LogWarning($"Reward inventory entry '{entry.RewardId}' could not be resolved from the reward catalog.");
                    continue;
                }

                if (rewardData.Kind == RewardType.Cash || rewardData.Kind == RewardType.Gold)
                    continue;

                InventoryRewardAction action = !casePresentationActive && rewardData.Kind == RewardType.Case
                    ? InventoryRewardAction.OpenCase
                    : InventoryRewardAction.None;

                destination.Add(new InventoryRewardEntry(new ResolvedReward(rewardData, entry.Amount), action));
            }

            destination.Sort(CompareRewards);
        }

        public bool TryYeetBankedItem(string rewardId, int amount)
        {
            EnsureProfile();

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            int resolvedAmount = Mathf.Max(0, amount);
            if (resolvedAmount <= 0)
                return false;

            if (_config == null || !_config.TryGetReward(rewardId, out RewardData rewardData) || rewardData == null)
                return false;

            if (rewardData.Kind != RewardType.ItemCard)
                return false;

            return _profile.TrySpendInventoryReward(rewardId, resolvedAmount);
        }

        private static int CompareRewards(InventoryRewardEntry left, InventoryRewardEntry right)
        {
            int rarityComparison = right.Rarity.CompareTo(left.Rarity);
            if (rarityComparison != 0)
                return rarityComparison;

            int nameComparison = string.Compare(left.RewardName, right.RewardName, System.StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0)
                return nameComparison;

            return right.Amount.CompareTo(left.Amount);
        }

        private void EnsureProfile()
        {
            if (_profile == null)
                throw new System.InvalidOperationException("InventoryManager requires ProfileManager before currency or inventory operations can be resolved.");
        }

        private void EnsureRewardManager()
        {
            if (_rewardManager == null)
                throw new System.InvalidOperationException("InventoryManager requires RewardManager before pending rewards can be banked.");
        }
    }
}
