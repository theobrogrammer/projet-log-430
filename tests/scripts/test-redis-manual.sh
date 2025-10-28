#!/bin/bash
# Test manuel simple Redis
set -e

EMAIL="manual-test-$RANDOM@example.com"

echo "1. Signup..."
SIGNUP=$(curl -s -X POST http://localhost:5000/api/v1/signup \
    -H "Content-Type: application/json" \
    -d "{
        \"email\": \"$EMAIL\",
        \"fullName\": \"Manual Test\",
        \"password\": \"SecureP@ss123\",
        \"confirmPassword\": \"SecureP@ss123\",
        \"phone\": \"+15145551111\"
    }")
echo "$SIGNUP" | jq '.'

CLIENT_ID=$(echo "$SIGNUP" | jq -r '.clientId')

if [ "$CLIENT_ID" = "null" ]; then
    echo "ERROR: Signup failed"
    exit 1
fi

# Récupérer le code OTP depuis les logs (voir RUNBOOK.md)
echo ""
echo "2. Récupération code OTP depuis logs..."
sleep 0.5
OTP=$(docker logs brokerx-api 2>&1 | grep "\[OTP\]" | tail -1 | grep -oP 'code=\K\d+')
echo "OTP code: $OTP"

echo ""
echo "3. Verify OTP..."
curl -s -X POST http://localhost:5000/api/v1/signup/verify-otp \
    -H "Content-Type: application/json" \
    -d "{\"clientId\": \"$CLIENT_ID\", \"code\": \"$OTP\"}" | jq '.'

echo ""
echo "4. Login (should trigger MFA)..."
LOGIN=$(curl -s -X POST http://localhost:5000/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d "{\"email\": \"$EMAIL\", \"password\": \"SecureP@ss123\"}")
echo "$LOGIN" | jq '.'

CHALLENGE_ID=$(echo "$LOGIN" | jq -r '.challengeId')
MFA_REQUIRED=$(echo "$LOGIN" | jq -r '.mfaRequired')

if [ "$MFA_REQUIRED" = "true" ]; then
    echo ""
    echo "5. Récupération code MFA depuis logs..."
    sleep 0.5
    MFA_CODE=$(docker logs brokerx-api 2>&1 | grep "\[OTP\]" | tail -1 | grep -oP 'code=\K\d+')
    echo "MFA code: $MFA_CODE"
    
    echo ""
    echo "6. Verify MFA (should use CACHE)..."
    curl -s -X POST http://localhost:5000/api/v1/auth/mfa/verify \
        -H "Content-Type: application/json" \
        -d "{\"clientId\": \"$CLIENT_ID\", \"challengeId\": \"$CHALLENGE_ID\", \"code\": \"$MFA_CODE\"}" | jq '.'
fi

echo ""
echo "7. Check Redis keys..."
docker exec brokerx-redis redis-cli keys "*"

echo ""
echo "8. Check CACHE logs..."
docker logs brokerx-api 2>&1 | grep "CACHE_" | tail -10
