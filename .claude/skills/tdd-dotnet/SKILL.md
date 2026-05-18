---
name: tdd-dotnet
description: Implement a .NET feature using strict Red-Green-Refactor TDD with xUnit, FluentAssertions, and Moq for PlaylistMiner backend.
---

# TDD Feature Implementation (.NET)

Implement the requested feature using strict Red-Green-Refactor TDD.

## Step 1: Red — Write Failing Tests

1. Create test class in `tests/PlaylistMiner.UnitTests/` matching the source file path
2. Name tests: `Test_MethodName_Scenario_ExpectedResult`
3. Use FluentAssertions: `.Should().Be()`, `.Should().Contain()`, `.Should().Throw<>()`
4. Use Moq for dependencies: `var mock = new Mock<IService>()`
5. Follow Arrange-Act-Assert pattern
6. Run `dotnet test` — confirm tests FAIL

## Step 2: Green — Minimal Implementation

1. Write the minimum code to make tests pass
2. No refactoring, no optimization, no extras
3. Run `dotnet test` — confirm tests PASS

## Step 3: Refactor

1. Clean up implementation keeping tests green
2. Extract interfaces, simplify, reduce duplication
3. Run `dotnet test` — confirm tests still PASS

## Rules

- NEVER write implementation before the test
- All async methods accept CancellationToken
- Return DTOs from Core project, never EF entities
- Use `[Category("Unit")]` for unit, `[Category("Integration")]` for integration tests
- Integration tests use Testcontainers with real PostgreSQL
