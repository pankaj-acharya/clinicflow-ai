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
  - `FOUNDRY_MODEL_DEPLOYMENT_NAME` (must already exist in the target Foundry project)
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
- Validates that `FOUNDRY_MODEL_DEPLOYMENT_NAME` already exists under `FOUNDRY_PROJECT_ENDPOINT` before any Foundry deployment work begins

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
3. **Foundry Preflight** (validates `FOUNDRY_MODEL_DEPLOYMENT_NAME` against `FOUNDRY_PROJECT_ENDPOINT`)
4. **Foundry Agent** (Booking assistant deployment plus a minimal smoke test)
5. **Cleanup** (Optional: destroys resources or just Foundry agents)

The Foundry stage fails fast if the configured model deployment is missing and then runs a post-deploy smoke test that only checks the agent invocation succeeds.

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

## Foundry Agent Logging (Option B: Unified Observability)

The Foundry booking assistant emits telemetry to **both** Azure AI Foundry portal (always) and **Log Analytics Workspace (LAW)** (optional, feature-flagged). This enables unified monitoring without code redeployment.

### Architecture Overview

```
Foundry Agent Deployment (GitHub Actions)
    ↓
    ├─→ Reads ENABLE_FOUNDRY_INSIGHTS_LOGGING from Key Vault secret
    ├─→ Reads APPINSIGHTS_INSTRUMENTATION_KEY from Key Vault secret
    ↓
Deploy Prompt Agent Script (foundry/scripts/deploy_prompt_agent.py)
    ├─→ If enabled: Initialize Azure Monitor OpenTelemetry SDK
    ├─→ Deploy agent version to Foundry
    ├─→ Output: appinsights_enabled: true/false flag
    ↓
At Runtime:
    ├─→ Foundry Portal: Agent execution always logged (independent)
    └─→ LAW: Agent traces sent (if feature flag enabled)
```

### Feature Flag Details

**Implementation**:
- Controlled by Key Vault secret: `enable-foundry-insights-logging`
- Read during **each workflow run** (not at deployment time)
- No code changes or redeployment needed to toggle

**Default state**: `true` (logs flow to LAW)

**Behavior**:
- When `true` (enabled):
  - Azure Monitor OpenTelemetry SDK initializes during agent deployment
  - Agent execution logs sent to AppInsights → flows to LAW
  - Logs also appear in Foundry portal (independent)
  - Both portals show the same agent traces
  
- When `false` (disabled):
  - Azure Monitor SDK not initialized
  - Agent only logs to Foundry portal
  - LAW receives no agent telemetry (faster startup, fewer AppInsights ingestion units)

### How to Toggle Foundry→LAW Logging

The feature is controlled dynamically via Azure Key Vault. **No redeployment needed.**

#### Disable (Logs stay in Foundry portal only)

```bash
az keyvault secret set \
  --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging \
  --value false

# Verify (optional)
az keyvault secret show \
  --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging \
  --query value -o tsv
# Output: false
```

Next time you run the workflow:
- Agent deployment will show `appinsights_enabled: false` in the output
- Logs only appear in Foundry portal
- No logs sent to LAW

#### Re-enable (Logs flow to both Foundry and LAW)

```bash
az keyvault secret set \
  --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging \
  --value true

# Verify (optional)
az keyvault secret show \
  --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging \
  --query value -o tsv
# Output: true
```

Next workflow run: Agent logs flow to both portals.

### Viewing Foundry Agent Logs

#### Option 1: Azure AI Foundry Portal (Always Available)

Foundry portal shows agent execution regardless of the LAW feature flag.

1. Go to https://ai.azure.com
2. Select your project
3. Navigate to **Agents** → **clinicflow-booking-assistant**
4. View:
   - Execution history (each agent invocation)
   - Tool calls and responses
   - Token usage and latency
   - Error traces (if any)

**Best for**: Agent behavior debugging, tool call inspection, cost tracking

#### Option 2: Log Analytics Workspace (When Feature Flag = true)

LAW provides unified view of all application logs + agent traces.

**Access LAW**:
1. Go to Azure Portal
2. Find resource group `clinicflow-ai-dev-rg`
3. Click on `clinicflow-ai-law` (Log Analytics Workspace)
4. Click **Logs** (KQL editor)

**Query agent traces**:

```kql
// All agent-related traces
AppTraces
| where OperationName contains "foundry" or OperationName contains "agent"
| project TimeGenerated, SeverityLevel, Message, OperationName
| order by TimeGenerated desc
| limit 100
```

**Query by severity**:

```kql
// Errors only
AppTraces
| where (OperationName contains "agent" or OperationName contains "foundry")
  and SeverityLevel in ("2", "3")  // Warning=1, Error=2, Critical=3
| project TimeGenerated, Message, OperationName
| order by TimeGenerated desc
```

**Correlate with API calls**:

```kql
// API request → Foundry agent call correlation
AppRequests
| where Name == "POST /ask"
| join kind=inner (
    AppTraces
    | where OperationName contains "foundry"
  ) on $left.OperationId == $right.OperationId
| project TimeGenerated, Name, ResultCode, Message
| order by TimeGenerated desc
```

### Unified Application Observability

When feature flag is **enabled**, see all telemetry in one place:

| Log Source | Table | Description |
|-----------|-------|-------------|
| **API Service** | `AppRequests` | HTTP requests to `/health`, `/availability`, `/ask`, `/bookings` |
| | `AppExceptions` | Errors (database connection, validation, Foundry call failures) |
| | `AppDependencies` | External calls (PostgreSQL queries, Foundry agent invocations) |
| **Web UI** | `AppTraces` | Container app logs (asset loads, API call results) |
| **Foundry Agent** | `AppTraces` | Agent execution, tool calls, LLM responses (when flag = true) |

**Example unified query**:

```kql
// All service activities in last 1 hour
union
  (AppRequests | project TimeGenerated, Service="API", Name, ResultCode),
  (AppTraces | project TimeGenerated, Service="Agent", Message, SeverityLevel),
  (AppDependencies | project TimeGenerated, Service="Dependencies", Name, DurationMs)
| where TimeGenerated > ago(1h)
| order by TimeGenerated desc
```

### Performance Considerations

**When feature flag is enabled**:
- AppInsights SDK initializes during agent deployment (one-time, <5s)
- Minimal runtime overhead; traces batched and sent asynchronously
- LAW ingestion cost: ~$0.005 per gigabyte

**When feature flag is disabled**:
- No AppInsights SDK load or initialization
- Agent deployment faster
- No LAW ingestion cost
- Logs still available in Foundry portal

### Troubleshooting

**Problem**: Agent deployment shows `appinsights_enabled: false` but I expected `true`

**Solution**: Check the feature flag value:
```bash
az keyvault secret show --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging --query value -o tsv
```

If it shows `false`, re-enable:
```bash
az keyvault secret set --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging --value true
```

Re-run the workflow.

---

**Problem**: Logs appear in Foundry portal but not in LAW

**Possible causes**:
1. Feature flag is `false` — check key vault secret
2. LAW workspace has not received data yet (5-10 min delay)
3. AppInsights SDK initialization failed (check workflow logs for warnings)

**Solution**:
1. Verify feature flag is `true`
2. Wait 10 minutes and retry LAW query
3. Check workflow logs:
   ```bash
   gh run view <run-id> --log | grep -i "appinsights"
   ```

---

**Problem**: Want to see logs in Foundry portal for demo but not clutter LAW

**Solution**: Toggle feature flag to `false` before demo:
```bash
az keyvault secret set --vault-name clinicflowaidevkv \
  --name enable-foundry-insights-logging --value false
```

Agent still logs to Foundry portal; LAW stays clean. Re-enable after demo.

