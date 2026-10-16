# Create Implementation Plan - Samples
 
## Sample 1: GOOD Example ✅
 
### Structured, Detailed, Actionable
 
```markdown
# Implementation Plan: Add Structured Logging to UserService
 
> **Status**: Draft 
> **Author**: Development Team 
> **Created**: 2024-08-07 
 
## Overview
 
The `UserService` currently logs errors and important events using basic `Console.WriteLine()`, making it difficult to filter logs in production and correlate events across services. We will introduce structured logging using Microsoft.Extensions.Logging and Serilog to enable correlation tracking, log aggregation, and severity filtering.
 
**Key Benefits:**
- Centralized log aggregation across microservices
- Trace correlation via LogContext and custom correlation IDs
- Structured queries in production (filter by severity, user ID, operation type)
- Compliance with observability standards (structured logging, OpenTelemetry ready)
 
## Architecture Overview
 
The logging infrastructure integrates into the dependency injection container at startup:
 
```
┌─────────────────┐
│   UserService   │
│   (producer)    │
└────────┬────────┘
         │ ILogger<UserService>
         ▼
┌─────────────────────────────────┐
│  ILogger Implementation         │
│  (from DI Container)            │
└────────┬────────────────────────┘
         │
    ┌────┴────┬──────────────────┐
    ▼         ▼                  ▼
┌────────┐ ┌──────────┐  ┌───────────────┐
│Console │ │File      │  │ OpenTelemetry │
│Sink    │ │Sink      │  │               │
└────────┘ └──────────┘  └───────────────┘
```
 
## Current State
 
The `UserService` currently uses `Console.WriteLine()` for logging:
 
```csharp
public class UserService
{
    public void CreateUser(CreateUserRequest request)
    {
        Console.WriteLine("Creating user: " + request.Email);
       
        if (string.IsNullOrEmpty(request.Email))
        {
            Console.WriteLine("ERROR: Email is empty");
            throw new ArgumentException("Email required");
        }
       
        Console.WriteLine("User created: " + request.Email);
    }
}
```
 
**Constraints:**
- No structured format (difficult to parse in log aggregators)
- No correlation IDs (can't trace a single user's request across services)
- No log levels (can't filter by severity in production)
- Console output only (can't be redirected to file/cloud)
- No context (user ID, request ID, environment missing)
 
## Step-by-Step Implementation
 
### Step 1: Add NuGet Dependencies
 
**Files**: `UserService.csproj`
 
**Intention**: 
Install Microsoft.Extensions.Logging and Serilog packages to enable structured logging capabilities. This establishes the foundation for all subsequent logging refactoring.
 
**Benefit**: 
Provides the abstraction layer (ILogger) and structured sink implementations needed for production-grade logging.
 
**Test Specification**:
```csharp
[Fact]
public void UserService_Constructor_Accepts_ILogger()
{
    // Arrange
    var mockLogger = new Mock<ILogger<UserService>>();
   
    // Act & Assert
    var service = new UserService(mockLogger.Object);
    Assert.NotNull(service);
}
```
 
**Implementation**:
```csharp
public class UserService
{
    private readonly ILogger<UserService> _logger;
   
    public UserService(ILogger<UserService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```
 
**Verification**: 
Run `dotnet restore` and verify no build errors. Confirm that `UserService` accepts the logger dependency without throwing exceptions.
 
**Design Rationale**: 
We use Serilog with the Microsoft.Extensions.Logging abstraction to allow swapping sinks without code changes. The console sink is configured here; file/cloud sinks can be added later via configuration.
 
---
 
### Step 2: Inject ILogger into UserService
 
**Files**: `UserService.cs`
 
**Intention**: 
Add a constructor parameter for `ILogger<UserService>` to enable dependency injection of the logging abstraction. This replaces hardcoded `Console.WriteLine()` calls with a testable dependency.
 
**Benefit**: 
Allows the logger to be mocked in tests and configured globally in the DI container.
 
**Test Specification**:
```csharp
[Fact]
public void CreateUser_AcceptsValidRequest()
{
    // Arrange
    var mockLogger = new Mock<ILogger<UserService>>();
    var service = new UserService(mockLogger.Object);
    var request = new CreateUserRequest { Email = "test@example.com" };
   
    // Act
    var result = service.CreateUser(request);
   
    // Assert
    Assert.NotNull(result);
    Assert.Equal("test@example.com", result.Email);
}
```
 
**Implementation**:
```csharp
// Before
public class UserService
{
    public void CreateUser(CreateUserRequest request)
    {
        Console.WriteLine("Creating user: " + request.Email);
        return new User(request.Email);
    }
}
 
// After
public class UserService
{
    private readonly ILogger<UserService> _logger;
   
    public UserService(ILogger<UserService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
   
    public User CreateUser(CreateUserRequest request)
    {
        _logger.LogInformation("Creating user: {Email}", request.Email);
        return new User(request.Email);
    }
}
```
 
**Verification**: 
Instantiate UserService with a mock logger; verify the constructor accepts it without error. Confirm the service logs via the injected logger.
 
**Design Rationale**: 
Using `ILogger<UserService>` creates a named logger (category = class name), enabling per-class log filtering. Structured logging with named parameters (`{Email}`) separates log message from data, enabling queries on the Email field.
 
---
 
### Step 3: Replace All Console Output with Structured Logging
 
**Files**: `UserService.cs`
 
**Intention**: 
Replace all remaining `Console.WriteLine()` calls with `_logger.Log*()` calls at appropriate log levels. This ensures all application events flow through structured logging.
 
**Benefit**: 
Uniform logging across the service; all events become searchable and filterable in log aggregators.
 
**Test Specification**:
```csharp
[Fact]
public void CreateUser_ThrowsArgumentException_WhenEmailEmpty()
{
    // Arrange
    var mockLogger = new Mock<ILogger<UserService>>();
    var service = new UserService(mockLogger.Object);
    var request = new CreateUserRequest { Email = "" };
   
    // Act & Assert
    var exception = Assert.Throws<ArgumentException>(() => service.CreateUser(request));
    Assert.Contains("Email required", exception.Message);
}
 
[Fact]
public void CreateUser_ReturnsUser_WhenEmailValid()
{
    // Arrange
    var mockLogger = new Mock<ILogger<UserService>>();
    var service = new UserService(mockLogger.Object);
    var request = new CreateUserRequest { Email = "test@example.com" };
   
    // Act
    var result = service.CreateUser(request);
   
    // Assert
    Assert.NotNull(result);
    Assert.Equal("test@example.com", result.Email);
}
```
 
**Implementation**:
```csharp
// Before
public User CreateUser(CreateUserRequest request)
{
    Console.WriteLine("Creating user: " + request.Email);
   
    if (string.IsNullOrEmpty(request.Email))
    {
        Console.WriteLine("ERROR: Email is empty");
        throw new ArgumentException("Email required");
    }
   
    Console.WriteLine("User created: " + request.Email);
    return new User(request.Email);
}

// After
public User CreateUser(CreateUserRequest request)
{
    _logger.LogInformation("Creating user: {Email}", request.Email);
   
    if (string.IsNullOrEmpty(request.Email))
    {
        _logger.LogError("Email validation failed: Email is empty");
        throw new ArgumentException("Email required");
    }
   
    _logger.LogInformation("User created successfully: {Email}", request.Email);
    return new User(request.Email);
}
```
 
**Verification**: 
Run unit tests from Step 2 and Step 3. Confirm no `Console.` calls remain in UserService. Run the application and verify logs appear in the configured sink (console, file).
 
**Design Rationale**: 
- `LogInformation` for successful operations (user created, email sent)
- `LogError` for validation failures and exception cases
- Named parameters (e.g., `{Email}`, `{UserId}`) enable structured queries
 
---
 
## Code Cleanup Opportunities
 
While implementing structured logging, we identified opportunities to improve code quality:
 
| Issue | Location | Type | Priority | Recommendation |
|-------|----------|------|----------|-----------------|
| Duplicate email validation in UserService.CreateUser() and UserValidator.Validate() | UserService.cs:42, UserValidator.cs:15 | DRY Violation | Important | Extract validation to shared `EmailValidator` utility class |
| Logging boilerplate repeated across 5 methods | UserService.cs:22, 45, 68, 91, 114 | Code Duplication | Nice-to-Have | Create `LogUserEvent()` extension method on ILogger<UserService> |
| Unused `OldUserRepository` class still referenced in comments | OldUserRepository.cs | Dead Code | Critical | Remove after confirming no historical usage in git logs |
| Inconsistent exception naming: UserNotFoundException vs UserNotFound_Exception | UserService.cs, UserRepository.cs | Naming Inconsistency | Important | Rename all exceptions to `UserNotFoundException` pattern |
 
## Full Workflow Verification
 
After all steps are complete, verify end-to-end functionality:
 
- [ ] Create a user via API endpoint and confirm all logs appear in console
- [ ] Verify logs contain structured fields (Email, UserId) searchable in log viewer
- [ ] Trigger validation error (empty email) and confirm error-level log appears
- [ ] Filter logs by LogLevel.Error and verify only error events appear
- [ ] Check that correlation IDs (if enabled) connect user creation to downstream service calls
- [ ] Confirm application startup logs the configured log level
 
## Rollout & Migration Path
 
**Phase 1** (Week 1): Deploy logging infrastructure to development
- Steps 1–3 complete
- Test in dev environment
- Confidence: Low risk (no behavioral changes, only logging added)
 
**Phase 2** (Week 2): Deploy to staging
- Monitor log volume and performance
- Confirm structured fields are populated correctly
- Validate log aggregator ingestion
 
**Phase 3** (Week 3): Deploy to production
- Gradual rollout (10% of instances)
- Monitor error rate and latency
- Scale to 100% if no issues detected
 
**Rollback Plan**: Disable Serilog sink in configuration; application continues with ILogger abstraction (switches to no-op logger if needed).
```
 
---
 
## Sample 2: BAD Example ❌
 
### Vague, Unstructured, Missing Sections
 
```markdown
# Add Logging Feature
 
## Overview
 
We need to add logging to the UserService so we can see what's happening.
 
## What to Do
 
1. Add logging package
2. Update UserService to use logging
3. Replace Console.WriteLine with logging calls
4. Test it
 
## Implementation Steps
 
Just add this code:
 
```csharp
public class UserService
{
    private ILogger logger;
   
    public void CreateUser(CreateUserRequest request)
    {
        logger.Log("Creating user");
       
        if (string.IsNullOrEmpty(request.Email))
        {
            logger.Log("Email empty");
            throw new Exception("Bad email");
        }
       
        logger.Log("Done");
    }
}
```
 
## Testing
 
Run the app and check if logs appear. Make sure it doesn't break anything.
 
## Notes
 
- Might need to configure Serilog but that's optional
- Don't worry about cleanup for now
- Team will figure out what log level to use
- Correlation IDs can be added later
```
 
**Why This Is Bad:**
- ❌ No "Current State" assessment — doesn't explain why logging is needed
- ❌ No "Architecture Overview" — no diagram or explanation of how it fits
- ❌ No structured test specifications — testing is vague ("check if logs appear")
- ❌ No "Benefit" or "Intention" per step
- ❌ Missing verification steps — how do you know it works?
- ❌ No code cleanup opportunities identified
- ❌ No "File" or exact locations specified
- ❌ Implementation shows `ILogger.Log()` without structured parameters
- ❌ No dependency injection strategy explained
- ❌ No rollout plan or migration path
- ❌ Unactionable tasks (e.g., "Team will figure out what log level to use")
 
---
 
## Sample 3: MEDIUM Example 🟡
 
### Partially Complete, Some Structure But Incomplete Details
 
```markdown
# Implementation Plan: Add Request Correlation IDs
 
## Overview
 
Add correlation ID tracking to user-facing API endpoints so we can trace requests across multiple services.
 
## Step-by-Step Implementation
 
### Step 1: Add Middleware
 
**Files**: `Startup.cs`
 
**Intention**: Inject a middleware that captures or generates a correlation ID from request headers.
 
**Test Specification**:
```csharp
[TestMethod]
public void CorrelationIdMiddleware_Generates_Id_If_Not_Present()
{
    // Test that middleware generates a correlation ID if not in headers
}
```
 
**Implementation**:
```csharp
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;
        await _next(context);
    }
}
```
 
**Verification**: TODO - Add verification steps
 
---
 
### Step 2: Store Correlation ID in LogContext
 
**Files**: `Startup.cs`
 
**Intention**: Store the correlation ID in Serilog's LogContext so all logs within a request include it.
 
**Benefit**: All logs from a single user request automatically include the correlation ID without needing to pass it to every method.
 
**Implementation**:
```csharp
// Add to CorrelationIdMiddleware
LogContext.PushProperty("CorrelationId", correlationId);
```
 
---
 
## Full Workflow Verification
 
- [ ] Make an API request and verify correlation ID appears in logs
- [ ] Check multiple services show the same correlation ID
- TODO: Add more verification steps
 
## Rollout Plan
 
Deploy to staging first, then production. Monitor for issues.
```
 
**Why This Is Medium (Incomplete):**
- ✅ Has "Overview" explaining the what and why
- ✅ Structured steps with some intent
- ✅ Includes test specifications (though sparse)
- ❌ Missing "Current State Assessment" / "Architecture Overview"
- ❌ Verification steps incomplete ("TODO" comments)
- ❌ No code cleanup opportunities section
- ❌ Rollout plan is generic and lacks details (specific phases, timing, rollback)
- ❌ Missing "Benefit" in Step 1
- ❌ No full-workflow verification checklist with concrete actions
- ⚠️ Too brief; lacks depth in test specs and implementation guidance
```