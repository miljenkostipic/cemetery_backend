# R0 Stabilise

Goal: CI catches contract drift and missing coverage before the bigger refactors start. No behaviour changes in this step.

Gate: CI fails when `openapi/cemetery.json` is out of date and when coverage is below the AGENTS.md targets.

---

## R0.1 Merge the payments migration fix

- Finding: B10
- Do: open a PR from `fix-payments-migration-order` to `main` and merge it once CI is green. Start every later task branch from the updated `main`.
- Don't: include the local `AGENTS.md` machine-setup edit (commit `5607629` keeps the `main` version on purpose).
- Done when: `git log main` contains "Apply payments row-level security in the Payments migration"; a fresh database migrates with `dotnet run --project src/Cemetery.Api`.

## R0.2 OpenAPI export + drift check

- Finding: B8
- Do:
  1. Generate the document at build time with `Microsoft.Extensions.ApiDescription.Server` (`<OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>`, `<OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../openapi</OpenApiDocumentsDirectory>`), file name `cemetery.json`.
  2. Make sure the host starts without a database during generation. `CemeteryHost` already has a `headless` flag: skip `DatabaseInitializer`, Hangfire server and outgoing services when it is set, and set it when the entry assembly is `GetDocument.Insider`.
  3. In `.github/workflows/ci.yml`, after build: `git diff --exit-code openapi/cemetery.json`.
  4. Commit the regenerated file (it will now include the billing endpoints).
- Don't: hand-edit `openapi/cemetery.json`, change endpoint shapes, or remove the `Servers` transformer.
- Done when: `dotnet build -c Release` rewrites `openapi/cemetery.json`; the file contains `/api/v1/cemeteries/{cemeteryId}/charges`; CI fails on a PR that changes an endpoint without the regenerated file (test it once on a scratch branch).

## R0.3 Coverage thresholds in CI

- Finding: B9
- Do: collect coverage in CI and fail below: 100% line and branch for `Cemetery.Domain` and `Cemetery.Application`; 90% line for `Cemetery.Api` and `Cemetery.Infrastructure` (excluding `Migrations/`).
  - If the test projects run on VSTest: `coverlet.msbuild` with `/p:CollectCoverage=true /p:Include="[Cemetery.Domain]*" /p:Threshold=100 /p:ThresholdType="line,branch"` (one call per assembly group).
  - If they run on Microsoft.Testing.Platform: `Microsoft.Testing.Extensions.CodeCoverage` + ReportGenerator, then a step that fails on the thresholds.
- Don't: lower a threshold, or add `[ExcludeFromCodeCoverage]` to production code to pass.
- Done when: CI prints a coverage summary per assembly. If current coverage is below target, CI is red. That is expected: list the gaps under "Found while working" and leave Api/Infrastructure red until R1 fills them. Domain/Application must be green.

## R0.4 Register handlers by assembly scan

- Finding: B7
- Do: replace the per-handler lines in `Cemetery.Application/DependencyInjection.cs` with Scrutor scanning of `ICommandHandler<,>` and `IQueryHandler<,>` (scoped), and keep `ValidatingCommandHandler<,>` as a decorator on every `ICommandHandler<,>` (`services.Decorate(typeof(ICommandHandler<,>), typeof(ValidatingCommandHandler<,>))`). Add an architecture test that builds the container and resolves every handler type found in the assembly, and asserts every command has an `IValidator<TCommand>`.
- Don't: change any handler code.
- Done when: `DependencyInjection.cs` is under 30 lines; all existing tests pass; deleting a validator makes the new architecture test fail.

---

## Found while working

(add items here, do not fix them in this step)
