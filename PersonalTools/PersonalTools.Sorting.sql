USE PersonalTools;

ALTER TABLE QuickLinks
    ADD COLUMN IF NOT EXISTS SortOrder INT NOT NULL DEFAULT 0 AFTER IconClass,
    ADD INDEX IF NOT EXISTS IX_QuickLinks_UserId_SortOrder (UserId, SortOrder);

CREATE TABLE IF NOT EXISTS DashboardWidgetOrders (
    UserId BIGINT UNSIGNED NOT NULL,
    WidgetKey VARCHAR(50) NOT NULL,
    SortOrder INT NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (UserId, WidgetKey),
    KEY IX_DashboardWidgetOrders_UserId_SortOrder (UserId, SortOrder),
    CONSTRAINT FK_DashboardWidgetOrders_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_notes_set_order_bulk$$
CREATE PROCEDURE sp_notes_set_order_bulk(IN p_user_id BIGINT, IN p_note_ids JSON)
BEGIN
    UPDATE Notes AS notes
    INNER JOIN JSON_TABLE(p_note_ids, '$[*]' COLUMNS (
        SortOrder FOR ORDINALITY,
        NoteId CHAR(36) PATH '$'
    )) AS positions ON positions.NoteId = notes.NoteId
    SET notes.SortOrder = positions.SortOrder
    WHERE notes.UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_quick_links_get$$
CREATE PROCEDURE sp_quick_links_get(IN p_user_id BIGINT)
SELECT QuickLinkId, Title, Url, IconClass, SortOrder, UpdatedUtc
FROM QuickLinks
WHERE UserId = p_user_id
ORDER BY SortOrder, CreatedUtc, QuickLinkId$$

DROP PROCEDURE IF EXISTS sp_quick_links_create$$
CREATE PROCEDURE sp_quick_links_create(IN p_user_id BIGINT, IN p_title VARCHAR(100), IN p_url VARCHAR(2048), IN p_icon_class VARCHAR(100))
BEGIN
    INSERT INTO QuickLinks(UserId, Title, Url, IconClass, SortOrder, CreatedUtc, UpdatedUtc)
    SELECT p_user_id, p_title, p_url, NULLIF(p_icon_class, ''), COALESCE(MAX(SortOrder) + 1, 0), UTC_TIMESTAMP(), UTC_TIMESTAMP()
    FROM QuickLinks
    WHERE UserId = p_user_id;
    SELECT LAST_INSERT_ID();
END$$

DROP PROCEDURE IF EXISTS sp_quick_links_set_order_bulk$$
CREATE PROCEDURE sp_quick_links_set_order_bulk(IN p_user_id BIGINT, IN p_quick_link_ids JSON)
BEGIN
    UPDATE QuickLinks AS links
    INNER JOIN JSON_TABLE(p_quick_link_ids, '$[*]' COLUMNS (
        SortOrder FOR ORDINALITY,
        QuickLinkId BIGINT PATH '$'
    )) AS positions ON positions.QuickLinkId = links.QuickLinkId
    SET links.SortOrder = positions.SortOrder
    WHERE links.UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_dashboard_widget_order_get$$
CREATE PROCEDURE sp_dashboard_widget_order_get(IN p_user_id BIGINT)
SELECT WidgetKey
FROM DashboardWidgetOrders
WHERE UserId = p_user_id
ORDER BY SortOrder, WidgetKey$$

DROP PROCEDURE IF EXISTS sp_dashboard_widget_order_set_bulk$$
CREATE PROCEDURE sp_dashboard_widget_order_set_bulk(IN p_user_id BIGINT, IN p_widget_keys JSON)
BEGIN
    INSERT INTO DashboardWidgetOrders(UserId, WidgetKey, SortOrder, UpdatedUtc)
    SELECT p_user_id, positions.WidgetKey, positions.SortOrder, UTC_TIMESTAMP()
    FROM JSON_TABLE(p_widget_keys, '$[*]' COLUMNS (
        SortOrder FOR ORDINALITY,
        WidgetKey VARCHAR(50) PATH '$'
    )) AS positions
    ON DUPLICATE KEY UPDATE SortOrder = VALUES(SortOrder), UpdatedUtc = VALUES(UpdatedUtc);
END$$

DELIMITER ;
