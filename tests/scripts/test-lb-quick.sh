#!/bin/bash

# Script de test rapide du load balancer NGINX
# Usage: ./test-lb-quick.sh

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Test NGINX Load Balancer - Quick        ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""

# 1. Démarrer avec load balancing
echo -e "${YELLOW}[1/4]${NC} Démarrage des services (4 instances API + NGINX)..."
docker compose -f docker-compose.yml -f docker-compose.lb.yml up -d --build

# Attendre que tout soit prêt
echo -e "${YELLOW}⏳ Attente 15s (démarrage MySQL + 4 API + NGINX)...${NC}"
sleep 15

# 2. Vérifier que NGINX répond
echo ""
echo -e "${YELLOW}[2/4]${NC} Test NGINX health..."
if curl -s http://localhost:8090/nginx-health > /dev/null 2>&1; then
    echo -e "${GREEN}✓ NGINX opérationnel${NC}"
else
    echo -e "${RED}✗ NGINX non disponible${NC}"
    exit 1
fi

# 3. Tester la distribution de charge
echo ""
echo -e "${YELLOW}[3/4]${NC} Test distribution sur 20 requêtes..."
echo -e "${BLUE}Instance ciblée par NGINX:${NC}"
echo ""

for i in {1..20}; do
    RESPONSE=$(curl -s http://localhost:8090/health 2>/dev/null || echo "Error")
    echo "  Requête $i: $RESPONSE"
    sleep 0.2
done

echo ""
echo -e "${GREEN}✓ Si vous voyez 'Healthy' et alternance entre instances = OK${NC}"

# 4. Test de charge rapide avec k6 (optionnel)
echo ""
echo -e "${YELLOW}[4/4]${NC} Test k6 rapide (30s)..."
read -p "Lancer test k6 30s? (y/n) " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}=== Test k6 rapide (30s, 20 VUs) via NGINX ===${NC}"
    BASE_URL=http://localhost:8090 k6 run --duration 30s --vus 20 scripts/k6/signup.js
    echo ""
    echo -e "${GREEN}✓ Test k6 terminé${NC}"
fi

echo ""
echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║              Résumé                        ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}Services actifs:${NC}"
docker compose -f docker-compose.yml -f docker-compose.lb.yml ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}"
echo ""
echo -e "${YELLOW}URLs disponibles:${NC}"
echo "  - NGINX LB:        http://localhost:8090/"
echo "  - API 1 (direct):  http://localhost:5001/health"
echo "  - API 2 (direct):  http://localhost:5002/health"
echo "  - API 3 (direct):  http://localhost:5003/health"
echo "  - API 4 (direct):  http://localhost:5004/health"
echo "  - Prometheus:      http://localhost:9090"
echo "  - Grafana:         http://localhost:3000"
echo ""
echo -e "${YELLOW}Commandes utiles:${NC}"
echo "  # Test failover (stopper instance 2)"
echo "  docker stop brokerx-api-2"
echo "  for i in {1..10}; do curl -s http://localhost:8090/health; echo; done"
echo ""
echo "  # Redémarrer instance 2"
echo "  docker start brokerx-api-2"
echo ""
echo "  # Arrêter tout"
echo "  docker compose -f docker-compose.yml -f docker-compose.lb.yml down"
echo ""
echo -e "${GREEN}✅ Load balancer opérationnel!${NC}"
