USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningCollection
(
    CollectionId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    SourceItemId VARCHAR(160) NOT NULL,
    FirstObtainedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (CollectionId),
    UNIQUE KEY UX_CaseOpeningCollection_UserCaseItem (UserId, CaseKey, SourceItemId),
    KEY IX_CaseOpeningCollection_UserCase (UserId, CaseKey),
    CONSTRAINT FK_CaseOpeningCollection_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

-- Existing inventory has already been obtained, so carry it into the new permanent collection.
INSERT IGNORE INTO CaseOpeningCollection
(
    CollectionId,
    UserId,
    CaseKey,
    SourceItemId,
    FirstObtainedUtc
)
SELECT
    UUID(),
    UserId,
    CaseKey,
    SourceItemId,
    MIN(OpenedUtc)
FROM CaseOpeningHistory
GROUP BY UserId, CaseKey, SourceItemId;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_collection_get//
CREATE PROCEDURE sp_case_opening_collection_get
(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80)
)
BEGIN
    SELECT CollectionId, UserId, CaseKey, SourceItemId, FirstObtainedUtc
    FROM CaseOpeningCollection
    WHERE BINARY UserId = BINARY p_user_id
      AND CaseKey = p_case_key
    ORDER BY FirstObtainedUtc, CollectionId;
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
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT INTO CaseOpeningHistory
    (
        OpeningId, UserId, CaseKey, SourceItemId, ItemName, MarketHashName, ImageUrl,
        Description, WeaponName, PatternName, PaintIndex, Phase, RarityKey, RarityName,
        RarityColor, Wear, IsStatTrak, IsRareSpecial, SupportsStatTrak, MinFloat,
        MaxFloat, FloatValue, PatternSeed, EstimatedPrice, OpenedUtc
    )
    VALUES
    (
        p_opening_id, p_user_id, p_case_key, p_source_item_id, p_item_name,
        p_market_hash_name, p_image_url, p_description, p_weapon_name, p_pattern_name,
        p_paint_index, p_phase, p_rarity_key, p_rarity_name, p_rarity_color, p_wear,
        p_is_stat_trak, p_is_rare_special, p_supports_stat_trak, p_min_float, p_max_float,
        p_float_value, p_pattern_seed, p_estimated_price, UTC_TIMESTAMP(6)
    );

    -- Collection ownership is intentionally separate from saleable inventory. A duplicate pull
    -- remains in history, while the collection keeps only the first time that skin was obtained.
    INSERT IGNORE INTO CaseOpeningCollection
    (
        CollectionId,
        UserId,
        CaseKey,
        SourceItemId,
        FirstObtainedUtc
    )
    VALUES
    (
        UUID(),
        p_user_id,
        p_case_key,
        p_source_item_id,
        UTC_TIMESTAMP(6)
    );

    COMMIT;
END//

DELIMITER ;
