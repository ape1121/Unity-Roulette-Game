using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    internal static class UIReferenceUtility
    {
        public static Button FindButtonByName(Component root, params string[] nameKeywords)
        {
            return FindUniqueInChildren<Button>(root, GetNormalizedLabels(nameKeywords), MatchesComponentName);
        }

        private static T FindUniqueInChildren<T>(Component root, string[] normalizedTokens, Func<T, string[], bool> predicate)
            where T : Component
        {
            if (root == null || normalizedTokens.Length == 0)
                return null;

            T[] candidates = root.GetComponentsInChildren<T>(true);
            T match = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (!predicate(candidate, normalizedTokens))
                    continue;

                if (match != null)
                    return null;

                match = candidate;
            }

            return match;
        }
        
        private static bool MatchesComponentName(Component component, string[] normalizedKeywords)
        {
            if (component == null)
                return false;

            string normalizedName = NormalizeToken(component.name);
            if (normalizedName.Length == 0)
                return false;

            for (int i = 0; i < normalizedKeywords.Length; i++)
            {
                if (normalizedName.IndexOf(normalizedKeywords[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static string[] GetNormalizedLabels(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();

            string[] normalized = new string[values.Length];
            int count = 0;

            for (int i = 0; i < values.Length; i++)
            {
                string token = NormalizeToken(values[i]);
                if (token.Length == 0)
                    continue;

                normalized[count] = token;
                count++;
            }

            if (count == normalized.Length)
                return normalized;

            Array.Resize(ref normalized, count);
            return normalized;
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] buffer = new char[value.Length];
            int count = 0;

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (!char.IsLetterOrDigit(current))
                    continue;

                buffer[count] = char.ToUpperInvariant(current);
                count++;
            }

            return count == 0 ? string.Empty : new string(buffer, 0, count);
        }
    }
}
