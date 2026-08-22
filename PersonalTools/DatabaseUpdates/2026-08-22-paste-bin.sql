USE PersonalTools;

CREATE TABLE IF NOT EXISTS PasteBinSettings (
    Id TINYINT NOT NULL,
    MaximumUploadSizeMb INT NOT NULL DEFAULT 50,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO PasteBinSettings (Id, MaximumUploadSizeMb, UpdatedUtc)
VALUES (1, 50, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE Id=VALUES(Id);

CREATE TABLE IF NOT EXISTS PasteBinPastes (
    PasteId CHAR(36) NOT NULL,
    CreatedByUserId CHAR(36) NOT NULL,
    ShortCode VARCHAR(16) NOT NULL,
    Title VARCHAR(200) NOT NULL,
    Language VARCHAR(30) NOT NULL,
    Content MEDIUMTEXT NULL,
    PasswordHash VARCHAR(512) NULL,
    CreatedUtc DATETIME NOT NULL,
    ExpiresUtc DATETIME NULL,
    PRIMARY KEY (PasteId),
    UNIQUE KEY UX_PasteBinPastes_ShortCode (ShortCode),
    KEY IX_PasteBinPastes_Active (ExpiresUtc, CreatedUtc),
    KEY IX_PasteBinPastes_Creator (CreatedByUserId, CreatedUtc),
    CONSTRAINT FK_PasteBinPastes_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users(UserId) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS PasteBinFiles (
    PasteFileId CHAR(36) NOT NULL,
    PasteId CHAR(36) NOT NULL,
    OriginalFileName VARCHAR(255) NOT NULL,
    StoredFileName CHAR(36) NOT NULL,
    ContentType VARCHAR(150) NOT NULL,
    FileExtension VARCHAR(20) NOT NULL,
    FileSizeBytes BIGINT UNSIGNED NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    PRIMARY KEY (PasteFileId),
    UNIQUE KEY UX_PasteBinFiles_PasteId (PasteId),
    UNIQUE KEY UX_PasteBinFiles_StoredFileName (StoredFileName),
    CONSTRAINT FK_PasteBinFiles_Pastes FOREIGN KEY (PasteId) REFERENCES PasteBinPastes(PasteId) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_paste_bin_settings_get//
CREATE PROCEDURE sp_paste_bin_settings_get()
BEGIN
    SELECT MaximumUploadSizeMb, UpdatedUtc FROM PasteBinSettings WHERE Id=1;
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_settings_update//
CREATE PROCEDURE sp_paste_bin_settings_update(IN p_maximum_upload_size_mb INT)
BEGIN
    IF p_maximum_upload_size_mb < 1 OR p_maximum_upload_size_mb > 50 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Paste Bin upload limit must be between 1 and 50 MB';
    END IF;
    UPDATE PasteBinSettings SET MaximumUploadSizeMb=p_maximum_upload_size_mb,UpdatedUtc=UTC_TIMESTAMP() WHERE Id=1;
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_pastes_get//
CREATE PROCEDURE sp_paste_bin_pastes_get()
BEGIN
    SELECT p.PasteId,p.CreatedByUserId,u.DisplayName AS CreatedByDisplayName,p.ShortCode,p.Title,p.Language,
           NULL AS Content,IF(p.PasswordHash IS NULL,NULL,'protected') AS PasswordHash,p.CreatedUtc,p.ExpiresUtc,
           f.PasteFileId,f.OriginalFileName,NULL AS StoredFileName,f.ContentType AS FileContentType,
           f.FileExtension,f.FileSizeBytes,f.CreatedUtc AS FileCreatedUtc
    FROM PasteBinPastes p
    INNER JOIN Users u ON u.UserId=p.CreatedByUserId
    LEFT JOIN PasteBinFiles f ON f.PasteId=p.PasteId
    WHERE p.ExpiresUtc IS NULL OR p.ExpiresUtc>UTC_TIMESTAMP()
    ORDER BY p.CreatedUtc DESC;
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_paste_get_by_short_code//
CREATE PROCEDURE sp_paste_bin_paste_get_by_short_code(IN p_short_code VARCHAR(16))
BEGIN
    SELECT p.PasteId,p.CreatedByUserId,u.DisplayName AS CreatedByDisplayName,p.ShortCode,p.Title,p.Language,
           p.Content,p.PasswordHash,p.CreatedUtc,p.ExpiresUtc,
           f.PasteFileId,f.OriginalFileName,f.StoredFileName,f.ContentType AS FileContentType,
           f.FileExtension,f.FileSizeBytes,f.CreatedUtc AS FileCreatedUtc
    FROM PasteBinPastes p
    INNER JOIN Users u ON u.UserId=p.CreatedByUserId
    LEFT JOIN PasteBinFiles f ON f.PasteId=p.PasteId
    WHERE p.ShortCode=p_short_code AND (p.ExpiresUtc IS NULL OR p.ExpiresUtc>UTC_TIMESTAMP())
    LIMIT 1;
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_short_code_exists//
CREATE PROCEDURE sp_paste_bin_short_code_exists(IN p_short_code VARCHAR(16))
BEGIN
    SELECT EXISTS(SELECT 1 FROM PasteBinPastes WHERE ShortCode=p_short_code);
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_paste_create//
CREATE PROCEDURE sp_paste_bin_paste_create(
    IN p_paste_id CHAR(36), IN p_created_by_user_id CHAR(36), IN p_short_code VARCHAR(16),
    IN p_title VARCHAR(200), IN p_language VARCHAR(30), IN p_content MEDIUMTEXT,
    IN p_password_hash VARCHAR(512), IN p_expires_utc DATETIME, IN p_paste_file_id CHAR(36),
    IN p_original_file_name VARCHAR(255), IN p_stored_file_name CHAR(36), IN p_content_type VARCHAR(150),
    IN p_file_extension VARCHAR(20), IN p_file_size_bytes BIGINT UNSIGNED)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;
    START TRANSACTION;
    INSERT INTO PasteBinPastes(PasteId,CreatedByUserId,ShortCode,Title,Language,Content,PasswordHash,CreatedUtc,ExpiresUtc)
    VALUES(p_paste_id,p_created_by_user_id,p_short_code,p_title,p_language,p_content,p_password_hash,UTC_TIMESTAMP(),p_expires_utc);
    IF p_paste_file_id IS NOT NULL THEN
        INSERT INTO PasteBinFiles(PasteFileId,PasteId,OriginalFileName,StoredFileName,ContentType,FileExtension,FileSizeBytes,CreatedUtc)
        VALUES(p_paste_file_id,p_paste_id,p_original_file_name,p_stored_file_name,p_content_type,p_file_extension,p_file_size_bytes,UTC_TIMESTAMP());
    END IF;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_paste_delete//
CREATE PROCEDURE sp_paste_bin_paste_delete(IN p_paste_id CHAR(36), IN p_user_id CHAR(36))
BEGIN
    DECLARE v_stored_file_name CHAR(36) DEFAULT NULL;
    IF EXISTS(SELECT 1 FROM PasteBinPastes WHERE PasteId=p_paste_id AND CreatedByUserId=p_user_id) THEN
        SELECT StoredFileName INTO v_stored_file_name FROM PasteBinFiles WHERE PasteId=p_paste_id LIMIT 1;
        DELETE FROM PasteBinPastes WHERE PasteId=p_paste_id AND CreatedByUserId=p_user_id;
        SELECT v_stored_file_name AS StoredFileName;
    END IF;
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_expired_pastes_get//
CREATE PROCEDURE sp_paste_bin_expired_pastes_get()
BEGIN
    SELECT p.PasteId,f.StoredFileName FROM PasteBinPastes p LEFT JOIN PasteBinFiles f ON f.PasteId=p.PasteId
    WHERE p.ExpiresUtc IS NOT NULL AND p.ExpiresUtc<=UTC_TIMESTAMP();
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_expired_pastes_delete//
CREATE PROCEDURE sp_paste_bin_expired_pastes_delete()
BEGIN
    DELETE FROM PasteBinPastes WHERE ExpiresUtc IS NOT NULL AND ExpiresUtc<=UTC_TIMESTAMP();
END//

DROP PROCEDURE IF EXISTS sp_paste_bin_stored_file_names_get//
CREATE PROCEDURE sp_paste_bin_stored_file_names_get()
BEGIN
    SELECT f.StoredFileName FROM PasteBinFiles f INNER JOIN PasteBinPastes p ON p.PasteId=f.PasteId
    WHERE p.ExpiresUtc IS NULL OR p.ExpiresUtc>UTC_TIMESTAMP();
END//

DELIMITER ;
