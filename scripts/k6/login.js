/**
 * k6 Load Test - UC-02 Authentification
 * 
 * Scenario: Ramping VUs (montée en charge progressive)
 * - 0 → 50 → 0 utilisateurs
 * - Durée: 3 minutes
 * - Endpoint: POST /api/v1/auth/login
 * 
 * Usage: k6 run scripts/k6/login.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Métriques personnalisées
const loginSuccessRate = new Rate('login_success_rate');
const loginDuration = new Trend('login_duration');
const mfaRequiredCounter = new Counter('mfa_required_count');

// Configuration du scénario - Ramping
export const options = {
  stages: [
    { duration: '1m', target: 20 },   // Montée progressive à 20 VUs
    { duration: '1m', target: 50 },   // Pic à 50 VUs
    { duration: '1m', target: 0 },    // Redescente à 0
  ],

  // Thresholds
  thresholds: {
    'http_req_duration': ['p(95)<500'],      // P95 < 500ms
    'http_req_failed': ['rate<0.05'],        // Taux d'erreur < 5%
    'login_success_rate': ['rate>0.90'],     // Taux de succès > 90%
    'login_duration': ['p(99)<1000'],        // P99 < 1s
  },

  // Tags
  tags: {
    test_type: 'ramp',
    use_case: 'UC02_login',
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8090';

// Pool d'utilisateurs de test (créés au préalable ou via setup)
const TEST_USERS = [
  { email: 'test1@example.com', password: 'SecurePass123!' },
  { email: 'test2@example.com', password: 'SecurePass123!' },
  { email: 'test3@example.com', password: 'SecurePass123!' },
  { email: 'user@brokerx.com', password: 'SecurePass123!' },
  { email: 'demo@test.com', password: 'SecurePass123!' },
];

export function setup() {
  console.log('Setup: Creating test users for login tests...');
  
  // Créer quelques utilisateurs de test si nécessaire
  const usersToCreate = [
    { email: 'loadtest1@k6.com', fullName: 'Load Test 1', password: 'SecurePass123!' },
    { email: 'loadtest2@k6.com', fullName: 'Load Test 2', password: 'SecurePass123!' },
    { email: 'loadtest3@k6.com', fullName: 'Load Test 3', password: 'SecurePass123!' },
  ];

  usersToCreate.forEach(user => {
    const payload = JSON.stringify({
      email: user.email,
      fullName: user.fullName,
      password: user.password,
      confirmPassword: user.password,
    });

    const response = http.post(`${BASE_URL}/api/v1/signup`, payload, {
      headers: { 'Content-Type': 'application/json' },
    });

    if (response.status === 200) {
      TEST_USERS.push({ email: user.email, password: user.password });
      console.log(`✓ Created user: ${user.email}`);
    }
  });

  return { users: TEST_USERS };
}

export default function (data) {
  // Sélectionner un utilisateur aléatoire
  const user = data.users[Math.floor(Math.random() * data.users.length)];

  const payload = JSON.stringify({
    email: user.email,
    password: user.password,
    ip: '192.168.1.100', // IP fictive pour les tests
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    tags: { name: 'login' },
  };

  // Effectuer la requête de login
  const startTime = Date.now();
  const response = http.post(`${BASE_URL}/api/v1/auth/login`, payload, params);
  const duration = Date.now() - startTime;

  loginDuration.add(duration);

  // Vérifications
  const success = check(response, {
    'status is 200 or 202': (r) => r.status === 200 || r.status === 202,
    'has valid response': (r) => {
      try {
        const body = JSON.parse(r.body);
        // Soit sessionId (login direct), soit challengeId (MFA requis)
        return body.sessionId !== undefined || body.challengeId !== undefined;
      } catch (e) {
        return false;
      }
    },
  });

  loginSuccessRate.add(success);

  // Compter les MFA
  if (response.status === 202) {
    mfaRequiredCounter.add(1);
  }

  // Log en cas d'erreur
  if (!success) {
    console.error(`Login failed for ${user.email}: ${response.status} - ${response.body}`);
  }

  // Think time variable selon la charge
  const thinkTime = __VU < 30 ? 2 : 1; // Plus de pause avec moins d'utilisateurs
  sleep(thinkTime);
}

export function handleSummary(data) {
  const mfaCount = data.metrics.mfa_required_count ? data.metrics.mfa_required_count.values.count : 0;
  const totalRequests = data.metrics.http_reqs.values.count;

  let summary = '\n';
  summary += 'Test: UC-02 Authentification (Ramping Load)\n';
  summary += '===========================================\n\n';
  summary += `✓ Total Requests: ${totalRequests}\n`;
  summary += `✓ Login Success:  ${(data.metrics.login_success_rate.values.rate * 100).toFixed(2)}%\n`;
  summary += `✓ MFA Required:   ${mfaCount} (${((mfaCount / totalRequests) * 100).toFixed(2)}%)\n`;
  summary += `✓ Duration P95:   ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `✓ Duration P99:   ${data.metrics.login_duration.values['p(99)'].toFixed(2)}ms\n`;
  summary += `✓ Failed:         ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%\n`;

  return {
    'stdout': summary,
    'resultats-k6/login-summary.json': JSON.stringify(data, null, 2),
  };
}
