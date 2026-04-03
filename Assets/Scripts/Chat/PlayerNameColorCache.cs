#nullable enable
using System;
using System.Collections.Generic;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using UnityEngine;

namespace ChatMod
{
    /// <summary>Cache Human ReferenceId → name color hex (RRGGBB). Updated on suit equip / first message.</summary>
    public static class PlayerNameColorCache
    {
        private static readonly Dictionary<long, string> Cache = new();
        private static readonly object Sync = new();

        public static bool TryGet(long humanId, out string? hex)
        {
            lock (Sync)
            {
                if (Cache.TryGetValue(humanId, out var value))
                {
                    hex = value;
                    return true;
                }

                hex = null;
                return false;
            }
        }

        public static void Set(long humanId, string hex)
        {
            lock (Sync)
            {
                Cache[humanId] = hex ?? ChatUiLayout.DefaultNameColorHex;
            }
        }

        public static string GetOrResolve(long humanId)
        {
            if (TryGet(humanId, out var cached))
                return cached!;

            var hex = ResolveAndComputeHex(humanId);
            Set(humanId, hex);
            return hex;
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Cache.Clear();
            }
        }

        private static string ResolveAndComputeHex(long humanId)
        {
            var human = Thing.Find<Human>(humanId);
            if (human == null)
                return ChatUiLayout.DefaultNameColorHex;

            return SuitColorToHex(human);
        }

        /// <summary>Computes name color hex from a Human's current suit (or default).</summary>
        public static string SuitColorToHex(Human human)
        {
            if (human?.Suit?.AsThing?.CustomColor == null)
                return ChatUiLayout.DefaultNameColorHex;

            var c = human.Suit.AsThing.CustomColor.Color;
            if (IsTooCloseToWhite(c))
                return ChatUiLayout.DefaultNameColorHex;

            return ColorToHex(c);
        }

        private static bool IsTooCloseToWhite(Color c)
        {
            const float threshold = 0.9f;
            return c.r >= threshold && c.g >= threshold && c.b >= threshold;
        }

        private static string ColorToHex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
