#!/bin/bash
# Script pour exécuter un test k6 et capturer les résultats

set -e

# Configuration
TEST_NAME=${1:-"baseline"}
SCRIPT=${2:-"scripts/k6/mixed.js"}
OUTPUT_DIR="resultats-k6/${TEST_NAME}"

# Couleurs
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  K6 Load Test Runner - BrokerX API    ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════╝${NC}"
echo ""

# Créer le dossier de résultats
mkdir -p "${OUTPUT_DIR}"
mkdir -p "${OUTPUT_DIR}/screenshots"

# Vérifier que l'API est UP
echo -e "${YELLOW}[1/5]${NC} Vérification de l'API..."
if ! curl -s http://localhost:5000/health > /dev/null 2>&1; then
    echo -e "${RED}❌ API non disponible sur http://localhost:5000${NC}"
    echo "Démarrez l'API avec: docker compose up -d api"
    exit 1
fi
echo -e "${GREEN}✓${NC} API disponible"
echo ""

# Vérifier Grafana
echo -e "${YELLOW}[2/5]${NC} Vérification de Grafana..."
if ! curl -s http://localhost:3000 > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️${NC} Grafana non disponible (optionnel)"
else
    echo -e "${GREEN}✓${NC} Grafana disponible sur http://localhost:3000"
    echo -e "   ${YELLOW}→${NC} Ouvrez dans votre navigateur pour capturer les screenshots"
fi
echo ""

# Nettoyer les logs avant le test
echo -e "${YELLOW}[3/5]${NC} Préparation..."
docker compose exec -T api sh -c "echo '' > /app/logs/app-$(date +%Y%m%d).jsonl" 2>/dev/null || true
echo -e "${GREEN}✓${NC} Logs nettoyés"
echo ""

# Démarrer le monitoring des logs en arrière-plan
echo -e "${YELLOW}[4/5]${NC} Démarrage du monitoring..."
LOGS_FILE="${OUTPUT_DIR}/api-logs-$(date +%Y%m%d_%H%M%S).log"
./tests/scripts/logs-readable.sh > "${LOGS_FILE}" 2>&1 &
LOGS_PID=$!
echo -e "${GREEN}✓${NC} Logs en cours d'enregistrement (PID: ${LOGS_PID})"
echo ""

# Exécuter le test k6
echo -e "${YELLOW}[5/5]${NC} Exécution du test k6..."
echo -e "   Test: ${SCRIPT}"
echo -e "   Output: ${OUTPUT_DIR}/${TEST_NAME}.json"
echo ""
echo -e "${GREEN}╔════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  Démarrage du test...                  ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════╝${NC}"
echo ""

# Capturer l'heure de début
START_TIME=$(date +%s)
START_TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')

# Exécuter k6
k6 run \
    --out "json=${OUTPUT_DIR}/${TEST_NAME}.json" \
    --summary-export="${OUTPUT_DIR}/${TEST_NAME}-summary.json" \
    "${SCRIPT}" | tee "${OUTPUT_DIR}/k6-console-output.txt"

TEST_EXIT_CODE=$?

# Capturer l'heure de fin
END_TIME=$(date +%s)
END_TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')
DURATION=$((END_TIME - START_TIME))

# Arrêter le monitoring des logs
kill $LOGS_PID 2>/dev/null || true

echo ""
echo -e "${GREEN}╔════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  Test terminé                          ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════╝${NC}"
echo ""

# Résumé
echo -e "${YELLOW}📊 RÉSUMÉ${NC}"
echo "  • Début:     ${START_TIMESTAMP}"
echo "  • Fin:       ${END_TIMESTAMP}"
echo "  • Durée:     ${DURATION}s"
echo ""

# Analyser les résultats
if [ -f "${OUTPUT_DIR}/${TEST_NAME}.json" ]; then
    echo -e "${YELLOW}📈 MÉTRIQUES CLÉS${NC}"
    
    # Extraire avec jq
    TOTAL_REQS=$(cat "${OUTPUT_DIR}/${TEST_NAME}.json" | jq -r 'select(.type=="Point" and .metric=="http_reqs") | .data.value' | tail -1)
    
    if [ ! -z "$TOTAL_REQS" ]; then
        RPS=$(echo "scale=2; $TOTAL_REQS / $DURATION" | bc)
        echo "  • Total Requests: ${TOTAL_REQS}"
        echo "  • RPS (avg):      ${RPS}"
    fi
    
    echo ""
fi

# Statut final
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✅ Test réussi (tous les thresholds passés)${NC}"
elif [ $TEST_EXIT_CODE -eq 99 ]; then
    echo -e "${YELLOW}⚠️  Test terminé avec thresholds non atteints${NC}"
    echo -e "   (exit code 99 = certains seuils dépassés)"
else
    echo -e "${RED}❌ Test échoué (exit code: ${TEST_EXIT_CODE})${NC}"
fi

echo ""
echo -e "${YELLOW}📁 FICHIERS GÉNÉRÉS${NC}"
echo "  • Résultats JSON:    ${OUTPUT_DIR}/${TEST_NAME}.json"
echo "  • Summary JSON:      ${OUTPUT_DIR}/${TEST_NAME}-summary.json"
echo "  • Console output:    ${OUTPUT_DIR}/k6-console-output.txt"
echo "  • Logs API:          ${LOGS_FILE}"
echo ""

# Instructions pour screenshots
echo -e "${YELLOW}📸 PROCHAINES ÉTAPES${NC}"
echo ""
echo "1. Capturer les screenshots Grafana:"
echo "   - Ouvrir http://localhost:3000"
echo "   - Dashboard: 4 Golden Signals"
echo "   - Time range: ${START_TIMESTAMP} à ${END_TIMESTAMP}"
echo "   - Sauvegarder dans: ${OUTPUT_DIR}/screenshots/"
echo ""
echo "2. Analyser les résultats:"
echo "   cat ${OUTPUT_DIR}/${TEST_NAME}.json | jq '.metrics'"
echo ""
echo "3. Comparer avec baseline:"
echo "   diff <(cat resultats-k6/baseline/baseline.json | jq '.metrics.http_req_duration') \\"
echo "        <(cat ${OUTPUT_DIR}/${TEST_NAME}.json | jq '.metrics.http_req_duration')"
echo ""

exit $TEST_EXIT_CODE
