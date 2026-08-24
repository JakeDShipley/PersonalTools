USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningTradeUps
(
    TradeUpId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    InputRarityKey VARCHAR(30) NOT NULL,
    OutputRarityKey VARCHAR(30) NOT NULL,
    OutputOpeningId CHAR(36) NOT NULL,
    OutputCaseKey VARCHAR(80) NOT NULL,
    AverageInputFloat DECIMAL(9,6) NOT NULL,
    CreatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (TradeUpId),
    KEY IX_CaseOpeningTradeUps_User_Created (UserId, CreatedUtc),
    CONSTRAINT FK_CaseOpeningTradeUps_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningTradeUpInputs
(
    TradeUpInputId CHAR(36) NOT NULL,
    TradeUpId CHAR(36) NOT NULL,
    InputOpeningId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    SourceItemId VARCHAR(160) NOT NULL,
    RarityKey VARCHAR(30) NOT NULL,
    FloatValue DECIMAL(9,6) NULL,
    IsStatTrak TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (TradeUpInputId),
    KEY IX_CaseOpeningTradeUpInputs_TradeUpId (TradeUpId),
    CONSTRAINT FK_CaseOpeningTradeUpInputs_TradeUps
        FOREIGN KEY (TradeUpId) REFERENCES CaseOpeningTradeUps (TradeUpId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_execute//

CREATE PROCEDURE sp_case_opening_trade_up_execute(
    IN p_user_id CHAR(36),
    IN p_trade_up_id CHAR(36),
    IN p_opening_ids JSON,
    IN p_input_rarity_key VARCHAR(30),
    IN p_output_rarity_key VARCHAR(30),
    IN p_output_opening_id CHAR(36),
    IN p_output_case_key VARCHAR(80),
    IN p_output_source_item_id VARCHAR(160),
    IN p_output_item_name VARCHAR(255),
    IN p_output_market_hash_name VARCHAR(300),
    IN p_output_image_url VARCHAR(2048),
    IN p_output_description TEXT,
    IN p_output_weapon_name VARCHAR(100),
    IN p_output_pattern_name VARCHAR(150),
    IN p_output_paint_index VARCHAR(20),
    IN p_output_phase VARCHAR(50),
    IN p_output_rarity_name VARCHAR(80),
    IN p_output_rarity_color CHAR(7),
    IN p_output_wear VARCHAR(40),
    IN p_output_is_stat_trak TINYINT(1),
    IN p_output_is_rare_special TINYINT(1),
    IN p_output_supports_stat_trak TINYINT(1),
    IN p_output_min_float DECIMAL(9,6),
    IN p_output_max_float DECIMAL(9,6),
    IN p_output_float_value DECIMAL(9,6),
    IN p_output_pattern_seed INT,
    IN p_output_estimated_price DECIMAL(12,2),
    IN p_average_input_float DECIMAL(9,6)
)
BEGIN
    DECLARE v_selected_count INT DEFAULT 0;
    DECLARE v_rarity_count INT DEFAULT 0;
    DECLARE v_actual_rarity_key VARCHAR(30) DEFAULT '';
    DECLARE v_stat_trak_count INT DEFAULT 0;
    DECLARE v_rare_special_count INT DEFAULT 0;
    DECLARE v_deleted_count INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT
        COUNT(*),
        COUNT(DISTINCT h.RarityKey),
        COALESCE(MAX(h.RarityKey), ''),
        COUNT(DISTINCT h.IsStatTrak),
        COALESCE(SUM(h.IsRareSpecial), 0)
    INTO
        v_selected_count,
        v_rarity_count,
        v_actual_rarity_key,
        v_stat_trak_count,
        v_rare_special_count
    FROM CaseOpeningHistory h
    INNER JOIN
    (
        SELECT DISTINCT OpeningId
        FROM JSON_TABLE
        (
            p_opening_ids,
            '$[*]' COLUMNS
            (
                OpeningId CHAR(36) PATH '$'
            )
        ) AS selectedIds
    ) selectedIds
        ON BINARY selectedIds.OpeningId = BINARY h.OpeningId
    WHERE BINARY h.UserId = BINARY p_user_id;

    IF v_selected_count <> 10 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Select exactly 10 inventory skins for a Trade Up Contract.';
    END IF;

    IF v_rarity_count <> 1
       OR v_actual_rarity_key <> p_input_rarity_key
       OR v_rare_special_count <> 0
       OR v_stat_trak_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected skins are not a valid Trade Up Contract.';
    END IF;

    IF (p_input_rarity_key = 'mil-spec' AND p_output_rarity_key <> 'restricted')
       OR (p_input_rarity_key = 'restricted' AND p_output_rarity_key <> 'classified')
       OR (p_input_rarity_key = 'classified' AND p_output_rarity_key <> 'covert')
       OR p_input_rarity_key NOT IN ('mil-spec', 'restricted', 'classified')
       OR p_output_is_rare_special <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected Trade Up Contract rarity is not valid.';
    END IF;

    INSERT INTO CaseOpeningTradeUps
    (
        TradeUpId,
        UserId,
        InputRarityKey,
        OutputRarityKey,
        OutputOpeningId,
        OutputCaseKey,
        AverageInputFloat,
        CreatedUtc
    )
    VALUES
    (
        p_trade_up_id,
        p_user_id,
        p_input_rarity_key,
        p_output_rarity_key,
        p_output_opening_id,
        p_output_case_key,
        p_average_input_float,
        UTC_TIMESTAMP(6)
    );

    INSERT INTO CaseOpeningTradeUpInputs
    (
        TradeUpInputId,
        TradeUpId,
        InputOpeningId,
        CaseKey,
        SourceItemId,
        RarityKey,
        FloatValue,
        IsStatTrak
    )
    SELECT
        UUID(),
        p_trade_up_id,
        h.OpeningId,
        h.CaseKey,
        h.SourceItemId,
        h.RarityKey,
        h.FloatValue,
        h.IsStatTrak
    FROM CaseOpeningHistory h
    INNER JOIN
    (
        SELECT DISTINCT OpeningId
        FROM JSON_TABLE
        (
            p_opening_ids,
            '$[*]' COLUMNS
            (
                OpeningId CHAR(36) PATH '$'
            )
        ) AS selectedIds
    ) selectedIds
        ON BINARY selectedIds.OpeningId = BINARY h.OpeningId
    WHERE BINARY h.UserId = BINARY p_user_id;

    DELETE h
    FROM CaseOpeningHistory h
    INNER JOIN
    (
        SELECT DISTINCT OpeningId
        FROM JSON_TABLE
        (
            p_opening_ids,
            '$[*]' COLUMNS
            (
                OpeningId CHAR(36) PATH '$'
            )
        ) AS selectedIds
    ) selectedIds
        ON BINARY selectedIds.OpeningId = BINARY h.OpeningId
    WHERE BINARY h.UserId = BINARY p_user_id;

    SET v_deleted_count = ROW_COUNT();

    IF v_deleted_count <> 10 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected inventory changed before the Trade Up Contract could finish.';
    END IF;

    INSERT INTO CaseOpeningHistory
    (
        OpeningId,
        UserId,
        CaseKey,
        SourceItemId,
        ItemName,
        MarketHashName,
        ImageUrl,
        Description,
        WeaponName,
        PatternName,
        PaintIndex,
        Phase,
        RarityKey,
        RarityName,
        RarityColor,
        Wear,
        IsStatTrak,
        IsRareSpecial,
        SupportsStatTrak,
        MinFloat,
        MaxFloat,
        FloatValue,
        PatternSeed,
        EstimatedPrice,
        OpenedUtc
    )
    VALUES
    (
        p_output_opening_id,
        p_user_id,
        p_output_case_key,
        p_output_source_item_id,
        p_output_item_name,
        p_output_market_hash_name,
        p_output_image_url,
        p_output_description,
        p_output_weapon_name,
        p_output_pattern_name,
        p_output_paint_index,
        p_output_phase,
        p_output_rarity_key,
        p_output_rarity_name,
        p_output_rarity_color,
        p_output_wear,
        p_output_is_stat_trak,
        p_output_is_rare_special,
        p_output_supports_stat_trak,
        p_output_min_float,
        p_output_max_float,
        p_output_float_value,
        p_output_pattern_seed,
        p_output_estimated_price,
        UTC_TIMESTAMP(6)
    );

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
        p_output_case_key,
        p_output_source_item_id,
        UTC_TIMESTAMP(6)
    );

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_reset_dev//

CREATE PROCEDURE sp_case_opening_reset_dev(
    IN p_user_id CHAR(36)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    DELETE FROM CaseOpeningBots
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningBotServers
    WHERE UserId = p_user_id;

    -- Contract audit rows are user data too. Resetting a simulator account should not leave
    -- historic inputs behind once the inventory and collection are being wiped.
    DELETE FROM CaseOpeningTradeUps
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningCollection
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningHistory
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningUnlockedCases
    WHERE UserId = p_user_id;

    INSERT INTO CaseOpeningUnlockedCases
    (
        UserId,
        CaseKey,
        UnlockedUtc
    )
    VALUES
    (
        p_user_id,
        'kilowatt',
        UTC_TIMESTAMP()
    );

    INSERT INTO CaseOpeningProgress
    (
        UserId,
        Stars,
        Xp,
        SkipAnimationUnlocked,
        MultiOpenLevel,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        0,
        0,
        0,
        0,
        UTC_TIMESTAMP()
    )
    ON DUPLICATE KEY UPDATE
        Stars = 0,
        Xp = 0,
        SkipAnimationUnlocked = 0,
        MultiOpenLevel = 0,
        UpdatedUtc = UTC_TIMESTAMP();

    COMMIT;
END//

DELIMITER ;
