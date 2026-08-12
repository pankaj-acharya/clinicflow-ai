import json
import os
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition
from azure.identity import DefaultAzureCredential
from azure.monitor.opentelemetry import configure_azure_monitor


def _require_env(name: str) -> str:
    value = os.getenv(name, "").strip()
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def _optional_env(name: str, default: str = "") -> str:
    return os.getenv(name, default).strip()


def _should_enable_insights() -> bool:
    """Check if AppInsights logging is enabled via environment variable."""
    enabled_str = _optional_env("ENABLE_FOUNDRY_INSIGHTS_LOGGING", "false").lower()
    return enabled_str in ("true", "1", "yes", "on")


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


def _deployment_identity_hint() -> str:
    client_id = os.getenv("AZURE_CLIENT_ID", "").strip()
    if client_id:
        return f"Azure client ID {client_id}"

    return "the current Azure deployment identity"


def main() -> None:
    project_endpoint = _require_env("FOUNDRY_PROJECT_ENDPOINT")
    model_deployment_name = _require_env("FOUNDRY_MODEL_DEPLOYMENT_NAME")
    agent_name = os.getenv("FOUNDRY_AGENT_NAME", "clinicflow-booking-assistant").strip() or "clinicflow-booking-assistant"

    # Initialize AppInsights if enabled
    if _should_enable_insights():
        instrumentation_key = _optional_env("APPINSIGHTS_INSTRUMENTATION_KEY")
        if instrumentation_key:
            try:
                configure_azure_monitor(instrumentation_key=instrumentation_key)
                print(f"✅ AppInsights telemetry enabled for Foundry agent (key: {instrumentation_key[:20]}...)")
            except Exception as e:
                print(f"⚠️ Warning: Failed to configure AppInsights: {e}")
        else:
            print("⚠️ AppInsights logging enabled but no instrumentation key provided")
    else:
        print("ℹ️ AppInsights logging disabled for Foundry agent (set ENABLE_FOUNDRY_INSIGHTS_LOGGING=true to enable)")

    definition = PromptAgentDefinition(
        kind="prompt",
        model=model_deployment_name,
        instructions=_build_instructions(),
        temperature=0.2,
    )

    credential = DefaultAzureCredential()
    try:
        with AIProjectClient(endpoint=project_endpoint, credential=credential) as project_client:
            agent_version = project_client.agents.create_version(
                agent_name=agent_name,
                definition=definition,
            )
    except Exception as exc:
        raise RuntimeError(
            "Foundry deployment failed. "
            f"Ensure {_deployment_identity_hint()} has write-capable access to {project_endpoint} "
            "such as Contributor or a tenant-specific Foundry author role before rerunning the workflow."
        ) from exc

    deployment_result = {
        "agent_name": agent_version.name,
        "agent_version": agent_version.version,
        "model_deployment": model_deployment_name,
        "project_endpoint": project_endpoint,
        "appinsights_enabled": _should_enable_insights(),
    }

    output_path = Path("foundry") / "foundry-deployment-result.json"
    output_path.write_text(json.dumps(deployment_result, indent=2), encoding="utf-8")
    print(json.dumps(deployment_result, indent=2))


if __name__ == "__main__":
    main()
