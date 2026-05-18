# PlaylistMiner.Api — Agent Instructions

## Role
You are working on the ASP.NET Core Web API for PlaylistMiner, a YouTube playlist organizer.

## Rules
1. **TDD First:** Write the failing xUnit test before any implementation code. Red → Green → Refactor.
2. **No domain logic in controllers.** Controllers call services, services contain logic.
3. **Always use CancellationToken** on async methods.
4. **Return DTOs, never EF entities.** DTOs are in PlaylistMiner.Core.
5. **Use FluentAssertions** in tests (`.Should().Be()`).
6. **Parameterize all SQL.** Never concatenate user input into queries.
7. **Add `[ProducesResponseType]`** attributes to every controller action.
8. **Use ProblemDetails** for all error responses (400, 404, 409, 410).

## Project References
- This project → Core + Infrastructure + ServiceDefaults
- Core has domain models, interfaces, DTOs (zero dependencies)
- Infrastructure has EF Core, YouTube client, repositories

## Testing
- Controller tests: use `WebApplicationFactory<Program>` with test database
- Service tests: use Moq to mock repositories
- Test file location: `tests/PlaylistMiner.UnitTests/` and `tests/PlaylistMiner.IntegrationTests/`
- Naming: `Test_MethodName_Scenario_ExpectedResult`

## When Adding an Endpoint
1. Write controller test (Red)
2. Write service test (Red)
3. Implement service (Green)
4. Implement controller (Green)
5. Verify Swagger shows the endpoint with correct schemas
