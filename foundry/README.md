# Foundry Artifacts

This folder contains the repository-stored Microsoft Foundry agent manifest package.

Current contents:

- [agent.manifest.yaml](agent.manifest.yaml) - hosted-agent manifest template for the ClinicFlow booking assistant.
- [scripts/deploy_prompt_agent.py](scripts/deploy_prompt_agent.py) - CI deployment script that creates a new prompt-agent version in Foundry.

The CI pipeline validates this manifest, fails fast if the configured model deployment is missing, and performs a real Foundry deployment by creating a new prompt-agent version.

Before the first deployment run, the GitHub Actions deployment identity must already have write-capable access to the target Foundry / Azure AI project scope, such as `Contributor` or an equivalent tenant-specific Foundry author role. The workflow now validates the configured Foundry endpoint before deployment and fails with explicit remediation if that bootstrap permission is missing.

Required GitHub repository secrets for the Foundry deploy stage:

- `FOUNDRY_PROJECT_ENDPOINT`
- `FOUNDRY_MODEL_DEPLOYMENT_NAME`
- `FOUNDRY_AGENT_INSTRUCTIONS` (optional, can be empty)
- `CLINICFLOW_API_BASE_URL` (optional)
- `CLINICFLOW_GATEWAY_BASE_URL` (optional)

After deployment, the workflow runs a smoke test that only checks the agent call succeeds.