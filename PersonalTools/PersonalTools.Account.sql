USE PersonalTools;

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_auth_user_change_password$$
CREATE PROCEDURE sp_auth_user_change_password(
    IN p_user_id BIGINT,
    IN p_session_id CHAR(32),
    IN p_password_hash VARCHAR(512)
)
BEGIN
    UPDATE Users
    SET PasswordHash = p_password_hash
    WHERE UserId = p_user_id
      AND IsActive = 1;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Account is unavailable';
    END IF;

    DELETE FROM UserSessions
    WHERE UserId = p_user_id
      AND SessionId <> p_session_id;
END$$

DELIMITER ;
