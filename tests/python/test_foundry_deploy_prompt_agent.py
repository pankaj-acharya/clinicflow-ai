import importlib.util
from contextlib import AbstractContextManager
from pathlib import Path
import unittest


def _load_module(relative_path: str, module_name: str):
    repo_root = Path(__file__).resolve().parents[2]
    module_path = repo_root / relative_path
    spec = importlib.util.spec_from_file_location(module_name, module_path)
    module = importlib.util.module_from_spec(spec)
    assert spec is not None and spec.loader is not None
    spec.loader.exec_module(module)
    return module


class FakeOpenAIClient:
    def __init__(self) -> None:
        self.calls = []
        self.responses = self

    def create(self, **kwargs):
        self.calls.append(kwargs)
        return type("Response", (), {"id": "resp-123"})()


class FakeOpenAIContext(AbstractContextManager):
    def __init__(self, client: FakeOpenAIClient) -> None:
        self.client = client

    def __enter__(self):
        return self.client

    def __exit__(self, exc_type, exc, tb):
        return False


class FakeProjectClient:
    def __init__(self, client: FakeOpenAIClient) -> None:
        self._client = client

    def get_openai_client(self):
        return FakeOpenAIContext(self._client)


class FoundryDeployPromptAgentTests(unittest.TestCase):
    def setUp(self) -> None:
        self.module = _load_module("foundry/scripts/deploy_prompt_agent.py", "deploy_prompt_agent_test_module")

    def test_run_smoke_test_uses_model_deployment_name(self) -> None:
        openai_client = FakeOpenAIClient()
        project_client = FakeProjectClient(openai_client)

        response_id = self.module._run_smoke_test(project_client, "gpt-4.1-mini")

        self.assertEqual(response_id, "resp-123")
        self.assertEqual(
            openai_client.calls,
            [
                {
                    "model": "gpt-4.1-mini",
                    "input": "Reply with OK.",
                    "max_output_tokens": 8,
                    "temperature": 0,
                }
            ],
        )


if __name__ == "__main__":
    unittest.main()
