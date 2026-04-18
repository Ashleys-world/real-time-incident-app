#!/bin/bash
# ==============================================================================
# Real-Time Incident App — Teardown Script
# Deletes ALL resources by deleting the resource group.
# WARNING: This is irreversible. All data will be lost.
# ==============================================================================

set -euo pipefail

RESOURCE_GROUP="rg-incidentapp-dev"

echo ""
echo "WARNING: This will permanently delete resource group '${RESOURCE_GROUP}'"
echo "and ALL resources inside it, including the PostgreSQL database."
echo ""
read -p "Type the resource group name to confirm: " CONFIRM

if [[ "${CONFIRM}" != "${RESOURCE_GROUP}" ]]; then
  echo "Aborted."
  exit 1
fi

echo "Deleting resource group ${RESOURCE_GROUP}..."
az group delete \
  --name "${RESOURCE_GROUP}" \
  --yes \
  --no-wait

echo "Deletion initiated. Resources will be removed within a few minutes."
echo "Run: az group show --name ${RESOURCE_GROUP} to check status."
