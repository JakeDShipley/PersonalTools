/*
    PersonalTools - Notes and CS2 Skin Tracker
    ------------------------------------------------------------
    Run this entire script in a HeidiSQL query tab while connected
    to the MariaDB server.

    Requirements:
      - The PersonalTools database already exists.
      - The Users table already exists.

    This script is safe to run again:
      - Tables use CREATE TABLE IF NOT EXISTS.
      - Procedures are dropped and recreated.
      - Existing Notes and TrackedSkins rows are not deleted.
*/

USE PersonalTools;

CREATE TABLE IF NOT EXISTS Notes
(
    NoteId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    Title VARCHAR(200) NOT NULL,
    Body MEDIUMTEXT NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (NoteId),
    KEY IX_Notes_UserId_SortOrder (UserId, SortOrder),
    CONSTRAINT FK_Notes_Users
        FOREIGN KEY (UserId)
        REFERENCES Users (UserId)
        ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS TrackedSkins
(
    SkinId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    Name VARCHAR(200) NOT NULL,
    Weapon VARCHAR(100) NOT NULL,
    Exterior VARCHAR(100) NOT NULL,
    MarketHashName VARCHAR(255) NOT NULL,
    ExternalImageUrl VARCHAR(2048) NOT NULL,
    PurchasePrice DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
    CurrentPrice DECIMAL(12, 2) NULL,
    PurchaseDate DATE NULL,
    Notes TEXT NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (SkinId),
    KEY IX_TrackedSkins_UserId_UpdatedUtc (UserId, UpdatedUtc),
    CONSTRAINT FK_TrackedSkins_Users
        FOREIGN KEY (UserId)
        REFERENCES Users (UserId)
        ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_notes_get$$
CREATE PROCEDURE sp_notes_get(IN p_user_id BIGINT UNSIGNED)
BEGIN
    SELECT
        NoteId,
        Title,
        Body,
        SortOrder,
        CreatedUtc,
        UpdatedUtc
    FROM Notes
    WHERE UserId = p_user_id
    ORDER BY SortOrder, UpdatedUtc DESC;
END$$

DROP PROCEDURE IF EXISTS sp_notes_create$$
CREATE PROCEDURE sp_notes_create
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_note_id CHAR(36),
    IN p_title VARCHAR(200),
    IN p_body MEDIUMTEXT
)
BEGIN
    DECLARE v_sort_order INT DEFAULT 0;

    SELECT COALESCE(MAX(SortOrder) + 1, 0)
    INTO v_sort_order
    FROM Notes
    WHERE UserId = p_user_id;

    INSERT INTO Notes
    (
        NoteId,
        UserId,
        Title,
        Body,
        SortOrder,
        CreatedUtc,
        UpdatedUtc
    )
    VALUES
    (
        p_note_id,
        p_user_id,
        p_title,
        p_body,
        v_sort_order,
        UTC_TIMESTAMP(),
        UTC_TIMESTAMP()
    );
END$$

DROP PROCEDURE IF EXISTS sp_notes_update$$
CREATE PROCEDURE sp_notes_update
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_note_id CHAR(36),
    IN p_title VARCHAR(200),
    IN p_body MEDIUMTEXT
)
BEGIN
    UPDATE Notes
    SET
        Title = p_title,
        Body = p_body,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE NoteId = p_note_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_notes_delete$$
CREATE PROCEDURE sp_notes_delete
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_note_id CHAR(36)
)
BEGIN
    DELETE FROM Notes
    WHERE NoteId = p_note_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_notes_set_order$$
CREATE PROCEDURE sp_notes_set_order
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_note_id CHAR(36),
    IN p_sort_order INT
)
BEGIN
    UPDATE Notes
    SET SortOrder = p_sort_order
    WHERE NoteId = p_note_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_tracked_skins_get$$
CREATE PROCEDURE sp_tracked_skins_get(IN p_user_id BIGINT UNSIGNED)
BEGIN
    SELECT
        SkinId,
        Name,
        Weapon,
        Exterior,
        MarketHashName,
        ExternalImageUrl,
        PurchasePrice,
        CurrentPrice,
        PurchaseDate,
        Notes,
        CreatedUtc,
        UpdatedUtc
    FROM TrackedSkins
    WHERE UserId = p_user_id
    ORDER BY UpdatedUtc DESC;
END$$

DROP PROCEDURE IF EXISTS sp_tracked_skins_create$$
CREATE PROCEDURE sp_tracked_skins_create
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_skin_id CHAR(36),
    IN p_name VARCHAR(200),
    IN p_weapon VARCHAR(100),
    IN p_exterior VARCHAR(100),
    IN p_market_hash_name VARCHAR(255),
    IN p_external_image_url VARCHAR(2048),
    IN p_purchase_price DECIMAL(12, 2),
    IN p_current_price DECIMAL(12, 2),
    IN p_purchase_date DATE,
    IN p_notes TEXT
)
BEGIN
    INSERT INTO TrackedSkins
    (
        SkinId,
        UserId,
        Name,
        Weapon,
        Exterior,
        MarketHashName,
        ExternalImageUrl,
        PurchasePrice,
        CurrentPrice,
        PurchaseDate,
        Notes,
        CreatedUtc,
        UpdatedUtc
    )
    VALUES
    (
        p_skin_id,
        p_user_id,
        p_name,
        p_weapon,
        p_exterior,
        p_market_hash_name,
        p_external_image_url,
        p_purchase_price,
        p_current_price,
        p_purchase_date,
        p_notes,
        UTC_TIMESTAMP(),
        UTC_TIMESTAMP()
    );
END$$

DROP PROCEDURE IF EXISTS sp_tracked_skins_update$$
CREATE PROCEDURE sp_tracked_skins_update
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_skin_id CHAR(36),
    IN p_name VARCHAR(200),
    IN p_weapon VARCHAR(100),
    IN p_exterior VARCHAR(100),
    IN p_market_hash_name VARCHAR(255),
    IN p_external_image_url VARCHAR(2048),
    IN p_purchase_price DECIMAL(12, 2),
    IN p_current_price DECIMAL(12, 2),
    IN p_purchase_date DATE,
    IN p_notes TEXT
)
BEGIN
    UPDATE TrackedSkins
    SET
        Name = p_name,
        Weapon = p_weapon,
        Exterior = p_exterior,
        MarketHashName = p_market_hash_name,
        ExternalImageUrl = p_external_image_url,
        PurchasePrice = p_purchase_price,
        CurrentPrice = p_current_price,
        PurchaseDate = p_purchase_date,
        Notes = p_notes,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE SkinId = p_skin_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_tracked_skins_delete$$
CREATE PROCEDURE sp_tracked_skins_delete
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_skin_id CHAR(36)
)
BEGIN
    DELETE FROM TrackedSkins
    WHERE SkinId = p_skin_id
      AND UserId = p_user_id;
END$$

DELIMITER ;

/*
    Optional verification after running this script:

    SHOW TABLES LIKE 'Notes';
    SHOW TABLES LIKE 'TrackedSkins';
    SHOW PROCEDURE STATUS
    WHERE Db = DATABASE()
      AND Name IN
      (
          'sp_notes_get',
          'sp_notes_create',
          'sp_notes_update',
          'sp_notes_delete',
          'sp_notes_set_order',
          'sp_tracked_skins_get',
          'sp_tracked_skins_create',
          'sp_tracked_skins_update',
          'sp_tracked_skins_delete'
      );
*/
