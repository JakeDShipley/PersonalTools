-- Case Opening catalogue prices are tiered so cases in the same progression tier always
-- have the same permanent unlock and repeat purchase price.

UPDATE CaseOpeningCaseSettings
SET
    UnlockCostStars = CASE
        WHEN CaseKey = 'kilowatt' THEN 0
        WHEN CaseKey IN ('fever','gallery','fracture','austin-2025-legends') THEN 10
        WHEN CaseKey IN ('snakebite','revolution','prisma-2','copenhagen-2024-legends','dreams-and-nightmares','recoil','prisma') THEN 25
        WHEN CaseKey IN ('paris-2023-legends','clutch','shattered-web','chroma-2') THEN 50
        WHEN CaseKey IN ('antwerp-2022-legends','broken-fang','breakout','cs20') THEN 100
        WHEN CaseKey IN ('stockholm-2021-legends','gamma-2','riptide','spectrum-2') THEN 175
        WHEN CaseKey IN ('atlanta-2017-legends','hydra','glove') THEN 300
        WHEN CaseKey IN ('esports-2013','weapon-case-3','esports-2014-summer','esports-2013-winter') THEN 500
        WHEN CaseKey IN ('weapon-case-1','weapon-case-2') THEN 800
        WHEN CaseKey IN ('cologne-2014-legends','katowice-2014-challengers','katowice-2014-legends','cologne-2014-cobblestone-souvenir') THEN 1500
        ELSE UnlockCostStars
    END,
    PurchaseCostStars = CASE
        WHEN CaseKey = 'kilowatt' THEN 1
        WHEN CaseKey IN ('fever','gallery','fracture','austin-2025-legends') THEN 1
        WHEN CaseKey IN ('snakebite','revolution','prisma-2','copenhagen-2024-legends','dreams-and-nightmares','recoil','prisma') THEN 3
        WHEN CaseKey IN ('paris-2023-legends','clutch','shattered-web','chroma-2') THEN 5
        WHEN CaseKey IN ('antwerp-2022-legends','broken-fang','breakout','cs20') THEN 10
        WHEN CaseKey IN ('stockholm-2021-legends','gamma-2','riptide','spectrum-2') THEN 18
        WHEN CaseKey IN ('atlanta-2017-legends','hydra','glove') THEN 30
        WHEN CaseKey IN ('esports-2013','weapon-case-3','esports-2014-summer','esports-2013-winter') THEN 50
        WHEN CaseKey IN ('weapon-case-1','weapon-case-2') THEN 80
        WHEN CaseKey IN ('cologne-2014-legends','katowice-2014-challengers','katowice-2014-legends','cologne-2014-cobblestone-souvenir') THEN 150
        ELSE PurchaseCostStars
    END,
    UpdatedUtc = UTC_TIMESTAMP();
