-- Adds last-sign-in activity to the administrator list. It uses session creation time rather than
-- browser data, so it remains useful without recording a separate invasive activity history.

DELIMITER //

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
        u.CreatedUtc
    ORDER BY u.DisplayName, u.Email;
END//

DELIMITER ;
