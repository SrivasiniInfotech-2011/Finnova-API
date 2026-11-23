--Receipt Processing dev seed(FINNOVA - 61).Do not run in production: the F&A Bank Master owns this table later.
INSERT INTO fa_bank_accounts (Id, BankName, BankBranch, AccountNumber, MicrCode, GlCode, SlCode, IsActive, CreatedAt, UpdatedAt)
VALUES
  (NEWID(), 'HDFC Bank Ltd',      'Mumbai Fort',  '50200012345678', '400240002', '110100101', NULL,   1, SYSUTCDATETIME(), SYSUTCDATETIME()),
  (NEWID(), 'State Bank of India', 'Pune Camp', '30012345678', '411002003', '110100102', 'SL01', 1, SYSUTCDATETIME(), SYSUTCDATETIME());