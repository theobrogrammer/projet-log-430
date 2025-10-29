#!/bin/bash
# ============================================================================
# Script de test du KrakenD API Gateway (Phase 2b)
# ============================================================================
# Usage: ./tests/scripts/test-krakend-gateway.sh
# 
# Ce script teste:
# 1. Health check du gateway
# 2. Métriques Prometheus
# 3. Routing des endpoints API via le gateway (avec k6)
# 4. Comparaison latence (direct vs gateway avec k6)
# 
# Note: Utilise les scripts k6 existants dans scripts/k6/
# ============================================================================

# Note: Pas de set -e pour permettre aux tests de continuer même en cas d'échec

# Couleurs pour l'output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
GATEWAY_URL="http://localhost:8080"
DIRECT_API_URL="http://localhost:5000"
NGINX_LB_URL="http://localhost:8090"
METRICS_URL="http://localhost:9091/metrics"
K6_SCRIPTS_DIR="scripts/k6"

# Compteurs
TESTS_PASSED=0
TESTS_FAILED=0

# ============================================================================
# Fonctions utilitaires
# ============================================================================

print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

print_test() {
    echo -e "${YELLOW}[TEST]${NC} $1"
}

print_success() {
    echo -e "${GREEN}✅ PASS:${NC} $1"
    ((TESTS_PASSED++))
}

print_error() {
    echo -e "${RED}❌ FAIL:${NC} $1"
    ((TESTS_FAILED++))
}

print_info() {
    echo -e "${BLUE}ℹ️  INFO:${NC} $1"
}

# ============================================================================
# Tests
# ============================================================================

test_krakend_health() {
    print_test "1. KrakenD Health Check"
    
    local response=$(curl -s -w "\n%{http_code}" "$GATEWAY_URL/__health")
    local body=$(echo "$response" | head -n -1)
    local status=$(echo "$response" | tail -n 1)
    
    if [ "$status" = "200" ]; then
        print_success "KrakenD est healthy (HTTP $status)"
        print_info "Response: $body"
    else
        print_error "KrakenD health check échoué (HTTP $status)"
        return 1
    fi
}

test_prometheus_metrics() {
    print_test "2. Métriques Prometheus"
    
    local response=$(curl -s -w "\n%{http_code}" "$METRICS_URL")
    local body=$(echo "$response" | head -n -1)
    local status=$(echo "$response" | tail -n 1)
    
    if [ "$status" = "200" ] && echo "$body" | grep -q "go_gc_duration_seconds"; then
        print_success "Métriques Prometheus disponibles (HTTP $status)"
        local metrics_count=$(echo "$body" | grep "^# HELP" | wc -l)
        print_info "Nombre de métriques exposées: $metrics_count"
    else
        print_error "Métriques Prometheus non disponibles (HTTP $status)"
        return 1
    fi
}

test_gateway_routing() {
    print_test "3. Routing API via Gateway (curl)"
    
    # Test simple: un signup via le gateway
    local email="gateway-test-$(date +%s)@example.com"
    local payload=$(cat <<EOF
{
  "email": "$email",
  "fullName": "Gateway Test",
  "password": "SecureP@ss123",
  "confirmPassword": "SecureP@ss123"
}
EOF
)
    
    print_info "POST $GATEWAY_URL/api/v1/signup"
    
    local response=$(curl -s -w "\n%{http_code}" \
        -X POST "$GATEWAY_URL/api/v1/signup" \
        -H "Content-Type: application/json" \
        -d "$payload")
    
    local body=$(echo "$response" | head -n -1)
    local status=$(echo "$response" | tail -n 1)
    
    if [ "$status" = "200" ] || [ "$status" = "201" ]; then
        print_success "Routing signup via gateway fonctionne (HTTP $status)"
    elif [ "$status" = "400" ]; then
        if echo "$body" | grep -q "already\|existe"; then
            print_success "Gateway route correctement (HTTP $status)"
        else
            print_error "Erreur validation: $body"
            return 1
        fi
    else
        print_error "Routing échoué (HTTP $status): $body"
        return 1
    fi
}

test_performance_comparison() {
    print_test "4. Comparaison Performance avec k6 (OPTIONNEL)"
    
    # Vérifier si k6 est installé
    if ! command -v k6 &> /dev/null; then
        print_info "k6 non installé - test de performance ignoré"
        print_info "Pour installer: sudo snap install k6"
        return 0
    fi
    
    print_info "Test comparatif avec k6: 10s, 5 VUs"
    
    print_info "  1/3: Direct API (port 5000)..."
    local direct_output=$(BASE_URL="$DIRECT_API_URL" k6 run --duration 10s --vus 5 --quiet "$K6_SCRIPTS_DIR/signup.js" 2>&1)
    local direct_p95=$(echo "$direct_output" | grep "Duration P95:" | awk '{print $4}')
    
    print_info "  2/3: Via NGINX LB (port 8090)..."
    local nginx_output=$(BASE_URL="$NGINX_LB_URL" k6 run --duration 10s --vus 5 --quiet "$K6_SCRIPTS_DIR/signup.js" 2>&1)
    local nginx_p95=$(echo "$nginx_output" | grep "Duration P95:" | awk '{print $4}')
    
    print_info "  3/3: Via Gateway (port 8080)..."
    local gateway_output=$(BASE_URL="$GATEWAY_URL" k6 run --duration 10s --vus 5 --quiet "$K6_SCRIPTS_DIR/signup.js" 2>&1)
    local gateway_p95=$(echo "$gateway_output" | grep "Duration P95:" | awk '{print $4}')
    
    print_success "Comparaison performance complétée"
    print_info "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    print_info "Direct API:   P95=$direct_p95"
    print_info "NGINX LB:     P95=$nginx_p95"
    print_info "Via Gateway:  P95=$gateway_p95"
    print_info "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

test_gateway_endpoints() {
    print_test "5. Test des endpoints via Gateway"
    
    # Test simple avec curl pour vérifier que le routing fonctionne
    print_info "Test endpoint: POST $GATEWAY_URL/api/v1/signup"
    
    local email="quick-test-$(date +%s)@example.com"
    local payload=$(cat <<EOF
{
  "email": "$email",
  "fullName": "Quick Test",
  "password": "SecureP@ss123",
  "confirmPassword": "SecureP@ss123"
}
EOF
)
    
    local response=$(curl -s -w "\n%{http_code}" \
        -X POST "$GATEWAY_URL/api/v1/signup" \
        -H "Content-Type: application/json" \
        -d "$payload")
    
    local body=$(echo "$response" | head -n -1)
    local status=$(echo "$response" | tail -n 1)
    
    if [ "$status" = "200" ] || [ "$status" = "201" ]; then
        print_success "Endpoint signup accessible via gateway (HTTP $status)"
    elif [ "$status" = "400" ]; then
        if echo "$body" | grep -q "already\|existe"; then
            print_success "Gateway route correctement (HTTP $status)"
        else
            print_error "Erreur de validation: $body"
            return 1
        fi
    else
        print_error "Endpoint signup échoué (HTTP $status)"
        print_info "Body: $body"
        return 1
    fi
}

check_docker_services() {
    print_test "0. Vérification des services Docker"
    
    local services=("brokerx-api" "brokerx-krakend" "brokerx-mysql" "brokerx-redis")
    local all_healthy=true
    
    for service in "${services[@]}"; do
        if docker ps --filter "name=$service" --filter "health=healthy" | grep -q "$service"; then
            print_info "$service: healthy ✓"
        elif docker ps --filter "name=$service" | grep -q "$service"; then
            print_info "$service: running (no healthcheck or starting)"
        else
            print_error "$service: non démarré"
            all_healthy=false
        fi
    done
    
    if [ "$all_healthy" = true ]; then
        print_success "Tous les services requis sont opérationnels"
    else
        print_error "Certains services ne sont pas démarrés"
        echo -e "\n${YELLOW}Démarrer les services avec:${NC}"
        echo "  docker compose up -d"
        exit 1
    fi
}

# ============================================================================
# Main
# ============================================================================

main() {
    print_header "🚀 Test KrakenD API Gateway - Phase 2b"
    
    echo "Gateway URL:  $GATEWAY_URL"
    echo "NGINX LB URL: $NGINX_LB_URL"
    echo "Direct API:   $DIRECT_API_URL"
    echo "Metrics URL:  $METRICS_URL"
    
    # Vérification préalable
    check_docker_services
    
    # Tests fonctionnels rapides (toujours exécutés)
    sleep 2
    test_krakend_health || true
    sleep 1
    test_prometheus_metrics || true
    sleep 1
    test_gateway_routing || true
    sleep 1
    test_gateway_endpoints || true
    
    # Test de performance avec k6 (optionnel)
    sleep 1
    test_performance_comparison || true
    
    # Résumé
    print_header "📊 Résumé des tests"
    
    local total=$((TESTS_PASSED + TESTS_FAILED))
    echo -e "${GREEN}Tests réussis: $TESTS_PASSED${NC}"
    echo -e "${RED}Tests échoués: $TESTS_FAILED${NC}"
    echo -e "Total: $total"
    
    if [ "$TESTS_FAILED" -eq 0 ]; then
        echo -e "\n${GREEN}✅ Tous les tests fonctionnels sont passés!${NC}"
        echo -e "\n${BLUE}📝 Prochaines étapes:${NC}"
        echo "  - Métriques: http://localhost:9091/metrics"
        echo "  - Grafana: http://localhost:3000"
        echo "  - Tests charge k6: ./tests/scripts/run-k6-test.sh gateway"
        exit 0
    else
        echo -e "\n${RED}❌ Certains tests ont échoué${NC}"
        echo -e "\n${YELLOW}💡 Troubleshooting:${NC}"
        echo "  - Logs: docker logs brokerx-krakend"
        echo "  - Config: cat krakend.json"
        exit 1
    fi
}

# Exécution
main "$@"
