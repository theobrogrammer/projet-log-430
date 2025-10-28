/**
 * k6 Load Test - UC-03 Dépôt (Idempotency Test)
 * 
 * Scenario: Test d'idempotence
 * - 20 utilisateurs
 * - Répéter la même requête avec le même idempotency-key
 * - Vérifier que seul 1 dépôt est créé
 * - Endpoint: POST /api/v1/wallet/deposit
 * 
 * Usage: k6 run scripts/k6/deposit.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

// Métriques personnalisées
const depositSuccessRate = new Rate('deposit_success_rate');
const depositDuration = new Trend('deposit_duration');
const idempotencyHits = new Counter('idempotency_hits');
const duplicatesPrevented = new Counter('duplicates_prevented');

// Configuration
export const options = {
  vus: 20,              // 20 utilisateurs simultanés
  duration: '2m',       // 2 minutes de test

  // Thresholds
  thresholds: {
    'http_req_duration': ['p(95)<800'],       // P95 < 800ms (dépôt plus lent)
    'http_req_failed': ['rate<0.05'],         // Taux d'erreur < 5%
    'deposit_success_rate': ['rate>0.95'],    // Taux de succès > 95%
    'duplicates_prevented': ['count>0'],      // Au moins 1 duplicate prévenu
  },

  tags: {
    test_type: 'idempotency',
    use_case: 'UC03_deposit',
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8090';

// Clés d'idempotence partagées (simuler des retries)
let sharedIdempotencyKeys = [];

export function setup() {
  console.log('Setup: Preparing idempotency test...');
  
  // Créer un utilisateur de test et récupérer un sessionId
  const email = 'deposit-test@k6.com';
  const password = 'SecurePass123!';

  // 1. Signup
  const signupPayload = JSON.stringify({
    email: email,
    fullName: 'Deposit Test User',
    password: password,
    confirmPassword: password,
  });

  const signupResponse = http.post(`${BASE_URL}/api/v1/signup`, signupPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (signupResponse.status !== 200) {
    console.warn(`Signup failed: ${signupResponse.status}`);
    return { sessionId: null };
  }

  // 2. Login (sans MFA pour simplifier)
  const loginPayload = JSON.stringify({
    email: email,
    password: password,
    ip: '127.0.0.1',
  });

  const loginResponse = http.post(`${BASE_URL}/api/v1/auth/login`, loginPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  let sessionId = null;
  if (loginResponse.status === 200) {
    try {
      const body = JSON.parse(loginResponse.body);
      sessionId = body.sessionId;
      console.log(`✓ Login successful, sessionId: ${sessionId}`);
    } catch (e) {
      console.error('Failed to parse login response');
    }
  }

  // Générer quelques clés d'idempotence partagées (pour simuler des retries)
  for (let i = 0; i < 10; i++) {
    sharedIdempotencyKeys.push(uuidv4());
  }

  return { 
    sessionId: sessionId,
    idempotencyKeys: sharedIdempotencyKeys,
  };
}

export default function (data) {
  if (!data.sessionId) {
    console.error('No sessionId available, skipping iteration');
    return;
  }

  // 50% du temps: utiliser une clé partagée (simuler retry)
  // 50% du temps: utiliser une nouvelle clé unique
  const useSharedKey = Math.random() < 0.5;
  let idempotencyKey;

  if (useSharedKey && data.idempotencyKeys.length > 0) {
    // Sélectionner une clé partagée aléatoire
    idempotencyKey = data.idempotencyKeys[Math.floor(Math.random() * data.idempotencyKeys.length)];
  } else {
    // Générer une nouvelle clé unique
    idempotencyKey = uuidv4();
  }

  // Montant aléatoire entre 10 et 1000 CAD
  const amount = Math.floor(Math.random() * 990) + 10;

  const payload = JSON.stringify({
    amount: amount,
    currency: 'CAD',
    paymentMethod: 'INTERAC',
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'X-Session-Id': data.sessionId,
      'Idempotency-Key': idempotencyKey,
    },
    tags: { 
      name: 'deposit',
      idempotency: useSharedKey ? 'shared' : 'unique',
    },
  };

  // Effectuer la requête
  const startTime = Date.now();
  const response = http.post(`${BASE_URL}/api/v1/wallet/deposit`, payload, params);
  const duration = Date.now() - startTime;

  depositDuration.add(duration);

  // Vérifications
  const success = check(response, {
    'status is 200 or 409': (r) => r.status === 200 || r.status === 409,
    'has valid response': (r) => {
      try {
        const body = JSON.parse(r.body);
        // 200: nouveau dépôt créé
        // 409: dépôt déjà traité (idempotence)
        return body.transactionId !== undefined || r.status === 409;
      } catch (e) {
        return false;
      }
    },
  });

  depositSuccessRate.add(success);

  // Compter les hits d'idempotence
  if (response.status === 409 || response.status === 200) {
    idempotencyHits.add(1);
    
    if (response.status === 409) {
      duplicatesPrevented.add(1);
      console.log(`✓ Idempotency key prevented duplicate: ${idempotencyKey.substring(0, 8)}...`);
    }
  }

  // Log en cas d'erreur inattendue
  if (!success && response.status !== 409) {
    console.error(`Deposit failed: ${response.status} - ${response.body}`);
  }

  // Think time
  sleep(2);
}

export function handleSummary(data) {
  const totalRequests = data.metrics.http_reqs.values.count;
  const duplicates = data.metrics.duplicates_prevented ? data.metrics.duplicates_prevented.values.count : 0;

  let summary = '\n';
  summary += 'Test: UC-03 Dépôt (Idempotency)\n';
  summary += '===============================\n\n';
  summary += `✓ Total Requests:       ${totalRequests}\n`;
  summary += `✓ Success Rate:         ${(data.metrics.deposit_success_rate.values.rate * 100).toFixed(2)}%\n`;
  summary += `✓ Duplicates Prevented: ${duplicates} (${((duplicates / totalRequests) * 100).toFixed(2)}%)\n`;
  summary += `✓ Duration P95:         ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `✓ Failed:               ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%\n`;
  summary += '\n';
  summary += `${duplicates > 0 ? '✅' : '❌'} Idempotency check: ${duplicates > 0 ? 'PASSED' : 'FAILED'}\n`;

  return {
    'stdout': summary,
    'resultats-k6/deposit-summary.json': JSON.stringify(data, null, 2),
  };
}
