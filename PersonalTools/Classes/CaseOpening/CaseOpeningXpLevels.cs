namespace PersonalTools.Classes.CaseOpening;

// Increasing-increment XP curve: level N needs an additional 100*N xp beyond level N-1, so each
// level genuinely takes more xp than the last (level1=100 total, level2=+200 -> 300 total,
// level3=+300 -> 600 total, ...). Kept as a static formula (not DB-driven) since the level a
// given total xp maps to must never change retroactively once earned.
public static class CaseOpeningXpLevels
{
    private const int BaseXpPerLevel = 100;

    public static int GetLevel(int totalXp)
    {
        int level = 0;
        while (totalXp >= GetCumulativeXpForLevel(level + 1)) level++;
        return level;
    }

    public static int GetCumulativeXpForLevel(int level) => BaseXpPerLevel * level * (level + 1) / 2;

    public static int GetXpIntoLevel(int totalXp) => totalXp - GetCumulativeXpForLevel(GetLevel(totalXp));

    public static int GetXpForNextLevel(int totalXp)
    {
        int level = GetLevel(totalXp);
        return GetCumulativeXpForLevel(level + 1) - GetCumulativeXpForLevel(level);
    }
}
