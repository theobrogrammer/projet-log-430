/**
 * k6 Load Test - UC-01 Inscription
 * 
 * Scenario: Constant VUs (Virtual Users)
 * - 10 utilisateurs simultanés
 * - Durée: 1 minute
 * - Endpoint: POST /api/v1/signup
 * 
 * Usage: k6 run scripts/k6/signup.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Métriques personnalisées
const signupSuccessRate = new Rate('signup_success_rate');
const signupDuration = new Trend('signup_duration');

// Configuration du scénario
export const options = {
  vus: 10,              // 10 utilisateurs virtuels constants
  duration: '1m',       // 1 minute de test

  // Thresholds (seuils de réussite)
  thresholds: {
    'http_req_duration': ['p(95)<500'],      // P95 < 500ms
    'http_req_failed': ['rate<0.05'],        // Taux d'erreur < 5%
    'signup_success_rate': ['rate>0.95'],    // Taux de succès > 95%
    'signup_duration': ['p(95)<1000'],       // P95 signup < 1s
  },

  // Tags pour identification dans Grafana
  tags: {
    test_type: 'load',
    use_case: 'UC01_signup',
  },
};

// URL de base (configurable via ENV)
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8090';

export default function () {
  // Générer un email unique par itération
  const email = `user_${__VU}_${Date.now()}@k6test.com`;
  const fullName = `K6 Test User ${__VU}`;
  const password = 'SecurePass123!';

  const payload = JSON.stringify({
    email: email,
    fullName: fullName,
    password: password,
    confirmPassword: password,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    tags: { name: 'signup' },
  };

  // Effectuer la requête
  const startTime = Date.now();
  const response = http.post(`${BASE_URL}/api/v1/signup`, payload, params);
  const duration = Date.now() - startTime;

  // Enregistrer les métriques
  signupDuration.add(duration);

  // Vérifications
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'has clientId': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.clientId !== undefined && body.clientId !== null;
      } catch (e) {
        return false;
      }
    },
    'has accountId': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.accountId !== undefined && body.accountId !== null;
      } catch (e) {
        return false;
      }
    },
    'status is Pending': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.status === 'Pending';
      } catch (e) {
        return false;
      }
    },
  });

  signupSuccessRate.add(success);

  // Log en cas d'erreur
  if (!success || response.status !== 200) {
    console.error(`Signup failed: ${response.status} - ${response.body}`);
  }

  // Pause entre les requêtes (think time)
  sleep(1);
}

// Fonction de résumé en fin de test
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'resultats-k6/signup-summary.json': JSON.stringify(data, null, 2),
  };
}

function textSummary(data, options) {
  const indent = options.indent || '';
  const enableColors = options.enableColors || false;

  let summary = '\n';
  summary += `${indent}Test: UC-01 Inscription (Constant Load)\n`;
  summary += `${indent}========================================\n\n`;
  
  summary += `${indent}✓ Requests:     ${data.metrics.http_reqs.values.count}\n`;
  summary += `${indent}✓ Success Rate: ${(data.metrics.signup_success_rate.values.rate * 100).toFixed(2)}%\n`;
  summary += `${indent}✓ Duration P95: ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `${indent}✓ Failed:       ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%\n`;
  
  return summary;
}
