# OneApproval Dataverse Plugin Solution

## 1. Architectural Overview & Design Principles

This solution refactors the monolithic `RouteOneKycApprovalAndAdvicePlugin` (~2,500 lines previously contained in a single unstructured file) into a clean, enterprise-grade, layered architecture following **SOLID** principles, design patterns from Clean Architecture / Java-style small classes, and Microsoft Dataverse (Power Apps / Dynamics 365) plugin best practices.

### Core Design Goals
- **Elimination of Magic Numbers & Strings**: All OptionSets, stages, entity names, and attribute logical names are encapsulated in strongly-typed constant classes.
- **Single Responsibility Principle (SRP)**: Each class has exactly one reason to change (e.g., query logic belongs to repositories, calculation logic belongs to calculators, condition evaluation belongs to matchers).
- **Separation of Concerns (SoC)**: Presentation/Pipeline mechanics are decoupled from domain business logic and data access.
- **High Testability**: Repositories and domain services are interface-driven and can be tested with mocking frameworks (such as FakeXrmEasy or Moq) without requiring a live Dataverse connection.
- **Strict Business Logic & Audit Fidelity**: 100% of existing behavior, tracing messages, edge cases, null-clearing updates, and temporary routing constraints are preserved.

---

## 2. Project Directory & File Layout

All solution files are centralized in the workspace root:

```
PowerappsCode/
├── Constants/
│   ├── PluginConstants.cs         # Dataverse execution stages, messages, state codes, execution limits
│   ├── OptionSets.cs              # All global & local OptionSet choice integer values
│   └── EntityMetadata.cs          # Strongly-typed schema constants for entities and attributes
│
├── Models/
│   └── RoutingContextData.cs      # In-memory DTO holding resolved source and derived routing values
│
├── Common/
│   ├── EntityHelper.cs            # Dataverse SDK equality, sparse updates (AddIfDifferent), record naming
│   ├── RiskMapper.cs              # Conversion of OneKYC risk score labels into target OptionSets
│   ├── FlowHelper.cs              # Target flow validation and display label resolution
│   └── StageHelper.cs             # Trigger stage validation
│
├── Repositories/
│   ├── IOneKycRepository.cs       # Contract for reading source OneKYC records
│   ├── OneKycRepository.cs        # Queries for ApprovalAndAdvice, Case, Party, ClientUnit, Group, Analyst
│   ├── IRoutingRuleRepository.cs  # Contract for reading configuration routing rules
│   ├── RoutingRuleRepository.cs   # Dataverse query for active ka_routingrule rows
│   ├── IOneApprovalRepository.cs  # Contract for persisting target OneApproval records
│   └── OneApprovalRepository.cs   # Create, retrieve, and update operations for Tasks and KYC Approval Cases
│
├── Services/
│   ├── GroupCompositionCalculator.cs # Mathematical calculation of group composition from member parties
│   ├── RoutingRuleMatcher.cs         # Rule filtering, unsupported rule skip logic, condition matching
│   └── ApprovalRoutingService.cs     # End-to-end orchestration of the business routing workflow
│
├── Plugin/
│   ├── LocalPluginContext.cs                 # Encapsulates IServiceProvider and Dataverse pipeline services
│   └── RouteOneKycApprovalAndAdvicePlugin.cs # Clean IPlugin entry point (< 80 lines)
│
├── OneApproval.Plugins.csproj     # Visual Studio / MSBuild project file (target: net462)
└── README.md                      # Complete architectural and technical documentation
```

---

## 3. Deep-Dive: Layers, Technical Logic & Calculations

### Layer 1: Constants (`Constants/`)
Eliminates all raw literal strings and magic numbers scattered throughout the code.

#### `PluginConstants.cs`
- **`PluginStage.PostOperation = 40`**: The plugin must run at Stage 40 because the triggering `aab_approvalandadvice` record must physically exist in the database to be retrieved.
- **`PluginMessage.Create = "Create"`**: Restricts plugin execution exclusively to creation events.
- **`PluginParameter.Target = "Target"`**: Fallback input parameter name to retrieve the created entity ID if `PrimaryEntityId` is empty.
- **`EntityState.Active = 0` / `Inactive = 1`**: Standard Dataverse `statecode` filter ensuring inactive records are ignored.
- **`ExecutionLimits.MaxDepth = 1`**: Prevents infinite loops or unwanted cascaded execution.

#### `OptionSets.cs`
Centralizes confirmed integer values from Dataverse Choice definitions:
- **`OneKycStage`**: `MediumApproval (745460009)`, `ComplianceAdvice (745460010)`, `IncreasedApproval (745460011)`, `CarcApproval (745460013)`.
- **`OneKycPartyType`**: `BusinessClient (1)` (BC), `NaturalPerson (2)` (NP).
- **`OneKycRoleGroup`**: `CaseAnalyst (958630000)`.
- **`RoutingScenario`**: `SingleBcNumber (123900001)`, `LogicalGroup (123900002)`.
- **`GroupComposition`**: `NotApplicable (123900000)`, `NpOnly (123900001)`, `BcOnly (123900002)`, `MixedBcAndNp (123900003)`, `Unknown (123900004)`.
- **`RoutingRisk`**: `Medium (123900000)`, `Increased (123900001)`, `Unacceptable (123900002)`.
- **`ApprovalCaseRiskScore`**: `Neutral (123900000)`, `Medium (123900001)`, `Increased (123900002)`, `Unacceptable (123900003)`.
- **`TriStateMatch`**: `Any (123900000)`, `Yes (123900001)`, `No (123900002)`.
- **`OneApprovalFlow`**: `Carc (123900000)`, `Coe (123900001)`, `Sma (123900002)`, `Unrouted (123900003)`.

#### `EntityMetadata.cs`
Defines string constants for every entity logical name (`aab_approvalandadvice`, `aab_case`, `aab_party`, `ka_routingrule`, `ka_approvaltask`, `ka_kycapprovalcase`, etc.) and attribute logical names.

---

### Layer 2: Models (`Models/`)

#### `RoutingContextData.cs`
An in-memory Data Transfer Object (DTO) that holds all loaded OneKYC case attributes, party details, resolved client unit text, group identifiers, and derived calculation results.
- **Why it matters**: Resolving this data **once** before rule evaluation avoids repetitive Dataverse database queries during rule matching, dramatically reducing latency and API request count.

---

### Layer 3: Common Utilities (`Common/`)

#### `EntityHelper.cs` (Sparse Updates & State Synchronization)
1. **`AreValuesEqual(object left, object right)`**:
   - Compares Dataverse SDK types accurately:
     - `EntityReference`: Validates both GUID `Id` and `LogicalName` (case-insensitive).
     - `OptionSetValue`: Compares numeric integer `.Value`.
     - `string`: Compares using exact ordinal semantics.
     - `DateTime`: Compares date/time ticks.
     - `null`: `(null, null)` is equal; `(null, value)` is not equal.
2. **`AddIfDifferent(...)` (Sparse Update Logic & Null Clearing)**:
   - Dataverse allows updating only modified fields.
   - If an existing target record contains a value `V`, and the newly calculated source value is `null`, `AddIfDifferent` assigns `null` to the update payload:
     $$\text{Target}[attr] = \text{null}$$
     In Dataverse, submitting an attribute set to `null` instructs the server to **clear/delete** the existing database column.
   - If `left == right`, the attribute is **omitted** from the update request, preventing unnecessary database triggers, audit log pollution, and row-level locking.
3. **`BuildRecordName(prefix, oneKycCaseNumber, flowLabel)`**:
   - If case number exists: `"{prefix} - {caseNumber.Trim()} - {flowLabel}"` (e.g., `"KYC Approval - 12345 - SMA"`).
   - If case number is missing: `"{prefix} - {flowLabel} - {yyyyMMddHHmmssfff}"` (fallback timestamp).

#### `RiskMapper.cs` (Risk Scoring Conversions)
Source risk in OneKYC is stored as free-form text (`aab_riskscore`). The mapper normalizes and converts it:
- **Routing Risk** (used on Routing Rules):
  - `"Medium"` $\rightarrow$ `123900000` (`RoutingRisk.Medium`)
  - `"Increased"` $\rightarrow$ `123900001` (`RoutingRisk.Increased`)
  - `"Unacceptable"` $\rightarrow$ `123900002` (`RoutingRisk.Unacceptable`)
  - *Note*: `"Neutral"` is not a valid condition on Routing Rules and yields `null` (safe exit).
- **Approval Case Risk Score** (used on target KYC Approval Case):
  - `"Neutral"` $\rightarrow$ `123900000` (`ApprovalCaseRiskScore.Neutral`)
  - `"Medium"` $\rightarrow$ `123900001` (`ApprovalCaseRiskScore.Medium`)
  - `"Increased"` $\rightarrow$ `123900002` (`ApprovalCaseRiskScore.Increased`)
  - `"Unacceptable"` $\rightarrow$ `123900003` (`ApprovalCaseRiskScore.Unacceptable`)

#### `FlowHelper.cs` & `StageHelper.cs`
- `FlowHelper.IsSupportedTargetFlow`: Accepts only `CARC (123900000)`, `CoE (123900001)`, `SMA (123900002)`. Rejects `Unrouted (123900003)`.
- `StageHelper.IsRoutingTriggerStage`: Validates that the event stage is one of: Medium Approval, Increased Approval, Compliance Advice, or CARC Approval.

---

### Layer 4: Repositories (`Repositories/`)

Encapsulates all Dataverse database operations (`Retrieve`, `RetrieveMultiple`, `Create`, `Update`).
- **`OneKycRepository`**: Selects only strictly necessary columns (`ColumnSet`) for `aab_approvalandadvice`, `aab_case`, `aab_party`, `aab_clientunit`, and `aab_group`.
  - Also executes the query on `aab_caseowner` filtered by `aab_caseid == caseId && aab_rolegroup == 958630000 (Case Analyst) && statecode == 0`. Uses `TopCount = 2` to detect ambiguity (if count > 1, returns `null` rather than guessing).
- **`RoutingRuleRepository`**: Retrieves active rules (`statecode == 0`) sorted deterministically by rule name.
- **`OneApprovalRepository`**: Queries for an active existing case (`ka_nnekyccasenumber == oneKycCaseId && statecode == 0`), and handles CRUD on `ka_approvaltask` and `ka_kycapprovalcase`.

---

### Layer 5: Services & Calculations (`Services/`)

#### 1. `GroupCompositionCalculator.cs` (Group Composition Calculation)
Determines whether a logical group consists of Business Clients (BC), Natural Persons (NP), or a mix.

**Mathematical Algorithm**:
1. If `groupReference == null`:
   $$\text{Composition} = \text{NotApplicable (123900000)}$$
2. Query `aab_party` where `aab_groupid == groupReference.Id`:
   - If count is 0 (no party records found for group):
     - Fallback to current party type:
       - If `currentPartyType == NaturalPerson (2)` $\rightarrow \text{NpOnly (123900001)}$
       - If `currentPartyType == BusinessClient (1)` $\rightarrow \text{BcOnly (123900002)}$
       - Else $\rightarrow \text{Unknown (123900004)}$
3. Inspect all returned member parties:
   - If any member party has a `null` party type or a value other than BC or NP:
     $$\text{hasUnknown} = \text{true}$$
   - If member has `NaturalPerson (2)`: $\text{hasNp} = \text{true}$
   - If member has `BusinessClient (1)`: $\text{hasBc} = \text{true}$
4. Evaluate truth table:
   - If $\text{hasUnknown} == \text{true} \implies \text{Unknown (123900004)}$
   - If $\text{hasNp} \land \text{hasBc} \implies \text{MixedBcAndNp (123900003)}$
   - If $\text{hasNp} \land \neg\text{hasBc} \implies \text{NpOnly (123900001)}$
   - If $\neg\text{hasNp} \land \text{hasBc} \implies \text{BcOnly (123900002)}$
   - Otherwise $\implies \text{Unknown (123900004)}$

#### 2. `RoutingRuleMatcher.cs` (Condition Matching & Special Rules)
- **Temporary Special Rule Skipping**:
  - Rules requiring unconfirmed external data are skipped:
    - Compliance Advice Choice populated $\rightarrow$ skip.
    - GCARC Match Choice = `Yes (123900001)` $\rightarrow$ skip.
    - TM Match Choice = `Yes (123900001)` $\rightarrow$ skip.
    - Compliance Override Match Choice = `Yes (123900001)` $\rightarrow$ skip.
    - Invalid tri-state numbers (not Any, Yes, or No) $\rightarrow$ skip.
- **Client Unit Text Matching**:
  - Routing Rule contains broad match text (e.g., `"Wealth Management"`).
  - Source Case has specific record name (e.g., `"Wealth Management Netherlands"`).
  - Algorithm:
    1. If rule text is blank/whitespace $\rightarrow$ match (`Any`).
    2. If source name is blank $\rightarrow$ no match.
    3. If source name equals rule text (case-insensitive) $\rightarrow$ match.
    4. If source name starts with `(ruleText + " ")` (space-delimited prefix) $\rightarrow$ match.
       *Why the space?* Prevents false positives (e.g., rule text `"Wealth"` will not match `"WealthX"`, but will match `"Wealth Management"`).
- **OptionSet Matching**:
  - For each populated rule condition (`ClientType`, `RoutingScenario`, `GroupComposition`, `RoutingRisk`, `OneKycStage`, `OneKycCaseType`), the integer values must match. Blank rule condition means `Any`.
- **Catch-All Protection**:
  - Rules with 100% blank conditions are rejected via `HasAnyConfirmedSpecificCondition` to prevent unintentional default fallbacks.

#### 3. `ApprovalRoutingService.cs` (End-to-End Orchestrator)
Orchestrates the entire routing pipeline:
1. Validates stage trigger gate.
2. Extracts source Case, Party, Client Unit, Group.
3. Derives Scenario, Group Composition, and Risk Scores into `RoutingContextData`.
4. Checks whether an active `ka_kycapprovalcase` already exists for this OneKYC Case:
   - If **none exists**: Prepares for a **CREATE** operation.
   - If **one exists**: Prepares for an **UPDATE** operation to synchronize existing records.
5. Queries active rules and invokes `RoutingRuleMatcher`:
   - If `matchingRules.Count == 0`: Logs trace and exits cleanly without creating invalid orphan records.
   - If `matchingRules.Count > 1`: Logs a severe **CONFIGURATION ERROR** listing all conflicting rule IDs and names, and stops cleanly to prevent arbitrary flow selection.
   - If `matchingRules.Count == 1`: Rule index 0 is the winning rule.
6. Retrieves Case Analyst and Group display info.
7. Executes `CreateOrUpdateApprovalTask`:
   - Reuses existing Task if one was already linked to the Case, applying sparse updates (`ka_flow` and primary name).
   - Creates a new Task if none existed.
8. Executes `CreateOrUpdateApprovalCase`:
   - In update mode: Synchronizes all fields via `AddIfDifferent`. Clears fields that have become null in source.
   - In create mode: Inserts new KYC Approval Case.
9. Writes final audit identifiers to `ITracingService`.

---

### Layer 6: Plugin Entry Point (`Plugin/`)

#### `LocalPluginContext.cs`
Wraps Dataverse's `IServiceProvider` to provide typed access to:
- `IPluginExecutionContext`
- `IOrganizationService` (created via `IOrganizationServiceFactory.CreateOrganizationService(context.UserId)`)
- `ITracingService`

#### `RouteOneKycApprovalAndAdvicePlugin.cs`
Clean `< 80` line entry point implementing `IPlugin`:
- Validates:
  - `MessageName == "Create"`
  - `PrimaryEntityName == "aab_approvalandadvice"`
  - `Stage == 40` (PostOperation)
  - `Depth <= 1` (Recursion guard)
  - Valid `PrimaryEntityId` (with `Target` parameter fallback)
- Instantiates dependencies and calls `ApprovalRoutingService.RouteApprovalAndAdvice(id)`.
- Handles exceptions:
  - Rethrows `InvalidPluginExecutionException` to ensure synchronous transaction rollback.
  - Catches unexpected `Exception`, writes details to Plugin Trace Log, and wraps it in `InvalidPluginExecutionException`.

---

## 4. Plugin Registration Step Configuration

Register the plugin using the **Dataverse Plugin Registration Tool** (PRT):

| Setting | Value |
|---|---|
| **Assembly** | `OneApproval.Plugins.dll` |
| **Plugin Type** | `OneApproval.Plugins.RouteOneKycApprovalAndAdvicePlugin` |
| **Message** | `Create` |
| **Primary Entity** | `aab_approvalandadvice` |
| **Stage of Execution** | `PostOperation` (40) |
| **Execution Mode** | `Synchronous` |
| **Filtering Attributes** | All Attributes (or `aab_stage`, `aab_caseid`) |
| **Deployment** | Server |
| **Execution Order** | `1` |
