# Architecture Decision Log

## 2026-08-10

### Backend choice

Decision: use ASP.NET Core Minimal APIs on .NET 10.

Rationale: it is the lightest production-shaped backend option in the requested stack and works well for a modular monolith with strongly typed endpoints.

### .NET baseline upgrade

Decision: standardize the repository on .NET 10.

Rationale: the machine now has .NET 10 SDK support, and the solution builds and tests successfully on `net10.0`.

### Frontend choice

Decision: use React with TypeScript.

Rationale: no existing frontend framework is present, and this matches the requested direction.

### Persistence choice

Decision: use PostgreSQL as the system of record for appointment state.

Rationale: the domain needs transactional constraints, concurrency control, and durable scheduling data.
