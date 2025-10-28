/**
 * k6 Load Test - Scénario Mixte Court (Pour tests de scaling)
 * 
 * Scenario: Trafic mixte réaliste - VERSION COURTE
 * - 60% de lectures (health, status)
 * - 40% d'écritures (signup, login, deposit)
 * - Charge constante: 20 VUs pendant 2 minutes
 * 
 * Usage: k6 run scripts/k6/mixed-short.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

// Métriques personnalisées
const readSuccessRate = new Rate('read_success_rate');
const writeSuccessRate = new Rate('write_success_rate');
const overallDuration = new Trend('overall_duration');
const readCounter = new Counter('read_operations');
const writeCounter = new Counter('write_operations');

// Configuration - Charge constante pour tests rapides
export const options = {
  vus: 20,
  duration: '1m',

  // Thresholds globaux
  thresholds: {
    'http_req_duration': ['p(95)<500'],       // P95 < 500ms
    'http_req_failed': ['rate<0.05'],         // Taux d'erreur < 5%
    'read_success_rate': ['rate>0.98'],       // Lectures > 98%
    'write_success_rate': ['rate>0.92'],      // Écritures > 92%
    'http_req_duration{operation:read}': ['p(95)<200'],   // Lectures rapides
    'http_req_duration{operation:write}': ['p(95)<800'],  // Écritures plus lentes
  },

  tags: {
    test_type: 'mixed_short',
    use_case: 'scaling_test',
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8090';

export function setup() {
  console.log('Setup: Creating test users with JWT tokens...');
  console.log(`Using BASE_URL: ${BASE_URL}`);
  
  // Créer quelques utilisateurs avec leurs tokens JWT
  const testUsers = [];
  for (let i = 1; i <= 5; i++) {
    const email = `scaling-test${i}-${Date.now()}@k6.com`;
    const password = 'SecurePass123!';

    // 1. Signup
    const signupPayload = JSON.stringify({
      email: email,
      fullName: `Scaling Test User ${i}`,
      password: password,
      confirmPassword: password,
    });

    const signupResponse = http.post(`${BASE_URL}/api/v1/signup`, signupPayload, {
      headers: { 'Content-Type': 'application/json' },
    });

    if (signupResponse.status === 200) {
      // 2. Login pour obtenir JWT
      const loginPayload = JSON.stringify({
        email: email,
        password: password,
        ip: '10.0.0.1',
      });

      const loginResponse = http.post(`${BASE_URL}/api/v1/auth/login`, loginPayload, {
        headers: { 'Content-Type': 'application/json' },
      });

      let token = null;
      if (loginResponse.status === 200) {
        try {
          const body = JSON.parse(loginResponse.body);
          token = body.token;
        } catch (e) {
          console.log(`✗ Failed to parse login response for ${email}`);
        }
      }

      testUsers.push({ email, password, token });
      console.log(`✓ Created user: ${email} ${token ? '(with JWT)' : '(no token)'}`);
    }
  }

  return { users: testUsers };
}

export default function (data) {
  // Décider de l'opération: 60% read, 40% write
  const rand = Math.random();

  if (rand < 0.60) {
    // READ OPERATION (60%)
    performReadOperation();
  } else {
    // WRITE OPERATION (40%)
    performWriteOperation(data);
  }
}

function performReadOperation() {
  const operations = [
    // Health check (70%)
    () => {
      const response = http.get(`${BASE_URL}/health`, {
        tags: { operation: 'read', type: 'health' },
      });
      
      const success = check(response, {
        'health status 200': (r) => r.status === 200,
      });
      
      readSuccessRate.add(success);
      readCounter.add(1);
      return response;
    },

    // Metrics check (30%)
    () => {
      const response = http.get(`${BASE_URL}/metrics`, {
        tags: { operation: 'read', type: 'metrics' },
      });
      
      const success = check(response, {
        'metrics status 200': (r) => r.status === 200,
      });
      
      readSuccessRate.add(success);
      readCounter.add(1);
      return response;
    },
  ];

  // Sélectionner une opération (70% health, 30% metrics)
  const operation = Math.random() < 0.7 ? operations[0] : operations[1];
  
  const startTime = Date.now();
  operation();
  const duration = Date.now() - startTime;
  
  overallDuration.add(duration);
  sleep(0.3); // Think time court pour reads
}

function performWriteOperation(data) {
  const operations = [
    // Signup (30%)
    () => {
      const email = `user_${__VU}_${Date.now()}@k6test.com`;
      const payload = JSON.stringify({
        email: email,
        fullName: `K6 User ${__VU}`,
        password: 'SecurePass123!',
        confirmPassword: 'SecurePass123!',
      });

      const response = http.post(`${BASE_URL}/api/v1/signup`, payload, {
        headers: { 'Content-Type': 'application/json' },
        tags: { operation: 'write', type: 'signup' },
      });

      const success = check(response, {
        'signup status 200': (r) => r.status === 200,
      });

      writeSuccessRate.add(success);
      writeCounter.add(1);
      return response;
    },

    // Login (40%)
    () => {
      if (data.users.length === 0) {
        return null;
      }

      const user = data.users[Math.floor(Math.random() * data.users.length)];
      const payload = JSON.stringify({
        email: user.email,
        password: user.password,
        ip: '192.168.1.100',
      });

      const response = http.post(`${BASE_URL}/api/v1/auth/login`, payload, {
        headers: { 'Content-Type': 'application/json' },
        tags: { operation: 'write', type: 'login' },
      });

      const success = check(response, {
        'login status 200 or 202': (r) => r.status === 200 || r.status === 202,
      });

      writeSuccessRate.add(success);
      writeCounter.add(1);
      return response;
    },

    // Deposit (30%)
    () => {
      // Utiliser un utilisateur authentifié avec JWT
      if (data.users.length === 0) {
        writeSuccessRate.add(false);
        writeCounter.add(1);
        return null;
      }

      const user = data.users[Math.floor(Math.random() * data.users.length)];
      
      // Si pas de token, on skip
      if (!user.token) {
        writeSuccessRate.add(false);
        writeCounter.add(1);
        return null;
      }

      const payload = JSON.stringify({
        amount: Math.floor(Math.random() * 500) + 50,
        currency: 'CAD',
        paymentMethod: 'INTERAC',
      });

      const response = http.post(`${BASE_URL}/api/v1/wallet/deposit`, payload, {
        headers: { 
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${user.token}`,
          'Idempotency-Key': uuidv4(),
        },
        tags: { operation: 'write', type: 'deposit' },
      });

      // On accepte 200 (succès) ou 409 (idempotency duplicate)
      const success = check(response, {
        'deposit status ok': (r) => r.status === 200 || r.status === 409,
      });

      writeSuccessRate.add(success);
      writeCounter.add(1);
      return response;
    },
  ];

  // Sélectionner une opération avec pondération
  const rand = Math.random();
  let operation;
  
  if (rand < 0.3) {
    operation = operations[0]; // Signup 30%
  } else if (rand < 0.7) {
    operation = operations[1]; // Login 40%
  } else {
    operation = operations[2]; // Deposit 30%
  }

  const startTime = Date.now();
  operation();
  const duration = Date.now() - startTime;
  
  overallDuration.add(duration);
  sleep(1.0); // Think time plus long pour writes
}

export function handleSummary(data) {
  const totalRequests = data.metrics.http_reqs.values.count;
  const readOps = data.metrics.read_operations ? data.metrics.read_operations.values.count : 0;
  const writeOps = data.metrics.write_operations ? data.metrics.write_operations.values.count : 0;

  const readPercent = ((readOps / totalRequests) * 100).toFixed(1);
  const writePercent = ((writeOps / totalRequests) * 100).toFixed(1);

  let summary = '\n';
  summary += '╔════════════════════════════════════════════════╗\n';
  summary += '║  Test: Scénario Mixte Court (Scaling)         ║\n';
  summary += '╚════════════════════════════════════════════════╝\n\n';
  
  summary += '📊 TRAFIC\n';
  summary += `  • Total Requests:  ${totalRequests}\n`;
  summary += `  • Read Operations: ${readOps} (${readPercent}%)\n`;
  summary += `  • Write Operations: ${writeOps} (${writePercent}%)\n`;
  summary += `  • RPS:             ${data.metrics.http_reqs.values.rate.toFixed(2)}\n`;
  summary += '\n';
  
  summary += '⚡ PERFORMANCE\n';
  summary += `  • Duration P50:    ${data.metrics.http_req_duration.values.med.toFixed(2)}ms\n`;
  summary += `  • Duration P95:    ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `  • Duration P99:    ${(data.metrics.http_req_duration.values['p(99)'] || 0).toFixed(2)}ms\n`;
  summary += '\n';
  
  summary += '✅ SUCCÈS\n';
  summary += `  • Read Success:    ${(data.metrics.read_success_rate.values.rate * 100).toFixed(2)}%\n`;
  summary += `  • Write Success:   ${(data.metrics.write_success_rate.values.rate * 100).toFixed(2)}%\n`;
  summary += `  • Overall Failed:  ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%\n`;
  summary += '\n';

  // Vérifier les thresholds
  const p95Ok = data.metrics.http_req_duration.values['p(95)'] < 500;
  const errorRateOk = data.metrics.http_req_failed.values.rate < 0.05;
  
  summary += '🎯 THRESHOLDS\n';
  summary += `  ${p95Ok ? '✅' : '❌'} P95 < 500ms: ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `  ${errorRateOk ? '✅' : '❌'} Error Rate < 5%: ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%\n`;
  summary += '\n';

  return {
    'stdout': summary,
  };
}
