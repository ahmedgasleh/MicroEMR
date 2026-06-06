# MicroEMR Development Rules

Technology:
- ASP.NET Core
- C#
- SQL Server
- Stored procedures only for data changes
- Bootstrap 5 UI
- TypeScript preferred over plain JavaScript

Architecture:
- Clean Architecture style
- Controllers are thin
- Business logic goes into Application layer
- SQL access only through Infrastructure

Coding Rules:
- Use dependency injection
- Use async/await
- Use ILogger
- Use DTOs between layers
- Avoid Entity Framework migrations

Healthcare Rules:
- Every patient data change requires audit logging
- Never physically delete clinical data
- Use soft delete where needed

Security:
- OAuth2/OpenID Connect ready
- Role based permissions