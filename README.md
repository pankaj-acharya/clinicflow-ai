# clinicflow-ai

ClinicFlow AI is a .NET 10 solution with three runnable local hosts and a dev-ready Azure deployment shape:

- `ClinicFlowAi.Api` for booking and availability endpoints.
- `ClinicFlowAi.Web` for the patient-facing browser shell.
- `ClinicFlowAi.AgentGateway` for allowlisted agent-facing actions.

The current local experience is intentionally lightweight:

- Availability and booking behavior run in-memory.
- PostgreSQL persistence is still a placeholder and is not required for local testing.
- The web host forwards its local `/availability` requests to the API so the browser flow works without browser-side cross-origin setup.
- The agent gateway now has a typed POST `/agents/booking/check-availability` path that forwards to the API and returns slots.

## Prerequisites

- .NET SDK 10.0.x
- Windows PowerShell or another shell that can run `dotnet`

Verify the SDK:

```powershell
dotnet --info
```

## Solution Layout

- `src/ClinicFlowAi.Api` - minimal API for health, availability, slot holds, and bookings.
- `src/ClinicFlowAi.Web` - static web shell plus a same-origin façade for availability requests.
- `src/ClinicFlowAi.AgentGateway` - agent-facing gateway with health, route discovery, and typed check-availability handling.
- `src/ClinicFlowAi.Domain` - domain rules and booking engine.
- `src/ClinicFlowAi.Infrastructure.Postgres` - persistence placeholder.
- `tests/ClinicFlowAi.Tests` - unit tests and host smoke tests.

## Dev Azure Shape

The dev deployment target is Azure Container Apps with supporting infrastructure provisioned by Terraform:

- Resource group, Container Apps environment, App Insights, Log Analytics, managed identity, Key Vault, and ACR.
- API and Agent Gateway container apps, each with its own image and ingress endpoint.
- Gateway-to-API wiring through `ClinicFlowApi:BaseUrl`, injected from Key Vault during deployment.

The deployment workflow is defined in [.github/workflows/dev-deploy.yml](.github/workflows/dev-deploy.yml). It:

1. Validates bootstrap prerequisites, registers required Azure resource providers, and surfaces the deployment identity and target scopes.
2. Boots Terraform state storage.
3. Applies base infrastructure, including ACR role assignments for the deployment identity and runtime managed identity.
4. Verifies the ACR role assignments before any image build or push work starts.
5. Builds and pushes API and gateway images.
6. Applies the application layer with those image tags.
7. Validates Foundry prerequisites and deploys a prompt agent version into your Foundry project using the assets under [foundry/](foundry/).

### Azure bootstrap boundary

The pipeline now manages the repeatable, in-scope prerequisites that it can create itself:

- Azure resource provider registration for `Microsoft.App`, `Microsoft.CognitiveServices`, `Microsoft.ContainerRegistry`, `microsoft.insights`, `Microsoft.KeyVault`, `Microsoft.ManagedIdentity`, `Microsoft.OperationalInsights`, and `Microsoft.Storage`
- `AcrPush` on the created ACR for the GitHub Actions deployment identity
- `User Access Administrator` on the created ACR for the GitHub Actions deployment identity
- `AcrPull` on the created ACR for the user-assigned managed identity used by Container Apps

The following bootstrap permissions must still exist before the first run:

- the deployment identity can create Azure resources in the target subscription or resource group
- the deployment identity can create Azure role assignments for newly created ACR resources, typically through `Owner`, `User Access Administrator`, or `Role Based Access Control Administrator` at a parent scope
- the deployment identity already has write-capable Foundry / Azure AI access at the target project or parent resource scope, such as `Contributor` or an equivalent tenant-specific Foundry role

If one of those bootstrap permissions is missing, the workflow fails in a preflight stage with remediation guidance before container image build and deployment steps begin.

### Cleanup Options

The workflow supports optional resource cleanup to reduce Azure costs:

- **Destroy All Resources** (`destroy_resources` checkbox): Runs `terraform destroy` to remove all provisioned infrastructure (Container Apps, ACR, App Insights, Key Vault, etc.). The Terraform state backend storage account is preserved for future deployments.
- **Destroy Foundry Agents Only** (`destroy_foundry_agents` checkbox): Removes only agent versions from your Foundry project. The Foundry project, AI Services, and model deployments are preserved.

To use cleanup:

1. Go to **Actions** → **dev-deploy** in GitHub
2. Click **Run workflow**
3. Check the desired cleanup option(s) and run
4. Only the cleanup job(s) will execute (deployment is skipped)

## Fixed Local Ports

The runnable hosts use fixed localhost ports through `launchSettings.json`:

- API: `http://localhost:5071`
- Web: `http://localhost:5072`
- Agent gateway: `http://localhost:5073`

## Restore, Build, and Test

From the repository root:

```powershell
dotnet restore
dotnet build ClinicFlowAi.sln
dotnet test ClinicFlowAi.sln
```

The test suite currently includes:

- domain booking engine tests
- outbox dispatcher tests
- host smoke tests for API, web, proxied availability, and agent gateway health

## Run Locally

Open three terminals from the repository root.

### Terminal 1: API

```powershell
dotnet run --project src/ClinicFlowAi.Api/ClinicFlowAi.Api.csproj
```

### Terminal 2: Web

```powershell
dotnet run --project src/ClinicFlowAi.Web/ClinicFlowAi.Web.csproj
```

### Terminal 3: Agent Gateway

```powershell
dotnet run --project src/ClinicFlowAi.AgentGateway/ClinicFlowAi.AgentGateway.csproj
```

## Verify Each Host

### API checks

```powershell
Invoke-RestMethod http://localhost:5071/health
```

```powershell
Invoke-RestMethod "http://localhost:5071/availability?ClinicId=clinic-1&ClinicianId=clinician-1&WindowStartUtc=2026-08-11T00:00:00Z&WindowEndUtc=2026-08-12T00:00:00Z&AppointmentTypeCode=exam"
```

### Web checks

Open `http://localhost:5072` in a browser and select `Load availability`.

You can also verify the web façade directly:

```powershell
Invoke-RestMethod http://localhost:5072/health
```

```powershell
Invoke-RestMethod "http://localhost:5072/availability?ClinicId=clinic-1&ClinicianId=clinician-1&WindowStartUtc=2026-08-11T00:00:00Z&WindowEndUtc=2026-08-12T00:00:00Z&AppointmentTypeCode=exam"
```

### Agent gateway checks

```powershell
Invoke-RestMethod http://localhost:5073/agents/health
```

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5073/agents/booking/check-availability -ContentType application/json -Body '{"clinicId":"clinic-1","clinicianId":"clinician-1","windowStartUtc":"2026-08-11T00:00:00Z","windowEndUtc":"2026-08-12T00:00:00Z","appointmentTypeCode":"exam"}'
```

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5073/agents/faq/answer -ContentType application/json -Body '{"question":"How do I change an appointment?"}'
```

## Local Request Flow

For the current local browser path:

1. The browser loads `ClinicFlowAi.Web` on port `5072`.
2. The page requests `/availability` from the same origin.
3. `ClinicFlowAi.Web` forwards that request to `ClinicFlowAi.Api` on port `5071`.
4. The API returns slot data to the web host, which returns it to the browser.

## Current Scope Notes

- Local availability responses are generated from in-memory domain rules in the API.
- No PostgreSQL connection string is needed for the current local run path.
- The infrastructure and persistence projects are present for future phases, but they are not part of the active local runtime loop yet.
- The gateway check-availability route is now the first real agent-facing endpoint and should be the primary entry point for dev validation.
