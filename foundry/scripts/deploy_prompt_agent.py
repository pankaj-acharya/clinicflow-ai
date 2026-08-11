import json
import os
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition
from azure.identity import DefaultAzureCredential


def _require_env(name: str) -> str:
    value = os.getenv(name, "").strip()
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def _build_instructions() -> str:
    provided = os.getenv("FOUNDRY_AGENT_INSTRUCTIONS", "").strip()
    if provided:
        return provided

    api_base_url = os.getenv("CLINICFLOW_API_BASE_URL", "").strip()
    gateway_base_url = os.getenv("CLINICFLOW_GATEWAY_BASE_URL", "").strip()

    lines = [
        "You are the ClinicFlow booking assistant.",
        "Answer clearly and briefly.",
        "Never reveal secrets, tokens, or internal-only configuration.",
    ]

    if api_base_url:
        lines.append(f"ClinicFlow API base URL: {api_base_url}")
    if gateway_base_url:
        lines.append(f"ClinicFlow Agent Gateway base URL: {gateway_base_url}")

    return "\n".join(lines)


def main() -> None:
    project_endpoint = _require_env("FOUNDRY_PROJECT_ENDPOINT")
    model_deployment_name = _require_env("FOUNDRY_MODEL_DEPLOYMENT_NAME")
    agent_name = os.getenv("FOUNDRY_AGENT_NAME", "clinicflow-booking-assistant").strip() or "clinicflow-booking-assistant"

    definition = PromptAgentDefinition(
        kind="prompt",
        model=model_deployment_name,
        instructions=_build_instructions(),
        temperature=0.2,
    )

    credential = DefaultAzureCredential()
    with AIProjectClient(endpoint=project_endpoint, credential=credential) as project_client:
        agent_version = project_client.agents.create_version(
            agent_name=agent_name,
            definition=definition,
        )

    deployment_result = {
        "agent_name": agent_version.name,
        "agent_version": agent_version.version,
        "model_deployment": model_deployment_name,
        "project_endpoint": project_endpoint,
    }

    output_path = Path("foundry") / "foundry-deployment-result.json"
    output_path.write_text(json.dumps(deployment_result, indent=2), encoding="utf-8")
    print(json.dumps(deployment_result, indent=2))


if __name__ == "__main__":
    main()