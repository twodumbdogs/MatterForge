# CMIForge Feature Walkthrough Test Plan

Current version: `20260616.1`

This is a practical manual test plan for getting familiar with CMIForge while also smoke-testing the major product slices. It is written as a guided tour, not just a bug-hunt checklist.

If you want the shortest useful pass, run Sections 1 through 7 in order. If you want the fuller product tour, run the whole thing.

## Goal

By the end of this walkthrough, you should have personally exercised:

- Navigation and overall product shape
- Dynamic forms and form versioning
- Submissions and submission statuses
- Workflow definitions and approval queues
- Clients, matters, parties, and users
- Submission conversion into operational records
- Conflict searches and review notes
- CSV import templates, validation, and import flows
- Security/team/role pages
- System settings and demo-mode protection
- Attachments and external links
- Plan page and in-app help

## Assumptions

- You are running locally or in the live dev app.
- Local examples below assume `http://localhost:5154`.
- The live dev app is available at `https://cmiforge-dev-web-06161223.azurewebsites.net`.
- The public marketing site is available at `https://cmiforge.com`.
- The live dev app currently uses demo mode with the seeded `Ima User` context.
- The current hardcoded plan is `Professional`, so all features should be available with the Professional user and matter limits.
- Azure SQL and Azure Blob attachment storage are already configured.
- In the live dev app, private submission attachments use Azure Blob Storage through managed identity.

## Suggested Time

- Quick familiarization pass: `30-45 minutes`
- Full walkthrough: `60-90 minutes`

## Suggested Test Data

Use these values if you do not want to invent your own as you go:

- Client name: `Northwind Harbor Holdings`
- Matter name: `Apex Renewal Dispute`
- Practice area: `Corporate`
- Matter summary: `Client needs review of a renewal dispute with a strategic vendor.`
- Estimated fees: `125000`
- Assigned user: `Ima User`

For conflicts testing:

- `Stark Stone`
- `Globex Bio Systems`
- `Mina Caldera`

For time testing:

- Matter: use any existing matter, or create one first
- Hours: `1.25`
- Narrative: `Reviewed intake materials and prepared next-step notes.`
- Status: `Draft`

## 1. Dashboard, Help, and Navigation

Purpose: understand the basic product layout and current information architecture.

Steps:

1. Open `/`.
2. Confirm the top navigation now shows:
   - `Submissions`
   - `Entities`
   - `Conflicts`
   - `System`
   - `Help`
3. Open the `System` dropdown and confirm it contains:
   - `Forms`
   - `Workflows`
   - `Imports`
   - `Security`
   - `Terms`
4. If you are not in demo mode, confirm `Settings` also appears under `System`.
5. Open `/Help`.
6. Skim each help section in the contents list.

Expected results:

- The nav feels consolidated and work-focused.
- The Help page explains the big-picture flow: form -> submission -> workflow -> conflicts -> conversion.
- Nothing in the nav throws an error.

## 2. Users, Teams, and Security Basics

Purpose: understand who exists in the system and how roles/teams are represented.

Steps:

1. Open `/Entities/Users`.
2. Confirm users have:
   - clickable system IDs
   - first, middle, and last name fields
   - display name
   - email
3. Open Gabe's user detail record.
4. Confirm the page shows direct roles and team membership.
5. Open `/Security`.
6. Open `/Security/Teams`.
7. Review seeded teams such as:
   - `Admins`
   - `Intake Team`
   - `CDD Team`
   - `Partner Approvers`

Expected results:

- Users are operational records, not just display strings.
- Security is visibly role-based and team-aware.
- Gabe appears wired into the seeded admin/intake setup.

## 3. Entities Walkthrough

Purpose: understand the operational records side of the platform.

### 3A. Clients

Steps:

1. Open `/Entities/Clients`.
2. Confirm the first column is the linked client number.
3. Create a new client.
4. Use a clear test name, such as `Northwind Harbor Holdings`.
5. Open the created client detail page.

Expected results:

- Client numbers are zero-padded like `00000001`.
- The client number is clickable.
- The detail page loads without error.

### 3B. Matters

Steps:

1. Open `/Entities/Matters`.
2. Create a new matter linked to the client you just created.
3. Set Gabe as the responsible user if desired.
4. Open the matter detail page.

Expected results:

- Matter numbers are zero-padded and linked.
- The matter links back to the client.
- The matter shows the responsible user correctly.

### 3C. Parties

Steps:

1. Open `/Entities/Parties`.
2. Browse the seeded demo parties.
3. Open a few records that look conflict-friendly, especially names involving:
   - `Stark`
   - `Globex`
   - `Caldera`
4. Confirm aliases and relationship-style data exist on the party side.

Expected results:

- Parties feel distinct from clients and matters.
- The seeded demo data looks intentionally designed for conflict-match richness.

## 4. Forms and Form Versioning

Purpose: understand the configurable intake designer and version model.

Steps:

1. Open `/Forms`.
2. Open an existing form and note the latest version number and attached workflow, if any.
3. Create a new form.
4. Use a few fields:
   - text
   - select
   - currency or numeric-style field
   - textarea
5. Confirm blank extra rows do not block publishing.
6. Publish the form.
7. Re-open that form in edit mode.
8. Make one small change, such as:
   - add a field
   - change description
   - attach or change workflow
9. Save again to create a new version.

Expected results:

- Create publishes the first version.
- Edit publishes a new version instead of rewriting history.
- Blank designer rows are ignored.
- Select-style fields only require options when they actually use options.

## 5. Submit a Form and Review the Submission

Purpose: follow the core intake flow from the user side.

Steps:

1. Open `/Forms`.
2. Click into a form submission page.
3. Confirm `Submitted by` is a picker from the `Users` table.
4. Submit a new intake using the suggested data above.
5. Open `/Submissions`.
6. Confirm the new submission appears with:
   - a zero-padded submission number
   - a linked number in the first column
   - submitter name
   - status
7. Open the submission detail page.

Expected results:

- The submission stores and renders correctly.
- The submission detail page shows the answer set cleanly.
- The initial status is sensible for a newly submitted intake.

## 6. Attachments and Links

Purpose: confirm first-pass submission attachment handling.

Steps:

1. On a submission detail page, add:
   - one allowed file attachment (`.pdf`, `.docx`, or `.xlsx`)
   - one external hyperlink attachment (`https://...`)
2. Confirm each attachment appears in the attachment panel.
3. Download the uploaded file through the app.
4. Remove one attachment.

Expected results:

- Upload succeeds for allowed file types under 25 MB.
- Hyperlinks save and render correctly.
- Downloads work through the app.
- Delete/remove succeeds cleanly.

## 7. Workflow Definitions and Queue

Purpose: understand how submissions move through approval.

Steps:

1. Open `/Workflow/Definitions`.
2. Review the seeded `Standard Intake Review` workflow.
3. Confirm steps are ordered and can contain outcomes.
4. Review the outcome format and any routing conditions.
5. Open `/Workflow/Queue`.
6. Review:
   - `My Queue`
   - `Team Queue`
   - `All Open` if visible
7. Return to your new submission detail page.
8. Use the available workflow action buttons.
9. If useful, test both:
   - a return-style action
   - an approve/final-approve path

Expected results:

- Submitting a form with an attached workflow starts a workflow instance automatically.
- Queue tasks appear in the right place.
- Taking an outcome updates task and submission state.
- Returned submissions can move to `Returned`.
- Approved submissions can move toward `Approved`.

## 8. Submission Statuses and Conversion

Purpose: validate the intake-to-operations bridge.

Steps:

1. On the submission detail page, note the current status.
2. Confirm conversion is blocked until the submission is approved.
3. Move the submission through workflow until it reaches `Approved`.
4. Click `Create Client/Matter`.
5. Review the prefilled values on the conversion page.
6. Save the conversion.
7. Return to the submission detail page.

Expected results:

- Conversion is not allowed early.
- Approved submissions can be converted.
- The conversion form pre-fills likely values from submission answers.
- The submission links to the created client and matter after conversion.
- The submission status becomes `Converted`.

## 9. Conflicts Walkthrough

Purpose: learn the native conflicts slice and current review behavior.

### 9A. Run a Standalone Search

Steps:

1. Open `/Conflicts/Create`.
2. Run a search using one or more of:
   - `Stark Stone`
   - `Globex Bio Systems`
   - `Mina Caldera`
3. Open the resulting detail page.

Expected results:

- You should see multiple interesting hits from the seeded data.
- Results should include score, risk, explanation, and AI-style assessment.

### 9B. Review and Clear Results

Steps:

1. On the conflict detail page, review the overall search decision panel.
2. On an individual result row, set a result clearance status.
3. Add result-level clearance notes.
4. Save the row.
5. Repeat for enough rows to confirm the overall search status rolls up appropriately.

Expected results:

- Each result can store its own status, notes, reviewer, and timestamp.
- The saved row-level clearance persists after reload.
- Search-level status updates when result decisions collectively indicate clear, needs info, potential conflict, or conflict.

### 9C. Confirm Prior-History Matching

Steps:

1. Add a distinctive phrase or party name to a conflict result clearance note.
2. Run a new conflict search using that distinctive text.
3. Open the new search detail page.

Expected results:

- Prior conflict search text can appear as a historical hit.
- Prior result clearance notes can appear as a historical hit.
- Historical hits show as matched items rather than party records.

### 9D. Run Conflicts from Context

Steps:

1. Start a conflict search from a submission detail page.
2. Start a conflict search from a matter detail page.
3. Confirm those searches are linked back to their originating records.

Expected results:

- Context-linked searches appear associated to the submission or matter.
- Submission and matter pages show the linked conflict searches.

### 9D. History Behavior

Checks:

- Confirm previous searches remain available as history.
- Remember that current matching does **not** search prior search text or prior result notes.

## 10. Import Center

Purpose: understand the migration/import story for real firm data.

Steps:

1. Open `/Imports`.
2. Confirm each card has:
   - `Download Template`
   - `Validate CSV`
   - `Import CSV`
3. Download all three templates:
   - Clients
   - Matters
   - Parties
4. Prepare a tiny CSV for each type, such as 2 to 3 rows.
5. Run `Validate CSV` first.
6. Review the validation messages and counts.
7. Run `Import CSV`.

Expected results:

- Validation does not write operational data.
- Import writes accepted rows and records a batch summary.
- Client imports upsert by `ClientNumber`.
- Matter imports require an existing `ClientNumber`.
- Party imports link to matters by `MatterNumber`.

## 11. My Submissions vs All Submissions

Purpose: understand the queue split and visibility model.

Steps:

1. Open `/Submissions`.
2. Review the `My Submissions` view.
3. If the page exposes `All Submissions`, switch to it.
4. Compare the scope of results.

Expected results:

- Personal queue behavior is distinguishable from admin/global visibility.
- The page reinforces that not every user should see all operational intake.

## 12. Plan and Product Gating

Purpose: understand how the current hardcoded product tiers are represented.

Steps:

1. Open `/Billing` directly or use a contextual `View plan` link from the dashboard or a plan-limit message.
2. Review the visible tiers:
   - Community
   - Professional
   - Enterprise
3. Confirm the current development plan is shown as `Professional`.

Expected results:

- Billing is presented as product gating, not real payment plumbing.
- The current build is feature-unlocked for development, while still showing Professional user and matter limits.

## 13. System Settings

Purpose: confirm operational settings exist, are grouped clearly, and are protected in demo mode.

Steps:

1. In a non-demo/local environment, open `System -> Settings`.
2. Review the General and Email settings.
3. Confirm SMTP-related settings include host, port, SSL/TLS, username, from email, from name, and a secret-reference field for the SMTP password.
4. Save a harmless non-secret change, such as the support email or from name, then change it back.
5. In the live demo environment, confirm Settings is not shown in the System dropdown.
6. In the live demo environment, browse directly to `/System/Settings`.

Expected results:

- Non-demo admins can update settings.
- Demo mode shows Settings as read-only and blocks saving.
- SMTP password values are not displayed as plain text.
- Entra remains the expected place for password resets, verification, MFA, and sign-in policy.

## 14. Demo Mode Guardrails

Purpose: confirm the public demo stays safe, disposable, and clearly labeled.

Steps:

1. Open the live demo app.
2. Confirm the demo banner appears at the top of the page.
3. Open `System -> Demo Mode`.
4. Create a harmless client named `Reset Sentinel Demo Client`.
5. Click `Reset demo now`.
6. Confirm the sentinel client disappears and starter clients, matters, parties, forms, workflows, users, and time entries return.
7. Try to create a client with obvious abusive language in the name.
8. Try a blocked admin action, such as creating a team from `/Security/Teams`.

Expected results:

- The demo app points at the separate `cmiforge-demo` database.
- Demo reset clears visitor-created data and reseeds starter records.
- Bad content redirects to the demo-blocked page and creates no record.
- Admin/destructive demo actions redirect to the demo-blocked page.
- User `00000001` remains protected.

## 15. Optional Entra Login Review

Purpose: understand the authentication direction, even if not active locally.

Steps:

1. Open `System -> Security`.
2. Review user and role concepts with the understanding that authentication can later be delegated to Microsoft Entra.
3. If Entra is configured in a future environment, verify:
   - sign-in works
   - authenticated users map into the `Users` table
   - permissions still come from CMIForge roles/teams

Expected results:

- Authentication and authorization are separate concepts in the platform design.

## 16. Suggested Smoke Regression Pass

Use this as the short “did we break anything obvious?” sweep after future changes:

- [ ] Dashboard loads
- [ ] Help loads
- [ ] System menu opens
- [ ] Forms list loads
- [ ] Create form works
- [ ] Edit form publishes a new version
- [ ] Submit form works
- [ ] Submission detail loads
- [ ] Attachment upload works
- [ ] Workflow queue loads
- [ ] Workflow action updates status
- [ ] Approved submission converts to client/matter
- [ ] Client detail loads
- [ ] Matter detail loads
- [ ] User detail/edit loads
- [ ] Conflict search runs
- [ ] Conflict review saves
- [ ] Import templates download
- [ ] CSV validation works
- [ ] CSV import works
- [ ] Security pages load
- [ ] Security audit log loads
- [ ] System settings page loads in non-demo or is read-only in demo
- [ ] Attachment upload/download still works after storage key rotation
- [ ] Visible timestamps match the browser's local timezone
- [ ] Plan page loads

## 17. What To Notice While Testing

This is the “buyer brain” part of the walkthrough. As you test, pay attention to:

- Does the product tell a coherent story from intake to workflow to matter creation?
- Do the linked records make the app feel like one system instead of separate modules?
- Does the conflicts workflow feel attached to operational context, not just a search box?
- Does the platform feel more like firm software than a demo CRUD app?
- Which screens feel “beta but useful” versus “next obvious polish target”?

## 18. Notes Template

Use this if you want to jot reactions as you go:

```text
Section:
What I tested:
What worked well:
What felt confusing:
What I would change next:
Any bug or rough edge:
```
