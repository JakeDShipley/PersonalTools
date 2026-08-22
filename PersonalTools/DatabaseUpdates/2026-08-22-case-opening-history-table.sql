-- Returns the complete per-user opening history because search and pagination are owned by the browser.
-- Recreating this procedure removes the original safety cap without changing any saved results.
DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_history_get//
CREATE PROCEDURE sp_case_opening_history_get(IN p_user_id CHAR(36))
BEGIN
    SELECT OpeningId, UserId, CaseKey, SourceItemId, ItemName, MarketHashName, ImageUrl,
           Description, WeaponName, PatternName, PaintIndex, Phase,
           RarityKey, RarityName, RarityColor, Wear, IsStatTrak, IsRareSpecial, SupportsStatTrak,
           MinFloat, MaxFloat, FloatValue, PatternSeed, EstimatedPrice, OpenedUtc
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id
    ORDER BY OpenedUtc DESC, OpeningId DESC;
END//

DELIMITER ;
