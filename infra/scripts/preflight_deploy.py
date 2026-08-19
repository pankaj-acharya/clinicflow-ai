#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential


REQUIRED_PROVIDERS = [
    "Microsoft.App",
    "Microsoft.CognitiveServices",
    "Microsoft.ContainerRegistry",
    "microsoft.insights",
    "Microsoft.KeyVault",
    "Microsoft.ManagedIdentity",
    "Microsoft.OperationalInsights",
    "Microsoft.Storage",
]

ROLE_BASED_ACCESS_CONTROL_ADMIN_ROLES = {
    "Owner",
    "Role Based Access Control Administrator",
    "User Access Administrator",
}


class PreflightError(RuntimeError):
    pass


def _run(command: list[str], *, expect_json: bool = False, allow_failure: bool = False) -> object:
    completed = subprocess.run(command, capture_output=True, text=True)
    if completed.returncode != 0:
        if allow_failure:
            return None

        stderr = completed.stderr.strip() or completed.stdout.strip()
        raise PreflightError(f"Command failed: {' '.join(command)}\n{stderr}")

    output = completed.stdout.strip()
    if expect_json:
        return json.loads(output or "null")

    return output


def _az(command: list[str], *, expect_json: bool = False, allow_failure: bool = False) -> object:
    return _run(["az", *command], expect_json=expect_json, allow_failure=allow_failure)


def _write_summary(lines: list[str]) -> None:
    summary_path = os.getenv("GITHUB_STEP_SUMMARY", "").strip()
    if not summary_path:
        return

    with Path(summary_path).open("a", encoding="utf-8") as summary_file:
        summary_file.write("\n".join(lines) + "\n")


def _get_subscription() -> dict[str, object]:
    account = _az(["account", "show", "-o", "json"], expect_json=True)
    if not isinstance(account, dict):
        raise PreflightError("Unable to read the active Azure account.")

    return account


def _resolve_principal() -> dict[str, str]:
    client_id = os.getenv("AZURE_CLIENT_ID", "").strip()
    if client_id:
        service_principal = _az(["ad", "sp", "show", "--id", client_id, "-o", "json"], expect_json=True, allow_failure=True)
        if isinstance(service_principal, dict):
            return {
                "display_name": str(service_principal.get("displayName", client_id)),
                "object_id": str(service_principal.get("id", "")),
                "kind": "service-principal",
            }

    account = _get_subscription()
    user = account.get("user") if isinstance(account, dict) else None
    if isinstance(user, dict):
        name = str(user.get("name", "current-principal"))
        return {"display_name": name, "object_id": name, "kind": str(user.get("type", "principal")).lower()}

    raise PreflightError("Unable to determine the current deployment principal.")


def _ensure_providers_registered() -> list[tuple[str, str]]:
    states: list[tuple[str, str]] = []

    for provider in REQUIRED_PROVIDERS:
        initial_state = str(_az(["provider", "show", "--namespace", provider, "--query", "registrationState", "-o", "tsv"]))
        if initial_state != "Registered":
            print(f"Registering Azure resource provider {provider} (current state: {initial_state})...")
            _az(["provider", "register", "--namespace", provider, "--wait", "--only-show-errors"])

        final_state = str(_az(["provider", "show", "--namespace", provider, "--query", "registrationState", "-o", "tsv"]))
        if final_state != "Registered":
            raise PreflightError(
                f"Azure resource provider {provider} is still '{final_state}'. "
                "The bootstrap identity must be allowed to register required Azure providers."
            )

        states.append((provider, final_state))

    return states


def _load_terraform_outputs(path: str) -> dict[str, object]:
    outputs = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(outputs, dict):
        raise PreflightError(f"Terraform outputs at {path} were not a JSON object.")

    return outputs


def _output_value(outputs: dict[str, object], name: str) -> str:
    value = outputs.get(name)
    if not isinstance(value, dict) or "value" not in value:
        raise PreflightError(f"Missing Terraform output '{name}'.")

    return str(value["value"])


def _load_foundry_sdk() -> tuple[object, object, type[BaseException], type[BaseException], type[BaseException], object]:
    from azure.ai.projects import AIProjectClient
    from azure.ai.projects.models import DeploymentType
    from azure.core.exceptions import ClientAuthenticationError, HttpResponseError, ResourceNotFoundError
    from azure.identity import DefaultAzureCredential

    return AIProjectClient, DeploymentType, ClientAuthenticationError, HttpResponseError, ResourceNotFoundError, DefaultAzureCredential


def _is_ready_foundry_deployment(deployment: object) -> bool:
    ready_states = {"ready", "succeeded", "success", "active", "healthy", "online"}
    not_ready_states = {"creating", "provisioning", "updating", "pending", "starting", "deploying", "failed", "error", "cancelled", "canceled"}

    candidates = [
        getattr(deployment, "status", None),
        getattr(deployment, "state", None),
        getattr(getattr(deployment, "properties", None), "status", None),
        getattr(getattr(deployment, "properties", None), "provisioning_state", None),
        getattr(getattr(deployment, "properties", None), "provisioningState", None),
    ]

    for candidate in candidates:
        if candidate is None:
            continue

        normalized = str(candidate).strip().lower()
        if not normalized:
            continue
        if normalized in ready_states:
            return True
        if normalized in not_ready_states:
            return False

    return False


def _validate_foundry_model_deployment(project_endpoint: str, model_deployment_name: str) -> object:
    AIProjectClient, DeploymentType, ClientAuthenticationError, HttpResponseError, ResourceNotFoundError, DefaultAzureCredential = _load_foundry_sdk()

    try:
        with AIProjectClient(endpoint=project_endpoint, credential=DefaultAzureCredential()) as project_client:
            deployment = project_client.deployments.get(model_deployment_name)
    except ResourceNotFoundError as exc:
        raise PreflightError(
            f"Foundry model deployment '{model_deployment_name}' was not found at {project_endpoint}. "
            "Create it first or update FOUNDRY_MODEL_DEPLOYMENT_NAME to an existing deployment."
        ) from exc
    except ClientAuthenticationError as exc:
        raise PreflightError(
            f"Unable to read Foundry model deployment '{model_deployment_name}' at {project_endpoint} because the "
            "deployment identity cannot authenticate or lacks access. Grant write-capable Foundry / Azure AI access "
            "and rerun the workflow."
        ) from exc
    except HttpResponseError as exc:
        status_code = getattr(getattr(exc, "response", None), "status_code", getattr(exc, "status_code", None))
        if status_code in (401, 403):
            raise PreflightError(
                f"Unable to read Foundry model deployment '{model_deployment_name}' at {project_endpoint} because "
                "the deployment identity cannot authenticate or lacks access. Grant write-capable Foundry / Azure AI "
                "access and rerun the workflow."
            ) from exc

        raise PreflightError(
            f"Unable to validate Foundry model deployment '{model_deployment_name}' at {project_endpoint}: {exc}"
        ) from exc

    if getattr(deployment, "type", None) != DeploymentType.MODEL_DEPLOYMENT:
        raise PreflightError(
            f"Foundry resource '{model_deployment_name}' at {project_endpoint} exists but is not a model deployment."
        )

    if not _is_ready_foundry_deployment(deployment):
        raise PreflightError(
            f"Foundry model deployment '{model_deployment_name}' exists at {project_endpoint}, but it is not ready. "
            "Wait for the deployment to finish provisioning or update FOUNDRY_MODEL_DEPLOYMENT_NAME to a ready model deployment."
        )

    return deployment

def _role_assignments(scope: str) -> list[dict[str, object]]:
    assignments = _az(
        ["role", "assignment", "list", "--scope", scope, "-o", "json"],
        expect_json=True,
    )
    if not isinstance(assignments, list):
        raise PreflightError(f"Role assignment lookup for scope {scope} did not return a list.")

    return assignments


def _principal_roles(assignments: list[dict[str, object]], principal_id: str) -> set[str]:
    roles = set()
    for assignment in assignments:
        if str(assignment.get("principalId", "")) == principal_id:
            roles.add(str(assignment.get("roleDefinitionName", "")))

    return roles


def _require_role(principal_label: str, principal_id: str, roles: set[str], required_roles: set[str], remediation: str) -> None:
    if roles.intersection(required_roles):
        return

    current_roles = ", ".join(sorted(role for role in roles if role)) or "none"
    expected_roles = ", ".join(sorted(required_roles))
    raise PreflightError(
        f"{principal_label} ({principal_id}) is missing one of the required roles: {expected_roles}. "
        f"Current roles at scope: {current_roles}. {remediation}"
    )


def _bootstrap_mode() -> None:
    account = _get_subscription()
    principal = _resolve_principal()
    provider_states = _ensure_providers_registered()

    summary_lines = [
        "## Deployment bootstrap preflight",
        f"- Subscription: `{account.get('id', 'unknown')}`",
        f"- Deployment principal: `{principal['display_name']}` ({principal['kind']})",
        f"- Deployment principal object id: `{principal['object_id']}`",
        "- Required Azure providers:",
    ]
    summary_lines.extend([f"  - `{provider}`: {state}" for provider, state in provider_states])
    summary_lines.extend(
        [
            "- Bootstrap boundary:",
            "  - The deployment principal must be able to create Azure resources in the target subscription or resource group.",
            "  - The deployment principal must be able to create Azure role assignments on newly created ACR resources.",
            "  - The deployment principal must already have write-capable access to the target Foundry / Azure AI project scope.",
        ]
    )

    _write_summary(summary_lines)
    print("\n".join(summary_lines))


def _post_base_mode(terraform_outputs_path: str) -> None:
    outputs = _load_terraform_outputs(terraform_outputs_path)
    acr_scope = _output_value(outputs, "container_registry_id")
    deployment_principal_id = _output_value(outputs, "deployment_principal_object_id")
    managed_identity_principal_id = _output_value(outputs, "managed_identity_principal_id")

    assignments = _role_assignments(acr_scope)
    deployment_roles = _principal_roles(assignments, deployment_principal_id)
    managed_identity_roles = _principal_roles(assignments, managed_identity_principal_id)

    _require_role(
        "Deployment principal",
        deployment_principal_id,
        deployment_roles,
        {"AcrPush"},
        f"Grant AcrPush on {acr_scope} or bootstrap the pipeline identity so Terraform can create that assignment automatically.",
    )
    _require_role(
        "Deployment principal",
        deployment_principal_id,
        deployment_roles,
        ROLE_BASED_ACCESS_CONTROL_ADMIN_ROLES,
        f"Grant User Access Administrator, Role Based Access Control Administrator, or Owner on {acr_scope} or a parent scope.",
    )
    _require_role(
        "Runtime managed identity",
        managed_identity_principal_id,
        managed_identity_roles,
        {"AcrPull"},
        f"Grant AcrPull on {acr_scope} or rerun Terraform after restoring the deployment identity's RBAC assignment permissions.",
    )

    summary_lines = [
        "## Deployment RBAC preflight",
        f"- ACR scope: `{acr_scope}`",
        f"- Deployment principal object id: `{deployment_principal_id}`",
        f"- Deployment principal roles at ACR scope: `{', '.join(sorted(deployment_roles))}`",
        f"- Runtime managed identity principal id: `{managed_identity_principal_id}`",
        f"- Runtime managed identity roles at ACR scope: `{', '.join(sorted(managed_identity_roles))}`",
    ]
    _write_summary(summary_lines)
    print("\n".join(summary_lines))


def _foundry_mode() -> None:
    project_endpoint = os.getenv("FOUNDRY_PROJECT_ENDPOINT", "").strip()
    model_deployment_name = os.getenv("FOUNDRY_MODEL_DEPLOYMENT_NAME", "").strip()
    principal = _resolve_principal()

    if not project_endpoint:
        raise PreflightError("Missing required environment variable: FOUNDRY_PROJECT_ENDPOINT")
    if not model_deployment_name:
        raise PreflightError("Missing required environment variable: FOUNDRY_MODEL_DEPLOYMENT_NAME")

    deployment = _validate_foundry_model_deployment(project_endpoint, model_deployment_name)
    summary_lines = [
        "## Foundry deployment preflight",
        f"- Foundry project endpoint: `{project_endpoint}`",
        f"- Model deployment: `{model_deployment_name}`",
        f"- Validation: found existing `{str(getattr(deployment, 'type', 'unknown'))}` deployment",
        f"- Model deployment validation: ready={str(_is_ready_foundry_deployment(deployment)).lower()}",
        f"- Deployment principal: `{principal['display_name']}` ({principal['kind']})",
        f"- Deployment principal object id: `{principal['object_id']}`",
        "- Required bootstrap permission: write-capable Foundry / Azure AI access at the target project or parent resource scope.",
    ]
    _write_summary(summary_lines)
    print("\n".join(summary_lines))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("bootstrap", "post-base", "foundry"), required=True)
    parser.add_argument("--terraform-outputs", default="")
    args = parser.parse_args()

    try:
        if args.mode == "bootstrap":
            _bootstrap_mode()
        elif args.mode == "post-base":
            if not args.terraform_outputs:
                raise PreflightError("--terraform-outputs is required when --mode=post-base")
            _post_base_mode(args.terraform_outputs)
        else:
            _foundry_mode()
    except PreflightError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
