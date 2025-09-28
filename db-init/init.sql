-- Entity Framework va créer automatiquement toutes les tables
-- basées sur la configuration dans BrokerXDbContext.cs
-- 
-- Les tables créées seront :
-- - Clients
-- - Accounts 
-- - Portfolios
-- - PayTxs
-- - LedgerEntries  
-- - KycDossiers (nouveau)
-- - ContactOtps (nouveau)
--
-- Avec toutes les relations et contraintes définies dans OnModelCreating()

-- ===================================================================
-- DONNÉES DE TEST
-- ===================================================================
-- Note: Ces INSERT vont être exécutés APRÈS que EF ait créé les tables

-- Attendre un peu pour s'assurer que les tables sont créées
-- (Les INSERT échoueront silencieusement si les tables n'existent pas encore)

-- Client 1: Utilisateur déjà vérifié et actif
INSERT IGNORE INTO Clients (ClientId, Email, NomComplet, Telephone, DateNaissance, Statut, CreatedAt, UpdatedAt) 
VALUES (
  '550e8400-e29b-41d4-a716-446655440001',
  'alice.martin@example.com',
  'Alice Martin',
  '438-555-0101',
  '1995-03-15',
  'Active',
  '2025-09-28 10:00:00.000000',
  '2025-09-28 10:00:00.000000'
);

-- Client 2: Utilisateur en attente (pour tester le processus KYC/OTP)
INSERT IGNORE INTO Clients (ClientId, Email, NomComplet, Telephone, DateNaissance, Statut, CreatedAt, UpdatedAt) 
VALUES (
  '550e8400-e29b-41d4-a716-446655440002',
  'bob.dupont@example.com',
  'Bob Dupont',
  '438-555-0102',
  '1988-07-22',
  'Pending',
  '2025-09-28 11:00:00.000000',
  '2025-09-28 11:00:00.000000'
);

-- Client 3: Utilisateur rejeté (pour tester les cas d'erreur)
INSERT IGNORE INTO Clients (ClientId, Email, NomComplet, Telephone, DateNaissance, Statut, CreatedAt, UpdatedAt) 
VALUES (
  '550e8400-e29b-41d4-a716-446655440003',
  'charlie.rejects@example.com',
  'Charlie Rejeté',
  '438-555-0103',
  '1990-12-05',
  'Rejected',
  '2025-09-28 12:00:00.000000',
  '2025-09-28 12:00:00.000000'
);

-- Compte pour Alice (utilisateur actif)
INSERT IGNORE INTO Accounts (AccountId, ClientId, AccountNo, Statut, CreatedAt, UpdatedAt)
VALUES (
  '660e8400-e29b-41d4-a716-446655440001',
  '550e8400-e29b-41d4-a716-446655440001',
  'BX-20250928-100001',
  'Active',
  '2025-09-28 10:05:00.000000',
  '2025-09-28 10:05:00.000000'
);

-- Portefeuille pour le compte d'Alice
INSERT IGNORE INTO Portfolios (Id, AccountId, Devise, SoldeMonnaie, UpdatedAt)
VALUES (
  '770e8400-e29b-41d4-a716-446655440001',
  '660e8400-e29b-41d4-a716-446655440001',
  'CAD',
  1500.50,
  '2025-09-28 10:10:00.000000'
);

-- Dossier KYC vérifié pour Alice
INSERT IGNORE INTO KycDossiers (KycId, ClientId, Niveau, Statut, UpdatedAt)
VALUES (
  '880e8400-e29b-41d4-a716-446655440001',
  '550e8400-e29b-41d4-a716-446655440001',
  'Standard',
  'Verified',
  '2025-09-28 10:15:00.000000'
);

-- Dossier KYC en attente pour Bob
INSERT IGNORE INTO KycDossiers (KycId, ClientId, Niveau, Statut, UpdatedAt)
VALUES (
  '880e8400-e29b-41d4-a716-446655440002',
  '550e8400-e29b-41d4-a716-446655440002',
  'Basic',
  'Pending',
  '2025-09-28 11:05:00.000000'
);

-- OTP vérifié pour Alice (email)
INSERT IGNORE INTO ContactOtps (OtpId, ClientId, Canal, Statut, ExpiresAt, CreatedAt)
VALUES (
  '990e8400-e29b-41d4-a716-446655440001',
  '550e8400-e29b-41d4-a716-446655440001',
  'Email',
  'Verified',
  '2025-09-28 10:30:00.000000',
  '2025-09-28 10:20:00.000000'
);

-- OTP en attente pour Bob (SMS)
INSERT IGNORE INTO ContactOtps (OtpId, ClientId, Canal, Statut, ExpiresAt, CreatedAt)
VALUES (
  '990e8400-e29b-41d4-a716-446655440002',
  '550e8400-e29b-41d4-a716-446655440002',
  'Sms',
  'Pending',
  '2025-09-28 23:59:00.000000',
  '2025-09-28 11:10:00.000000'
);

-- Transaction de dépôt réussie pour Alice
INSERT IGNORE INTO PayTxs (PaymentTxId, AccountId, Amount, Currency, Statut, IdempotencyKey, CreatedAt, SettledAt, FailureReason)
VALUES (
  'aa0e8400-e29b-41d4-a716-446655440001',
  '660e8400-e29b-41d4-a716-446655440001',
  1000.00,
  'CAD',
  'Settled',
  'DEPOSIT-ALICE-20250928-001',
  '2025-09-28 10:25:00.000000',
  '2025-09-28 10:26:00.000000',
  NULL
);

-- Transaction échouée pour Alice (pour tester les cas d'erreur)
INSERT IGNORE INTO PayTxs (PaymentTxId, AccountId, Amount, Currency, Statut, IdempotencyKey, CreatedAt, SettledAt, FailureReason)
VALUES (
  'aa0e8400-e29b-41d4-a716-446655440002',
  '660e8400-e29b-41d4-a716-446655440001',
  500.00,
  'CAD',
  'Failed',
  'DEPOSIT-ALICE-20250928-002',
  '2025-09-28 12:00:00.000000',
  NULL,
  'Insufficient funds on source account'
);

-- Entrées de ledger correspondant au dépôt réussi
INSERT IGNORE INTO LedgerEntries (LedgerEntryId, AccountId, Amount, Currency, Kind, RefType, RefId, CreatedAt)
VALUES (
  'bb0e8400-e29b-41d4-a716-446655440001',
  '660e8400-e29b-41d4-a716-446655440001',
  1000.00,
  'CAD',
  'DEPOSIT',
  'PAYMENT_TX',
  'aa0e8400-e29b-41d4-a716-446655440001',
  '2025-09-28 10:26:00.000000'
);

-- Ajustement manuel dans le ledger
INSERT IGNORE INTO LedgerEntries (LedgerEntryId, AccountId, Amount, Currency, Kind, RefType, RefId, CreatedAt)
VALUES (
  'bb0e8400-e29b-41d4-a716-446655440002',
  '660e8400-e29b-41d4-a716-446655440001',
  500.50,
  'CAD',
  'ADJUSTMENT',
  'OTHER',
  'cc0e8400-e29b-41d4-a716-446655440001',
  '2025-09-28 15:00:00.000000'
);

-- ===================================================================
-- RÉSUMÉ DES DONNÉES DE TEST
-- ===================================================================
-- 
-- CLIENTS:
--   1. alice.martin@example.com (Active) - Prêt à utiliser
--   2. bob.dupont@example.com (Pending) - En cours de vérification  
--   3. charlie.rejects@example.com (Rejected) - Rejeté
--
-- COMPTES & PORTEFEUILLES:
--   - Alice a un compte actif (BX-20250928-100001) avec 1500.50 CAD
--
-- KYC:
--   - Alice: Vérifié (Standard)
--   - Bob: En attente (Basic)
--
-- OTP:
--   - Alice: Email vérifié
--   - Bob: SMS en attente
--
-- TRANSACTIONS:
--   - Alice: 1 dépôt réussi (1000 CAD), 1 échec (500 CAD)
--
-- LEDGER:
--   - Entrée de dépôt (1000 CAD) + Ajustement (500.50 CAD)
--
-- ===================================================================
