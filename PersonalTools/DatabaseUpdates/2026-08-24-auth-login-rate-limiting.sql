USE PersonalTools;

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS FailedLoginAttempts INT UNSIGNED NOT NULL DEFAULT 0 AFTER UserRole,
    ADD COLUMN IF NOT EXISTS LockoutUntilUtc DATETIME(6) NULL AFTER FailedLoginAttempts,
    ADD COLUMN IF NOT EXISTS LastFailedLoginUtc DATETIME(6) NULL AFTER LockoutUntilUtc,
    ADD INDEX IF NOT EXISTS IX_Users_LockoutUntilUtc (LockoutUntilUtc);

-- The public first-account setup flow has been removed. Accounts are now created by an
-- authenticated administrator, so the old bootstrap procedures must no longer remain callable.
DELIMITER //

DROP PROCEDURE IF EXISTS sp_auth_user_count//
DROP PROCEDURE IF EXISTS sp_auth_owner_create//

DROP PROCEDURE IF EXISTS sp_auth_user_get_by_email//
CREATE PROCEDURE sp_auth_user_get_by_email(
    IN p_email VARCHAR(254)
)
BEGIN
    SELECT
        UserId,
        Email,
        DisplayName,
        PasswordHash,
        IsActive,
        SteamId,
        UserRole AS Role,
        FailedLoginAttempts,
        LockoutUntilUtc,
        LastFailedLoginUtc
    FROM Users
    WHERE Email = p_email
    LIMIT 1;
END//

DROP PROCEDURE IF EXISTS sp_auth_user_get_by_id//
CREATE PROCEDURE sp_auth_user_get_by_id(
    IN p_user_id CHAR(36)
)
BEGIN
    SELECT
        UserId,
        Email,
        DisplayName,
        PasswordHash,
        IsActive,
        SteamId,
        UserRole AS Role,
        FailedLoginAttempts,
        LockoutUntilUtc,
        LastFailedLoginUtc
    FROM Users
    WHERE UserId = p_user_id
    LIMIT 1;
END//

DROP PROCEDURE IF EXISTS sp_auth_users_get_all//
CREATE PROCEDURE sp_auth_users_get_all()
BEGIN
    SELECT
        u.UserId,
        u.Email,
        u.DisplayName,
        u.IsActive,
        u.UserRole AS Role,
        u.CreatedUtc,
        u.FailedLoginAttempts,
        u.LockoutUntilUtc,
        u.LastFailedLoginUtc,
        MAX(s.CreatedUtc) AS LastLoginUtc
    FROM Users u
    LEFT JOIN UserSessions s
        ON s.UserId = u.UserId
    GROUP BY
        u.UserId,
        u.Email,
        u.DisplayName,
        u.IsActive,
        u.UserRole,
        u.CreatedUtc,
        u.FailedLoginAttempts,
        u.LockoutUntilUtc,
        u.LastFailedLoginUtc
    ORDER BY u.DisplayName, u.Email;
END//

DROP PROCEDURE IF EXISTS sp_auth_login_failure_record//
CREATE PROCEDURE sp_auth_login_failure_record(
    IN p_user_id CHAR(36),
    IN p_maximum_attempts INT,
    IN p_lockout_minutes INT
)
BEGIN
    DECLARE v_failed_attempts INT DEFAULT 0;
    DECLARE v_lockout_until DATETIME(6) DEFAULT NULL;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    -- Lock the account row while incrementing so concurrent bad requests cannot lose updates
    -- and accidentally avoid the configured threshold.
    SELECT
        FailedLoginAttempts,
        LockoutUntilUtc
    INTO
        v_failed_attempts,
        v_lockout_until
    FROM Users
    WHERE UserId = p_user_id
    FOR UPDATE;

    IF v_lockout_until IS NULL OR v_lockout_until <= UTC_TIMESTAMP(6) THEN
        IF v_lockout_until IS NOT NULL THEN
            SET v_failed_attempts = 0;
        END IF;

        SET v_failed_attempts = v_failed_attempts + 1;

        IF v_failed_attempts >= p_maximum_attempts THEN
            SET v_lockout_until = DATE_ADD(UTC_TIMESTAMP(6), INTERVAL p_lockout_minutes MINUTE);
        END IF;

        UPDATE Users
        SET
            FailedLoginAttempts = v_failed_attempts,
            LockoutUntilUtc = v_lockout_until,
            LastFailedLoginUtc = UTC_TIMESTAMP(6)
        WHERE UserId = p_user_id;
    END IF;

    COMMIT;

    SELECT
        UserId,
        FailedLoginAttempts,
        LockoutUntilUtc,
        LastFailedLoginUtc
    FROM Users
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_auth_login_success_record//
CREATE PROCEDURE sp_auth_login_success_record(
    IN p_user_id CHAR(36)
)
BEGIN
    UPDATE Users
    SET
        FailedLoginAttempts = 0,
        LockoutUntilUtc = NULL
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_auth_login_lockout_reset//
CREATE PROCEDURE sp_auth_login_lockout_reset(
    IN p_user_id CHAR(36)
)
BEGIN
    UPDATE Users
    SET
        FailedLoginAttempts = 0,
        LockoutUntilUtc = NULL
    WHERE UserId = p_user_id;
END//

DELIMITER ;
