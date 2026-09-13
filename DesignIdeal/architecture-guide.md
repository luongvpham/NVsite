# Backend Architecture Guide — Clean Architecture + CQRS (.NET)

> Audience: AI coding agents and engineers starting a **new** .NET backend project.
> This guide distills a proven, production-tested layout (adapted from the TPCS backend in this
> repo) into a project-agnostic reference. Replace `{Project}` with your solution prefix
> (e.g. `Acme`) and `{Entity}` / `{Aggregate}` with real domain nouns.

---

## 0. Core principles

1. **Clean Architecture, dependencies point inward.** Domain depends on nothing. Application
   depends only on Domain. Infrastructure implements Application's interfaces. The API layer wires
   everything together but contains no business logic.
2. **CQRS via MediatR.** Every use case is a `Command` (mutation) or `Query` (read), each with its
   own `Handler`. No "fat services" — one class, one job.
3. **Feature folders, not technical folders.** Group by aggregate/use-case (`Orders/Commands/CreateOrder/`),
   not by artifact type (`Commands/`, `Handlers/`, `Validators/` at the top level). This keeps every
   file needed to understand one use case in one place.
4. **One class per file.** Command, Handler, Validator, Mapping each get their own `.cs` file — never
   bundled into one file "for convenience."
5. **TDD + coverage gate.** Tests are written alongside (ideally before) the implementation. No
   command/query/domain-entity change ships without a corresponding test. Target ≥ 80% coverage on
   Application + Domain.
6. **Errors are a typed contract, not free text.** Every handled exception carries a stable,
   machine-readable `ErrorCode` and an HTTP status. The frontend (or any API consumer) branches on
   `ErrorCode`, never on parsing the human-readable message. See §6.
7. **A `traceId` ties a user-visible error to a server log line**, so "it's broken" bug reports are
   traceable to one request without needing DB timestamps or guesswork.

---

## 1. Solution structure

```
src/
├── {Project}.Domain/          # Entities, Value Objects, Enums, Domain Exceptions, Domain Events, Domain Services
├── {Project}.Application/     # Commands, Queries, DTOs, Validators, Behaviors, interfaces
├── {Project}.Infrastructure/  # EF DbContext, EF Configurations, Migrations, external clients, repositories
└── {Project}.Web.API/         # Controllers, Filters (global exception handler), Program.cs
tests/
└── {Project}.UnitTest/        # NUnit + FluentAssertions
```

| Layer | Can depend on | Rule |
|---|---|---|
| Domain | nothing | Pure business rules only. No EF, no HTTP, no external SDKs. |
| Application | Domain | No infrastructure concerns (no `DbContext`, no HTTP clients). Everything is behind an interface. |
| Infrastructure | Application, Domain | Implements Application's interfaces (repositories, services, external clients). |
| Web.API | Application (always) + Infrastructure (DI wiring only) | Controllers never reference Infrastructure types directly. |

**API layer clarification:**
- `Program.cs` / `DependencyInjection.cs` → the *only* place allowed to call `AddInfrastructureServices()` for DI wiring.
- Controllers → only use Application types (`ISender`, DTOs, interfaces). Thin — no business logic, no `try/catch` (the global exception middleware handles it, see §6).
- Controllers → never inject Infrastructure classes directly (`ApplicationDbContext`, `EmailService`, etc.) — go through Application interfaces.

---

## 2. Domain layer — design and layout

```
{Project}.Domain/
├── Common/          BaseEntity.cs | BaseAuditableEntity.cs   ← shared base classes (Id, audit fields)
├── Entities/        {Entity}.cs
├── ValueObjects/    {Name}.cs
├── Enums/           {Name}.cs
├── Exceptions/      AppException.cs (base) | NotFoundException.cs | DomainException.cs | ConflictException.cs | ForbiddenAccessException.cs
├── Events/          {Entity}{PastTenseVerb}Event.cs   ← implements INotification
└── Services/        {Operation}Service.cs   ← ONLY for logic that spans multiple aggregates
```

### Entities

- One entity per file, singular noun (`Order`, not `Orders`).
- Has an `Id` and maps to a DB table.
- **Business rules live as methods on the entity**, not as free functions elsewhere — e.g.
  `order.Cancel()`, `encounter.Complete()`, not `OrderHelper.Cancel(order)`.
- An entity should never be constructible into an invalid state: use a private/protected
  constructor + a static factory method (`Order.Create(...)`) or guard clauses in the public
  constructor, so invariants are enforced at the single point of creation.
- Keep an entity's `ValidateInvariants()`-style checks (rules that depend only on the entity's own
  fields, e.g. "EndDate must be after StartDate") in the entity itself — don't push those up to a
  handler or down to a FluentValidation validator.

### Value Objects

- Immutable, no `Id`, defined by their values (e.g. `Money`, `Address`, `PhoneNumber`).
- Equality by value, not reference — override `Equals`/`GetHashCode` or use a C# `record`.
- Encapsulate validation/normalization in the constructor (e.g. `PhoneNumber` rejects malformed input at construction, so a valid instance is a guarantee, not a hope).
- EF Core: map with `OwnsOne` (owned entity) when the VO doesn't need its own table.

### Enums

- One enum per file under `Enums/`. Prefer explicit numeric values if the enum is persisted, so
  reordering members doesn't silently reshuffle stored data.

### Domain Exceptions

A single **exception contract** used everywhere in the codebase — this is what makes the global
exception handler (§6) and the frontend error-code mapping (§7) possible.

```csharp
/// Base type for every handled application/domain exception. Carries a stable, machine-readable
/// ErrorCode consumers can branch/translate on, plus the HTTP status to return. LogDetail may hold
/// internal context (ids, entity state) safe for server logs but never echoed in the response body —
/// Message is the human-safe text that IS returned to the client.
public abstract class AppException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    public IDictionary<string, string[]> Errors { get; }
    public object? ErrorParams { get; }
    public string? LogDetail { get; }

    protected AppException(string errorCode, string message, int statusCode,
        IDictionary<string, string[]>? errors = null, object? errorParams = null, string? logDetail = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Errors = errors ?? new Dictionary<string, string[]>();
        ErrorParams = errorParams;
        LogDetail = logDetail;
    }
}
```

Concrete subclasses (all in `Domain/Exceptions/`):

| Exception | HTTP | ErrorCode | When |
|---|---|---|---|
| `NotFoundException(entityName, key)` | 404 | `{ENTITY}_NOT_FOUND` (derived automatically — never pass one manually) | Entity lookup failed |
| `DomainException(errorCode, message)` | 400 | caller-supplied, `SCREAMING_SNAKE_CASE` | A business rule was violated |
| `ConflictException(errorCode, message)` | 409 | caller-supplied | Request conflicts with current resource state (e.g. "already dispensed") — distinct from 400 because the fix is "refresh state", not "resubmit differently" |
| `ForbiddenAccessException(errorCode, message)` | 403 | caller-supplied | Authenticated but not authorized for this action |
| `ValidationException(failures)` (Application layer, extends `AppException`) | 422 | `VALIDATION_ERROR` | FluentValidation failures, field-level `Errors` dict populated |

**Rule:** `DomainException`/`ConflictException`/`ForbiddenAccessException` have **no generic
fallback ErrorCode** — the constructor forces every call site to supply one. This is deliberate:
it's what lets the compiler catch an un-classified error before it reaches a client as an
untranslatable string.

### Domain Events

- `{Entity}{PastTenseVerb}Event` implementing `INotification` (e.g. `OrderPlacedEvent`,
  `EncounterCompletedEvent`).
- Raised via `entity.AddDomainEvent(new OrderPlacedEvent(order.Id))` inside the entity method that
  causes the transition — never raised from the handler.
- Dispatched by a MediatR pipeline behavior (or `DbContext.SaveChangesAsync` override) *after* the
  transaction commits, so a handler failing after the event fires never leaves a "phantom"
  side-effect.
- Handlers for domain events live in Infrastructure (they usually have infra side-effects: send
  email, call external API) and implement `INotificationHandler<{Event}>`.

### Domain Services

Use **only** when logic mutates 2+ aggregates and doesn't naturally belong to one entity:

```csharp
public class EmpanelmentService
{
    public void HandleCreation(Empanelment empanelment, DoctorProfile doctor, Facility facility)
    {
        empanelment.Activate();
        doctor.IncrementEmpanelment();
        facility.IncrementEmpanelment();
    }
}
```

If the logic only touches one aggregate, it belongs on that entity instead — don't default to a
Domain Service out of habit.

---

## 3. Application layer — CQRS feature folders

```
{Project}.Application/
├── Common/
│   ├── Behaviors/       {Concern}Behavior.cs        ← ValidationBehavior, LoggingBehavior, TransactionBehavior
│   ├── Exceptions/      ValidationException.cs      ← wraps FluentValidation failures into AppException
│   ├── Interfaces/      I{Name}.cs                  ← IApplicationDbContext, IEmailService, I{UseCase}Hook
│   ├── Mappings/        MappingProfile.cs | MappingExtensions.cs
│   └── Models/          PaginatedList.cs | Result.cs
│
├── {Aggregate}s/                                     ← one folder per aggregate root (e.g. Orders/)
│   ├── DTOs/
│   │   ├── {Entity}Dto.cs                            ← detail/response DTO
│   │   ├── {Entity}SummaryDto.cs                      ← list/pagination DTO (lighter — no deep graphs)
│   │   ├── {Entity}CommandBaseDto.cs                  ← shared input fields for Create/Update
│   │   └── {Child}Dto.cs / {Child}CommandDto.cs       ← child/owned-collection DTOs
│   ├── Commands/
│   │   ├── Create{Entity}/  {..}Command.cs {..}Handler.cs {..}Validator.cs {..}Mapping.cs
│   │   ├── Update{Entity}/   same shape
│   │   ├── {Transition}{Entity}/  e.g. Approve{Entity}/, Cancel{Entity}/
│   │   └── Delete{Entity}/  Command.cs + Handler.cs only (no Validator/Mapping needed)
│   ├── Queries/
│   │   ├── Get{Entity}Detail/       Get{Entity}ByIdQuery.cs + Handler.cs
│   │   └── Get{Entity}Pagination/   Get{Entity}ByPaginationQuery.cs + Handler.cs
│   ├── Repositories/                ← ONLY when justified, see §4
│   │   └── I{Entity}Repository.cs
│   └── Mappings/
│       └── {Entity}MappingProfile.cs                 ← shared Entity→Dto / Nav→BriefInfoDto maps
│
├── Externals/{Provider}/            ← top-level, shared across aggregates — see §5
│   ├── I{Provider}Client.cs
│   ├── {Action}{Provider}Request.cs
│   └── {Action}{Provider}Dto.cs
│
└── {Operation}/                     ← cross-aggregate use case that doesn't belong to one entity
    ├── {Operation}Command.cs
    ├── {Operation}Handler.cs
    └── {Operation}Validator.cs
```

### Naming conventions

| Artifact | Pattern | Example |
|---|---|---|
| Command | `{Action}{Entity}Command` | `CreateOrderCommand` |
| Handler | `{Action}{Entity}Handler` | `CreateOrderHandler` |
| Validator | `{Action}{Entity}Validator` | `CreateOrderValidator` |
| Feature mapping | `{Action}{Entity}Mapping` | `CreateOrderMapping` |
| Query | `Get{Entity}By{Field\|Noun}Query` | `GetOrderByIdQuery`, `GetOrderByPaginationQuery` |
| DTO — detail | `{Entity}Dto` | `OrderDto` |
| DTO — list | `{Entity}SummaryDto` | `OrderSummaryDto` |
| Mapping profile | `{Entity}MappingProfile` | `OrderMappingProfile` |
| Repository interface | `I{Entity}Repository` | `IOrderRepository` |
| Generic interface | `I{Name}` | `IEmailService`, `IApplicationDbContext` |
| Pipeline behavior | `{Concern}Behavior` | `ValidationBehavior` |
| External client | `I{Provider}Client` | `IPaymentGatewayClient` |
| Domain-event hook | `I{UseCase}Hook` | `IOrderPlacedHook` |

### Feature-folder rules

- **One class per file.**
- **Delete commands** typically skip Validator/Mapping — just `Command` + `Handler`.
- **Shared mappings** (`{Entity}MappingProfile`) live in the aggregate's `Mappings/` folder;
  feature-specific input→entity maps live beside their command as `{Action}{Entity}Mapping.cs`.
- **Pagination**: `Get{Entity}ByPaginationQuery` returns `PaginatedList<{Entity}SummaryDto>` via a
  shared `MappingExtensions.PaginatedListAsync(IQueryable<TSource>)` helper — don't hand-roll
  paging in every handler.

### Pipeline behaviors (MediatR)

Register cross-cutting concerns as `IPipelineBehavior<TRequest, TResponse>` so handlers stay free
of boilerplate:

- `ValidationBehavior` — runs all registered `IValidator<TRequest>` before the handler executes; on
  failure, throws `ValidationException` (422) so handlers never manually validate input.
- `LoggingBehavior` — logs request/response with the same `TraceId` used by the exception
  middleware (see §6), so a full request lifecycle is greppable by one id.
- Optional: `TransactionBehavior` (wrap command execution in a DB transaction),
  `PerformanceBehavior` (warn on slow handlers).

---

## 4. Repository rules — when to introduce one

**Default:** use `IApplicationDbContext` + `DbSet<TEntity>` directly inside handlers for plain CRUD.
Do **not** create a repository just to wrap `Add`/`GetById`/`Update` — that's needless indirection
over EF Core, which is already a repository/unit-of-work.

**Create `I{Entity}Repository` only when:**
1. Complex query logic (multi-join, "latest version", "currently active") that would otherwise be
   duplicated across handlers.
2. A business invariant check requires a DB query (e.g. "patient has no other active empanelment").
3. The same data-access logic is needed in 2+ places.

✔ Good: `GetActiveEmpanelment(patientId)`, `ExpireActiveEmpanelment(patientId)`
❌ Bad: `Add(entity)`, `GetById(id)`, `Update(entity)` — this is EF Core's job already.

| Artifact | Path |
|---|---|
| Interface | `Application/{Aggregate}s/Repositories/I{Entity}Repository.cs` |
| Implementation | `Infrastructure/Persistence/Repositories/{Entity}Repository.cs` |

---

## 5. Infrastructure layer

```
{Project}.Infrastructure/
├── Persistence/
│   ├── ApplicationDbContext.cs
│   ├── Configurations/     {Entity}Configuration.cs   ← IEntityTypeConfiguration<T>, one per entity
│   ├── Repositories/       {Entity}Repository.cs
│   └── Migrations/
├── Services/               {Name}Service.cs | {UseCase}Hook.cs
├── ExternalServices/{Provider}/
│   ├── {Provider}Client.cs
│   └── DTOs/               ← provider-specific wire-format envelopes, translated to the clean Application DTO here
└── DependencyInjection.cs
```

**Interface → implementation mapping** (always symmetrical):

```
IEmailService            (Application/Common/Interfaces)  → EmailService            (Infrastructure/Services)
IPaymentGatewayClient     (Application/Externals/Payment)   → PaymentGatewayClient     (Infrastructure/ExternalServices/Payment)
IOrderRepository          (Application/Orders/Repositories) → OrderRepository          (Infrastructure/Persistence/Repositories)
IOrderPlacedHook          (Application/Common/Interfaces)   → OrderPlacedHook          (Infrastructure/Services)
```

### External integrations

For any use case calling an external API (payment gateway, SMS, a government/insurance API, etc.):

- **Interface + request/response DTOs** live in `Application/Externals/{Provider}/` — top level,
  *not* nested inside one aggregate's folder, so any handler can depend on them.
- **Implementation** in `Infrastructure/ExternalServices/{Provider}/`.
- **Stub implementations** (before the real integration is built, or for local/dev environments)
  return default-success values — never `throw new NotImplementedException()`, which would break
  every caller's happy-path tests.
- Provider-specific wire formats that don't match the clean Application DTO get their own type
  under `Infrastructure/ExternalServices/{Provider}/DTOs/` and are translated inside the client.

### EF Core conventions

- One `IEntityTypeConfiguration<T>` per entity — never configure entities via bulk `OnModelCreating` code.
- Generate migrations from the API/Infra project: `dotnet ef migrations add <Name> --project <Infra> --startup-project <API>`.
- **Never hand-edit a generated migration file** and never apply schema changes via manual SQL —
  regenerate instead, so the migration history stays a faithful, replayable record of every schema change.
- If the project needs tenant isolation, keep `TenantId` filtering exclusively inside the
  `DbContext` (global query filter), never scattered across handler-level `Where` clauses — one
  place to audit for "did we forget to filter by tenant."

---

## 6. Global exception handling (and why it matters for the frontend)

A single ASP.NET Core middleware translates every exception into a consistent
[RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807) response — this is the seam that
makes error codes and trace ids usable end-to-end.

```csharp
public class GlobalExceptionHandlerMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Every log line written during this request (MediatR LoggingBehavior, handlers, this
        // middleware) carries the same TraceId — a user-reported error code + traceId is enough
        // to grep the full request context in server logs, with no DB timestamp guesswork.
        using (_logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = context.TraceIdentifier }))
        {
            try { await _next(context); }
            catch (Exception ex) { await HandleExceptionAsync(context, ex); }
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // switch on exception type:
        //   AppException            -> use its StatusCode / ErrorCode / Errors / ErrorParams / LogDetail
        //   UnauthorizedAccessException -> 401 / "UNAUTHORIZED"
        //   anything else            -> 500 / "INTERNAL_SERVER_ERROR" (never leak exception.Message to the client)

        // 500s are logged at Error (paged/alerted); everything else at Warning.
        // Response body:
        // { type, title, status, errorCode, traceId, params, errors }
    }
}
```

Rules this encodes:
1. **Unknown/unexpected exceptions never leak internal details.** Only `AppException` subclasses
   control their own `title`/`message` shown to the client; anything else becomes a generic
   "An unexpected error occurred" + `INTERNAL_SERVER_ERROR`, with the real exception logged
   server-side only.
2. **`traceId` is always present**, generated per-request by ASP.NET Core (`HttpContext.TraceIdentifier`) — no extra plumbing needed.
3. **Controllers never `try/catch`.** All exception-to-HTTP translation happens in one place.
4. Response `Content-Type: application/problem+json`, camelCase JSON.

---

## 7. Frontend/client error-contract sync

The value of §6's typed exceptions only pays off if the frontend consumes the same contract
instead of parsing English strings. Define one shared type at the API-client boundary:

```typescript
/** RFC 7807 Problem Details — returned by BE for 400/401/403/404/409/422 */
export interface ApiError {
  status: number;
  /** Human-safe default (English) message. Always present, even for unmapped errorCodes. */
  title: string;
  /** Stable machine-readable code (e.g. "ENCOUNTER_LOCKED") for mapping to a localized message. */
  errorCode?: string;
  /** Correlates this response to a server log entry. Safe to show to the user for support reports. */
  traceId?: string;
  /** Safe metadata for message interpolation (e.g. { entityName: "Patient" }). Never internal ids/state. */
  params?: Record<string, unknown>;
  errors: Record<string, string[]>;
}
```

Then, at the HTTP client's response interceptor, normalize **every** failed response into this
shape once — so every screen in the app handles errors the same way instead of each component
reaching into `error.response.data` by hand:

```typescript
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (!error.response) throw { status: 0, title: 'Network error', errors: {} } satisfies ApiError;
    const { status, data } = error.response;
    if (status === 401) { clearTokens(); window.location.href = '/login'; return Promise.reject(error); }
    throw {
      status: data?.status ?? status,
      title: data?.title ?? `Error ${status}`,
      errorCode: data?.errorCode,
      traceId: data?.traceId,
      params: data?.params,
      errors: data?.errors ?? {},
    } satisfies ApiError;
  },
);
```

**Localization / message mapping**: keep a single `errorCode -> i18n key` catalog
(`errorCodeMessageKeys`) rather than hardcoding translated strings per screen. Unmapped codes fall
back to the backend's safe `title` — so adding an entry is always an incremental UX improvement,
never a correctness requirement (nothing breaks if a new `ErrorCode` isn't localized yet):

```typescript
export const errorCodeMessageKeys: Record<string, string> = {
  VALIDATION_ERROR: 'common:errors.validationError',
  PATIENT_NOT_FOUND: 'common:errors.patientNotFound',   // {ENTITY}_NOT_FOUND convention
  ENCOUNTER_LOCKED: 'common:errors.encounterLocked',    // DomainException-specific example
};

export function getApiErrorMessage(error: ApiError, t: TFunction): string {
  const key = error.errorCode ? errorCodeMessageKeys[error.errorCode] : undefined;
  if (!key) return error.title; // safe fallback — never blank, never a raw stack trace
  return t(key, { defaultValue: error.title, ...error.params });
}
```

**Bug-report / support workflow**: surface `traceId` in the toast/notification UI (e.g. small grey
text under the error message). A user forwarding "error, code XYZ, trace abc-123" lets an engineer
`grep TraceId=abc-123` server logs and see the entire request — no need to ask "what time did this
happen" or reproduce the bug blind.

---

## 8. Mapping (Entity ⇄ DTO)

- Use **AutoMapper** with one `Profile` per aggregate (`{Entity}MappingProfile` in
  `Application/{Aggregate}s/Mappings/`), registered via `IHaveMappingProfile` reflection/assembly
  scan at startup — never a single giant `MappingProfile.cs` for the whole solution.
- **Feature-specific** command→entity maps (e.g. "how `CreateOrderCommand` becomes a new `Order`")
  live beside the command as `{Action}{Entity}Mapping.cs` — they're one-off and shouldn't pollute
  the shared aggregate profile.
- **Shared** Entity→Dto and Navigation→BriefInfoDto maps (reused by multiple queries) belong in the
  aggregate's shared `{Entity}MappingProfile`.
- Prefer `ProjectTo<TDto>()` over `Map<TDto>()` when mapping directly from `IQueryable` — it lets
  EF Core project only the needed columns instead of loading full entities into memory first.
- A `PaginatedList<T>` + `MappingExtensions.PaginatedListAsync(IQueryable<TSource>)` helper
  standardizes "map + paginate" into one call so every list query looks the same.

---

## 9. Testing

```
{Project}.UnitTest/
├── {Entity}s/
│   ├── {Action}{Entity}CommandTests.cs   ← Create/Update/Delete handler tests
│   ├── {Entity}DomainTests.cs            ← entity/aggregate business-rule tests
│   ├── {Entity}ValidatorTests.cs
│   └── {Entity}QueryTests.cs             ← GetById / GetPagination tests
├── TestDbContextFactory.cs               ← in-memory or SQLite test DbContext
├── MockFactory.cs
└── ITestSeeder.cs                        ← catalog every seed helper here FIRST, before adding a new one
```

- **TDD**: write the failing test first, then the minimum code to pass it.
- **Coverage gate ≥ 80%** on Domain + Application before a story is considered done.
- **NUnit + FluentAssertions** is a solid, low-friction default combo for .NET.
- **Seed-helper discipline**: before adding a new `SeedXxxAsync` helper, check `ITestSeeder.cs` for
  an existing one — duplicated seed logic across test files is a common source of drift.

---

## 10. Cheatsheet — one aggregate, every file it touches

For an aggregate `Order`:

```
Domain:
  Entities/Order.cs
  Events/OrderPlacedEvent.cs
  Exceptions/ (shared — DomainException etc., not per-entity)

Application:
  Orders/DTOs/OrderDto.cs, OrderSummaryDto.cs, OrderCommandBaseDto.cs
  Orders/Commands/CreateOrder/{CreateOrderCommand.cs, CreateOrderHandler.cs, CreateOrderValidator.cs, CreateOrderMapping.cs}
  Orders/Commands/UpdateOrder/{...}
  Orders/Commands/DeleteOrder/{DeleteOrderCommand.cs, DeleteOrderHandler.cs}
  Orders/Queries/GetOrderDetail/{GetOrderByIdQuery.cs, GetOrderByIdHandler.cs}
  Orders/Queries/GetOrderPagination/{GetOrderByPaginationQuery.cs, GetOrderByPaginationHandler.cs}
  Orders/Mappings/OrderMappingProfile.cs
  Orders/Repositories/IOrderRepository.cs        (only if justified, §4)

Infrastructure:
  Persistence/Configurations/OrderConfiguration.cs
  Persistence/Repositories/OrderRepository.cs     (only if the interface exists)

Web.API:
  Controllers/OrdersController.cs                 (thin — ISender only)

UnitTest:
  Orders/CreateOrderCommandTests.cs
  Orders/OrderDomainTests.cs
  Orders/OrderValidatorTests.cs
  Orders/OrderQueryTests.cs
```

---

## 11. Applying this to a new project

1. Scaffold the four `src/` projects + one `tests/` project, wire up project references per §1's
   dependency table.
2. Add `Common/` scaffolding first: `BaseEntity`/`BaseAuditableEntity` (Domain), `AppException`
   hierarchy (Domain), `IApplicationDbContext` + `ValidationBehavior` + `LoggingBehavior`
   (Application), `GlobalExceptionHandlerMiddleware` (Web.API).
3. Pick your first aggregate, build it end-to-end (Entity → Command/Query → Configuration →
   Controller → tests) to validate the skeleton before replicating the pattern across the rest of
   the domain.
4. Wire the frontend's `ApiError` type + response interceptor (§7) as soon as the first endpoint
   exists, so error-code discipline is a habit from commit #1, not a retrofit.
5. If the project is multi-service or multi-zone (internal/external network boundaries), decide the
   cross-service communication pattern (gRPC vs HTTPS, generic message envelope vs. dedicated
   endpoints) *before* the second service exists — retrofitting a transport convention across many
   services is expensive.
