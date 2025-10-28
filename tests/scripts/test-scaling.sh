#!/bin/bash

# Script de tests de charge comparatifs avec N instances
# Usage: ./test-scaling.sh [baseline|comparison]

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

MODE=${1:-comparison}  # baseline or comparison

echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Tests k6 - Scaling N=1,2,3,4 instances  ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""

# Créer dossiers de résultats
mkdir -p resultats-k6/scaling/{n1,n2,n3,n4}

# Fonction pour attendre que l'API soit prête
wait_for_api() {
    local max_attempts=30
    local attempt=1
    
    echo -e "${YELLOW}⏳ Attente API (max ${max_attempts}s)...${NC}"
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -f http://localhost:8090/health > /dev/null 2>&1; then
            echo -e "${GREEN}✓ API prête après ${attempt}s${NC}"
            return 0
        fi
        echo -n "."
        sleep 1
        ((attempt++))
    done
    
    echo -e "${RED}✗ API non disponible après ${max_attempts}s${NC}"
    return 1
}

# Fonction pour exécuter test k6
run_k6_test() {
    local n_instances=$1
    local test_name="n${n_instances}"
    local output_dir="resultats-k6/scaling/${test_name}"
    
    echo ""
    echo -e "${BLUE}═══════════════════════════════════════════${NC}"
    echo -e "${BLUE}  Test avec N=${n_instances} instance(s)${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════${NC}"
    echo ""
    
    # Nettoyer logs
    echo -e "${YELLOW}🧹 Nettoyage des logs...${NC}"
    truncate -s 0 Infrastructure.Web/logs/*.jsonl 2>/dev/null || true
    
    # Démarrer monitoring logs en arrière-plan
    if [ -f "./tests/scripts/logs-readable.sh" ]; then
        ./tests/scripts/logs-readable.sh > "${output_dir}/api-logs.log" 2>&1 &
        LOGS_PID=$!
        echo -e "${GREEN}✓ Logs monitoring démarré (PID: ${LOGS_PID})${NC}"
    fi
    
    # Attendre stabilisation
    sleep 5
    
    # Exécuter k6
    echo -e "${GREEN}🚀 Exécution k6 (1 minute, 20 VUs)...${NC}"
    START_TIME=$(date +%s)
    
    BASE_URL=http://localhost:8090 k6 run \
        --out "json=${output_dir}/results.json" \
        --summary-export="${output_dir}/summary.json" \
        scripts/k6/mixed-short.js \
        > "${output_dir}/k6-console.txt" 2>&1 || true
    
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    # Arrêter logs monitoring
    if [ ! -z "$LOGS_PID" ]; then
        kill $LOGS_PID 2>/dev/null || true
        echo -e "${GREEN}✓ Logs monitoring arrêté${NC}"
    fi
    
    echo -e "${GREEN}✓ Test terminé (${DURATION}s)${NC}"
    
    # Analyser résultats
    if [ -f "${output_dir}/summary.json" ]; then
        echo ""
        echo -e "${BLUE}📊 Résultats N=${n_instances}:${NC}"
        
        RPS=$(jq -r '.metrics.http_reqs.rate // 0' "${output_dir}/summary.json" | xargs printf "%.2f")
        P95=$(jq -r '.metrics.http_req_duration.values["p(95)"] // 0' "${output_dir}/summary.json" | xargs printf "%.2f")
        ERRORS=$(jq -r '.metrics.http_req_failed.values.rate // 0' "${output_dir}/summary.json" | awk '{printf "%.2f", $1*100}')
        
        echo "  RPS:         ${RPS} req/s"
        echo "  Latency P95: ${P95} ms"
        echo "  Error Rate:  ${ERRORS}%"
    fi
    
    # Sauvegarder métadonnées
    cat > "${output_dir}/metadata.json" << EOF
{
  "instances": ${n_instances},
  "test_date": "$(date -Iseconds)",
  "duration_seconds": ${DURATION},
  "script": "scripts/k6/mixed.js"
}
EOF
    
    echo -e "${GREEN}✓ Résultats sauvegardés: ${output_dir}/${NC}"
}

# Fonction pour reconfigurer docker-compose avec N instances
configure_instances() {
    local n=$1
    
    echo -e "${YELLOW}⚙️  Configuration pour ${n} instance(s)...${NC}"
    
    # Arrêter tous les services
    echo -e "${YELLOW}  Arrêt des services...${NC}"
    docker compose down 2>/dev/null || true
    
    # Créer docker-compose override pour N instances
    cat > docker-compose.override.yml << EOF
version: '3.8'

services:
  # Créer N instances API
EOF
    
    for i in $(seq 1 $n); do
        cat >> docker-compose.override.yml << EOF
  brokerx-api-${i}:
    image: \${DOCKER_REGISTRY-}brokerx-api
    container_name: brokerx-api-${i}
    build:
      context: .
      dockerfile: Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=mysql;Port=3306;Database=brokerx;User=root;Password=root123;
      - INSTANCE_NAME=api-${i}
    depends_on:
      mysql:
        condition: service_healthy
    networks:
      - brokerx-network
    volumes:
      - ./Infrastructure.Web/logs:/app/logs

EOF
    done
    
    # Ajouter NGINX
    cat >> docker-compose.override.yml << EOF
  nginx:
    image: nginx:alpine
    container_name: brokerx-nginx
    ports:
      - "8090:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
EOF
    
    for i in $(seq 1 $n); do
        echo "      - brokerx-api-${i}" >> docker-compose.override.yml
    done
    
    cat >> docker-compose.override.yml << EOF
    networks:
      - brokerx-network

networks:
  brokerx-network:
    driver: bridge
EOF
    
    echo -e "${GREEN}✓ Configuration docker-compose.override.yml créée${NC}"
    
    # Générer nginx.conf dynamique avec N upstreams
    echo -e "${YELLOW}  Génération nginx.conf pour ${n} instance(s)...${NC}"
    cat > nginx.conf << 'NGINX_EOF'
events {
    worker_connections 1024;
}

http {
    upstream backend {
        least_conn;
NGINX_EOF
    
    # Ajouter les N upstreams
    for i in $(seq 1 $n); do
        echo "        server brokerx-api-${i}:8080 max_fails=3 fail_timeout=30s;" >> nginx.conf
    done
    
    cat >> nginx.conf << 'NGINX_EOF'
        keepalive 32;
    }

    server {
        listen 80;
        
        location /nginx-health {
            access_log off;
            return 200 "healthy\n";
            add_header Content-Type text/plain;
        }
        
        location / {
            proxy_pass http://backend;
            proxy_http_version 1.1;
            
            proxy_set_header Connection "";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            
            proxy_connect_timeout 5s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;
        }
    }
}
NGINX_EOF
    
    echo -e "${GREEN}✓ nginx.conf créé avec ${n} upstream(s)${NC}"
    
    # Démarrer services
    echo -e "${YELLOW}  Démarrage des services (MySQL, ${n} API, Prometheus, Grafana, NGINX)...${NC}"
    docker compose up -d --build
    
    # Attendre que l'API soit prête
    wait_for_api
}

# Test de tolérance aux pannes
test_fault_tolerance() {
    local n_instances=4
    
    echo ""
    echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║      Test de Tolérance aux Pannes         ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
    echo ""
    
    # S'assurer que 4 instances sont démarrées
    configure_instances $n_instances
    
    mkdir -p resultats-k6/fault-tolerance
    
    # Démarrer test k6 en arrière-plan
    echo -e "${GREEN}🚀 Démarrage test k6 (2 minutes, 20 VUs)...${NC}"
    BASE_URL=http://localhost:8090 k6 run \
        --vus 20 \
        --duration 2m \
        --out "json=resultats-k6/fault-tolerance/results.json" \
        --summary-export="resultats-k6/fault-tolerance/summary.json" \
        scripts/k6/mixed-short.js \
        > resultats-k6/fault-tolerance/k6-console.txt 2>&1 &
    K6_PID=$!
    
    echo -e "${GREEN}✓ k6 démarré (PID: ${K6_PID})${NC}"
    
    # Attendre 30 secondes
    echo -e "${YELLOW}⏳ Attente 30s (système en charge normale)...${NC}"
    sleep 30
    
    # Stopper 1 instance
    echo -e "${RED}🔴 Arrêt de l'instance 2 (simulation panne)...${NC}"
    docker stop brokerx-api-2
    echo "$(date -Iseconds): Stopped brokerx-api-2" >> resultats-k6/fault-tolerance/events.log
    
    # Attendre 30 secondes
    echo -e "${YELLOW}⏳ Attente 30s (système avec 3 instances)...${NC}"
    sleep 30
    
    # Redémarrer l'instance
    echo -e "${GREEN}🟢 Redémarrage de l'instance 2...${NC}"
    docker start brokerx-api-2
    echo "$(date -Iseconds): Started brokerx-api-2" >> resultats-k6/fault-tolerance/events.log
    
    # Attendre fin du test k6
    echo -e "${YELLOW}⏳ Attente fin du test k6...${NC}"
    wait $K6_PID
    
    echo -e "${GREEN}✓ Test de tolérance aux pannes terminé${NC}"
    echo -e "${BLUE}📊 Résultats: resultats-k6/fault-tolerance/${NC}"
}

# Menu principal
if [ "$MODE" == "baseline" ]; then
    # Test baseline avec 1 instance (déjà fait normalement)
    echo -e "${YELLOW}Mode: Baseline (N=1)${NC}"
    configure_instances 1
    run_k6_test 1
    
elif [ "$MODE" == "comparison" ]; then
    # Tests comparatifs N=1,2,3,4
    echo -e "${YELLOW}Mode: Comparaison (N=1,2,3,4)${NC}"
    
    for n in 1 2 3 4; do
        configure_instances $n
        run_k6_test $n
        
        # Pause entre tests
        if [ $n -lt 4 ]; then
            echo ""
            echo -e "${YELLOW}⏸️  Pause 10s avant test suivant...${NC}"
            sleep 10
        fi
    done
    
    # Générer rapport comparatif
    echo ""
    echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║         Rapport Comparatif                 ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
    echo ""
    
    printf "%-10s %-15s %-15s %-15s\n" "Instances" "RPS" "P95 (ms)" "Errors (%)"
    echo "--------------------------------------------------------"
    
    for n in 1 2 3 4; do
        SUMMARY="resultats-k6/scaling/n${n}/summary.json"
        if [ -f "$SUMMARY" ]; then
            RPS=$(jq -r '.metrics.http_reqs.rate // 0' "$SUMMARY" | xargs printf "%.2f")
            P95=$(jq -r '.metrics.http_req_duration.values["p(95)"] // 0' "$SUMMARY" | xargs printf "%.2f")
            ERRORS=$(jq -r '.metrics.http_req_failed.values.rate // 0' "$SUMMARY" | awk '{printf "%.2f", $1*100}')
            
            printf "%-10s %-15s %-15s %-15s\n" "N=$n" "$RPS" "$P95" "$ERRORS"
        fi
    done
    
    echo ""
    echo -e "${GREEN}✓ Tests comparatifs terminés${NC}"
    echo -e "${BLUE}📊 Résultats: resultats-k6/scaling/${NC}"
    
    # Proposer test de tolérance aux pannes
    echo ""
    read -p "Lancer test de tolérance aux pannes? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        test_fault_tolerance
    fi
    
elif [ "$MODE" == "fault-tolerance" ]; then
    test_fault_tolerance
    
else
    echo -e "${RED}Mode invalide: $MODE${NC}"
    echo "Usage: $0 [baseline|comparison|fault-tolerance]"
    exit 1
fi

echo ""
echo -e "${GREEN}✅ Terminé!${NC}"
