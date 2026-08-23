USE PersonalTools;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_condition_exists//
CREATE PROCEDURE sp_case_opening_condition_exists
(
    IN p_user_id CHAR(36),
    IN p_source_item_id VARCHAR(160),
    IN p_float_value DECIMAL(9,6),
    IN p_pattern_seed INT
)
BEGIN
    SELECT COUNT(*)
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id
      AND SourceItemId = p_source_item_id
      AND FloatValue = p_float_value
      AND PatternSeed = p_pattern_seed;
END//

DELIMITER ;
