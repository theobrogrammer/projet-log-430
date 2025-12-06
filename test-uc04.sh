#!/bin/bash

# Script de test complet pour BrokerX UC-04
# Ce script ouvre un navigateur avec les pages de test

API_URL="http://localhost:5000"

echo "======================================"
echo "🚀 BrokerX - Test UC-04"
echo "======================================"
echo ""

# 1. Vérifier que l'API est accessible
echo "1️⃣  Vérification de l'API..."
if curl -s -f "$API_URL/health" > /dev/null 2>&1; then
    echo "   ✅ API accessible sur $API_URL"
else
    echo "   ❌ API non accessible"
    echo "   💡 Démarrez Docker Compose: docker compose up -d"
    exit 1
fi

# 2. Vérifier les symboles disponibles
echo ""
echo "2️⃣  Symboles disponibles..."
SYMBOLS=$(curl -s "$API_URL/api/v1/market/symbols" | jq -r '.symbols[]' 2>/dev/null)
if [ -n "$SYMBOLS" ]; then
    echo "   ✅ Symboles: $SYMBOLS"
else
    echo "   ❌ Impossible de récupérer les symboles"
fi

# 3. Ouvrir la page HTML de test dans le navigateur
echo ""
echo "3️⃣  Ouverture des pages de test..."

# Chemin absolu vers le fichier de test
TEST_FILE="/home/pop28/Documents/github/projet-log-430/test-uc04-simple.html"

if [ -f "$TEST_FILE" ]; then
    echo "   📄 Test client: file://$TEST_FILE"
    
    # Essayer d'ouvrir avec différents navigateurs
    if command -v xdg-open > /dev/null; then
        xdg-open "$TEST_FILE" 2>/dev/null &
        echo "   ✅ Ouverture avec xdg-open"
    elif command -v firefox > /dev/null; then
        firefox "$TEST_FILE" 2>/dev/null &
        echo "   ✅ Ouverture avec Firefox"
    elif command -v google-chrome > /dev/null; then
        google-chrome "$TEST_FILE" 2>/dev/null &
        echo "   ✅ Ouverture avec Chrome"
    else
        echo "   ⚠️  Ouvrez manuellement: file://$TEST_FILE"
    fi
else
    echo "   ❌ Fichier de test introuvable: $TEST_FILE"
fi

# 4. Ouvrir la page web de l'application
echo ""
echo "4️⃣  Application Web BrokerX..."
echo "   🌐 Page d'accueil: $API_URL"
echo "   🔐 Page de connexion: $API_URL/signin.html"
echo "   📝 Page d'inscription: $API_URL/signup-otp.html"

if command -v xdg-open > /dev/null; then
    xdg-open "$API_URL" 2>/dev/null &
fi

echo ""
echo "======================================"
echo "✅ Tests lancés !"
echo "======================================"
echo ""
echo "📊 Pour tester SignalR (UC-04):"
echo "   1. Ouvrez file://$TEST_FILE"
echo "   2. Cliquez sur '🔌 Connect'"
echo "   3. Cliquez sur '✅ Subscribe All'"
echo "   4. Vous devriez voir des quotes en temps réel"
echo ""
echo "🔐 Pour créer un compte:"
echo "   1. Allez sur $API_URL/signup-otp.html"
echo "   2. Remplissez le formulaire"
echo "   3. Utilisez le code OTP: 123456 (pour la démo)"
echo ""
echo "📋 Logs de l'API:"
echo "   docker logs -f brokerx-api"
echo ""
