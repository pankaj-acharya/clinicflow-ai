import importlib.util
from pathlib import Path
import unittest


MODULE_PATH = Path(__file__).resolve().parents[2] / "infra" / "scripts" / "preflight_deploy.py"


def load_module():
    spec = importlib.util.spec_from_file_location("preflight_deploy", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class FakeDeploymentError(Exception):
    def __init__(self, status_code: int, message: str):
        super().__init__(message)
        self.status_code = status_code


class FakeDeployments:
    def __init__(self, *, result=None, error=None):
        self.result = result
        self.error = error
        self.requested_name = None

    def get(self, name):
        self.requested_name = name
        if self.error is not None:
            raise self.error
        return self.result


class FakeProjectClient:
    def __init__(self, deployments):
        self.deployments = deployments

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class FakeCredential:
    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class PreflightFoundryValidationTests(unittest.TestCase):
    def setUp(self):
        self.module = load_module()
        self.original_credential = self.module.DefaultAzureCredential
        self.original_client = self.module.AIProjectClient

    def tearDown(self):
        self.module.DefaultAzureCredential = self.original_credential
        self.module.AIProjectClient = self.original_client

    def test_verify_foundry_model_deployment_succeeds_when_present(self):
        deployments = FakeDeployments(result=object())
        self.module.DefaultAzureCredential = lambda: FakeCredential()
        self.module.AIProjectClient = lambda **kwargs: FakeProjectClient(deployments)

        self.module._verify_foundry_model_deployment("https://example", "demo-model")

        self.assertEqual(deployments.requested_name, "demo-model")

    def test_verify_foundry_model_deployment_fails_fast_when_missing(self):
        deployments = FakeDeployments(error=FakeDeploymentError(404, "missing"))
        self.module.DefaultAzureCredential = lambda: FakeCredential()
        self.module.AIProjectClient = lambda **kwargs: FakeProjectClient(deployments)

        with self.assertRaises(self.module.PreflightError) as ctx:
            self.module._verify_foundry_model_deployment("https://example", "demo-model")

        self.assertIn("demo-model", str(ctx.exception))
        self.assertIn("https://example", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
