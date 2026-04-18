#!/bin/bash
# ==============================================================================
# Real-Time Incident App — Useful Azure CLI Utility Commands
# One-liners for day-to-day operations in Azure Cloud Shell
# ==============================================================================

RESOURCE_GROUP="rg-incidentapp-dev"
ENV_SUFFIX="dev"
API_APP_NAME="api-incidentapp-${ENV_SUFFIX}"
PG_SERVER_NAME="psql-incidentapp-${ENV_SUFFIX}"
KEY_VAULT_NAME="kv-incapp-${ENV_SUFFIX}"
APP_INSIGHTS_NAME="ai-incidentapp-${ENV_SUFFIX}"
SWA_NAME="swa-incidentapp-${ENV_SUFFIX}"

# ==============================================================================
# RESOURCE GROUP
# ==============================================================================

# List all resources in the group
az resource list --resource-group "${RESOURCE_GROUP}" --output table

# Get total estimated cost
az consumption usage list --resource-group "${RESOURCE_GROUP}" --output table

# ==============================================================================
# APP SERVICE — API
# ==============================================================================

# Stream live logs
az webapp log tail \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}"

# View current app settings
az webapp config appsettings list \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}" \
  --output table

# Restart the API
az webapp restart \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${API_APP_NAME}"

# Scale up App Service Plan (e.g. B1 → P1v3 for production)
# az appservice plan update \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "asp-incidentapp-${ENV_SUFFIX}" \
#   --sku P1v3

# Scale out (increase instance count)
# az appservice plan update \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "asp-incidentapp-${ENV_SUFFIX}" \
#   --number-of-workers 2

# ==============================================================================
# POSTGRESQL
# ==============================================================================

# Add your local IP to the firewall (replace with your IP)
MY_IP=$(curl -s https://api.ipify.org)
az postgres flexible-server firewall-rule create \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${PG_SERVER_NAME}" \
  --rule-name "LocalDev" \
  --start-ip-address "${MY_IP}" \
  --end-ip-address "${MY_IP}"

# Remove your local IP when done
# az postgres flexible-server firewall-rule delete \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${PG_SERVER_NAME}" \
#   --rule-name "LocalDev" --yes

# List all databases on the server
az postgres flexible-server db list \
  --resource-group "${RESOURCE_GROUP}" \
  --server-name "${PG_SERVER_NAME}" \
  --output table

# Show connection string (pulls password from Key Vault)
PG_PASS=$(az keyvault secret show --vault-name "${KEY_VAULT_NAME}" --name PgAdminPassword --query value --output tsv)
echo "psql \"Host=${PG_SERVER_NAME}.postgres.database.azure.com;Database=incidentapp;Username=pgadmin;Password=${PG_PASS};Ssl Mode=Require\""

# ==============================================================================
# KEY VAULT
# ==============================================================================

# List all secrets (names only)
az keyvault secret list \
  --vault-name "${KEY_VAULT_NAME}" \
  --output table

# Read a secret value
az keyvault secret show \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "JwtKey" \
  --query value --output tsv

# Rotate the JWT secret
NEW_JWT=$(openssl rand -base64 48)
az keyvault secret set \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "JwtKey" \
  --value "${NEW_JWT}"
# Then restart the API to pick up the new value:
az webapp restart --resource-group "${RESOURCE_GROUP}" --name "${API_APP_NAME}"

# ==============================================================================
# APPLICATION INSIGHTS
# ==============================================================================

# View recent exceptions
az monitor app-insights query \
  --app "${APP_INSIGHTS_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --analytics-query "exceptions | order by timestamp desc | take 20" \
  --output table

# View request durations (p95)
az monitor app-insights query \
  --app "${APP_INSIGHTS_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --analytics-query "requests | summarize p95=percentile(duration, 95) by name | order by p95 desc | take 10" \
  --output table

# View SignalR hub activity
az monitor app-insights query \
  --app "${APP_INSIGHTS_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --analytics-query "customEvents | where name startswith 'SignalR' | order by timestamp desc | take 20" \
  --output table

# ==============================================================================
# STATIC WEB APP
# ==============================================================================

# Get deployment token (needed for CI/CD)
az staticwebapp secrets list \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${SWA_NAME}" \
  --query properties.apiKey --output tsv

# List environments
az staticwebapp environment list \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${SWA_NAME}" \
  --output table

# ==============================================================================
# PHASE 4 UTILITIES (uncomment when provisioned)
# ==============================================================================

# ── SignalR ──────────────────────────────────────────────────────────────────
# SIGNALR_NAME="sigr-incidentapp-${ENV_SUFFIX}"
#
# View SignalR metrics
# az monitor metrics list \
#   --resource "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.SignalRService/SignalR/${SIGNALR_NAME}" \
#   --metric ConnectionCount \
#   --output table

# ── Service Bus ──────────────────────────────────────────────────────────────
# SERVICE_BUS_NS="sb-incidentapp-${ENV_SUFFIX}"
#
# View queue message count
# az servicebus queue show \
#   --resource-group "${RESOURCE_GROUP}" \
#   --namespace-name "${SERVICE_BUS_NS}" \
#   --name "notifications" \
#   --query countDetails --output table

# ── Azure Functions ──────────────────────────────────────────────────────────
# FUNCTIONS_APP_NAME="func-incidentapp-${ENV_SUFFIX}"
#
# Stream function logs
# az webapp log tail --resource-group "${RESOURCE_GROUP}" --name "${FUNCTIONS_APP_NAME}"
#
# List deployed functions
# az functionapp function list \
#   --resource-group "${RESOURCE_GROUP}" \
#   --name "${FUNCTIONS_APP_NAME}" \
#   --output table
