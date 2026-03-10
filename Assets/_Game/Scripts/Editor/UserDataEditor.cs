using System.Collections.Generic;
using Ape.Profile;
using UnityEditor;
using UnityEngine;

namespace Ape.Editor
{
    public sealed class UserDataEditor : EditorWindow
    {
        private const string SaveKey = "CriticalShot.SaveData";

        private readonly List<RewardInventoryEntry> _inventoryEntries = new List<RewardInventoryEntry>();

        private Vector2 _scrollPosition;
        private int _level;
        private int _cash;
        private int _gold;

        [MenuItem("Tools/Critical Shot/Profile/User Data Editor")]
        private static void OpenWindow()
        {
            UserDataEditor window = GetWindow<UserDataEditor>("User Data");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadDraftFromPlayerPrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("User Profile Data", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Edits the saved profile stored in PlayerPrefs under '{SaveKey}'.", MessageType.None);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                DrawToolbar();
                DrawProfileFields();
                DrawInventorySection();
                DrawFooter();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Editing is disabled while entering Play Mode or during Play Mode.", MessageType.Info);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reload", GUILayout.Width(90f)))
                LoadDraftFromPlayerPrefs();

            if (GUILayout.Button("New Default", GUILayout.Width(110f)))
                ApplyDraft(CreateDefaultData());

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
        }

        private void DrawProfileFields()
        {
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
            _level = Mathf.Max(0, EditorGUILayout.IntField("Level", _level));
            _cash = Mathf.Max(0, EditorGUILayout.IntField("Cash", _cash));
            _gold = Mathf.Max(0, EditorGUILayout.IntField("Gold", _gold));
            EditorGUILayout.Space(8f);
        }

        private void DrawInventorySection()
        {
            EditorGUILayout.LabelField("Inventory", EditorStyles.boldLabel);
            int removeIndex = -1;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MinHeight(180f));

            if (_inventoryEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No inventory rewards saved.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _inventoryEntries.Count; i++)
                {
                    if (DrawInventoryEntryRow(i))
                        removeIndex = i;
                }
            }

            EditorGUILayout.EndScrollView();

            if (removeIndex >= 0)
                _inventoryEntries.RemoveAt(removeIndex);

            if (GUILayout.Button("Add Inventory Entry"))
                _inventoryEntries.Add(new RewardInventoryEntry(string.Empty, 1));

            EditorGUILayout.Space(8f);
        }

        private bool DrawInventoryEntryRow(int index)
        {
            RewardInventoryEntry entry = _inventoryEntries[index];
            bool shouldDelete = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Entry {index + 1}", EditorStyles.miniBoldLabel);

            if (GUILayout.Button("Delete", GUILayout.Width(70f)))
                shouldDelete = true;

            EditorGUILayout.EndHorizontal();

            if (!shouldDelete)
            {
                entry.RewardId = EditorGUILayout.TextField("Reward Id", entry.RewardId);
                entry.Amount = Mathf.Max(0, EditorGUILayout.IntField("Amount", entry.Amount));
                _inventoryEntries[index] = entry;
            }

            EditorGUILayout.EndVertical();
            return shouldDelete;
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Profile", GUILayout.Height(28f)))
                SaveDraftToPlayerPrefs();

            if (GUILayout.Button("Delete Saved Profile", GUILayout.Height(28f)))
                DeleteSavedProfile();

            EditorGUILayout.EndHorizontal();
        }

        private void LoadDraftFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                ApplyDraft(CreateDefaultData());
                return;
            }

            string json = PlayerPrefs.GetString(SaveKey);
            SaveData saveData = string.IsNullOrWhiteSpace(json)
                ? CreateDefaultData()
                : JsonUtility.FromJson<SaveData>(json);

            ApplyDraft(saveData);
        }

        private void SaveDraftToPlayerPrefs()
        {
            SaveData saveData = BuildSaveDataFromDraft();
            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
            ShowNotification(new GUIContent("Profile saved"));
        }

        private void DeleteSavedProfile()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Saved Profile",
                    "Delete the saved user profile from PlayerPrefs?",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            ApplyDraft(CreateDefaultData());
            ShowNotification(new GUIContent("Saved profile deleted"));
        }

        private void ApplyDraft(SaveData saveData)
        {
            _level = Mathf.Max(0, saveData.Level);
            _cash = Mathf.Max(0, saveData.Cash);
            _gold = Mathf.Max(0, saveData.Gold);

            _inventoryEntries.Clear();
            if (saveData.Inventory == null)
                return;

            for (int i = 0; i < saveData.Inventory.Count; i++)
            {
                RewardInventoryEntry entry = saveData.Inventory[i];
                _inventoryEntries.Add(new RewardInventoryEntry(entry.RewardId, Mathf.Max(0, entry.Amount)));
            }
        }

        private SaveData BuildSaveDataFromDraft()
        {
            var inventory = new List<RewardInventoryEntry>(_inventoryEntries.Count);

            for (int i = 0; i < _inventoryEntries.Count; i++)
            {
                RewardInventoryEntry entry = _inventoryEntries[i];
                string rewardId = string.IsNullOrWhiteSpace(entry.RewardId) ? string.Empty : entry.RewardId.Trim();
                int amount = Mathf.Max(0, entry.Amount);

                if (string.IsNullOrWhiteSpace(rewardId) || amount <= 0)
                    continue;

                inventory.Add(new RewardInventoryEntry(rewardId, amount));
            }

            return new SaveData
            {
                Level = Mathf.Max(0, _level),
                Cash = Mathf.Max(0, _cash),
                Gold = Mathf.Max(0, _gold),
                Inventory = inventory
            };
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
    }
}
