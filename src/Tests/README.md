# VL.Fuse Tests

This directory contains tests for the VL.Fuse project. The tests are organized by component/namespace to make it easier to locate and run specific tests.

## Directory Structure

- `ComputeSystem/`: Tests for the ComputeSystem namespace components
  - `AttributeMapTests.cs`: Tests for the AttributeMap class
  - (More test files will be added as needed)

## Running Tests

To run all tests:

```bash
cd src/Tests/ComputeSystem
dotnet run
```

To run specific test projects:

```bash
cd src/Tests/[TestDirectory]
dotnet run
```

## Adding New Tests

When adding tests for a new component:

1. Create a new test file in the appropriate directory
2. Add the test class to the TestRunner in that directory
3. Follow the existing pattern of test methods and assertions

## Test Guidelines

- Each test method should focus on testing a single aspect of functionality
- Use descriptive test method names that indicate what is being tested
- Include assertions that verify the expected behavior
- Add console output to indicate test progress and results
