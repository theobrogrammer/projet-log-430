#!/bin/bash

echo "=========================================="
echo "🚀 UC-04 SignalR Test - Démarrage"
echo "=========================================="
echo ""

# 1. Vérifier Docker
echo "✅ Étape 1: Vérification Docker..."
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep brokerx-api

if [ $? -ne 0 ]; then
    echo "❌ ERREUR: Le container brokerx-api n'est pas démarré!"
    echo "   Démarrez-le avec: docker compose up -d"
    exit 1
fi

echo ""

# 2. Tester l'API
echo "✅ Étape 2: Test API HTTP..."
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/api/v1/market/symbols)

if [ "$HTTP_STATUS" != "200" ]; then
    echo "❌ ERREUR: L'API ne répond pas correctement (HTTP $HTTP_STATUS)"
    echo "   Vérifiez les logs: docker logs brokerx-api"
    exit 1
fi

echo "   ✅ API accessible (HTTP 200)"
echo ""

# 3. Afficher les symbols disponibles
echo "✅ Étape 3: Symbols disponibles..."
SYMBOLS=$(curl -s http://localhost:5000/api/v1/market/symbols | jq -r '.symbols[]' 2>/dev/null)

if [ -z "$SYMBOLS" ]; then
    echo "❌ ERREUR: Impossible de récupérer les symbols"
    exit 1
fi

echo "   $SYMBOLS" | tr '\n' ', '
echo ""
echo ""

# 4. Vérifier les logs UC-04
echo "✅ Étape 4: Vérification du Market Data Feed..."
UC04_LOGS=$(docker logs brokerx-api 2>&1 | grep "UC04_MARKET_FEED" | tail -3)

if [ -z "$UC04_LOGS" ]; then
    echo "⚠️  ATTENTION: Aucun log UC04 trouvé"
else
    echo "$UC04_LOGS"
fi

echo ""

# 5. Démarrer serveur HTTP local
echo "✅ Étape 5: Démarrage serveur HTTP local sur port 8888..."
echo ""

cd /home/pop28/Documents/github/projet-log-430

# Tuer le serveur existant s'il y en a un
pkill -f "python3 -m http.server 8888" 2>/dev/null

# Démarrer le serveur en arrière-plan
python3 -m http.server 8888 > /tmp/http-server.log 2>&1 &
HTTP_PID=$!

# Attendre que le serveur démarre
sleep 2

if ps -p $HTTP_PID > /dev/null; then
    echo "   ✅ Serveur HTTP démarré (PID: $HTTP_PID)"
else
    echo "   ❌ Échec du démarrage du serveur HTTP"
    exit 1
fi

echo ""
echo "=========================================="
echo "✅ TOUT EST PRÊT!"
echo "=========================================="
echo ""
echo "📋 Instructions:"
echo ""
echo "1. Ouvrez votre navigateur à l'adresse:"
echo "   👉 http://localhost:8888/test-uc04-simple.html"
echo ""
echo "2. Ouvrez la console du navigateur (F12)"
echo ""
echo "3. Cliquez sur '🔌 Connect'"
echo ""
echo "4. Cliquez sur '✅ Subscribe All'"
echo ""
echo "5. Observez les quotes qui arrivent!"
echo ""
echo "=========================================="
echo "📊 Logs en direct:"
echo "=========================================="
echo ""

# 6. Suivre les logs
docker logs -f --tail 20 brokerx-api 2>&1 | grep --line-buffered -E "(UC04|SignalR|Connected|ReceiveQuote)"
