#!/usr/bin/env python3
import os
import sys

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential


def _require_env(name: str) -> str:
    value = os.getenv(name, "").strip()
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def _run_smoke_test(project_endpoint: str, agent_name: str, smoke_prompt: str) -> None:
    print("=== Foundry smoke test ===")
    print(f"- Agent: {agent_name}")

    try:
        with (
            DefaultAzureCredential() as credential,
            AIProjectClient(endpoint=project_endpoint, credential=credential, allow_preview=True) as project_client,
            project_client.get_openai_client(agent_name=agent_name) as openai_client,
        ):
            conversation = openai_client.conversations.create(
                items=[{"type": "message", "role": "user", "content": smoke_prompt}],
            )
            try:
                response = openai_client.responses.create(conversation=conversation.id)
                output_text = (getattr(response, "output_text", "") or "").strip()
                if not output_text:
                    raise RuntimeError("Smoke test response completed but did not return any text.")

                print(
                    "Foundry smoke test passed "
                    f"(conversation={conversation.id}, response={getattr(response, 'id', 'unknown')})"
                )
            finally:
                try:
                    openai_client.conversations.delete(conversation_id=conversation.id)
                except Exception as cleanup_exc:
                    print(f"Smoke-test conversation cleanup skipped: {cleanup_exc}")
    except Exception as exc:
        raise RuntimeError(
            f"Foundry smoke test failed for agent '{agent_name}' at {project_endpoint}: {exc}"
        ) from exc


def main() -> None:
    project_endpoint = _require_env("FOUNDRY_PROJECT_ENDPOINT")
    agent_name = os.getenv("FOUNDRY_AGENT_NAME", "clinicflow-booking-assistant").strip() or "clinicflow-booking-assistant"
    smoke_prompt = os.getenv("FOUNDRY_SMOKE_TEST_PROMPT", "Reply with the single word ok.").strip()
    _run_smoke_test(project_endpoint, agent_name, smoke_prompt)


if __name__ == "__main__":
    try:
        main()
    except RuntimeError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
