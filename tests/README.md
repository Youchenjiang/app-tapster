# Tapster Test Suite

This directory contains automated unit and regression tests for Tapster.

## Structure

```
tests/
  Tapster.Core.Tests/
    KeyboardTests.cs            # Tests virtual key code mapping and canonical naming
    MacroModelTests.cs          # Tests MacroAction data models, enums, and JSON roundtrip
    MacroTimingAndSpeedTests.cs # Tests speed multiplier scaling and MacroRecorder lifecycle
    Tapster.Core.Tests.csproj   # xUnit test project targeting net8.0-windows
  README.md
```

## Running Tests Locally

Run the complete test suite from repository root:

```powershell
dotnet test Tapster.sln -c Release --logger "console;verbosity=normal"
```

To run only the core test project:

```powershell
dotnet test tests/Tapster.Core.Tests/Tapster.Core.Tests.csproj -c Release
```

> [!IMPORTANT]
> Always execute build server shutdown after running builds or tests locally to maintain clean worker hygiene on Windows:
> ```powershell
> dotnet build-server shutdown
> ```

## Test Philosophy & Design

1. **Deterministic Execution**:
   - Tests do not rely on timing race conditions or active desktop focus.
   - Delay scaling tests evaluate the mathematical contract rather than spinning physical sleeps.

2. **Safe Headless Testing**:
   - Unit tests strictly test pure functions, data models, serialization contracts, and state machines.
   - Physical keyboard/mouse hardware simulation (`SendInput`) is isolated from unit test runs to prevent ghost clicks on developer machines or CI runners.

3. **CI Automation**:
   - The test suite is executed on every push and pull request via the GitHub Actions CI pipeline (`.github/workflows/ci.yml`).
