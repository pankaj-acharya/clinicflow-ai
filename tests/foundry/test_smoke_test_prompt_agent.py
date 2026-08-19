import importlib.util
from pathlib import Path
import unittest


MODULE_PATH = Path(__file__).resolve().parents[2] / "foundry" / "scripts" / "smoke_test_prompt_agent.py"


def load_module():
    spec = importlib.util.spec_from_file_location("smoke_test_prompt_agent", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class FakeConversation:
    def __init__(self, conversation_id="conversation-1"):
        self.id = conversation_id


class FakeResponse:
    def __init__(self, response_id="response-1", output_text="ok"):
        self.id = response_id
        self.output_text = output_text


class FakeConversations:
    def __init__(self):
        self.created_items = None
        self.deleted_conversation_id = None

    def create(self, items):
        self.created_items = items
        return FakeConversation()

    def delete(self, conversation_id):
        self.deleted_conversation_id = conversation_id


class FakeResponses:
    def __init__(self):
        self.requested_conversation_id = None

    def create(self, conversation):
        self.requested_conversation_id = conversation
        return FakeResponse()


class FakeOpenAIClient:
    def __init__(self):
        self.conversations = FakeConversations()
        self.responses = FakeResponses()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class FakeProjectClient:
    def __init__(self):
        self.requested_agent_name = None
        self.openai_client = FakeOpenAIClient()

    def get_openai_client(self, agent_name):
        self.requested_agent_name = agent_name
        return self.openai_client

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class FakeCredential:
    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False


class SmokeTestPromptAgentTests(unittest.TestCase):
    def setUp(self):
        self.module = load_module()
        self.original_credential = self.module.DefaultAzureCredential
        self.original_client = self.module.AIProjectClient

    def tearDown(self):
        self.module.DefaultAzureCredential = self.original_credential
        self.module.AIProjectClient = self.original_client

    def test_run_smoke_test_invokes_agent_once(self):
        fake_project_client = FakeProjectClient()
        self.module.DefaultAzureCredential = lambda: FakeCredential()
        self.module.AIProjectClient = lambda **kwargs: fake_project_client

        self.module._run_smoke_test("https://example", "clinicflow-booking-assistant", "Reply with ok.")

        self.assertEqual(fake_project_client.requested_agent_name, "clinicflow-booking-assistant")
        self.assertEqual(
            fake_project_client.openai_client.conversations.created_items,
            [{"type": "message", "role": "user", "content": "Reply with ok."}],
        )
        self.assertEqual(
            fake_project_client.openai_client.responses.requested_conversation_id,
            "conversation-1",
        )
        self.assertEqual(
            fake_project_client.openai_client.conversations.deleted_conversation_id,
            "conversation-1",
        )


if __name__ == "__main__":
    unittest.main()
