using System;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Coin balance (saved by SaveService). Coins buy cosmetics (see Cosmetics / CosmeticCatalog) and the two time
    /// boosts of timed levels (Boosts) — the only purchases that affect gameplay; difficulty itself can't be bought.
    /// </summary>
    public static class CoinManager
    {
        public const int BaseReward = 10;
        public const int HardBonus = 5;
        public const int BossBonus = 10;
        public const int UnusedUndoBonus = 5;   // per undo left over — rewards efficient play
        public const int ParTimeBonus = 10;

        /// <summary>Raised after every change. Arguments: new balance, change (+ earned / - spent).</summary>
        public static event Action<int, int> BalanceChanged;

        public static int GetBalance() => SaveService.Coins;

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            Set(GetBalance() + amount, amount);
        }

        /// <summary>Spends coins if the balance covers it. Returns false (and spends nothing) otherwise.</summary>
        public static bool Spend(int amount)
        {
            if (amount < 0 || amount > GetBalance()) return false;
            if (amount > 0) Set(GetBalance() - amount, -amount);
            return true;
        }

        /// <summary>
        /// Coins for finishing a level: base + HARD/BOSS bonus + a bonus per unused undo + a par-time bonus.
        /// There is no undo or timer system yet, so callers pass 0 / false for those two for now.
        /// </summary>
        public static int LevelReward(int levelNumber, int unusedUndos = 0, bool beatParTime = false) =>
            BaseReward
            + (Difficulty.IsBoss(levelNumber) ? BossBonus : Difficulty.IsHard(levelNumber) ? HardBonus : 0)
            + UnusedUndoBonus * Mathf.Max(0, unusedUndos)
            + (beatParTime ? ParTimeBonus : 0);

        static void Set(int balance, int delta)
        {
            SaveService.Coins = balance;
            SaveService.Save();
            BalanceChanged?.Invoke(balance, delta);
        }
    }
}
