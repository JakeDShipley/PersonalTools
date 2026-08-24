-- Personal Tools user roles.
-- User = 1, Admin = 2. The named owner accounts are promoted during this upgrade;
-- all other accounts keep the default User role.

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS UserRole TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER IsActive;

UPDATE Users
SET UserRole = 1
WHERE UserRole NOT IN (1, 2);

UPDATE Users
SET UserRole = 2
WHERE Email IN ('nerb163@gmail.com', 'blits@gmail.com');

DELIMITER //

DROP PROCEDURE IF EXISTS sp_auth_user_get_by_email//
CREATE PROCEDURE sp_auth_user_get_by_email(IN p_email VARCHAR(254))
BEGIN
    SELECT
        UserId,
        Email,
        DisplayName,
        PasswordHash,
        IsActive,
        SteamId,
        UserRole AS Role
    FROM Users
    WHERE Email = p_email
    LIMIT 1;
END//

DROP PROCEDURE IF EXISTS sp_auth_user_get_by_id//
CREATE PROCEDURE sp_auth_user_get_by_id(IN p_user_id CHAR(36))
BEGIN
    SELECT
        UserId,
        Email,
        DisplayName,
        PasswordHash,
        IsActive,
        SteamId,
        UserRole AS Role
    FROM Users
    WHERE UserId = p_user_id
    LIMIT 1;
END//

DROP PROCEDURE IF EXISTS sp_auth_owner_create//
CREATE PROCEDURE sp_auth_owner_create(
    IN p_user_id CHAR(36),
    IN p_email VARCHAR(254),
    IN p_display_name VARCHAR(100),
    IN p_password_hash VARCHAR(512))
BEGIN
    INSERT INTO Users
    (
        UserId,
        Email,
        DisplayName,
        PasswordHash,
        UserRole,
        CreatedUtc
    )
    VALUES
    (
        p_user_id,
        p_email,
        p_display_name,
        p_password_hash,
        2,
        UTC_TIMESTAMP()
    );
END//

DELIMITER ;
