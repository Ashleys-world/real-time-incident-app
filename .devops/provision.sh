#!/bin/bash
# ==============================================================================
# Real-Time Incident Coordination App — Azure Resource Provisioning
# Run this script in Azure Cloud Shell (Bash)
#
# All resources are created under ONE resource group.
# Organized by phase — comment out Phase 3/4 sections if not needed yet.
#
# Usage:
#   1. Open https://shell.azure.com (Bash mode)
#   2. Upload this file or paste it
#   3. Set your variable values in the CONFIGURATION section below
#   4. chmod +x provision.sh && ./provision.sh
# ==============================================================================

set -euo pipefail

# ── CONFIGURATION — edit these before running ─────────────────────────────────

RESOURCE_GROUP="rg-incidentapp-dev"
LOCATION="southafricanorth"               # az account list-locations --output table
SWA_LOCATION="westeurope"                 # Static Web Apps not available in southafricanorth
ENV_SUFFIX="dev"                          # dev | staging | prod

# PostgreSQL Flexible Server
PG_SERVER_NAME="psql-incidentapp-${ENV_SUFFIX}"
PG_DB_NAME="incidentapp"
PG_ADMIN_USER="pgadmin"
PG_ADMIN_PASSWORD="IncApp@2026"           # Azure PostgreSQL requires upper, lower, number & special char

# JWT secret (min 32 chars — generate with: openssl rand -base64 48)
JWT_SECRET=""                             # Set below or export JWT_SECRET=...

# App Service
APP_PLAN_NAME="asp-incidentapp-${ENV_SUFFIX}"
API_APP_NAME="api-incidentapp-${ENV_SUFFIX}"    # must be globally unique
APP_PLAN_SKU="B1"                               # B1 (dev) | P1v3 (prod)

# Angular Static Web App
SWA_NAME="swa-incidentapp-${ENV_SUFFIX}"        # must be globally unique

# Key Vault (name must be globally unique, 3–24 alphanumeric/hyphens)
KEY_VAULT_NAME="kv-incapp-${ENV_SUFFIX}"

# Monitoring
LOG_ANALYTICS_NAME="log-incidentapp-${ENV_SUFFIX}"
APP_INSIGHTS_NAME="ai-incidentapp-${ENV_SUFFIX}"

# SignalR Service (Phase 4)
SIGNALR_NAME="sigr-incidentapp-${ENV_SUFFIX}"

# Service Bus (Phase 4)
SERVICE_BUS_NS="sb-incidentapp-${ENV_SUFFIX}"
SERVICE_BUS_QUEUE="notifications"

# Azure Functions + Storage (Phase 4)
STORAGE_ACCOUNT_NAME="stincapp${ENV_SUFFIX}"    # 3–24 lowercase alphanumeric, globally unique
FUNCTIONS_APP_NAME="func-incidentapp-${ENV_SUFFIX}"

# ── PROMPT for JWT secret if not set ──────────────────────────────────────────

if [[ -z "${JWT_SECRET:-}" ]]; then
  # Auto-generate if not provided
  JWT_SECRET=$(openssl rand -base64 48)
  echo "Generated JWT secret (save this!): ${JWT_SECRET}"
fi

# ──────────────────────────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo " Provisioning: ${RESOURCE_GROUP} in ${LOCATION}"
echo "============================================================"
echo ""

# ==============================================================================
# PHASE 1 — Core Infrastructure
# ==============================================================================

# ── 1. Resource Group ─────────────────────────────────────────────────────────
echo "[1/11] Creating resource group..."
az group create \
  --name "${RESOURCE_GROUP}" \
  --location "${LOCATION}" \
  --tags project=incidentapp env="${ENV_SUFFIX}"

# ── 2. Log Analytics Workspace ────────────────────────────────────────────────
echo "[2/11] Creating Log Analytics workspace..."
if ! az monitor log-analytics workspace show --resource-group "${RESOURCE_GROUP}" --workspace-name "${LOG_ANALYTICS_NAME}" &>/dev/null; then
  az monitor log-analytics workspace create \
    --resource-group "${RESOURCE_GROUP}" \
    --workspace-name "${LOG_ANALYTICS_NAME}" \
    --location "${LOCATION}" \
    --sku PerGB2018 \
    --retention-time 30
else
  echo "    Already exists, skipping."
fi

LOG_ANALYTICS_ID=$(az monitor log-analytics workspace show \
  --resource-group "${RESOURCE_GROUP}" \
  --workspace-name "${LOG_ANALYTICS_NAME}" \
  --query id --output tsv)

# ── 3. Application Insights ───────────────────────────────────────────────────
echo "[3/11] Creating Application Insights..."
if ! az monitor app-insights component show --app "${APP_INSIGHTS_NAME}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
  az monitor app-insights component create \
    --app "${APP_INSIGHTS_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --kind web \
    --workspace "${LOG_ANALYTICS_ID}"
else
  echo "    Already exists, skipping."
fi

APP_INSIGHTS_CONNECTION_STRING=$(az monitor app-insights component show \
  --app "${APP_INSIGHTS_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query connectionString --output tsv)

# ── 4. Key Vault ──────────────────────────────────────────────────────────────
echo "[4/11] Creating Key Vault..."

CURRENT_USER_ID=$(az ad signed-in-user show --query id --output tsv)

if ! az keyvault show --name "${KEY_VAULT_NAME}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
  az keyvault create \
    --name "${KEY_VAULT_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --sku standard \
    --enable-rbac-authorization true
else
  echo "    Already exists, skipping create."
fi

KV_SCOPE=$(az keyvault show --name "${KEY_VAULT_NAME}" --resource-group "${RESOURCE_GROUP}" --query id --output tsv)

# Grant the current user Key Vault Secrets Officer (idempotent — duplicate assignments are ignored)
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee "${CURRENT_USER_ID}" \
  --scope "${KV_SCOPE}" 2>/dev/null || echo "    Role assignment already exists, skipping."

# Store secrets
echo "[4a] Storing secrets in Key Vault..."
az keyvault secret set --vault-name "${KEY_VAULT_NAME}" --name "JwtKey"           --value "${JWT_SECRET}"
az keyvault secret set --vault-name "${KEY_VAULT_NAME}" --name "PgAdminPassword"  --value "${PG_ADMIN_PASSWORD}"
az keyvault secret set --vault-name "${KEY_VAULT_NAME}" --name "AppInsightsConn"  --value "${APP_INSIGHTS_CONNECTION_STRING}"

# ── 5. PostgreSQL Flexible Server ─────────────────────────────────────────────
echo "[5/11] Creating PostgreSQL Flexible Server (this takes ~5 min)..."
if ! az postgres flexible-server show --resource-group "${RESOURCE_GROUP}" --name "${PG_SERVER_NAME}" &>/dev/null; then
  az postgres flexible-server create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${PG_SERVER_NAME}" \
    --location "${LOCATION}" \
    --admin-user "${PG_ADMIN_USER}" \
    --admin-password "${PG_ADMIN_PASSWORD}" \
    --sku-name Standard_B1ms \
    --tier Burstable \
    --storage-size 32 \
    --version 16 \
    --public-access 0.0.0.0 \
    --yes
else
  echo "    Already exists, skipping."
fi

# Create the application database
echo "[5a] Creating database..."
az postgres flexible-server db create \
  --resource-group "${RESOURCE_GROUP}" \
  --server-name "${PG_SERVER_NAME}" \
  --database-name "${PG_DB_NAME}" 2>/dev/null || echo "    Database already exists, skipping."

# Allow Azure services (App Service outbound) through the firewall
az postgres flexible-server firewall-rule create \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${PG_SERVER_NAME}" \
  --rule-name "AllowAzureServices" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 2>/dev/null || echo "    Firewall rule already exists, skipping."

PG_HOST="${PG_SERVER_NAME}.postgres.database.azure.com"
PG_CONNECTION_STRING="Host=${PG_HOST};Port=5432;Database=${PG_DB_NAME};Username=${PG_ADMIN_USER};Password=${PG_ADMIN_PASSWORD};Ssl Mode=Require"

# Store connection string in Key Vault
az keyvault secret set \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "PostgresConnectionString" \
  --value "${PG_CONNECTION_STRING}"

# ── 6. App Service Plan ───────────────────────────────────────────────────────
echo "[6/11] Creating App Service Plan (Linux)..."
if ! az appservice plan show --resource-group "${RESOURCE_GROUP}" --name "${APP_PLAN_NAME}" &>/dev/null; then
  az appservice plan create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${APP_PLAN_NAME}" \
    --location "${LOCATION}" \
    --is-linux \
    --sku "${APP_PLAN_SKU}"
else
  echo "    Already exists, skipping."
fi

# ── 7. App Service (ASP.NET Core API) ─────────────────────────────────────────
echo "[7/11] Creating App Service for API..."
if ! az webapp show --resource-group "${RESOURCE_GROUP}" --name "${API_APP_NAME}" &>/dev/null; then
  az webapp create \
    --resource-group "${RESOURCE_GROUP}" \
    --plan "${APP_PLAN_NAME}" \
    --name "${API_APP_NAME}" \
    --runtime "DOTNETCORE:9.0"
else
  echo "    Already exists, skipping."
fi

# Enable managed identity for Key Vault access
az webapp identity assign \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}"

API_IDENTITY=$(az webapp identity show \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --query principalId --output tsv)

# Grant App Service identity Key Vault Secrets User
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee "${API_IDENTITY}" \
  --scope "${KV_SCOPE}" 2>/dev/null || echo "    Role assignment already exists, skipping."

# Configure App Service settings
az webapp config appsettings set \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    AllowedOrigins="https://${SWA_NAME}.azurestaticapps.net" \
    Jwt__Issuer="IncidentApp" \
    Jwt__Audience="IncidentAppUsers" \
    Jwt__Key="@Microsoft.KeyVault(VaultName=${KEY_VAULT_NAME};SecretName=JwtKey)" \
    ConnectionStrings__DefaultConnection="@Microsoft.KeyVault(VaultName=${KEY_VAULT_NAME};SecretName=PostgresConnectionString)" \
    APPLICATIONINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(VaultName=${KEY_VAULT_NAME};SecretName=AppInsightsConn)"

# Enable Application Insights auto-instrumentation
az webapp config appsettings set \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --settings ApplicationInsightsAgent_EXTENSION_VERSION="~3"

# CORS — allow the Static Web App origin
az webapp cors add \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --allowed-origins "https://${SWA_NAME}.azurestaticapps.net" "http://localhost:4200"

# ── 8. Azure Static Web App (Angular frontend) ────────────────────────────────
echo "[8/11] Creating Static Web App..."
az staticwebapp create \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${SWA_NAME}" \
  --location "${SWA_LOCATION}" \
  --sku Free

SWA_URL=$(az staticwebapp show \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${SWA_NAME}" \
  --query defaultHostname --output tsv)

echo "    Static Web App URL: https://${SWA_URL}"

# ==============================================================================
# PHASE 3 — Distributed Cache (Redis)
# ==============================================================================

# Uncomment when starting Phase 3
# echo "[Phase 3] Creating Azure Cache for Redis..."
# REDIS_NAME="redis-incidentapp-${ENV_SUFFIX}"
# az redis create \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${REDIS_NAME}" \
#   --location "${LOCATION}" \
#   --sku Basic \
#   --vm-size C0
#
# REDIS_CONNECTION_STRING=$(az redis show \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${REDIS_NAME}" \
#   --query "[hostName,port]" --output tsv | paste -sd ':')
#
# REDIS_KEY=$(az redis list-keys \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${REDIS_NAME}" \
#   --query primaryKey --output tsv)
#
# az keyvault secret set \
#   --vault-name "${KEY_VAULT_NAME}" \
#   --name "RedisConnectionString" \
#   --value "${REDIS_CONNECTION_STRING},password=${REDIS_KEY},ssl=True,abortConnect=False"

# ==============================================================================
# PHASE 4 — Azure SignalR Service, Service Bus, Functions, Blob Storage
# ==============================================================================

# Uncomment when starting Phase 4

# ── Azure SignalR Service ─────────────────────────────────────────────────────
# echo "[Phase 4] Creating Azure SignalR Service..."
# az signalr create \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${SIGNALR_NAME}" \
#   --location "${LOCATION}" \
#   --sku Standard_S1 \
#   --unit-count 1 \
#   --service-mode Default
#
# SIGNALR_CONNECTION_STRING=$(az signalr key list \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${SIGNALR_NAME}" \
#   --query primaryConnectionString --output tsv)
#
# az keyvault secret set \
#   --vault-name "${KEY_VAULT_NAME}" \
#   --name "AzureSignalRConnectionString" \
#   --value "${SIGNALR_CONNECTION_STRING}"
#
# az webapp config appsettings set \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${API_APP_NAME}" \
#   --settings \
#     Azure__SignalR__ConnectionString="@Microsoft.KeyVault(VaultName=${KEY_VAULT_NAME};SecretName=AzureSignalRConnectionString)"

# ── Azure Service Bus ─────────────────────────────────────────────────────────
# echo "[Phase 4] Creating Service Bus..."
# az servicebus namespace create \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${SERVICE_BUS_NS}" \
#   --location "${LOCATION}" \
#   --sku Basic
#
# az servicebus queue create \
#   --resource-group "${RESOURCE_GROUP}" \
#   --namespace-name "${SERVICE_BUS_NS}" \
#   --name "${SERVICE_BUS_QUEUE}"
#
# SB_CONNECTION_STRING=$(az servicebus namespace authorization-rule keys list \
#   --resource-group "${RESOURCE_GROUP}" \
#   --namespace-name "${SERVICE_BUS_NS}" \
#   --name RootManageSharedAccessKey \
#   --query primaryConnectionString --output tsv)
#
# az keyvault secret set \
#   --vault-name "${KEY_VAULT_NAME}" \
#   --name "ServiceBusConnectionString" \
#   --value "${SB_CONNECTION_STRING}"

# ── Storage Account (Blobs + Functions backing) ───────────────────────────────
# echo "[Phase 4] Creating Storage Account..."
# az storage account create \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${STORAGE_ACCOUNT_NAME}" \
#   --location "${LOCATION}" \
#   --sku Standard_LRS \
#   --kind StorageV2 \
#   --allow-blob-public-access false
#
# az storage container create \
#   --account-name "${STORAGE_ACCOUNT_NAME}" \
#   --name "attachments" \
#   --auth-mode login
#
# STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${STORAGE_ACCOUNT_NAME}" \
#   --query connectionString --output tsv)
#
# az keyvault secret set \
#   --vault-name "${KEY_VAULT_NAME}" \
#   --name "StorageConnectionString" \
#   --value "${STORAGE_CONNECTION_STRING}"

# ── Azure Functions App ───────────────────────────────────────────────────────
# echo "[Phase 4] Creating Azure Functions App..."
# az functionapp create \
#   --resource-group "${RESOURCE_GROUP}" \
#   --consumption-plan-location "${LOCATION}" \
#   --runtime dotnet-isolated \
#   --runtime-version 9 \
#   --functions-version 4 \
#   --name "${FUNCTIONS_APP_NAME}" \
#   --storage-account "${STORAGE_ACCOUNT_NAME}"
#
# az functionapp identity assign \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${FUNCTIONS_APP_NAME}"
#
# FUNC_IDENTITY=$(az functionapp identity show \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${FUNCTIONS_APP_NAME}" \
#   --query principalId --output tsv)
#
# az role assignment create \
#   --role "Key Vault Secrets User" \
#   --assignee "${FUNC_IDENTITY}" \
#   --scope "$(az keyvault show --name "${KEY_VAULT_NAME}" --resource-group "${RESOURCE_GROUP}" --query id --output tsv)"
#
# az functionapp config appsettings set \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${FUNCTIONS_APP_NAME}" \
#   --settings \
#     ServiceBusConnectionString="@Microsoft.KeyVault(VaultName=${KEY_VAULT_NAME};SecretName=ServiceBusConnectionString)" \
#     APPLICATIONINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(VaultName=${KEY_VAULT_NAME};SecretName=AppInsightsConn)"

# ==============================================================================
# SUMMARY
# ==============================================================================

API_URL=$(az webapp show \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --query defaultHostName --output tsv)

echo ""
echo "============================================================"
echo " Provisioning complete!"
echo "============================================================"
echo ""
echo "  Resource Group   : ${RESOURCE_GROUP}"
echo "  Location         : ${LOCATION}"
echo ""
echo "  API URL          : https://${API_URL}"
echo "  Frontend URL     : https://${SWA_URL}"
echo "  Key Vault        : https://${KEY_VAULT_NAME}.vault.azure.net"
echo "  App Insights     : ${APP_INSIGHTS_NAME}"
echo ""
echo "  PostgreSQL Host  : ${PG_HOST}"
echo "  Database         : ${PG_DB_NAME}"
echo ""
echo "  Next steps:"
echo "  1. Run EF Core migrations against the Azure PostgreSQL server"
echo "  2. Deploy the API:      az webapp deployment (see deploy.sh)"
echo "  3. Deploy the frontend: az staticwebapp (see deploy.sh)"
echo "============================================================"
