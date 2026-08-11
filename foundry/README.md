# Foundry Artifacts

This folder contains the repository-stored Microsoft Foundry agent manifest package.

Current contents:

- [agent.manifest.yaml](agent.manifest.yaml) - hosted-agent manifest template for the ClinicFlow booking assistant.
- [scripts/deploy_prompt_agent.py](scripts/deploy_prompt_agent.py) - CI deployment script that creates a new prompt-agent version in Foundry.

The CI pipeline validates this manifest and performs a real Foundry deployment by creating a new prompt-agent version.

Required GitHub repository secrets for the Foundry deploy stage:

- `FOUNDRY_PROJECT_ENDPOINT`
- `FOUNDRY_MODEL_DEPLOYMENT_NAME`
- `FOUNDRY_AGENT_INSTRUCTIONS` (optional, can be empty)
- `CLINICFLOW_API_BASE_URL` (optional)
- `CLINICFLOW_GATEWAY_BASE_URL` (optional)