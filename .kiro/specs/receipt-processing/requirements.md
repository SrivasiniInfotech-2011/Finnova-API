# Requirements Document

## Introduction

The Receipt Processing feature (JIRA: FINNOVA-61, "Receipt Processing – Multi-Mode Receipt Entry (Cash, Cheque, Bank Transfer) with Denominations & Adjustments"; FRS Sections 1.6 and 1.13) lets a Cashier or Branch Collection Officer capture a customer payment as a receipt. A receipt is one of three types (Temporary, Regular, Third-Party) and is received in one of three modes (Cash, Cheque, Bank Transfer). The receipt acknowledges funds received, allocates the amount to one or more live contracts (loan accounts) of the selected customer, optionally applies Add/Less adjustments, and is saved in the status "Pending Authorization" with a unique, system-generated Document Number so that it can later be authorized and posted to the General Ledger.

The three receipt types share one screen layout and one field set. The single difference is the TR Ref No field, which is available only on Regular and Third-Party receipts. The payment mode drives which details are required: Cash requires a currency denomination breakdown whose total equals the Document Amount; Cheque requires instrument and drawee bank details; Bank Transfer requires the deposit bank, account, MICR, bank branch, and GL/SL codes taken from the F&A bank master. The Document Amount equals the sum of the Account Details (lines) grid plus the Add entries and minus the Less entries of the Add/Less grid.

Each line of a receipt is credited to a General Ledger account. WHEN GL Based is Yes, the credit goes to the GL account entered on the line. WHEN GL Based is No, the credit goes to the GL account of the Customer. The service resolves and stores the credit GL account on each line at save time so that the later posting step is deterministic.

This specification covers the receipt capture slice: create (Save), read, list, audit trail, and the reference-data reads that the screen needs. The Authorize and Revoke actions shown on the legacy Receipt Authorization screens, and the submission of an authorized receipt to the JV Posting Engine, are a follow-up (see Assumption A2). The backend is delivered inside the existing `Finnova.AccountsService` host behind the existing `Finnova.ApiGateway`, following the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, `IRepository<T>`-style repositories, record-based contracts, `PaginatedResponse<T>`). The user-facing screen is implemented in the Finnova-UI repository (React + TypeScript + Material UI, service-layer with mock/real toggle).

Finnova is India-only and English-only. All amounts are in Indian Rupees (INR) with two decimals. The Jira story and the legacy screens refer to Oman (RO currency, Baisa coins, three-decimal amounts, Muscat); those references are not carried into this specification (see Assumption A1).

### Dependencies and Assumptions

- **Assumption A1 (Localization conflict, needs confirmation):** The story, the Gherkin examples (for example "3500.000"), and the legacy screens use Omani conventions (RO currency, Baisa coins, three-decimal amounts, Muscat, Sultanate of Oman). Finnova is India-only (product-context steering). This specification therefore uses INR (Rupees and Paise), two-decimal amounts, and Indian denominations. The Gherkin amounts are read as 3500.00, 3200.00, 300.00, 500.00, and 450.00. The denomination catalog is: notes of 500, 200, 100, 50, 20, and 10; coins of 10, 5, 2, and 1; and a 0.50 (50 paise) coin. Legacy ₹2000 notes are excluded because they are withdrawn from circulation. Amounts in words use the Indian numbering system (Lakh, Crore). Confirm before implementation.
- **Assumption A2 (Authorize, Revoke, and GL posting are a follow-up):** The legacy "Receipt Authorization" screens (Authorize, Revoke, Close buttons; Authorized By; Authorized Date) are a separate maker-checker step. This ticket saves a receipt in "Pending Authorization", exposes it read-only, and stores everything the posting step needs (resolved credit GL per line, bank GL/SL for Bank Transfer). The ReceiptStatus enum includes `Authorized` and `Revoked` and the receipt carries nullable `AuthorizedBy` and `AuthorizedAtUtc` columns, but no endpoint changes them in this ticket. Authorization, revocation, and submission to the JV Posting Engine (inbox submission through `JvPostingSubmissionService`, with the debit side resolved by the future Cashflow Master rule) are delivered by a follow-up ticket. Confirm.
- **Assumption A3 (Host and gateway):** Receipt Processing is hosted in the existing `Finnova.AccountsService` (accounts-cluster, port 5020) alongside loan accounts and demand processing, under the route `api/receipts`. The UI reaches it through the gateway alias `/api/ua/api/receipts/**`, which is rewritten to `/api/receipts/**` on the accounts cluster (the same pattern as `ua-loan-accounts-alias-route`).
- **Assumption A4 (Contract = Loan Account):** The "contract" and "Account Number" of the story map to the existing `LoanAccount` entity (`AccountNumber`, `CustomerId`, `Status`, `ClosedAt`). A contract is "live" when its Status is `Active` and `ClosedAt` is null. The existing `AccountStatus` enum has no "pre-maturely terminated" or "cancelled" values; those states, when introduced, are expected to be non-Active and are therefore rejected without further change. `Inactive` and `Frozen` accounts are also rejected. Confirm that Frozen accounts must not accept receipts.
- **Assumption A5 (Prime and Sub Account No):** "Prime Account No" is the `LoanAccount.AccountNumber`. "Sub Account No" is an optional free-text reference of at most 30 characters; no sub-account master exists and the value is not validated.
- **Assumption A6 (Entry mode and Document Number):** The radio "Online" (default) means the system issues the Document Number on Save through the existing Document Number Control (DCN) module, using the document types `TemporaryReceipt`, `RegularReceipt`, and `ThirdPartyReceipt` resolved by Line of Business and Branch (fallback issuance). The radio "Manual" means the user keys a pre-printed receipt book number; a Manual number does not consume a DCN sequence. Numbering schemes for the three document types must be configured by a System Administrator before go-live. If persistence fails after a number is issued, the number is skipped (gap); the DCN module does not support rollback of a sequence.
- **Assumption A7 (Idempotent Save):** The UI generates a `ClientRequestId` (GUID) when the Create screen opens. The service treats it as an idempotency key so that a double click or a retry does not issue two Document Numbers. This key is not in the source material and is proposed for confirmation.
- **Assumption A8 (F&A Bank Master does not exist):** The repository contains no F&A Bank Master. This feature introduces a minimal, read-only `FaBankAccount` table (Bank Name, Bank Branch, Account Number, MICR Code, GL Code, SL Code, Is Active) as the lookup source for Bank Transfer. Maintenance of that master is out of scope and a future F&A Bank Master feature is expected to own the table. Rows are loaded by a documented dev seed script, not by a production seed. Confirm table ownership.
- **Assumption A9 (Drawee Bank source):** "Drawee Bank (from Challan rules master)" maps to the existing Drawee Bank Master (`DraweeBank` with `DraweeBranch`, FINNOVA-16). Only active banks are offered. Drawee Branch is selected from the branches of the chosen bank whose effective date range includes the receipt Value Date. Challan rule formats (`ChallanRule.FormatPattern`) are not applied to Instrument No in this ticket because the story fixes Instrument No at six digits.
- **Assumption A10 (Marketing Officer):** "Marketing Officer" is an active `UserAccount` (user management master). The receipt stores the user identifier.
- **Assumption A11 (Receipt From):** "Receipt From" has the values Customer (default), Guarantor, and Third Party. A Third-Party receipt must have a Receipt From other than Customer. Temporary and Regular receipts accept any value. No separate payer name field is captured.
- **Assumption A12 (GL Based field):** "GL Based" is a Yes/No flag on the receipt header, default No. The legacy screens do not show it. When GL Based is No, any GL Account or SL Account supplied on a line is ignored and the Customer's GL account is stored as the credit GL.
- **Assumption A13 (Line components):** The Gherkin lines "Installment Inflow" and "ODI" are modeled as a per-line `Component` taken from the Lookup Master (module `Receipts`, lookup type `RECEIPT_COMPONENT`, for example `INSTALLMENT_INFLOW` and `ODI`). The lookup values must be created by a System Administrator. The line SL Account is an optional free-text code of at most 20 characters and is not validated against a master.
- **Assumption A14 (TR Ref No):** TR Ref No is optional free text of at most 30 characters on Regular and Third-Party receipts. It is not validated against existing Temporary receipts in this ticket. Confirm whether a Regular receipt must reference an existing Temporary receipt.
- **Assumption A15 (Instrument date and cheque rules):** Instrument Date defaults to the current date in the business timezone Asia/Kolkata. Post-dated and stale (older than three months) cheques are accepted in this ticket; no duplicate-cheque check is performed. These controls are flagged for confirmation.
- **Assumption A16 (Add/Less sign convention):** An "Add" row increases the Document Amount and a "Less" row decreases it. Each Add/Less row carries an Account Number (validated like a line account), a GL Account (required, an active, approved, postable GL), and an amount greater than zero.
- **Assumption A17 (Authorization and scope):** Two new roles are added to the role catalog: `Cashier` and `BranchCollectionOfficer`. Both, and `SystemAdmin`, may create and read receipts. Restricting a user to specific Lines of Business or Branches through their access assignments is not enforced in this ticket and is flagged as a follow-up.
- **Assumption A18 (Amount limits):** A Document Amount is greater than 0.00 and at most 99,99,99,999.99 (less than 100 Crore). Each denomination count is an integer from 0 to 1,000,000.
- **Assumption A19 (Currency and time zone):** The currency is fixed to INR. Dates are calendar dates. "Today" and "current date" are evaluated in Asia/Kolkata.
- **Assumption A20 (Appropriation button not in scope):** The legacy Create screen has an Appropriation button (automatic allocation of the amount across dues). The story does not describe it, so it is not built. The Customer section offers a Show button that lists the live contracts of the customer instead. Confirm whether Appropriation is wanted in this ticket.
- **Dependency (Existing masters):** Customer (`Customer`, `CustomerAddress`, `GLAccountId`), Loan Account, Line of Business, Location (Level 5 = Branch), User Management, Lookup, Drawee Bank, GL Master (`IGLAccountRepository`), and Document Number Control (`IssueNumberCommand`) are reused without modification.
- **Dependency (Repository location):** The backend repository is `d:\Projects\Finnova\Finnova-API`. The UI repository was found at `D:\Projects\Finnova\Finnova-UI` on this machine (the steering file names `E:\Finnova\Finnova-UI\Finnova-UI`); the UI tasks use paths relative to the UI repository root `src/`.

## Glossary

- **Receipt**: A saved record of funds received from a payer, with header, mode-specific details, Account Details lines, and Add/Less rows.
- **Receipt_Service**: The backend service, hosted in `Finnova.AccountsService`, that creates, reads, lists, and audits receipts and serves receipt reference data.
- **Receipt_UI**: The React "Receipt Processing" create screen and the receipt list/view screen in Finnova-UI.
- **Receipt_Calculator**: The pure domain helper that computes line totals, adjustment totals, the Document Amount, and the denomination total.
- **Amount_In_Words_Formatter**: The pure helper that converts an INR amount to words using the Indian numbering system.
- **Receipt Type**: One of Temporary, Regular, or Third-Party.
- **Receipt Mode (Mode)**: One of Cash, Cheque, or Bank Transfer.
- **Entry Mode**: Online (system-issued Document Number) or Manual (user-keyed Document Number).
- **Document Number (Doc No)**: The unique number of a receipt.
- **Sequence Number**: The running sequence value issued by Document Number Control for an Online Document Number.
- **Document Amount (Doc Amount)**: The total receipt amount. Equals the sum of the Account Details lines plus the sum of Add rows minus the sum of Less rows.
- **Account Details (Lines)**: The grid of allocations, each with Prime Account No, Sub Account No, Component, GL Account, SL Account, and Amount.
- **Add/Less Row**: A grid row with Account Number, Vehicle Number / Machine Serial Number, Add or Less, GL Account, SL Account, Amount, and Remarks that adjusts the Document Amount.
- **GL Based**: A Yes/No flag. Yes: the credit GL of each line is the GL Account entered on the line. No: the credit GL of each line is the Customer's GL account.
- **Cash Denomination**: A row of the denomination grid with a kind (Note or Coin), a face value, a count, and an amount equal to face value multiplied by count.
- **Contract**: A Loan Account identified by its Account Number.
- **Live Contract**: A Loan Account with Status Active and no Closed date.
- **Drawee Bank / Drawee Branch**: Records of the Drawee Bank Master that identify the bank and place on which a cheque is drawn.
- **F&A Bank Account**: A row of the `FaBankAccount` table (the deposit bank of a Bank Transfer).
- **Pending Authorization**: The status of a receipt immediately after Save.
- **Receipt Audit Entry**: An immutable record of a receipt action, including the Document Number and Sequence Number.
- **ClientRequestId**: A GUID created by the UI per Create screen instance and used as an idempotency key for Save.
- **INR**: Indian Rupee; amounts carry two decimals (Rupees and Paise).
- **EARS**: Easy Approach to Requirements Syntax.

## Requirements

### Requirement 1: Receipt Types and TR Ref No Availability

**User Story:** As a Cashier, I want Temporary, Regular, and Third-Party receipts to share one layout, so that I capture every receipt type the same way.

#### Acceptance Criteria

1. THE Receipt_Service SHALL accept the Receipt Types Temporary, Regular, and Third-Party on a create request and SHALL reject any other value with a validation error identifying Receipt Type.
2. THE Receipt_UI SHALL present the same fields in the same layout for all three Receipt Types, with the exception of the TR Ref No field.
3. WHERE the Receipt Type is Regular or Third-Party, THE Receipt_UI SHALL display the TR Ref No field as an editable text field of at most 30 characters.
4. WHERE the Receipt Type is Temporary, THE Receipt_UI SHALL hide the TR Ref No field.
5. IF a create request for a Temporary receipt contains a TR Ref No, THEN THE Receipt_Service SHALL reject the request with a validation error stating that TR Ref No is not applicable to Temporary receipts and SHALL NOT persist any record.
6. IF a create request contains a TR Ref No longer than 30 characters, THEN THE Receipt_Service SHALL reject the request with a validation error identifying TR Ref No and SHALL NOT persist any record.
7. IF a create request for a Third-Party receipt has Receipt From equal to Customer, THEN THE Receipt_Service SHALL reject the request with a validation error stating that a Third-Party receipt requires a Receipt From other than Customer and SHALL NOT persist any record.

### Requirement 2: Save, Document Number, Status, and Idempotency

**User Story:** As a Cashier, I want Save to generate a unique Document Number and place the receipt in Pending Authorization, so that every receipt is traceable and awaits authorization.

#### Acceptance Criteria

1. WHEN a Cashier saves a valid receipt with Entry Mode Online, THE Receipt_Service SHALL obtain the next Document Number from Document Number Control using the document type that matches the Receipt Type, the Line of Business, and the Branch, and SHALL store the Document Number and the Sequence Number on the receipt.
2. WHEN a valid receipt is saved, THE Receipt_Service SHALL set the receipt status to Pending Authorization.
3. WHEN a valid receipt is saved, THE Receipt_Service SHALL persist the receipt header, the mode-specific details, the Account Details lines, the Add/Less rows, the cash denominations, and the Receipt Audit Entry in a single atomic operation.
4. WHEN a receipt is saved, THE Receipt_Service SHALL return the receipt including its identifier, Document Number, status, Document Amount, and amount in words with HTTP status 201.
5. WHERE the Entry Mode is Manual, THE Receipt_Service SHALL require a Document Number of 1 to 30 characters supplied by the user and SHALL NOT consume a Document Number Control sequence.
6. IF a Manual Document Number matches, after trimming and case-insensitive comparison, the Document Number of an existing receipt of the same Receipt Type, THEN THE Receipt_Service SHALL reject the request with a conflict error carrying the message "Document number already exists" and SHALL NOT persist any record.
7. WHERE the Entry Mode is Online, THE Receipt_Service SHALL ignore any Document Number supplied on the request.
8. IF no active numbering scheme exists for the document type of the receipt, THEN THE Receipt_Service SHALL reject the request with an error stating that no numbering scheme is configured for the Receipt Type and SHALL NOT persist any record.
9. WHEN a create request carries a ClientRequestId that matches an existing receipt, THE Receipt_Service SHALL return the existing receipt with HTTP status 200, SHALL NOT issue a new Document Number, and SHALL NOT write a new Receipt Audit Entry.
10. IF a create request omits ClientRequestId or provides an empty GUID, THEN THE Receipt_Service SHALL reject the request with a validation error identifying ClientRequestId.
11. THE Receipt_Service SHALL ensure that no two receipts of the same Receipt Type share a Document Number.
12. IF any validation or business rule of Requirements 3 to 9 fails, THEN THE Receipt_Service SHALL NOT issue a Document Number and SHALL NOT persist any record.

### Requirement 3: Receipt Header Fields and Reference Validation

**User Story:** As a Branch Collection Officer, I want the receipt header validated against the masters, so that every receipt refers to a real line of business, branch, officer, and customer.

#### Acceptance Criteria

1. IF a create request omits Doc Date, Value Date, Line of Business, Location (Branch), Marketing Officer, Mode, Receipt From, Customer Code, or Doc Amount, THEN THE Receipt_Service SHALL reject the request with a validation error identifying each missing field and SHALL NOT persist any record.
2. WHEN a create request omits Doc Date or Value Date, THE Receipt_UI SHALL default each of them to the current date.
3. IF Value Date is earlier than Doc Date, THEN THE Receipt_Service SHALL reject the request with a validation error stating that Value Date cannot be earlier than Doc Date and SHALL NOT persist any record.
4. IF Doc Date is later than the current date in Asia/Kolkata, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Doc Date and SHALL NOT persist any record.
5. IF the Line of Business does not exist or is inactive, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Line of Business and SHALL NOT persist any record.
6. IF the Branch does not exist, is inactive, or is not a Level 5 Location, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Location and SHALL NOT persist any record.
7. IF the Marketing Officer is not an existing active user, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Marketing Officer and SHALL NOT persist any record.
8. IF the Customer Code does not match an existing active Customer, THEN THE Receipt_Service SHALL reject the request with a not-found error for the customer and SHALL NOT persist any record.
9. WHEN a receipt is saved, THE Receipt_Service SHALL store a snapshot of the Customer Code and the Customer Name on the receipt.
10. IF Doc Amount is not greater than 0.00, has more than two decimal places, or exceeds 99,99,99,999.99, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Doc Amount and SHALL NOT persist any record.
11. WHEN a Customer is selected, THE Receipt_UI SHALL display the Customer Code, Address, Phone, Mobile, Email Id, and Website of the Customer as read-only values.
12. WHEN a Cashier selects Show on the Customer section, THE Receipt_UI SHALL list the live contracts of the selected Customer.

### Requirement 4: Cash Mode and Denominations

**User Story:** As a Cashier, I want to record the notes and coins received, so that the cash counted matches the receipt amount.

#### Acceptance Criteria

1. WHERE the Mode is Cash, THE Receipt_UI SHALL disable the Bank Details tab.
2. WHERE the Mode is Cash, THE Receipt_UI SHALL enable the Cash Denominations button and SHALL open a denomination grid with one row per entry of the denomination catalog and a count input per row.
3. WHEN a count is entered in the denomination grid, THE Receipt_UI SHALL display the row amount as face value multiplied by count and SHALL display the grid total.
4. WHERE the Mode is Cash, THE Receipt_Service SHALL require at least one denomination row with a count greater than zero.
5. WHEN a Cash receipt is saved, THE Receipt_Service SHALL compute each denomination amount as face value multiplied by count and SHALL ignore any denomination amount supplied by the client.
6. IF the sum of the denomination amounts of a Cash receipt differs from the Doc Amount, THEN THE Receipt_Service SHALL reject the request with the error message "Document Amount must match the sum of Cash Denominations" and SHALL NOT persist any record.
7. IF a denomination row has a face value or kind that is not in the denomination catalog, or a count that is not an integer from 0 to 1,000,000, or the same face value and kind appears twice, THEN THE Receipt_Service SHALL reject the request with a validation error identifying the denomination row and SHALL NOT persist any record.
8. IF a Cash receipt contains Cheque details or Bank Transfer details, THEN THE Receipt_Service SHALL reject the request with a validation error stating that bank details are not applicable to Cash and SHALL NOT persist any record.
9. IF a Cheque or Bank Transfer receipt contains cash denominations with a count greater than zero, THEN THE Receipt_Service SHALL reject the request with a validation error stating that denominations are applicable only to Cash and SHALL NOT persist any record.
10. WHEN the Receipt_UI is showing a Cash receipt whose denomination total differs from the Doc Amount, THE Receipt_UI SHALL display the message "Document Amount must match the sum of Cash Denominations" and SHALL disable Save.

### Requirement 5: Cheque Mode

**User Story:** As a Cashier, I want to capture cheque details, so that the instrument can be cleared and traced.

#### Acceptance Criteria

1. WHERE the Mode is Cheque, THE Receipt_UI SHALL enable the Bank Details tab with the fields Instrument No, Instrument Date, Drawee Bank, Drawee Branch, Payment Gateway Ref No, and ACK No.
2. WHERE the Mode is Cheque, THE Receipt_Service SHALL require Instrument No, Instrument Date, Drawee Bank, and Drawee Branch.
3. IF Instrument No is not exactly six digits, THEN THE Receipt_Service SHALL reject the request with a validation error stating that Instrument No must be six digits and SHALL NOT persist any record.
4. WHEN a create request omits Instrument Date, THE Receipt_Service SHALL set Instrument Date to the current date in Asia/Kolkata.
5. WHERE the Mode is Cheque, THE Receipt_UI SHALL default Instrument Date to the current date.
6. IF the Drawee Bank does not exist or is inactive in the Drawee Bank Master, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Drawee Bank and SHALL NOT persist any record.
7. IF the Drawee Branch does not belong to the selected Drawee Bank, or its effective date range does not include the Value Date, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Drawee Branch and SHALL NOT persist any record.
8. IF Payment Gateway Ref No or ACK No is longer than 50 characters, THEN THE Receipt_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
9. WHEN a Cheque receipt is saved, THE Receipt_Service SHALL store the Drawee Bank and Drawee Branch identifiers together with a snapshot of the Drawee Bank Code, Bank Name, and Place Name.
10. IF a Cheque receipt contains Bank Transfer details, THEN THE Receipt_Service SHALL reject the request with a validation error and SHALL NOT persist any record.

### Requirement 6: Bank Transfer Mode

**User Story:** As a Cashier, I want to choose the deposit bank for a transfer, so that the receipt carries the correct bank account and GL codes.

#### Acceptance Criteria

1. WHERE the Mode is Bank Transfer, THE Receipt_UI SHALL enable the Bank Details tab with a Deposit Bank selector and the read-only fields Account No, MICR, Bank Branch, GL Code, and SL Code.
2. WHERE the Mode is Bank Transfer, THE Receipt_Service SHALL require a Deposit Bank that identifies an active F&A Bank Account.
3. IF the F&A Bank Account does not exist or is inactive, THEN THE Receipt_Service SHALL reject the request with a validation error identifying Deposit Bank and SHALL NOT persist any record.
4. WHEN a Bank Transfer receipt is saved, THE Receipt_Service SHALL store the F&A Bank Account identifier together with a snapshot of the Bank Name, Account No, MICR Code, Bank Branch, GL Code, and SL Code taken from the F&A Bank Account.
5. IF a Bank Transfer receipt contains Cheque details, THEN THE Receipt_Service SHALL reject the request with a validation error and SHALL NOT persist any record.
6. THE Receipt_Service SHALL expose the active F&A Bank Accounts to users with read access to receipts.

### Requirement 7: Contract Validation

**User Story:** As a Branch Collection Officer, I want receipts accepted only against live contracts of the selected customer, so that funds are not applied to the wrong or closed contract.

#### Acceptance Criteria

1. WHEN a receipt is saved, THE Receipt_Service SHALL verify that the Prime Account No of every Account Details line and of every Add/Less row identifies an existing Contract.
2. IF a Prime Account No does not identify an existing Contract, THEN THE Receipt_Service SHALL reject the request with a business-rule error naming the account number and stating that the account was not found, and SHALL NOT persist any record.
3. IF a Contract does not have Status Active, or has a Closed date, THEN THE Receipt_Service SHALL reject the request with a business-rule error naming the account number and stating that the account is not live, and SHALL NOT persist any record.
4. IF a Contract does not belong to the Customer identified by the Customer Code of the receipt, THEN THE Receipt_Service SHALL reject the request with a business-rule error naming the account number and stating that the account does not belong to the customer, and SHALL NOT persist any record.
5. WHEN the Receipt_UI lists the contracts of a Customer for selection, THE Receipt_Service SHALL return only the Live Contracts of that Customer.

### Requirement 8: Account Details Lines and GL Credit Rule

**User Story:** As a Branch Collection Officer, I want to allocate the receipt to accounts and components, so that each part of the payment is credited to the correct ledger.

#### Acceptance Criteria

1. THE Receipt_Service SHALL require at least one Account Details line on every receipt.
2. IF a line omits Prime Account No, Component, or Amount, or has an Amount that is not greater than 0.00 or has more than two decimal places, THEN THE Receipt_Service SHALL reject the request with a validation error identifying the line and field and SHALL NOT persist any record.
3. IF a line Component is not an active value of the lookup type RECEIPT_COMPONENT in the module Receipts, THEN THE Receipt_Service SHALL reject the request with a validation error identifying the line Component and SHALL NOT persist any record.
4. WHERE GL Based is Yes, THE Receipt_Service SHALL require a GL Account on every line and SHALL resolve it to an active, approved, postable GL account of the Customer's company.
5. IF GL Based is Yes and a line GL Account does not resolve to an active, approved, postable GL account of the Customer's company, THEN THE Receipt_Service SHALL reject the request with a business-rule error naming the GL code and SHALL NOT persist any record.
6. WHERE GL Based is Yes, THE Receipt_Service SHALL store the resolved GL account as the credit GL of the line and SHALL store the entered SL Account on the line.
7. WHERE GL Based is No, THE Receipt_Service SHALL store the GL account of the Customer as the credit GL of every line and SHALL ignore any GL Account and SL Account supplied on the line.
8. IF GL Based is No and the Customer has no GL account, or the Customer GL account is not an active, approved, postable GL account, THEN THE Receipt_Service SHALL reject the request with a business-rule error stating that the customer GL account is not configured and SHALL NOT persist any record.
9. WHEN a receipt is saved, THE Receipt_Service SHALL store on each line the credit GL account identifier, the credit GL code, and the Account Description taken from the Contract name.
10. THE Receipt_Service SHALL accept at most 50 Account Details lines per receipt.

### Requirement 9: Add/Less Adjustments and Document Amount

**User Story:** As a Branch Collection Officer, I want to add or deduct charges on a receipt, so that the Document Amount reflects the lines and adjustments exactly.

#### Acceptance Criteria

1. THE Receipt_Calculator SHALL compute the Document Amount as the sum of the line amounts, plus the sum of the Add rows, minus the sum of the Less rows.
2. WHEN a receipt is saved, THE Receipt_Service SHALL verify that the submitted Doc Amount equals the value computed by the Receipt_Calculator.
3. IF the submitted Doc Amount differs from the computed value, THEN THE Receipt_Service SHALL reject the request with a business-rule error stating that Doc Amount must equal the sum of the lines adjusted by the Add/Less rows and SHALL NOT persist any record.
4. WHERE a receipt has no Add/Less rows, THE Receipt_Calculator SHALL compute the Document Amount as the sum of the line amounts.
5. IF an Add/Less row omits Account Number, Add or Less, GL Account, or Amount, or has an Amount that is not greater than 0.00 or has more than two decimal places, THEN THE Receipt_Service SHALL reject the request with a validation error identifying the row and field and SHALL NOT persist any record.
6. IF the GL Account of an Add/Less row does not resolve to an active, approved, postable GL account of the Customer's company, THEN THE Receipt_Service SHALL reject the request with a business-rule error naming the GL code and SHALL NOT persist any record.
7. IF the computed Document Amount is not greater than 0.00, THEN THE Receipt_Service SHALL reject the request with a business-rule error and SHALL NOT persist any record.
8. THE Receipt_Service SHALL accept at most 50 Add/Less rows per receipt.
9. WHEN the Cashier changes a line or an Add/Less row on the Create screen, THE Receipt_UI SHALL recompute and display the computed Document Amount and the amount in words.

### Requirement 10: Amount in Words and Currency

**User Story:** As a Cashier, I want the amount shown in words, so that the written and numeric amounts can be checked against each other.

#### Acceptance Criteria

1. THE Amount_In_Words_Formatter SHALL convert an INR amount from 0.00 to 99,99,99,999.99 into words using the Indian numbering system with the units Crore, Lakh, Thousand, and Hundred, the prefix "Rupees", the optional "and N Paise" part for a non-zero paise value, and the suffix "Only".
2. WHEN the amount is 3500.00, THE Amount_In_Words_Formatter SHALL return "Rupees Three Thousand Five Hundred Only".
3. WHEN the amount is 1234567.50, THE Amount_In_Words_Formatter SHALL return "Rupees Twelve Lakh Thirty-Four Thousand Five Hundred Sixty-Seven and Fifty Paise Only".
4. WHEN a receipt is saved, THE Receipt_Service SHALL store the amount in words of the Document Amount on the receipt.
5. THE Receipt_Service SHALL store and return every amount with two decimal places and the currency code INR.

### Requirement 11: Audit Trail

**User Story:** As an auditor, I want every saved receipt logged with its sequence number, so that issued numbers are traceable.

#### Acceptance Criteria

1. WHEN a receipt is saved, THE Receipt_Service SHALL write exactly one Receipt Audit Entry with the action "Created", the receipt identifier, the Document Number, the Sequence Number (null for a Manual Document Number), the acting user identifier, the correlation identifier, a non-sensitive change summary, and the UTC timestamp, within the same atomic operation as the receipt.
2. IF a create request is rejected, THEN THE Receipt_Service SHALL NOT write a Receipt Audit Entry.
3. THE Receipt_Service SHALL expose no operation that updates or deletes a Receipt Audit Entry.
4. WHEN an authorized reader requests the audit trail of a receipt, THE Receipt_Service SHALL return the entries ordered by timestamp descending and then by entry identifier descending.
5. IF the audit trail of a receipt identifier that does not exist is requested, THEN THE Receipt_Service SHALL return an empty list.
6. THE Receipt_Service SHALL exclude instrument numbers, account numbers, and customer contact data from the audit change summary.

### Requirement 12: Query and View Receipts

**User Story:** As a Cashier, I want to find and view saved receipts, so that I can confirm what was captured.

#### Acceptance Criteria

1. WHEN an authorized reader requests receipts, THE Receipt_Service SHALL return a `PaginatedResponse<T>` with the page items, total, page, page size, and total pages.
2. WHERE a search term is supplied, THE Receipt_Service SHALL return only receipts whose Document Number or Customer Code contains the term, matched case-insensitively.
3. WHERE filters for Receipt Type, Mode, Status, Line of Business, Branch, or a Doc Date range are supplied, THE Receipt_Service SHALL return only receipts that match all supplied filters.
4. WHEN no page number is supplied, THE Receipt_Service SHALL apply page 1, and WHEN no page size is supplied, THE Receipt_Service SHALL apply page size 20.
5. IF the page number is less than 1, or the page size is less than 1 or greater than 100, THEN THE Receipt_Service SHALL reject the request with a validation error.
6. THE Receipt_Service SHALL order receipts by Doc Date descending, then by creation timestamp descending, then by identifier descending.
7. WHEN an authorized reader requests a receipt by identifier, THE Receipt_Service SHALL return the full receipt with the header, customer snapshot, mode details, lines, Add/Less rows, denominations, status, Authorized By, and Authorized Date.
8. IF a receipt identifier does not exist, THEN THE Receipt_Service SHALL return a not-found error.

### Requirement 13: Reference Data for the Screen

**User Story:** As a Cashier, I want the screen's dropdowns populated from the masters, so that I can only choose valid values.

#### Acceptance Criteria

1. THE Receipt_Service SHALL expose read endpoints for active Lines of Business, active Branches, a customer search by Customer Code or Customer Name, the details of one customer by Customer Code, the Live Contracts of a customer, active Marketing Officers, active Drawee Banks, the Drawee Branches of a Drawee Bank effective on a given date, active F&A Bank Accounts, the active RECEIPT_COMPONENT lookup values, and the cash denomination catalog.
2. WHEN a customer search term is supplied, THE Receipt_Service SHALL return active customers whose Customer Code or first name contains the term, case-insensitively, ordered by Customer Code, with paging.
3. THE Receipt_Service SHALL return the Customer Address as a single text composed of the available address parts of the Communication address of the customer, with the Permanent address used when no Communication address exists.
4. THE Receipt_Service SHALL restrict every reference endpoint to the same roles that may read receipts.

### Requirement 14: Authentication and Authorization

**User Story:** As a security stakeholder, I want receipt operations restricted to authorized roles, so that only cashiers and collection officers can capture receipts.

#### Acceptance Criteria

1. IF a request to a receipt endpoint carries a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Receipt_Service SHALL reject the request with HTTP status 401 and SHALL NOT persist any record.
2. IF an authenticated user without the role Cashier, BranchCollectionOfficer, or SystemAdmin calls a receipt endpoint, THEN THE Receipt_Service SHALL reject the request with HTTP status 403 and SHALL NOT persist any record.
3. WHEN a user with the role Cashier, BranchCollectionOfficer, or SystemAdmin calls a receipt endpoint with a valid token, THE Receipt_Service SHALL authorize the call and apply the rules of Requirements 1 to 13.
4. THE Receipt_Service SHALL take the acting user identity from the validated token and SHALL ignore any identity supplied in the request body.
5. THE Receipt_Service SHALL add the roles Cashier and BranchCollectionOfficer to the seeded role catalog.

### Requirement 15: Error Contract

**User Story:** As a UI developer, I want stable error codes, so that the screen can show the right message.

#### Acceptance Criteria

1. IF a FluentValidation rule fails, THEN THE Receipt_Service SHALL return an RFC 7807 problem response with HTTP status 400 and the code ERR-RCP-400.
2. IF a receipt, customer, or reference record is not found, THEN THE Receipt_Service SHALL return HTTP status 404 and the code ERR-RCP-404.
3. IF a Manual Document Number already exists, THEN THE Receipt_Service SHALL return HTTP status 409 and the code ERR-RCP-409.
4. IF a business rule of Requirements 4, 7, 8, or 9 fails, or no numbering scheme is configured, THEN THE Receipt_Service SHALL return HTTP status 422, the code ERR-RCP-422, and a `rule` extension carrying a stable rule key.
5. THE Receipt_Service SHALL include `code`, `message`, and `correlationId` extensions on every problem response.
6. IF an unexpected error occurs, THEN THE Receipt_Service SHALL return HTTP status 500 with the code ERR-RCP-500 and a generic message that contains no internal details.

### Requirement 16: Receipt Processing Screen

**User Story:** As a Cashier, I want one screen to enter a receipt, so that I can complete the capture quickly and correctly.

#### Acceptance Criteria

1. THE Receipt_UI SHALL provide a "Receipt Processing" create screen with the tabs General and Bank Details, the sections Receipt Information, Receipt Details, Customer Information, Account Details, Add/Less Details, and Amount In Words, and the buttons Save, Clear, and Cancel.
2. THE Receipt_UI SHALL provide the fields Entry Mode (Manual or Online), Doc No, Doc Date, Value Date, Receipt Type, Line of Business, Location, Marketing Officer, Mode, Receipt From (default Customer), GL Based (default No), and Doc Amount.
3. WHERE the Entry Mode is Online, THE Receipt_UI SHALL show the Doc No as read-only with the text "Generated on Save".
4. WHEN a Cashier selects Save and the receipt passes client-side validation, THE Receipt_UI SHALL send the create request and SHALL show the returned Document Number and the status "Pending Authorization".
5. IF the Receipt_Service returns an error, THEN THE Receipt_UI SHALL display the error message and SHALL keep the entered values.
6. WHEN a Cashier selects Clear, THE Receipt_UI SHALL reset every field to its default and SHALL create a new ClientRequestId.
7. WHEN a Cashier selects Cancel, THE Receipt_UI SHALL leave the screen without saving.
8. WHILE a Save request is in progress, THE Receipt_UI SHALL disable the Save button.
9. THE Receipt_UI SHALL provide a receipt list screen with search, filters, paging, and a read-only detail view that shows the General tab and the Bank Details tab of a saved receipt.
10. THE Receipt_UI SHALL display all text in English, with amounts in INR with two decimals, and SHALL contain no right-to-left layout handling.
11. WHEN the `VITE_USE_MOCK_API` setting is `true`, THE Receipt_UI SHALL use the mock receipt service, and otherwise SHALL use the real receipt service.

### Requirement 17: Gateway Routing

**User Story:** As a platform engineer, I want the receipt API reachable through the gateway, so that the UI calls one origin.

#### Acceptance Criteria

1. THE Finnova.ApiGateway SHALL route `/api/ua/api/receipts` and `/api/ua/api/receipts/{**catch-all}` to the accounts cluster after removing the prefix `/api/ua/api` and adding the prefix `/api`.
2. THE Finnova.ApiGateway SHALL forward the Authorization header of the incoming request to the accounts cluster.

## Acceptance Scenarios (from Jira FINNOVA-61)

### Scenario 1: Regular Cheque Receipt

- **Given** the Cashier opens a Regular Receipt screen with Line of Business "Vehicle", a Branch, and Customer Code CUST-100234, Doc Amount 3500.00, Mode Cheque, and Account ACC-889021 being a live contract of that customer
- **And** the lines allocate 3200.00 to Installment Inflow and 300.00 to ODI
- **And** the Bank Details tab holds Instrument No 458921, an Instrument Date, and a Drawee Bank
- **When** the Cashier selects Save
- **Then** a Document Number is generated, the status is Pending Authorization, and the audit trail records the sequence number (Requirements 2, 5, 7, 8, 9, 11)

### Scenario 2: Cash Receipt with mismatching denominations

- **Given** a Cash receipt with Doc Amount 500.00 and denominations that total 450.00
- **Then** the Bank Details tab is disabled
- **And** Save is blocked with the message "Document Amount must match the sum of Cash Denominations" (Requirements 4.1, 4.6, 4.10)
