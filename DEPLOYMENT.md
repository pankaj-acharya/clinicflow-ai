# Deployment Guide

This document explains how to deploy ClinicFlow AI to Azure.

## Prerequisites

- GitHub repository access with permission to configure secrets and workflows
- Azure subscription with appropriate permissions
- The following GitHub secrets already configured:
  - `AZURE_CLIENT_ID`
  - `AZURE_TENANT_ID`
  - `AZURE_SUBSCRIPTION_ID`
  - `FOUNDRY_PROJECT_ENDPOINT`
  - `FOUNDRY_MODEL_DEPLOYMENT_NAME`
  - `FOUNDRY_AGENT_INSTRUCTIONS`
  - `CLINICFLOW_API_BASE_URL`
  - `CLINICFLOW_GATEWAY_BASE_URL`

## PostgreSQL Setup for Development

To enable PostgreSQL Flexible Server in your Azure deployment, you must configure the database admin password as a GitHub secret.

### Step 1: Create the Secret in GitHub

1. Navigate to your GitHub repository
2. Go to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. **Name**: `POSTGRES_ADMIN_PASSWORD`
5. **Value**: Enter a secure password for the PostgreSQL admin user (clinicadmin)
6. Click **Add secret**

### Step 2: Deploy Infrastructure

The deployment pipeline (`dev-deploy.yml`) automatically:
- Enables PostgreSQL Flexible Server provisioning (`deploy_postgres=true`)
- Passes the password from the secret to Terraform
- Creates the database with migrations applied
- Seeds development data (in development environment)

### Step 3: Application Configuration

The API automatically:
- Detects PostgreSQL connection string from Terraform outputs
- Runs EF Core migrations on startup
- Connects to the database for appointment availability and booking queries
- Falls back to in-memory stub mode if connection fails

## GitHub Actions Workflow

The `dev-deploy.yml` workflow runs on manual dispatch and performs:

1. **Base Infrastructure** (Resource Group, ACR, Key Vault, PostgreSQL, etc.)
2. **Container Apps** (API, Agent Gateway, and Web UI deployments)
3. **Foundry Agent** (Booking assistant deployment)
4. **Cleanup** (Optional: destroys resources or just Foundry agents)

### Triggering a Deployment

1. Go to **Actions** → **dev-deploy**
2. Click **Run workflow**
3. Choose options:
   - `destroy_resources`: Set to `true` to delete all cloud resources (use to avoid costs)
   - `destroy_foundry_agents`: Set to `true` to delete only agents/models

## Live Endpoints (dev environment)

After a successful deployment the following URLs are active:

| Service | URL |
|---------|-----|
| **Web UI** | `https://clinicflow-ai-dev-web.<hash>.uksouth.azurecontainerapps.io` |
| **API** | `https://clinicflow-ai-dev-api.<hash>.uksouth.azurecontainerapps.io` |
| **Agent Gateway** | `https://clinicflow-ai-dev-gateway.<hash>.uksouth.azurecontainerapps.io` |

The exact FQDNs are emitted by `terraform output` at the end of the workflow (look for `web_url`, `api_url`, `gateway_url`).

## End-to-End User Journey

1. Open the **Web UI** URL in a browser.
2. In the **Ask AI** section, type a natural-language prompt such as:
   - `Show me next available dentist appointment`
   - `When is the next appointment with Hygienist Mrs Smith, preferably Monday morning?`
   - `Show me next 5 appointments with any dentist`
3. Click **Ask AI** — the Web UI calls `/ask` → API → Azure AI Foundry agent → LLM.
4. The AI returns matching slots; click **Book this** on any slot.
5. The UI POSTs `/book` → API → PostgreSQL; a ✅ calendar confirmation card appears.
6. To browse raw availability without AI, scroll down to the **Browse availability** section, choose a clinician from the dropdown, and click **Load availability**. Slots for the next 14 days are displayed.

## API Endpoints

Once deployed, the following endpoints are available:

- `GET /health` - Service health check
- `GET /availability` - Query available appointment slots
  - Falls back to in-memory stub if PostgreSQL unavailable
- `POST /bookings` - Create an appointment booking
  - Uses PostgreSQL when available, in-memory stub otherwise
- `POST /slot-holds` - Create a temporary slot hold
- `POST /ask` - Natural language scheduling request to Foundry agent

### Example: Check availability

```
GET /availability?ClinicId=clinic-1&ClinicianId=clinician-dentist-1&WindowStartUtc=<today ISO>&WindowEndUtc=<+14d ISO>&AppointmentTypeCode=exam
```

Seeded clinician IDs:

| ID | Name | Role |
|----|------|------|
| `clinician-dentist-1` | Dr. James Harper | Dentist |
| `clinician-dentist-2` | Dr. Sarah Okafor | Dentist |
| `clinician-hygienist-1` | Mrs. Lisa Smith | Hygienist |
| `clinician-hygienist-2` | Mr. David Chen | Hygienist |



- `GET /health` - Service health check
- `GET /availability` - Query available appointment slots
  - Falls back to in-memory stub if PostgreSQL unavailable
- `POST /bookings` - Create an appointment booking
  - Uses PostgreSQL when available, in-memory stub otherwise
- `POST /slot-holds` - Create a temporary slot hold
- `POST /ask` - Natural language scheduling request to Foundry agent

## Troubleshooting

### PostgreSQL Connection Issues

If the API cannot connect to PostgreSQL:
1. Check that the `POSTGRES_ADMIN_PASSWORD` secret is correctly configured
2. Verify the connection string in Key Vault matches the deployed database
3. Check Application Insights for connection errors
4. The API will automatically fall back to in-memory mode

### Terraform State Issues

The Terraform state is stored in Azure Storage Account:
- **Storage Account**: `clinicflowaitfstate` (in `clinicflow-ai-tfstate-rg` resource group)
- **Container**: `tfstate`
- **State File**: `clinicflow-ai.dev.tfstate`

To reset state (if needed):
```bash
az storage account show --name clinicflowaitfstate --resource-group clinicflow-ai-tfstate-rg
```

### Cost Management

To avoid unwanted Azure charges:
1. Delete resources after testing: Set `destroy_resources: true` in workflow dispatch
2. Or use the Azure portal to manually delete the `clinicflow-ai-dev-rg` resource group
3. Note: The Terraform state backend storage account is intentionally preserved

## Local Development

Without deploying to Azure, run the API locally:
```bash
cd src/ClinicFlowAi.Api
dotnet run
```

The API defaults to in-memory mode (no database connection required) unless the `ClinicFlowDb` connection string is configured in `appsettings.json`.

## Foundry Agent Logging

The Foundry booking assistant can emit telemetry to **both** Azure AI Foundry portal (always) and **Log Analytics Workspace** (optional, feature-flagged).

### Overview

- **Option A (Default)**: Logs only appear in Azure AI Foundry portal
- **Option B (Feature Flag)**: Logs flow to both Foundry portal AND LAW (Log Analytics Workspace)
  - Same logs visible in Foundry portal for agent debugging
  - Unified application logs in LAW for production monitoring
  - Toggle dynamically without redeployment

This deployment uses **Option B** with a dynamic feature flag for demonstrations and A/B testing.

### Toggle Foundry→LAW Logging

The feature is controlled by a Key Vault secret: `enable-foundry-insights-logging`

**To disable Foundry logging to LAW** (logs remain in Foundry portal):
```bash
az keyvault secret set \
  --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging \
  --value false
```

Next time the workflow runs, the agent deployment will skip AppInsights instrumentation.

**To re-enable**:
```bash
az keyvault secret set \
  --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging \
  --value true
```

**Note**: No redeployment needed — the flag is read at agent startup during each workflow run.

### Viewing Foundry Logs

**Option B logs (in both places)**:

1. **Azure AI Foundry Portal**: https://ai.azure.com
   - Go to your project → **Agents** → **clinicflow-booking-assistant**
   - View execution history and traces

2. **Log Analytics Workspace (LAW)**: 
   - Resource: `clinicflow-ai-law` in resource group `clinicflow-ai-dev-rg`
   - Query: Filter by `OperationName` containing `foundry` or `agent`
   ```kql
   AppTraces
   | where OperationName contains "foundry"
   | project TimeGenerated, SeverityLevel, Message
   ```

**Comparing with other application logs**:
- API logs: `AppRequests`, `AppExceptions`, `AppDependencies` (queries to PostgreSQL, calls to Foundry)
- Web UI logs: `AppTraces` from the container app
- Agent logs: `AppTraces` tagged with agent name (when Option B enabled)


