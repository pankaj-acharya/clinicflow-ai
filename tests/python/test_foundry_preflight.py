import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest

from azure.ai.projects.models import DeploymentType
from azure.core.exceptions import ClientAuthenticationError, ResourceNotFoundError


def _load_module(relative_path: str, module_name: str):
    repo_root = Path(__file__).resolve().parents[2]
    module_path = repo_root / relative_path
    spec = importlib.util.spec_from_file_location(module_name, module_path)
    module = importlib.util.module_from_spec(spec)
    assert spec is not None and spec.loader is not None
    spec.loader.exec_module(module)
    return module


class FoundryPreflightTests(unittest.TestCase):
    def setUp(self) -> None:
        self.module = _load_module("infra/scripts/preflight_deploy.py", "preflight_deploy_test_module")

    def test_validate_foundry_model_deployment_returns_deployment(self) -> None:
        deployment = SimpleNamespace(
            type=DeploymentType.MODEL_DEPLOYMENT,
            model_name="gpt-4.1-mini",
            model_version="2025-01-01",
            model_publisher="microsoft",
        )

        class FakeDeployments:
            def __init__(self) -> None:
                self.calls = []

            def get(self, name: str):
                self.calls.append(name)
                return deployment

        fake_deployments = FakeDeployments()

        class FakeClient:
            def __init__(self, *args, **kwargs) -> None:
                self.deployments = fake_deployments

            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, tb):
                return False

        original_client = self.module.AIProjectClient
        original_credential = self.module.DefaultAzureCredential
        self.module.AIProjectClient = FakeClient
        self.module.DefaultAzureCredential = lambda: object()
        try:
            result = self.module._validate_foundry_model_deployment(
                "https://example.services.ai.azure.com/api/projects/demo",
                "gpt-4.1-mini",
            )
        finally:
            self.module.AIProjectClient = original_client
            self.module.DefaultAzureCredential = original_credential

        self.assertIs(result, deployment)
        self.assertEqual(fake_deployments.calls, ["gpt-4.1-mini"])

    def test_validate_foundry_model_deployment_fails_when_missing(self) -> None:
        class MissingDeploymentError(ResourceNotFoundError):
            pass

        class FakeDeployments:
            def get(self, name: str):
                raise MissingDeploymentError("missing")

        class FakeClient:
            def __init__(self, *args, **kwargs) -> None:
                self.deployments = FakeDeployments()

            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, tb):
                return False

        original_client = self.module.AIProjectClient
        original_credential = self.module.DefaultAzureCredential
        self.module.AIProjectClient = FakeClient
        self.module.DefaultAzureCredential = lambda: object()
        try:
            with self.assertRaises(self.module.PreflightError) as ctx:
                self.module._validate_foundry_model_deployment(
                    "https://example.services.ai.azure.com/api/projects/demo",
                    "missing-deployment",
                )
        finally:
            self.module.AIProjectClient = original_client
            self.module.DefaultAzureCredential = original_credential

        self.assertIn("missing-deployment", str(ctx.exception))
        self.assertIn("was not found", str(ctx.exception))

    def test_validate_foundry_model_deployment_fails_when_access_denied(self) -> None:
        class AccessDeniedError(ClientAuthenticationError):
            pass

        class FakeDeployments:
            def get(self, name: str):
                raise AccessDeniedError("denied")

        class FakeClient:
            def __init__(self, *args, **kwargs) -> None:
                self.deployments = FakeDeployments()

            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, tb):
                return False

        original_client = self.module.AIProjectClient
        original_credential = self.module.DefaultAzureCredential
        self.module.AIProjectClient = FakeClient
        self.module.DefaultAzureCredential = lambda: object()
        try:
            with self.assertRaises(self.module.PreflightError) as ctx:
                self.module._validate_foundry_model_deployment(
                    "https://example.services.ai.azure.com/api/projects/demo",
                    "restricted-deployment",
                )
        finally:
            self.module.AIProjectClient = original_client
            self.module.DefaultAzureCredential = original_credential

        self.assertIn("restricted-deployment", str(ctx.exception))
        self.assertIn("cannot authenticate or lacks access", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
