# Repository Agent Workflow

## Working rules

- Inspect the repository before changing code.
- Prefer small, verifiable increments.
- Keep the repository buildable at every stage.
- Add or update tests with every functional change.
- Never write directly to the database from an LLM or agent; use typed application APIs.
- Preserve audit history and avoid logging sensitive patient data.

## Implementation flow

1. Identify the smallest complete slice.
2. Implement code.
3. Add tests.
4. Run formatting, build, and tests.
5. Fix failures before moving on.

