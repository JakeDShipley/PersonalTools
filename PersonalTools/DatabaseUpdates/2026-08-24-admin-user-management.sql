-- Run after 2026-08-24-user-roles.sql.
-- These procedures deliberately return only safe account fields to the administrator UI.

DELIMITER //

DROP PROCEDURE IF EXISTS sp_auth_users_get_all//
CREATE PROCEDURE sp_auth_users_get_all()
BEGIN
    SELECT UserId, Email, DisplayName, IsActive, UserRole AS Role, CreatedUtc
    FROM Users
    ORDER BY DisplayName, Email;
END//

DROP PROCEDURE IF EXISTS sp_auth_active_admin_count//
CREATE PROCEDURE sp_auth_active_admin_count()
BEGIN
    SELECT COUNT(*)
    FROM Users
    WHERE IsActive = 1 AND UserRole = 2;
END//

DROP PROCEDURE IF EXISTS sp_auth_user_create//
CREATE PROCEDURE sp_auth_user_create(
    IN p_user_id CHAR(36),
    IN p_email VARCHAR(254),
    IN p_display_name VARCHAR(100),
    IN p_password_hash VARCHAR(512),
    IN p_role TINYINT UNSIGNED,
    IN p_is_active TINYINT)
BEGIN
    IF p_role NOT IN (1, 2) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A valid user role is required.';
    END IF;

    INSERT INTO Users (UserId, Email, DisplayName, PasswordHash, IsActive, UserRole, CreatedUtc)
    VALUES (p_user_id, p_email, p_display_name, p_password_hash, p_is_active, p_role, UTC_TIMESTAMP());
END//

DROP PROCEDURE IF EXISTS sp_auth_user_update//
CREATE PROCEDURE sp_auth_user_update(
    IN p_user_id CHAR(36),
    IN p_email VARCHAR(254),
    IN p_display_name VARCHAR(100),
    IN p_password_hash VARCHAR(512),
    IN p_role TINYINT UNSIGNED,
    IN p_is_active TINYINT)
BEGIN
    IF p_role NOT IN (1, 2) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A valid user role is required.';
    END IF;

    UPDATE Users
    SET Email = p_email,
        DisplayName = p_display_name,
        PasswordHash = CASE WHEN NULLIF(p_password_hash, '') IS NULL THEN PasswordHash ELSE p_password_hash END,
        UserRole = p_role,
        IsActive = p_is_active
    WHERE UserId = p_user_id;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'The selected user account does not exist.';
    END IF;
END//

DELIMITER ;
