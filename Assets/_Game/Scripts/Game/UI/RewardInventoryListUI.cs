using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using Ape.Profile;
using UnityEngine;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    public sealed class RewardInventoryListUI : MonoBehaviour
    {
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private RewardCardUI _rewardCardPrefab;
        [SerializeField] private GameObject _emptyStateRoot;

        private readonly List<RewardCardUI> _spawnedCards = new List<RewardCardUI>();
        private readonly List<ResolvedReward> _resolvedRewards = new List<ResolvedReward>();

        private bool _isSubscribed;

        private void OnEnable()
        {
            SubscribeToProfile();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeFromProfile();
        }

        public void Refresh()
        {
            BuildResolvedRewards();
            SyncCardViews();
        }

        private void OnValidate()
        {
            _contentRoot ??= transform;
        }

        private void SubscribeToProfile()
        {
            if (_isSubscribed || App.Profile == null)
                return;

            App.Profile.DataChanged += HandleProfileDataChanged;
            _isSubscribed = true;
        }

        private void UnsubscribeFromProfile()
        {
            if (!_isSubscribed || App.Profile == null)
                return;

            App.Profile.DataChanged -= HandleProfileDataChanged;
            _isSubscribed = false;
        }

        private void HandleProfileDataChanged(SaveData _)
        {
            Refresh();
        }

        private void BuildResolvedRewards()
        {
            _resolvedRewards.Clear();

            if (_contentRoot == null || _rewardCardPrefab == null || App.Profile == null)
                return;

            GameConfig gameConfig = App.Config != null ? App.Config.GameConfig : null;
            IReadOnlyList<RewardInventoryEntry> inventory = App.Profile.Inventory;

            if (gameConfig == null || inventory == null)
                return;

            for (int i = 0; i < inventory.Count; i++)
            {
                RewardInventoryEntry entry = inventory[i];
                if (entry.Amount <= 0)
                    continue;

                if (!gameConfig.TryGetReward(entry.RewardId, out RewardData rewardData) || rewardData == null)
                {
                    Debug.LogWarning($"Reward inventory entry '{entry.RewardId}' could not be resolved from the reward catalog.", this);
                    continue;
                }

                if (rewardData.Kind != RewardData.RewardKind.ItemCard)
                    continue;

                _resolvedRewards.Add(new ResolvedReward(rewardData, entry.Amount));
            }

            _resolvedRewards.Sort(CompareRewards);
        }

        private void SyncCardViews()
        {
            bool hasRewards = _resolvedRewards.Count > 0 && _contentRoot != null && _rewardCardPrefab != null;

            for (int i = 0; i < _resolvedRewards.Count; i++)
            {
                RewardCardUI card = GetOrCreateCard(i);
                if (card == null)
                    continue;

                card.Bind(_resolvedRewards[i]);
                card.gameObject.SetActive(true);
            }

            for (int i = _resolvedRewards.Count; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    _spawnedCards[i].gameObject.SetActive(false);
            }

            if (_emptyStateRoot != null)
                _emptyStateRoot.SetActive(!hasRewards);
        }

        private RewardCardUI GetOrCreateCard(int index)
        {
            if (_contentRoot == null || _rewardCardPrefab == null)
                return null;

            while (_spawnedCards.Count <= index)
            {
                RewardCardUI card = Instantiate(_rewardCardPrefab, _contentRoot);
                card.gameObject.SetActive(false);
                _spawnedCards.Add(card);
            }

            return _spawnedCards[index];
        }

        private static int CompareRewards(ResolvedReward left, ResolvedReward right)
        {
            int rarityComparison = right.Rarity.CompareTo(left.Rarity);
            if (rarityComparison != 0)
                return rarityComparison;

            int nameComparison = string.Compare(left.RewardName, right.RewardName, System.StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0)
                return nameComparison;

            return right.Amount.CompareTo(left.Amount);
        }
    }
}
