-- CS2 Case Simulator: revoke Skip Animation for anyone who has it flagged unlocked from before the
-- speed-upgrade rebalance but hasn't actually reached the new max speed level (5) that now grants
-- it. Without this, it stays on as leftover state from when it was independently purchasable -
-- the unlock logic only ever sets the flag on reaching level 5, it never revokes it retroactively.
-- Safe to re-run (no-op once everyone's flag matches their actual speed level).

USE PersonalTools;

UPDATE CaseOpeningProgress p
INNER JOIN CaseOpeningGameSettings s ON s.Id = 1
SET p.SkipAnimationUnlocked = 0
WHERE p.OpenSpeedLevel < s.MaximumOpenSpeedLevel
  AND p.SkipAnimationUnlocked = 1;
