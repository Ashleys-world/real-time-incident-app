#!/bin/bash
# ==============================================================================
# Real-Time Incident App — Deploy Script
# Run in Azure Cloud Shell (Bash) after provision.sh has completed
#
# Deploys:
#   - ASP.NET Core API to Azure App Service (via zip deploy)
#   - Angular SPA to Azure Static Web Apps (via SWA CLI or GitHub Actions token)
# ==============================================================================

set -euo pipefail

# ── CONFIGURATION — must match provision.sh ───────────────────────────────────
RESOURCE_GROUP="rg-incidentapp-dev"
ENV_SUFFIX="dev"
API_APP_NAME="api-incidentapp-${ENV_SUFFIX}"
SWA_NAME="swa-incidentapp-${ENV_SUFFIX}"
PG_SERVER_NAME="psql-incidentapp-${ENV_SUFFIX}"
PG_DB_NAME="incidentapp"
PG_ADMIN_USER="pgadmin"
KEY_VAULT_NAME="kv-incapp-${ENV_SUFFIX}"

# ── PATHS — adjust if running from a different directory ──────────────────────
BACKEND_DIR="./real-time-incident-backend"
FRONTEND_DIR="./real-time-incident-frontend/incident-app"
PUBLISH_DIR="/tmp/incidentapp-publish"
FRONTEND_BUILD_DIR="${FRONTEND_DIR}/dist/incident-app/browser"

# ==============================================================================
# 1. Run EF Core migrations against Azure PostgreSQL
# ==============================================================================
echo "[1/4] Running EF Core migrations..."

PG_ADMIN_PASSWORD=$(az keyvault secret show \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "PgAdminPassword" \
  --query value --output tsv)

PG_CONNECTION_STRING="Host=${PG_SERVER_NAME}.postgres.database.azure.com;Port=5432;Database=${PG_DB_NAME};Username=${PG_ADMIN_USER};Password=${PG_ADMIN_PASSWORD};Ssl Mode=Require"

# Run migrations from local machine / Cloud Shell (requires dotnet-ef installed)
(
  cd "${BACKEND_DIR}"
  dotnet ef database update \
    --project ../IncidentApp.Infrastructure/IncidentApp.Infrastructure.csproj \
    --startup-project . \
    --connection "${PG_CONNECTION_STRING}"
)

# ==============================================================================
# 2. Build and publish the API
# ==============================================================================
echo "[2/4] Building API..."
(
  cd "${BACKEND_DIR}"
  dotnet publish IncidentApp.Api.csproj \
    --configuration Release \
    --output "${PUBLISH_DIR}/api"
)

# Zip and deploy to App Service
echo "[2/4] Deploying API to App Service..."
cd "${PUBLISH_DIR}/api"
zip -r /tmp/api.zip .

az webapp deploy \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --src-path /tmp/api.zip \
  --type zip \
  --async false

# ==============================================================================
# 3. Build Angular frontend
# ==============================================================================
echo "[3/4] Building Angular frontend..."
(
  cd "${FRONTEND_DIR}"

  # Point to the Azure API URL
  API_URL="https://$(az webapp show \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${API_APP_NAME}" \
    --query defaultHostName --output tsv)"

  # Write production environment file
  cat > src/environments/environment.prod.ts <<EOF
export const environment = {
  production: true,
  apiUrl: '${API_URL}/api',
  hubUrl: '${API_URL}/hubs/incident'
};
EOF

  npm run build -- --configuration=production
)

# ==============================================================================
# 4. Deploy Angular to Static Web Apps
# ==============================================================================
echo "[4/4] Deploying frontend to Static Web App..."

SWA_DEPLOYMENT_TOKEN=$(az staticwebapp secrets list \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${SWA_NAME}" \
  --query properties.apiKey --output tsv)

# Deploy using SWA CLI
npx @azure/static-web-apps-cli deploy "${FRONTEND_BUILD_DIR}" \
  --deployment-token "${SWA_DEPLOYMENT_TOKEN}" \
  --env production

# ==============================================================================
# SUMMARY
# ==============================================================================
API_URL=$(az webapp show --resource-group "${RESOURCE_GROUP}" --name "${API_APP_NAME}" --query defaultHostName --output tsv)
SWA_URL=$(az staticwebapp show --resource-group "${RESOURCE_GROUP}" --name "${SWA_NAME}" --query defaultHostname --output tsv)

echo ""
echo "============================================================"
echo " Deployment complete!"
echo "  API    : https://${API_URL}"
echo "  App    : https://${SWA_URL}"
echo "  Swagger: https://${API_URL}/swagger"
echo "============================================================"
