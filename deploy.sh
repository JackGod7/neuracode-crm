#!/usr/bin/env bash
# deploy.sh — Neuracode CRM production deploy
# Usage: ./deploy.sh [client-name]
# Run on the VPS after cloning the repo.

set -euo pipefail

CLIENT=${1:-crm}
DEPLOY_DIR="/opt/$CLIENT"

echo "=== Neuracode CRM Deploy: $CLIENT ==="

# 1. Verify .env exists
if [[ ! -f .env ]]; then
  echo "ERROR: .env not found. Copy .env.example to .env and fill in credentials."
  exit 1
fi

# 2. Verify required Meta vars
required_vars=(META_PHONE_NUMBER_ID META_WABA_ID META_ACCESS_TOKEN META_APP_SECRET META_VERIFY_TOKEN)
for var in "${required_vars[@]}"; do
  if ! grep -q "^${var}=" .env 2>/dev/null || grep -q "^${var}=$" .env 2>/dev/null; then
    echo "ERROR: $var is missing or empty in .env"
    exit 1
  fi
done

# 3. Ensure data directory exists
mkdir -p data

# 4. Build and start
echo "Building images..."
docker compose build --no-cache

echo "Starting services..."
docker compose up -d

# 5. Wait for API health
echo "Waiting for API health check..."
for i in {1..30}; do
  if curl -sf http://localhost:5000/healthz > /dev/null 2>&1; then
    echo "API healthy."
    break
  fi
  sleep 2
done

# 6. Verify web
if curl -sf http://localhost:3000 > /dev/null 2>&1; then
  echo "Web healthy."
fi

echo ""
echo "=== Deploy complete ==="
echo "Web:  http://localhost:3000"
echo "API:  http://localhost:5000"
echo ""
echo "Next steps:"
echo "  1. Configure nginx + SSL for your domain"
echo "  2. Register webhook in Meta:"
echo "     URL:   https://YOUR_DOMAIN/api/webhooks/whatsapp"
echo "     Token: \$(grep META_VERIFY_TOKEN .env | cut -d= -f2)"
echo "  3. Create templates under client WABA"
