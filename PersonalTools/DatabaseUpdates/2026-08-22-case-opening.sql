USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningHistory
(
    OpeningId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    SourceItemId VARCHAR(160) NOT NULL,
    ItemName VARCHAR(255) NOT NULL,
    MarketHashName VARCHAR(300) NOT NULL,
    ImageUrl VARCHAR(2048) NOT NULL,
    Description TEXT NOT NULL,
    WeaponName VARCHAR(100) NOT NULL,
    PatternName VARCHAR(150) NOT NULL,
    PaintIndex VARCHAR(20) NOT NULL,
    Phase VARCHAR(50) NOT NULL,
    RarityKey VARCHAR(30) NOT NULL,
    RarityName VARCHAR(80) NOT NULL,
    RarityColor CHAR(7) NOT NULL,
    Wear VARCHAR(40) NOT NULL,
    IsStatTrak TINYINT(1) NOT NULL DEFAULT 0,
    IsRareSpecial TINYINT(1) NOT NULL DEFAULT 0,
    SupportsStatTrak TINYINT(1) NOT NULL DEFAULT 0,
    MinFloat DECIMAL(9,6) NULL,
    MaxFloat DECIMAL(9,6) NULL,
    FloatValue DECIMAL(9,6) NULL,
    PatternSeed INT NULL,
    EstimatedPrice DECIMAL(12,2) NULL,
    OpenedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (OpeningId),
    KEY IX_CaseOpeningHistory_User_Opened (UserId, OpenedUtc),
    CONSTRAINT FK_CaseOpeningHistory_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_history_get//
CREATE PROCEDURE sp_case_opening_history_get(IN p_user_id CHAR(36))
BEGIN
    SELECT OpeningId, UserId, CaseKey, SourceItemId, ItemName, MarketHashName, ImageUrl,
           Description, WeaponName, PatternName, PaintIndex, Phase,
           RarityKey, RarityName, RarityColor, Wear, IsStatTrak, IsRareSpecial, SupportsStatTrak,
           MinFloat, MaxFloat, FloatValue, PatternSeed,
           EstimatedPrice, OpenedUtc
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id
    ORDER BY OpenedUtc DESC, OpeningId DESC;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_history_create//
CREATE PROCEDURE sp_case_opening_history_create
(
    IN p_user_id CHAR(36),
    IN p_opening_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_source_item_id VARCHAR(160),
    IN p_item_name VARCHAR(255),
    IN p_market_hash_name VARCHAR(300),
    IN p_image_url VARCHAR(2048),
    IN p_description TEXT,
    IN p_weapon_name VARCHAR(100),
    IN p_pattern_name VARCHAR(150),
    IN p_paint_index VARCHAR(20),
    IN p_phase VARCHAR(50),
    IN p_rarity_key VARCHAR(30),
    IN p_rarity_name VARCHAR(80),
    IN p_rarity_color CHAR(7),
    IN p_wear VARCHAR(40),
    IN p_is_stat_trak TINYINT(1),
    IN p_is_rare_special TINYINT(1),
    IN p_supports_stat_trak TINYINT(1),
    IN p_min_float DECIMAL(9,6),
    IN p_max_float DECIMAL(9,6),
    IN p_float_value DECIMAL(9,6),
    IN p_pattern_seed INT,
    IN p_estimated_price DECIMAL(12,2)
)
BEGIN
    INSERT INTO CaseOpeningHistory
    (
        OpeningId, UserId, CaseKey, SourceItemId, ItemName, MarketHashName, ImageUrl,
        Description, WeaponName, PatternName, PaintIndex, Phase,
        RarityKey, RarityName, RarityColor, Wear, IsStatTrak, IsRareSpecial, SupportsStatTrak,
        MinFloat, MaxFloat, FloatValue, PatternSeed,
        EstimatedPrice, OpenedUtc
    )
    VALUES
    (
        p_opening_id, p_user_id, p_case_key, p_source_item_id, p_item_name,
        p_market_hash_name, p_image_url, p_description, p_weapon_name, p_pattern_name,
        p_paint_index, p_phase, p_rarity_key, p_rarity_name, p_rarity_color,
        p_wear, p_is_stat_trak, p_is_rare_special, p_supports_stat_trak,
        p_min_float, p_max_float, p_float_value, p_pattern_seed, p_estimated_price, UTC_TIMESTAMP(6)
    );
END//

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

DROP PROCEDURE IF EXISTS sp_case_opening_history_clear//
CREATE PROCEDURE sp_case_opening_history_clear(IN p_user_id CHAR(36))
BEGIN
    DELETE FROM CaseOpeningHistory
    WHERE UserId = p_user_id;
END//

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
