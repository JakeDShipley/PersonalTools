USE PersonalTools;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_statistics_get//
CREATE PROCEDURE sp_case_opening_statistics_get
(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_target_rarity_key VARCHAR(30)
)
BEGIN
    DECLARE v_last_target_utc DATETIME(6) DEFAULT NULL;

    SELECT MAX(OpenedUtc)
    INTO v_last_target_utc
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id
      AND CaseKey = p_case_key
      AND RarityKey = p_target_rarity_key;

    SELECT COUNT(*) AS TotalOpenings,
           COALESCE(SUM(CASE WHEN RarityKey = p_target_rarity_key THEN 1 ELSE 0 END), 0) AS TargetPulls,
           CASE
               WHEN v_last_target_utc IS NULL THEN COUNT(*)
               ELSE COALESCE(SUM(CASE WHEN OpenedUtc > v_last_target_utc THEN 1 ELSE 0 END), 0)
           END AS CurrentDryStreak,
           v_last_target_utc AS LastTargetOpenedUtc
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id
      AND CaseKey = p_case_key;
END//

DELIMITER ;
