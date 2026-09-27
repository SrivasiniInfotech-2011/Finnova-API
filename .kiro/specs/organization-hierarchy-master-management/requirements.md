# Requirements Document

## Introduction

The Organization Hierarchy Master Management feature (JIRA: FINNOVA-5, Module: SystemAdmin, Master: Organization Hierarchy) provides a centralized administrative capability for defining and maintaining the Finnova platform's organizational structure as a tree of nodes (for example, Head Office → Region → Branch). The organization hierarchy is standardized in one place so that business modules can reference a single, consistent structure rather than duplicating org definitions per module.

A System Administrator can define the hierarchy structure by creating Organization Nodes, each carrying a code, a single English display name, a hierarchy level, and a parent relationship (root nodes have no parent). A System Administrator can modify an existing node's name and re-parent it to a different parent, subject to structural validation rules that keep the hierarchy a valid tree (unique node code, valid parent reference, no self-parenting, no cycles, and level consistency with the parent). A System Administrator can search nodes by code or name with pagination, read a node's direct children, and read the hierarchy tree. Every modification is captured in an audit trail so that changes to the standardized organization structure are traceable. All management and read operations are restricted to System Administrators; non-admin users are denied access.

This master-data feature mirrors the established Nationality Master Management feature (FINNOVA-9) and the Lookup Master Management feature (FINNOVA-8). It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI); this backend spec is API-only.

This document captures the requirements derived from the JIRA ticket (FINNOVA-5) acceptance criteria. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** Organization Hierarchy Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host (which already exposes the Lookup Master and Nationality Master), reached through the gateway prefix `/api/systemadmin/**`. This mirrors the Nationality Master decision and is confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8 and reused by FINNOVA-9). This feature reuses that scheme and policy. Token issuance (login) ownership is external to this feature.
- **Assumption (Audit trail scope):** The nationality-scoped audit trail introduced by FINNOVA-9 is not a shared, platform-wide capability today. This feature introduces an organization-hierarchy-scoped audit trail (Requirement 6). Whether the audit trail should be promoted to a shared, platform-wide capability is flagged for confirmation in design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Each organization node carries a single English display name (`Name`); there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **Organization Hierarchy Master**: The centralized administrative structure describing the Finnova organization as a tree of Organization Nodes used across business modules.
- **Organization Node (Node)**: A single record in the Organization Hierarchy Master, consisting of a Node Code, a single English display Name, a Level, an optional Parent reference, an Is Active flag, and audit timestamps.
- **Node Code**: A short machine-readable identifier for an organization node (for example, `HO`, `RGN-N`), unique across the Organization Hierarchy Master.
- **Name**: The single English display name of an organization node (for example, `Head Office`).
- **Level**: A positive integer indicating the depth of a node in the hierarchy, where a root node is Level 1 and each child is exactly one greater than its parent's Level.
- **Parent**: The Organization Node directly above a given node in the hierarchy. A Root Node has no Parent.
- **Root Node**: An Organization Node that has no Parent and whose Level is 1.
- **Child Node**: An Organization Node whose Parent reference points to another Organization Node.
- **Relationship**: The directed parent-child link between two Organization Nodes.
- **Cycle**: A sequence of parent references that returns to a node already visited (for example, A is parent of B and B is made parent of A). A valid hierarchy contains no cycles.
- **Hierarchy Tree**: The full set of Organization Nodes connected by their Parent relationships, forming one or more rooted trees.
- **Is Active**: A boolean flag indicating whether an organization node is available for consumption by business modules. Defaults to true.
- **Audit Entry**: An immutable record capturing a change to an organization node, including the affected node identifier, the action performed, the changed fields' before and after values, the acting System Administrator's identifier, and the timestamp of the change.
- **Audit Trail**: The ordered collection of Audit Entries recorded for the Organization Hierarchy Master.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim, authorized to manage organization nodes.
- **Consuming Module**: A business module that reads active organization nodes to reference the organization structure.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **OrgHierarchy_Service**: The backend service/API responsible for organization node management and retrieval.
- **OrgHierarchy_Admin_UI**: The React-based Organization Hierarchy Master administrative screen used by System Administrators (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Define Hierarchy Structure and Create Organization Node

**User Story:** As a System Administrator, I want to create a new organization node with its code, name, level, and parent, so that the organization structure is defined and available for consuming modules.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing a Node Code and a Name, THE OrgHierarchy_Service SHALL persist a new organization node and return the created record including a system-generated identifier, the resolved Level, the resolved Parent reference, and the resolved Is Active value.
2. WHERE a create request omits a Parent reference, THE OrgHierarchy_Service SHALL create the node as a Root Node with Level 1.
3. WHERE a create request provides a Parent reference, THE OrgHierarchy_Service SHALL set the new node's Level to the Parent's Level plus 1, ignoring any Level value supplied on the request so that Level is always resolved by the Service from the Parent relationship.
4. WHERE a create request provides Is Active, THE OrgHierarchy_Service SHALL persist the provided value; WHERE Is Active is absent, THE OrgHierarchy_Service SHALL default Is Active to true.
5. IF a create request omits Node Code or omits Name, or provides either as an empty or whitespace-only value, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
6. IF Node Code exceeds 20 characters after trimming leading and trailing whitespace, or Name exceeds 150 characters after trimming leading and trailing whitespace, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
   - **Assumption:** Length bounds (Code 20, Name 150) are not specified in the ticket; these values are proposed for confirmation.
7. IF a create request provides a Parent reference that does not correspond to an existing organization node, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the parent node does not exist and SHALL NOT persist any record.
8. IF a create request would produce a node whose Level exceeds the maximum supported hierarchy depth of 10, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the maximum hierarchy depth is exceeded and SHALL NOT persist any record.
   - **Assumption:** The ticket does not specify a maximum hierarchy depth; a bound of 10 levels is proposed for confirmation.
9. WHEN an organization node is created with Is Active set to true, THE OrgHierarchy_Service SHALL include the node in the results of the node query (Requirement 5) without requiring a service restart.

### Requirement 2: Prevent Duplicate Node Code

**User Story:** As a System Administrator, I want the system to prevent duplicate node codes, so that each organization node is uniquely identified.

#### Acceptance Criteria

1. THE OrgHierarchy_Service SHALL treat Node Code as unique across the Organization Hierarchy Master, comparing codes case-insensitively (for example, `HO` and `ho` are treated as the same code) after trimming leading and trailing whitespace.
2. IF a create request specifies a Node Code that, after trimming and case-insensitive comparison, matches the Node Code of an existing organization node, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error carrying the message "Node code must be unique", SHALL create no new record, and SHALL retain the existing record unchanged.
3. IF an update request would change a node's Node Code to a value that, after trimming and case-insensitive comparison, matches the Node Code of an existing organization node whose identifier differs from the node being updated, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error carrying the message "Node code must be unique" and SHALL preserve both records unchanged.
4. WHEN an update request submits, for the node identified by the request, a Node Code that after trimming and case-insensitive comparison equals that same node's current Node Code (differing only by letter casing or surrounding whitespace), THE OrgHierarchy_Service SHALL treat the code as unchanged for the uniqueness check and SHALL NOT reject the request on the grounds of duplication.
5. IF a create or update request specifies a Node Code that is empty, contains only whitespace, or exceeds 20 characters after trimming, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the node code is required and must be at most 20 characters, and SHALL make no change to the Organization Hierarchy Master.
6. WHEN the OrgHierarchy_Service evaluates the uniqueness of a Node Code on a create or update request, THE OrgHierarchy_Service SHALL apply the required and length validation (criterion 5) before the duplicate check (criteria 2 and 3), such that a request with an empty, whitespace-only, or over-length Node Code is rejected on those grounds rather than on duplication.

### Requirement 3: Enforce Hierarchy Structural Validity

**User Story:** As a System Administrator, I want the system to enforce tree validity when nodes and relationships are created or changed, so that the organization hierarchy remains a consistent, acyclic tree.

#### Acceptance Criteria

1. IF a create or update request sets a node's Parent reference to the node's own identifier, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating a node cannot be its own parent and SHALL make no change to the Organization Hierarchy Master.
2. IF an update request sets a node's Parent reference to one of the node's own descendants, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the change would create a cycle and SHALL make no change to the Organization Hierarchy Master.
3. WHEN the OrgHierarchy_Service assigns a node a Parent, THE OrgHierarchy_Service SHALL set the node's Level to exactly the Parent's Level plus 1 so that Level remains consistent with the Parent relationship.
4. WHEN the OrgHierarchy_Service re-parents a node that has descendants, THE OrgHierarchy_Service SHALL recompute the Level of the moved node and of every descendant so that each node's Level remains exactly one greater than its Parent's Level, applying the recomputation atomically as a single unit with no partial persistence.
   - **Assumption:** Re-parenting a subtree cascades Level recomputation to all descendants; the ticket does not specify subtree behavior, so this cascade is proposed for confirmation.
5. IF re-parenting a node would cause the moved node or any descendant's recomputed Level to fall outside the supported hierarchy depth range of 1 through 10, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the maximum hierarchy depth would be exceeded and SHALL make no change to the Organization Hierarchy Master.
6. THE OrgHierarchy_Service SHALL treat a node whose Parent reference is absent as a Root Node with Level 1.

### Requirement 4: Modify Organization Node and Relationships

**User Story:** As a System Administrator, I want to update an existing node's name and change its parent relationship, so that the organization structure stays accurate as the organization changes.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing node's Name and the submitted value passes validation, THE OrgHierarchy_Service SHALL persist the change, refresh the record's UpdatedAt timestamp, and return the updated record reflecting the submitted Name.
2. WHEN a System Administrator submits an update that changes an existing node's Parent reference to a different existing organization node and the change satisfies the structural-validity rules of Requirement 3 (no self-parenting, no cycle, and recomputed Levels within the maximum hierarchy depth of 10), THE OrgHierarchy_Service SHALL persist the new Relationship, recompute the moved node's Level to exactly the new Parent's Level plus 1 and every descendant's Level to exactly one greater than its own Parent's Level per Requirement 3, refresh the record's UpdatedAt timestamp, and return the updated record.
3. WHERE an update request sets the Parent reference to empty for a node that currently has a Parent, THE OrgHierarchy_Service SHALL make the node a Root Node with Level 1, recompute every descendant's Level to exactly one greater than its own Parent's Level, refresh the record's UpdatedAt timestamp, and return the updated record.
   - **Assumption:** Promoting a child to a Root Node by clearing its Parent is treated as allowed re-parenting; the ticket does not specify this, so it is proposed for confirmation.
4. IF a System Administrator submits an update where Name is empty or contains only whitespace, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error identifying Name as required and SHALL preserve the existing record unchanged.
5. IF a System Administrator submits an update where Name exceeds 150 characters, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error identifying Name and SHALL preserve the existing record unchanged.
6. IF a System Administrator submits an update whose Parent reference does not correspond to an existing organization node, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the parent node does not exist and SHALL preserve the existing record unchanged.
7. IF a System Administrator attempts to update a node whose identifier does not exist, THEN THE OrgHierarchy_Service SHALL return a not-found error and SHALL make no change to the Organization Hierarchy Master.
8. WHEN a System Administrator submits an update whose Name and Parent reference equal the record's current Name and Parent, THE OrgHierarchy_Service SHALL treat the update as a successful no-op, SHALL NOT alter the record's UpdatedAt timestamp or any stored field, and SHALL return the existing record unchanged.
   - **Assumption:** Re-parenting is allowed by this feature (the ticket calls for modifying relationships). If re-parenting must be restricted or disabled, that is flagged for confirmation.

### Requirement 5: Query, Search, and Read the Hierarchy

**User Story:** As a System Administrator, I want to search nodes by code or name and read the hierarchy tree and a node's children, so that I can locate and understand the organization structure.

#### Acceptance Criteria

1. WHEN a System Administrator requests organization nodes with a non-empty search term, THE OrgHierarchy_Service SHALL return only the nodes whose Node Code or Name contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
2. WHEN a System Administrator requests organization nodes with a search term consisting only of whitespace or an empty string, THE OrgHierarchy_Service SHALL treat the request as having no search term and return the organization nodes subject to the applied pagination parameters.
3. WHEN a System Administrator requests organization nodes, THE OrgHierarchy_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
4. WHEN a System Administrator requests organization nodes without specifying page number, THE OrgHierarchy_Service SHALL apply a default page number of 1.
5. WHEN a System Administrator requests organization nodes without specifying page size, THE OrgHierarchy_Service SHALL apply a default page size of 20.
6. IF a System Administrator requests a page number less than 1, THEN THE OrgHierarchy_Service SHALL reject the request and return a validation error indicating the page number is out of range, without returning any records.
7. IF a System Administrator requests a page size less than 1 or greater than 100, THEN THE OrgHierarchy_Service SHALL reject the request and return a validation error indicating the page size is out of range, without returning any records.
8. WHEN a System Administrator requests a page number beyond the last available page, THE OrgHierarchy_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
9. WHEN returning organization nodes, THE OrgHierarchy_Service SHALL order the records by Level ascending, and for records sharing the same Level SHALL apply a deterministic secondary ordering by Name ascending and a tertiary ordering by Node Code ascending.
10. WHEN a System Administrator requests the direct children of a specified node identifier, THE OrgHierarchy_Service SHALL return only the nodes whose Parent reference points to the specified node, ordered by Name ascending with Node Code ascending as a tie-breaker.
11. WHEN a System Administrator requests the hierarchy tree, THE OrgHierarchy_Service SHALL return the organization nodes structured so that each node's children are reachable from the node, with all Root Nodes presented at the top level ordered by Name ascending and by Node Code ascending as a tie-breaker, and with each node's child nodes ordered by Name ascending and by Node Code ascending as a tie-breaker.
12. IF a System Administrator requests the children of a node identifier that does not exist, THEN THE OrgHierarchy_Service SHALL return a not-found error and SHALL NOT return any nodes.
13. IF a search request matches no organization nodes, THEN THE OrgHierarchy_Service SHALL return an empty item collection with a total count of 0 and a success status.
14. WHEN a System Administrator requests the direct children of an existing node identifier that has no Child Nodes, THE OrgHierarchy_Service SHALL return an empty item collection with a success status and SHALL NOT return a not-found error.
15. WHEN a System Administrator requests the hierarchy tree while the Organization Hierarchy Master contains no organization nodes, THE OrgHierarchy_Service SHALL return an empty tree structure with a success status and SHALL NOT return an error.

### Requirement 6: Record Audit Trail on Modification

**User Story:** As a System Administrator, I want every node and relationship change captured in an audit trail, so that modifications to the standardized organization structure are traceable.

#### Acceptance Criteria

1. WHEN the OrgHierarchy_Service persists a change to a node's Name or Parent relationship (per Requirement 4), THE OrgHierarchy_Service SHALL record exactly one Audit Entry containing the affected node identifier, the update action indicator, the prior and new values of each changed field (Name and Parent, where an absent Parent is represented as no parent value), the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).
2. WHEN the OrgHierarchy_Service creates a node (per Requirement 1), THE OrgHierarchy_Service SHALL record exactly one Audit Entry containing the created node identifier, the create action indicator, the resulting field values, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).
   - **Assumption:** The ticket calls out an audit trail on modification; recording a create-time entry as well is proposed for a complete trail. Confirm whether create should be audited.
3. IF a create or update request is rejected by validation, by the duplicate-code rule (Requirement 2), or by a structural-validity rule (Requirement 3), THEN THE OrgHierarchy_Service SHALL NOT record an Audit Entry for that rejected request, and SHALL leave the count of persisted Audit Entries unchanged.
4. WHEN a System Administrator requests the audit entries for a specified node, THE OrgHierarchy_Service SHALL return the Audit Entries for that node ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
5. IF a System Administrator requests the audit entries for a node identifier that does not exist, THEN THE OrgHierarchy_Service SHALL return an empty result set and SHALL NOT return an error.
6. THE OrgHierarchy_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that any request to update or delete a previously recorded Audit Entry is rejected with an error indicating that audit entries are immutable.
7. WHEN a System Administrator submits an update whose Name and Parent reference equal the record's current Name and Parent and the OrgHierarchy_Service treats it as a successful no-op (per Requirement 4), THE OrgHierarchy_Service SHALL NOT record an Audit Entry and SHALL leave the count of persisted Audit Entries unchanged.
8. WHEN the OrgHierarchy_Service persists the deletion of an organization node (per Requirement 8), THE OrgHierarchy_Service SHALL record exactly one Audit Entry containing the deleted node identifier, the delete action indicator, the prior field values of the deleted node, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).

### Requirement 7: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want organization hierarchy management and read operations restricted to System Administrators, so that only authorized personnel can view or change the standardized organization structure.

#### Acceptance Criteria

1. IF a request to create, update, query, read the hierarchy or children, or read the audit trail of an organization node is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE OrgHierarchy_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored organization node, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts to create, update, query, read the hierarchy or children, or read the audit trail of an organization node, THEN THE OrgHierarchy_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored organization node, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, update, query, hierarchy-read, children-read, or audit-trail-read operation, THE OrgHierarchy_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 6.
4. WHEN the OrgHierarchy_Service evaluates authentication and authorization for any create, update, query, hierarchy-read, children-read, or audit-trail-read request, THE OrgHierarchy_Service SHALL enforce authentication before authorization such that a request carrying a missing, expired, malformed, or signature-invalid JWT Bearer token that also lacks SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the OrgHierarchy_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy (evaluated via the role claim) established by FINNOVA-8 and reused by FINNOVA-9. Establishing that scheme is a prerequisite already satisfied by the Lookup Master and Nationality Master features.
   - **Assumption:** Read access (query, hierarchy read, children read, and audit-trail read) is restricted to System Administrators, mirroring the Nationality Master's authorization requirement. If broader read access is desired for consuming modules, that is flagged for confirmation in design.

### Requirement 8: Node Deletion Scope

**User Story:** As a System Administrator, I want deletion of organization nodes to be well-defined, so that removing a node does not orphan children or corrupt the hierarchy.

#### Acceptance Criteria

1. WHEN a System Administrator deletes an existing organization node that has no Child Nodes, THE OrgHierarchy_Service SHALL remove the node so that it no longer appears in query (Requirement 5), children-read, or hierarchy-read results and is no longer available to Consuming Modules, and SHALL record an Audit Entry for the deletion action per Requirement 6.
2. IF a System Administrator attempts to delete an organization node that has one or more Child Nodes, THEN THE OrgHierarchy_Service SHALL reject the deletion with a validation error indicating the node has children, SHALL preserve the node and all of its Child Nodes unchanged, and SHALL record no Audit Entry for the rejected deletion.
   - **Assumption:** The ticket scope names Create and Modify; delete behavior is not specified. A leaf-only hard delete is proposed as the safe default (cascade delete and soft delete are alternatives) and is flagged for confirmation in design.
3. IF a System Administrator attempts to delete an organization node whose identifier does not exist, THEN THE OrgHierarchy_Service SHALL return a not-found error and SHALL make no change to the Organization Hierarchy Master.
4. IF a System Administrator submits a delete request whose node identifier is empty, whitespace-only, or otherwise not a well-formed identifier, THEN THE OrgHierarchy_Service SHALL reject the request with a validation error indicating the node identifier is required and SHALL make no change to the Organization Hierarchy Master.
