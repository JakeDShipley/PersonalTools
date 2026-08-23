-- Keeps the per-user float and pattern uniqueness check fast as opening history grows.
CREATE INDEX IF NOT EXISTS IX_CaseOpeningHistory_UniqueCondition
ON CaseOpeningHistory (UserId, SourceItemId, FloatValue, PatternSeed);
