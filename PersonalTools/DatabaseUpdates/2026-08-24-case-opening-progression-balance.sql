USE PersonalTools;

-- Case availability is shared globally, while ownership and unlock state remain per user.
-- XpRequirement is the player level required to unlock the case, not a raw XP total.
START TRANSACTION;

INSERT INTO CaseOpeningCaseSettings
(
    CaseKey,
    UnlockCostStars,
    PurchaseCostStars,
    XpRequirement,
    UpdatedUtc
)
VALUES
    ('kilowatt',                            0,    1,  0, UTC_TIMESTAMP()),

    ('austin-2025-legends',                10,    1,  0, UTC_TIMESTAMP()),
    ('fever',                              10,    1,  0, UTC_TIMESTAMP()),
    ('fracture',                           10,    1,  0, UTC_TIMESTAMP()),
    ('gallery',                            10,    1,  0, UTC_TIMESTAMP()),

    ('copenhagen-2024-legends',            25,    2,  1, UTC_TIMESTAMP()),
    ('dreams-and-nightmares',              25,    2,  1, UTC_TIMESTAMP()),
    ('prisma-2',                           25,    2,  1, UTC_TIMESTAMP()),
    ('prisma',                             25,    2,  1, UTC_TIMESTAMP()),
    ('recoil',                             25,    2,  1, UTC_TIMESTAMP()),
    ('revolution',                         25,    2,  1, UTC_TIMESTAMP()),
    ('snakebite',                          25,    2,  1, UTC_TIMESTAMP()),

    ('chroma-2',                           50,    3,  2, UTC_TIMESTAMP()),
    ('clutch',                             50,    3,  2, UTC_TIMESTAMP()),
    ('paris-2023-legends',                 50,    3,  2, UTC_TIMESTAMP()),
    ('shattered-web',                      50,    3,  2, UTC_TIMESTAMP()),

    ('antwerp-2022-legends',              100,    5,  3, UTC_TIMESTAMP()),
    ('breakout',                           100,    5,  3, UTC_TIMESTAMP()),
    ('broken-fang',                        100,    5,  3, UTC_TIMESTAMP()),
    ('cs20',                               100,    5,  3, UTC_TIMESTAMP()),

    ('gamma-2',                            175,    8,  4, UTC_TIMESTAMP()),
    ('riptide',                            175,    8,  4, UTC_TIMESTAMP()),
    ('spectrum-2',                         175,    8,  4, UTC_TIMESTAMP()),
    ('stockholm-2021-legends',             175,    8,  4, UTC_TIMESTAMP()),

    ('atlanta-2017-legends',               300,   12,  5, UTC_TIMESTAMP()),
    ('glove',                              300,   12,  5, UTC_TIMESTAMP()),
    ('hydra',                              300,   12,  5, UTC_TIMESTAMP()),

    ('esports-2013',                       500,   18,  6, UTC_TIMESTAMP()),
    ('esports-2013-winter',                500,   18,  6, UTC_TIMESTAMP()),
    ('esports-2014-summer',                500,   18,  6, UTC_TIMESTAMP()),
    ('weapon-case-3',                      500,   18,  6, UTC_TIMESTAMP()),

    ('weapon-case-1',                      800,   25,  8, UTC_TIMESTAMP()),
    ('weapon-case-2',                      800,   25,  8, UTC_TIMESTAMP()),

    ('cologne-2014-cobblestone-souvenir', 1500,  40, 10, UTC_TIMESTAMP()),
    ('cologne-2014-legends',               1500,  40, 10, UTC_TIMESTAMP()),
    ('katowice-2014-challengers',          1500,  40, 10, UTC_TIMESTAMP()),
    ('katowice-2014-legends',              1500,  40, 10, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE
    UnlockCostStars = VALUES(UnlockCostStars),
    PurchaseCostStars = VALUES(PurchaseCostStars),
    XpRequirement = VALUES(XpRequirement),
    UpdatedUtc = VALUES(UpdatedUtc);

COMMIT;

SELECT
    CaseKey,
    UnlockCostStars,
    PurchaseCostStars,
    XpRequirement,
    UpdatedUtc
FROM CaseOpeningCaseSettings
ORDER BY
    XpRequirement,
    UnlockCostStars,
    CaseKey;
