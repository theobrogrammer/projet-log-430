#!/bin/bash
# Test Redis Cache avec Métriques Prometheus
# Phase 2 Étape 2a - Validation cache + observabilité

set -e

BASE_URL="${BASE_URL:-http://localhost:5000}"

# Couleurs
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "=========================================="
echo "  Test Redis Cache + Métriques"
echo "=========================================="
echo ""

# 1. Vérifier que Redis et API sont up
echo -e "${YELLOW}[1/6] Vérification services...${NC}"
if ! docker ps | grep -q brokerx-redis; then
    echo -e "${RED}✗ Redis non démarré${NC}"
    exit 1
fi
if ! curl -sf $BASE_URL/health > /dev/null 2>&1; then
    echo -e "${RED}✗ API non disponible${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Services opérationnels${NC}"
echo ""

# 2. Nettoyer cache Redis
echo -e "${YELLOW}[2/6] Nettoyage cache Redis...${NC}"
docker exec brokerx-redis redis-cli flushdb > /dev/null
echo -e "${GREEN}✓ Cache vidé${NC}"
echo ""

# 3. Générer du trafic pour peupler le cache
echo -e "${YELLOW}[3/6] Génération trafic (10 signups + logins + MFA)...${NC}"

for i in {1..10}; do
    EMAIL="cache-test-$RANDOM@example.com"
    
    # Signup
    SIGNUP=$(curl -s -X POST "$BASE_URL/api/v1/signup" \
        -H "Content-Type: application/json" \
        -d "{
            \"email\": \"$EMAIL\",
            \"fullName\": \"Test Cache$i\",
            \"password\": \"SecureP@ss123\",
            \"confirmPassword\": \"SecureP@ss123\",
            \"phone\": \"+1514555$i\",
            \"birthDate\": \"1990-01-01\"
        }")
    
    CLIENT_ID=$(echo "$SIGNUP" | jq -r '.clientId')
    
    if [ ! -z "$CLIENT_ID" ] && [ "$CLIENT_ID" != "null" ]; then
        # Récupérer code OTP depuis logs
        sleep 0.3
        OTP=$(docker logs brokerx-api 2>&1 | grep "\[OTP\]" | tail -1 | grep -oP 'code=\K\d+')
        
        # Verify OTP
        curl -s -X POST "$BASE_URL/api/v1/signup/verify-otp" \
            -H "Content-Type: application/json" \
            -d "{\"clientId\": \"$CLIENT_ID\", \"code\": \"$OTP\"}" > /dev/null
        
        # Login (crée cache MFA challenge)
        LOGIN=$(curl -s -X POST "$BASE_URL/api/v1/auth/login" \
            -H "Content-Type: application/json" \
            -d "{\"email\": \"$EMAIL\", \"password\": \"SecureP@ss123\"}")
        
        CHALLENGE_ID=$(echo "$LOGIN" | jq -r '.challengeId')
        MFA_REQUIRED=$(echo "$LOGIN" | jq -r '.mfaRequired')
        
        # Si MFA requis, vérifier (teste le cache!)
        if [ "$MFA_REQUIRED" = "true" ] && [ ! -z "$CHALLENGE_ID" ] && [ "$CHALLENGE_ID" != "null" ]; then
            # Récupérer code MFA depuis logs
            sleep 0.3
            MFA_CODE=$(docker logs brokerx-api 2>&1 | grep "\[OTP\]" | tail -1 | grep -oP 'code=\K\d+')
            
            curl -s -X POST "$BASE_URL/api/v1/auth/mfa/verify" \
                -H "Content-Type: application/json" \
                -d "{\"clientId\": \"$CLIENT_ID\", \"challengeId\": \"$CHALLENGE_ID\", \"code\": \"$MFA_CODE\"}" > /dev/null
        fi
        
        echo -n "."
    fi
done

echo ""
echo -e "${GREEN}✓ Trafic généré${NC}"
echo ""

# 4. Vérifier clés en cache
echo -e "${YELLOW}[4/6] Vérification clés Redis...${NC}"
KEYS_COUNT=$(docker exec brokerx-redis redis-cli dbsize | tr -d '\r')
echo "  • Total clés: $KEYS_COUNT"

if [ "$KEYS_COUNT" -gt 0 ]; then
    echo -e "${BLUE}  • Exemples de clés:${NC}"
    docker exec brokerx-redis redis-cli keys "*" | head -5 | sed 's/^/    - /'
    echo -e "${GREEN}✓ Cache populated${NC}"
else
    echo -e "${YELLOW}⚠ Aucune clé en cache${NC}"
fi
echo ""

# 5. Vérifier métriques Prometheus
echo -e "${YELLOW}[5/6] Vérification métriques Prometheus...${NC}"

METRICS=$(curl -s $BASE_URL/metrics)

# Cache operations
CACHE_HITS=$(echo "$METRICS" | grep '^cache_operations_total{operation="hit"' | awk '{print $2}' | head -1)
CACHE_MISSES=$(echo "$METRICS" | grep '^cache_operations_total{operation="miss"' | awk '{print $2}' | head -1)
CACHE_SETS=$(echo "$METRICS" | grep '^cache_operations_total{operation="set"' | awk '{print $2}' | head -1)

# Default to 0 if empty
CACHE_HITS=${CACHE_HITS:-0}
CACHE_MISSES=${CACHE_MISSES:-0}
CACHE_SETS=${CACHE_SETS:-0}

echo "  • Cache hits: $CACHE_HITS"
echo "  • Cache misses: $CACHE_MISSES"
echo "  • Cache sets: $CACHE_SETS"

if [ "$CACHE_HITS" -gt 0 ] || [ "$CACHE_MISSES" -gt 0 ] || [ "$CACHE_SETS" -gt 0 ]; then
    # Calculer hit rate
    TOTAL_OPS=$((CACHE_HITS + CACHE_MISSES))
    if [ "$TOTAL_OPS" -gt 0 ]; then
        HIT_RATE=$(echo "scale=2; $CACHE_HITS * 100 / $TOTAL_OPS" | bc)
        echo "  • Hit rate: ${HIT_RATE}%"
    fi
    echo -e "${GREEN}✓ Métriques cache exposées${NC}"
else
    echo -e "${YELLOW}⚠ Aucune métrique cache trouvée${NC}"
fi

# Cache latency
CACHE_LATENCY=$(echo "$METRICS" | grep '^cache_operation_duration_seconds_sum' | head -1)
if [ ! -z "$CACHE_LATENCY" ]; then
    echo -e "${GREEN}✓ Métriques latence exposées${NC}"
else
    echo -e "${YELLOW}⚠ Métriques latency non trouvées${NC}"
fi
echo ""

# 6. Logs cache
echo -e "${YELLOW}[6/6] Vérification logs cache...${NC}"
echo -e "${BLUE}Derniers logs CACHE_*:${NC}"
docker logs brokerx-api 2>&1 | grep -E "CACHE_(HIT|MISS|SET|REMOVE)" | tail -10 | \
    jq -r 'select(.RenderedMessage) | .["@t"][11:19] + " | " + .RenderedMessage' 2>/dev/null || \
    docker logs brokerx-api 2>&1 | grep -E "CACHE_(HIT|MISS|SET)" | tail -5

echo ""

# Résumé
echo -e "${GREEN}=========================================="
echo "  ✓ Test Redis Cache Complet"
echo "==========================================${NC}"
echo ""
echo "📊 Résumé:"
echo "  • Clés en cache: $KEYS_COUNT"
echo "  • Cache hits: $CACHE_HITS"
echo "  • Cache misses: $CACHE_MISSES"
echo "  • Cache sets: $CACHE_SETS"

if [ "$TOTAL_OPS" -gt 0 ]; then
    echo "  • Hit rate: ${HIT_RATE}%"
fi

echo ""
echo "🔗 URLs utiles:"
echo "  • Métriques API: $BASE_URL/metrics"
echo "  • Prometheus: http://localhost:9090/graph"
echo "  • Query: cache_operations_total"
echo ""
