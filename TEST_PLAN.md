# CMIForge Feature Walkthrough Test Plan

Current version: `20260623.1`

This is a practical manual test plan for getting familiar with CMIForge while also smoke-testing the major product slices. It is written as a guided tour, not just a bug-hunt checklist.

If you want the shortest useful pass, run Sections 1 through 8 in order. If you want the fuller product tour, run the whole thing.

## Goal

By the end of this walkthrough, you should have personally exercised:

- Navigation and overall product shape
- Dynamic forms and form versioning
- Secure client-facing external form invites
- Submissions and submission statuses
- Workflow definitions, approval queues, and notification steps
- Clients, matters, contacts, parties, and users
- Entity archive, restore, audit history, pagination, and change approvals
- Submission conversion into operational records
- Conflict searches and review notes
- Conflict search full-text warmup, Azure AI Search proof-of-tech indexes, and 50-row result pagination
- CSV import templates, validation, and import flows
- Security/team/role pages
- System settings and demo-mode protection
- Attachments and external links
- Built-in reports and the basic report builder
- Plan page and in-app help
- Public signup legal agreement acceptance
- Build, migration, and model-snapshot health

## Assumptions

- You are running locally or in the live dev app.
- Local examples below assume `http://localhost:5153`.
- The public demo app is available at `https://demo.cmiforge.com`.
- The public demo fallback URL is `https://cmiforge-dev-web-06161223.azurewebsites.net`.
- Customer 0 / the real app doorway is `https://app.cmiforge.com`.
- The Customer 0 fallback URL is `https://cmiforge-customer0-web.azurewebsites.net`.
- The public marketing site is available at `https://cmiforge.com`.
- The live dev app currently uses demo mode with the seeded `Ima User` context.
- `cmiforge.com` DNS is hosted in Azure DNS while the domain registration remains at Namecheap.
- The current hardcoded plan is `Professional`, so all features should be available with the Professional user and matter limits.
- Azure SQL and Azure Blob attachment storage are already configured.
- In the live dev app, private submission attachments use Azure Blob Storage through managed identity.
- Azure SQL public access is disabled in the cloud environments; ad hoc cloud DB querying requires VNet reachability or a controlled temporary public-access maintenance window.
- Azure AI Search Basic uses tenant-separated indexes. Conflict-search indexes are optional candidate providers; entity-directory indexes are one per tenant/environment and should include clients, matters, parties, client aliases, party aliases, and contacts, but not users.
- After CMIForge changes, the normal Codex workflow is to build/verify, deploy affected live surfaces, and smoke-check the live URL unless Gabe explicitly says not to deploy.

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
- Client narrative: `Reviewed intake materials and prepared next-step notes.`
- Internal notes: `Drafted from test-plan smoke pass.`
- Save mode: `Draft`

## 1. Dashboard, Help, and Navigation

Purpose: understand the basic product layout and current information architecture.

Steps:

1. Open `/`.
2. Confirm the dashboard shows:
   - tenant/firm branding
   - Firm Admin, My Work, and Matter Partner dashboard selector
   - top count cards
   - submission status chart
   - conflict status chart
   - open workflow load
   - recent submission trend
   - plan usage meters
3. Open the `My Work` dashboard view and confirm it shows assigned work, team queue counts, recent submissions, personal time, and conflict escalations assigned to the signed-in user.
4. Open the `Matter Partner` dashboard view and confirm it shows lead matters, submitted time waiting for approval, pending conflicts, and partner-related submissions.
5. Confirm the dashboard selector only shows dashboard views available to the effective current user.
6. If time is waiting for approval, confirm each waiting time row links to the time detail and exposes an `Approve` action.
7. Confirm the top navigation now shows:
   - `Submissions`
   - `Entities`
   - `Conflicts`
   - `System`
   - `Help`
8. Open the `System` dropdown and confirm it contains:
   - `Forms`
   - `Workflows`
   - `Imports/Exports`
   - `Security`
   - `Terms`
9. If you are not in demo mode, confirm these also appear under `System`:
   - `Settings`
   - `Signup Requests`
   - `Onboarding`
   - `Archive`
10. Open `/Help`.
11. Skim each help section in the contents list.

Expected results:

- The nav feels consolidated and work-focused.
- The dashboard gives a quick visual sense of intake volume, conflict status, and workflow load.
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
4. Confirm the page shows direct roles, team membership, and lead-partner matters if any are assigned.
5. Open `/Security`.
6. Open `/Security/Teams`.
7. Open `/Security/Dashboards`.
8. Confirm Firm Admin has an Administrator role grant and Matter Partner has a Partner role grant.
9. Add a dashboard visibility grant to a test user, team, or role, then remove it.
10. Open `/Security/Impersonation`.
11. If the current user has `System.ImpersonateUsers`, choose a different active user and start impersonation.
12. Confirm the impersonation banner appears, then stop impersonation from the banner or page.
13. Open `My Profile` from the top navigation user control.
14. Select `Large`, save, and confirm the app reloads with larger text.
15. Return to `My Profile`, select `Standard`, save, and confirm text returns to the default size.
16. Review seeded teams such as:
   - `Admins`
   - `Intake Team`
   - `CDD Team`
   - `Partner Approvers`

Expected results:

- Users are operational records, not just display strings.
- Security is visibly role-based and team-aware.
- Dashboard access can be assigned by user, team, or role without code changes.
- Gabe appears wired into the seeded admin/intake setup.
- Users with the Partner role can appear in lead-partner pickers.
- Archived users are hidden from normal pickers and can be restored from System -> Archive.
- User impersonation is available only to authorized admins, changes the effective current user for permissions/routing, and shows a visible banner while active.
- Starting and stopping impersonation writes audit-log entries that keep the actual signed-in user as the actor.
- User profile font-size changes are saved per user and apply throughout the shared app layout.

## 3. Entities Walkthrough

Purpose: understand the operational records side of the platform.

### 3A. Clients

Steps:

1. Open `/Entities/Clients`.
2. Confirm the first column is the linked client number.
3. Confirm the list shows pagination text such as `Showing 1-50 of ...` when enough records exist.
4. Create a new client.
5. Use a clear test name, such as `Northwind Harbor Holdings`.
6. Add one or more aliases on create, such as `Northwind Harbor DBA` or `NHH`.
7. Open the created client detail page.
8. Confirm the direct-create warning appears and the status defaults to `Compliance Review`.
9. Add a new alias from the detail page Aliases panel and confirm it appears after save.
10. Edit one alias, save it, then delete a throwaway alias.
11. Submit a client change request moving status from `Compliance Review` to `Active`; first try without request notes, then add notes and submit.
12. Approve the change request from `/Entities/Approvals`.
13. Return to `/Entities/Clients` and search by alias text.

Expected results:

- Client numbers are zero-padded like `00000001`.
- The client number is clickable.
- The client list shows an alias count and can be searched by alias.
- The detail page loads without error.
- The client detail page has one Aliases panel containing both the add-alias controls and existing aliases.
- Client aliases can be added, edited, and deleted by users with entity edit rights.
- Adding a duplicate normalized client alias shows a validation error instead of silently doing nothing.
- The detail page shows related matters, linked contacts, and related parties derived from the client's matters.
- Direct-created clients default to `Compliance Review` and show a compliance-review warning.
- Moving a client from `Compliance Review` to `Active` requires request notes and creates an `EntityComplianceReviewed` audit entry when approved.
- The detail page shows a compact conversation-style notes area with newest notes first, older notes expandable, and author deletion controls.
- The detail page shows entity audit/history information when changes have been recorded.
- Archive/unarchive controls hide the client from default lists without deleting it.
- Archived clients can be restored from `/System/Archive` in non-demo admin environments.

### 3B. Matters

Steps:

1. Open `/Entities/Matters`.
2. Create a new matter linked to the client you just created.
3. Set Gabe as the responsible user if desired.
4. Set a lead partner if a partner-role user is available.
5. Open the matter detail page.
6. Confirm the direct-create warning appears and the status defaults to `Compliance Review`.
7. Submit a matter change request moving status from `Compliance Review` to `Open`; first try without request notes, then add notes and submit.
8. Approve the change request from `/Entities/Approvals`.

Expected results:

- Matter numbers and matter names are zero-padded/readable and linked.
- The matter links back to the client.
- The matter shows the responsible user correctly.
- The matter shows the lead partner correctly.
- The matter detail page keeps core matter facts on the left and child records on the right.
- The right-side related-record rail shows time entries, parties, and contacts when present.
- Direct-created matters default to `Compliance Review` and show a compliance-review warning.
- Moving a matter from `Compliance Review` to `Open` requires request notes and creates an `EntityComplianceReviewed` audit entry when approved.
- The detail page shows a compact conversation-style notes area with newest notes first, older notes expandable, and author deletion controls.
- The detail page shows entity audit/history information when changes have been recorded.
- Archive/unarchive controls hide the matter from default lists without deleting it.
- Archived matters can be restored from `/System/Archive` in non-demo admin environments.

### 3C. Parties

Steps:

1. Open `/Entities/Parties`.
2. Browse the seeded demo parties.
3. Open a few records that look conflict-friendly, especially names involving:
   - `Stark`
   - `Globex`
   - `Caldera`
4. Confirm aliases and relationship-style data exist on the party side.
5. Add a harmless party alias from the detail page Aliases panel and confirm it appears after save.
6. Edit a harmless party alias.
7. Delete a throwaway party alias.
8. Create a direct party and confirm it defaults to `Compliance Review`.

Expected results:

- Parties feel distinct from clients and matters.
- The seeded demo data looks intentionally designed for conflict-match richness.
- The party detail page has one Aliases panel containing both the add-alias controls and existing aliases.
- Party aliases can be added, edited, and deleted by users with entity edit rights.
- Adding a duplicate normalized party alias shows a validation error instead of silently doing nothing.
- Party detail pages show matter roles, related contacts from linked matters, and party relationships.
- Direct-created parties default to `Compliance Review` and show a compliance-review warning on details.
- Party detail pages show compact newest-first notes, author deletion controls, impersonation attribution when applicable, and archive/unarchive behavior.
- Party detail pages show entity audit/history information when changes have been recorded.

### 3D. Contacts

Steps:

1. Open `/Entities/Contacts`.
2. Create a new contact, such as `Riley Contact` at `Northwind Harbor Holdings`.
3. Confirm the direct-create guidance appears on the create page.
4. Open the contact detail page.
5. Link the contact to a client with the role `Primary Contact`.
6. Link the contact to a matter with the role `Matter Contact`.
7. Open the linked client and matter details.

Expected results:

- Contact numbers are zero-padded and linked.
- Contacts feel like address-book records, not login users and not conflict parties.
- Client and matter detail pages show the linked contact.
- Contacts can be archived from the detail page.
- Archived contacts are hidden from normal contact lists and restore from `/System/Archive`.
- Contact detail pages show linked clients, linked matters, and related parties from those matters.
- Contact detail pages show entity audit/history information when changes have been recorded.

### 3E. Entity Change Approvals, Archive, and Pagination

Purpose: confirm larger entity datasets stay manageable and reviewable.

Steps:

1. Open `/Entities/Approvals`.
2. Confirm the page has `Pending Changes` and `Recent Decisions` sections.
3. If pending client or matter edits exist, confirm the changed fields show current and proposed values before approving.
4. Approve or reject one with a short review note.
5. Open `/System/Archive` in a non-demo admin environment.
6. Confirm archived Clients, Matters, Parties, Contacts, and Users are grouped separately.
7. Restore one harmless archived test record if available.
8. Open large lists such as `/Entities/Clients`, `/Entities/Matters`, `/Entities/Parties`, `/Entities/Users`, `/Submissions`, and `/Workflow/Queue`.
9. Click several table headers on those lists and confirm the full result set sorts ascending, then descending, before pagination is applied.
10. Use `Next` and `Previous` pagination where visible.

Expected results:

- Entity approval requests show the entity type, record number, request time, summary, and notes.
- Entity approval requests show before/after values for each changed field so the reviewer can see the actual data change.
- Approve/reject actions apply only for authorized users.
- Recent approval decisions remain visible for audit context.
- System Archive restores records without creating duplicates.
- Standard read-only paginated tables expose clickable sortable column headers that sort the full result set before pagination.
- Record pagination preserves the current list view and never shows invalid page ranges.

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
5. Put fields into at least two named sections, such as `Client`, `Matter`, and `Review`.
6. Add one basic display condition by setting a field to show only when another field key has a matching value.
7. Add one workflow-step edit rule by setting a field's editable step to an approval step name.
8. Confirm blank extra rows do not block publishing.
9. Publish the form.
10. Re-open that form in edit mode.
11. Make one small change, such as:
   - add a field
   - change description
   - attach or change workflow
12. Save again to create a new version.
13. Return to `/Forms` and click `Copy` on the form.
14. Confirm the copied form opens in edit mode with a unique `-copy` style key and the same fields/workflow attachment.
15. Return to `/Forms` and confirm published forms expose a `Send` action plus open/completed client-link counts.

Expected results:

- Create publishes the first version.
- Edit publishes a new version instead of rewriting history.
- Copy creates a new active form from the source form's latest published version without mutating the source form or its submissions.
- Blank designer rows are ignored.
- Select-style fields only require options when they actually use options.
- Form sections survive publish/edit and become tabs on runtime forms.
- Conditional fields hide until their matching answer is present.
- The forms list makes the client-facing invite path visible without crowding the normal fill/edit actions.

## 5. Send a Client-Facing Form Link

Purpose: confirm a firm user can send a secure intake form link to a saved contact without exposing the internal app.

Steps:

1. Open `/Forms`.
2. Click `Send` on a published form.
3. Choose a saved contact with an email address.
4. Optionally choose a lead partner, add a short client message, and keep the expiration between 1 and 30 days.
5. Click `Create secure link`.
6. If email is configured, confirm the page reports that the invite was queued for email. If email is not configured, copy the displayed secure link.
7. Open the generated `/external/forms/{token}` link in an anonymous/private browser context.
8. Confirm the page uses the minimal secure-form layout and does not show internal-only controls such as `Submitted by`, internal user pickers, conflict preview, or app navigation.
9. Confirm section tabs render on the external form when the published form has sections.
10. If the form has a conditional field, confirm it appears only after the trigger answer is entered.
11. Complete the required form fields and submit.
12. Confirm the client-facing confirmation page shows a submission number.
13. Return to `/Forms/Send/{id}` and confirm the recent invite shows as completed with a linked submission number.
14. Confirm the recent sends panel shows Queued, Sent, Opened, and Completed steps for the invite.
15. Use `Resend` on an uncompleted invite and confirm CMIForge creates a fresh secure link, revokes the previous uncompleted invite, and queues a new email when tenant email is enabled.
16. Open the related contact, client, and matter detail pages and confirm the same recent sends panel appears with the invite's delivery/open/completion status.
17. Re-open the same external link and confirm it is no longer usable after completion.

Expected results:

- Invite recipients must come from saved contacts, not free-typed email addresses.
- The raw access token is shown only in the generated URL and is not visible as stored app data.
- External forms use the same section/tab rendering and basic display-condition behavior as internal forms.
- External form links expire and cannot be reused after completion.
- External submissions become normal form submissions and start the attached workflow when the form version has one.
- The audit history records that the external invite was created and completed.
- Resent invites use a new token; CMIForge does not recover or expose the old raw token.
- Email `Sent` means Microsoft Graph accepted the outbox send request, not that the recipient's mailbox provider guarantees human readership.

## 6. Submit a Form and Review the Submission

Purpose: follow the core intake flow from the user side.

Steps:

1. Open `/Forms`.
2. Click into a form submission page.
3. Confirm `Submitted by` is a picker from the `Users` table.
4. Confirm `Lead partner` is a picker limited to active, non-archived users with the Partner role.
5. Confirm section tabs render when the selected published form has sections.
6. If the form has a conditional field, confirm it hides until the trigger answer is entered and required validation ignores it while hidden.
7. Submit a new intake using the suggested data above.
8. Open `/Submissions`.
9. Confirm the new submission appears with:
   - a zero-padded submission number
   - a linked number in the first column
   - compact one-line rows without duplicate helper captions under client or matter
   - submitter name
   - lead partner when selected
   - status
10. Open the submission detail page.

Expected results:

- The submission stores and renders correctly.
- The submission detail page shows a main Submission Workspace tab set for form sections, linked conflict searches, and attachments.
- Older submissions whose saved form version only has the old `General` section still render with sensible display-only tabs inferred from field keys/labels.
- The submission detail page keeps Open Actions at the top of the right-side activity rail, with workflow tasks/history shown newest-first in a compact layout.
- The submission detail page shows the selected lead partner.
- The initial status is sensible for a newly submitted intake.

## 7. Attachments and Links

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

## 7A. Inbound Email Intake

Purpose: confirm trusted mailbox email can become a reviewable intake submission without bypassing workflow.

Steps:

1. Open `/System/Settings` and review the `Inbound Email` settings.
2. Confirm inbound processing is disabled unless the tenant mailbox, inbound address, allowed sender domains, and Graph mailbox permissions are ready.
3. For a configured tenant, send or simulate a message from an allowed sender domain with subject `Client: Acme Corp`.
4. Confirm a submission is created from the default intake form.
5. Confirm the submitted client value is `Acme Corp` and the matter value is blank.
6. Send or simulate `Client: Acme Corp; Matter: Lease Review`.
7. Confirm both client and matter values populate.
8. Confirm supported attachments are copied into the submission attachments tab.
9. Open `/System/InboundEmail` and review the processed message entry.
10. Send or simulate a message without `Client:` or from an unapproved domain.
11. Confirm it is rejected into the inbound email log and does not create a submission.

Expected results:

- Inbound emails create normal reviewable submissions, not direct client/matter records.
- Blank or omitted `Matter:` is allowed.
- Attachments flow through the same private attachment storage and app download permissions as manual uploads.
- Source email details appear on the submission detail page.
- Rejected/failed messages are visible to admins under System -> Inbound Email.
- Each accepted or rejected message writes audit history.

## 8. Workflow Definitions and Queue

Purpose: understand how submissions move through approval.

Steps:

1. Open `/Workflow/Definitions`.
2. Review the seeded `Standard Intake Review` workflow.
3. Confirm steps are ordered and can be typed as `Approval` or `Notification`.
4. Use the step move buttons to move a workflow step up/down and confirm the order numbers update before save.
5. Click `Copy` on a workflow and confirm the copy opens as `Saved draft / unpublished` with the copied steps.
6. Create or edit a workflow and confirm it can be saved as `Saved draft / unpublished`.
7. Try to publish an edit that is not publish-ready and confirm the latest edits remain saved as `Saved draft / unpublished`.
8. Publish a valid workflow and confirm it shows as `Published / live`.
9. Confirm approval steps can contain outcomes.
10. Confirm notification steps expose recipients, subject, and body fields.
11. Review the outcome format and any routing conditions.
12. Open `/Workflow/Queue`.
13. Review:
   - `My Queue`
   - `Team Queue`
   - `All Open` if visible
13. Return to your new submission detail page.
14. Use the available workflow action buttons.
15. If useful, test both:
   - a return-style action
   - an approve/final-approve path
16. After a return-style action, open the returned submission detail page.
17. Confirm the returned edit form keeps the form sections/tabs.
18. Edit at least one answer allowed by the current workflow step.
19. Confirm any field configured for a different workflow step is read-only.
20. Click `Save and resubmit`.

Expected results:

- Draft workflows are saved but do not appear in form workflow pickers or start new submissions.
- Copied workflows are saved as drafts so cloning an existing workflow never changes live routing until it is explicitly published.
- Moving steps updates step numbers and the saved/published workflow runs in the new order.
- Published, active workflows are available for form attachment and automatic workflow start.
- Submitting a form with an attached workflow starts a workflow instance automatically.
- Queue tasks appear in the right place.
- Taking an outcome updates task and submission state.
- Notification steps log a workflow event or send email when tenant notifications are enabled and the SaaS/platform sender is configured, then continue to the next matching step.
- Returned submissions can move to `Returned`.
- Returned submissions expose a sectioned edit form and can be resubmitted into review.
- Returned-submission editability follows the form field's workflow-step rule, and locked fields are preserved server-side.
- Approved submissions can move toward `Approved`.

## 9. Submission Statuses and Conversion

Purpose: validate the intake-to-operations bridge.

Steps:

1. On the submission detail page, note the current status.
2. Confirm a non-converted submission can be cancelled from the detail page.
3. Confirm cancelled submissions show `Cancelled`, hide workflow start/run-conflict actions, close open workflow tasks, and disappear from the normal `/Submissions` list.
4. Open `/System/Archive` and confirm the cancelled submission appears under `Cancelled Submissions`.
5. Restore the cancelled submission from the archive and confirm it returns to the normal submissions list.
6. Use a separate active submission and confirm conversion is blocked until the submission is approved.
7. Move the separate submission through workflow until it reaches `Approved`.
8. Click `Create Client/Matter`.
9. Review the prefilled values on the conversion page.
10. Confirm the lead partner carries forward or can be changed to another active Partner user.
11. Save the conversion.
12. Return to the submission detail page.

Expected results:

- Cancellation preserves the submission record and audit history instead of deleting operational evidence.
- Cancelled submissions behave like archived records: hidden from daily lists, visible in System Archive, and restorable.
- Conversion is not allowed early.
- Approved submissions can be converted.
- The conversion form pre-fills likely values from submission answers.
- The conversion form validates that the selected lead partner has the Partner role.
- The submission links to the created client and matter after conversion.
- The submission status becomes `Converted`.
- The created matter shows the selected lead partner.

## 10. Conflicts Walkthrough

Purpose: learn the native conflicts slice and current review behavior.

### 9A. Run a Standalone Search

Steps:

1. Open `/Conflicts/Create`.
2. Run a search using one or more of:
   - `Stark Stone`
   - `Globex Bio Systems`
   - `Mina Caldera`
3. Confirm the staged progress bar appears after clicking `Run search`.
4. Open the resulting detail page.
5. Add one additional term and click `Re-run search`.
6. Confirm the staged progress bar appears during the re-run.

Expected results:

- The create and re-run flows give immediate visual feedback while the search is running.
- You should see multiple interesting hits from the seeded data.
- Results should include score, risk, explanation, and AI-style assessment.
- On Azure SQL, run `tools/rebuild-conflict-search-documents.ps1` after a large data load; subsequent searches should use the full-text candidate index and feel materially faster than a full-table scan.
- Searches with more than 50 hits should show standard result pagination and display only 50 rows per page.

### 9A.1 Search Term Handling Examples

Steps:

1. Open `/Help` and review the conflict search term examples.
2. Run a conflict search for `Acme Anvil Works, Inc.; A.A.W.`.
3. Run a conflict search for `Elm & Vine`.
4. Run a separate search for a person-style name, such as `Mina Caldera`.
5. Run a mixed search with punctuation or line breaks, such as `Globex Bio Systems` plus a contact or matter phrase.
6. Create or identify a client alias, then run a conflict search using that alias text.
7. If sample/demo data is available, compare acronym hits such as `A.A.W.` against unrelated words that merely contain one matching letter.

Expected results:

- Common punctuation, commas, semicolons, and line breaks are treated as separators or cleanup, not as meaningful legal text.
- Corporate suffixes such as `Inc.`, `LLC`, and similar endings should not be the reason a result looks strong.
- Connector words and punctuation should normalize together, so `Elm & Vine` can find `Elm and Vine Capital` or `Elm & Vine Manufacturing`.
- Party aliases, client aliases, and normalized initials can help find a party/client such as `A.A.W.`, but a single shared letter should not create an inflated match.
- Matter context, related parties, prior search text, and prior clearance notes can add useful hits, but the UI should explain what matched.
- `Match strength` should describe text similarity. It should not pretend to be the final legal conflict decision.

### 9B. Review and Clear Results

Steps:

1. On the conflict detail page, review the overall search decision panel.
2. Confirm the result filters sit directly above the results table.
3. Expand `Row actions` on an individual result row, set a result clearance status, and add result-level clearance notes.
4. Save the row.
5. Select multiple result rows, apply `Clear` from the bulk action panel, and confirm the selected rows update.
6. Use the select-all checkbox in the results table header and apply a bulk decision.
7. Select three or four result rows, choose an active user in `Escalate to`, add notes, and click `Escalate selected`.
8. On an escalated row, approve the escalation as the assigned reviewer and add approval notes.
9. Repeat for enough rows to confirm the overall search status rolls up appropriately.
10. If impersonation is available, start impersonating another active user, clear a conflict result or overall search, and reload the conflict detail page.

Expected results:

- Each result can store its own status, notes, reviewer, and timestamp.
- The result filters remain visually attached to the table, while row-level review/escalation controls stay collapsed until needed.
- On high-volume searches, the result table shows 50 hits per page and the pagination text uses ranges such as `Showing 51-100 of ...`.
- Multiple conflict result rows can be selected and updated together, including all rows via the header checkbox.
- Multiple conflict result rows can be escalated together to another active user in one submit; users should not need to press each row's `Escalate row` button for a shared escalation.
- Escalated rows show assigned reviewer, escalation notes, escalation timestamp, approval status, and approval notes.
- Escalation and escalation approval appear in recent audit history for the conflict search.
- Clearance, escalation, and escalation approval attribution show the actual actor, and when impersonation is active they show `actual user impersonating effective user`; they should not fall back to `System` for user-driven actions.
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

### 9D. Live Conflict Preview On Submission Forms

Steps:

1. Open a published intake form.
2. Start typing a known party/client name, such as `Stark Stone`, into the client or matter field.
3. Continue typing slowly enough to see the preview update.
4. Click into a different field and then back into the client/matter field.
5. Open `/System/Settings`, turn `Conflicts.LivePreviewEnabled` off, save, and reload the intake form.
6. Turn the setting back on after verification.
7. In non-demo mode, turn `Use Azure AI Search candidates` on, run a known conflict search such as `manufacturing`, and confirm results still render with CMIForge match strength/explanations. Turn it back off to confirm SQL full-text remains the default candidate path.

Expected results:

- A floating conflict preview appears near the field being typed into.
- The preview updates as the typed terms become more specific.
- Similar parties and relationship/context hints can appear before the form is submitted. Prior-history scanning is reserved for formal conflict searches so live typing stays responsive on large tenants.
- The preview does not block normal form entry.
- The native existing-client picker should not cover the conflict preview; when the browser shows client suggestions, the preview should sit lower on the page.
- When disabled in settings, the conflict preview panel and script do not appear on intake forms.
- When Azure AI Search candidates are enabled and trusted Search config is present, conflict search still shows normal CMIForge-scored results; if Search is unavailable, the app falls back to SQL full-text.

### 9E. Run Conflicts from Context

Steps:

1. Start a conflict search from a submission detail page.
2. Start a conflict search from a matter detail page.
3. Confirm those searches are linked back to their originating records.

Expected results:

- Context-linked searches appear associated to the submission or matter.
- Submission and matter pages show the linked conflict searches.
- Submission-only conflict searches show as `S-######## Search`, while matter-linked searches keep the matter-oriented search name.

### 9F. History Behavior

Checks:

- Confirm previous searches remain available as history.
- Confirm conflict searching can surface prior search text and previous result clearance notes.

## 11. Imports/Exports

Purpose: understand the migration/import/export story for real firm data.

Steps:

1. Open `/Imports`.
2. Confirm the page title and navigation use `Imports/Exports`.
3. Confirm the export area lists downloadable CSV batches for:
   - Clients
   - Matters
   - Parties
   - Contacts
   - Users
   - Time Entries
4. Download the first clients export batch.
5. Confirm the export progress bar moves when a batch is selected or downloaded.
6. Confirm each import card has:
   - `Download Template`
   - `Validate CSV`
   - `Import CSV`
7. Download all three templates:
   - Clients
   - Matters
   - Parties
8. Prepare a tiny CSV for each type, such as 2 to 3 rows.
9. Run `Validate CSV` first.
10. Review the validation messages and counts.
11. Run `Import CSV`.
12. In the Photo OCR area, upload a clear client image or scan.
13. Click `Read Photo`.
14. Review the OCR text and drafted client fields.
15. Create the client only if the drafted values look reasonable.

Expected results:

- CSV exports download in fixed 1,000-row batches.
- Validation does not write operational data.
- Import writes accepted rows and records a batch summary.
- Client imports upsert by `ClientNumber`.
- Matter imports require an existing `ClientNumber`.
- Party imports link to matters by `MatterNumber`.
- Photo OCR extracts visible text, drafts client fields for review, and does not create the client until the user confirms.

## 12. My Submissions vs All Submissions

Purpose: understand the queue split and visibility model.

Steps:

1. Open `/Submissions`.
2. Review the `My Submissions` view.
3. If the page exposes `All Submissions`, switch to it.
4. Compare the scope of results.

Expected results:

- Personal queue behavior is distinguishable from admin/global visibility.
- The page reinforces that not every user should see all operational intake.

## 13. Plan and Product Gating

Purpose: understand how the configured product tiers are represented.

Steps:

1. Open `/Billing` directly or use a contextual `View plan` link from the dashboard or a plan-limit message.
2. Review the visible tiers:
   - Community
   - Professional
   - Enterprise
3. Confirm the current development plan is shown as `Professional`.
4. Confirm Professional is shown at `$149/month` and does not mention per-user add-on pricing.
5. Confirm Enterprise is shown at `$599/month`, 1,000 users, 5,000 clients, 5,000 matters, and customer SQL data access.
6. Confirm the app footer shows the active plan next to the CMIForge version.

Expected results:

- Billing is presented as product gating, not real payment plumbing.
- The current build is feature-unlocked for development, while still showing Professional user and matter limits.
- Tenant app settings can override the active plan, and the footer reflects that active plan.
- SQL data access is Enterprise-only.

## 13A. Reports

Purpose: confirm reporting feels like separate useful reports, not just one dashboard page.

Steps:

1. Open `/Reports`.
2. Run each built-in report:
   - `Intake Pipeline`
   - `Approval Queue Aging`
   - `Conflicts Review`
   - `Matter Roster`
   - `Time Detail`
3. Export one built-in report to CSV.
4. In the basic report builder, choose the `Matters` dataset.
5. Select fields such as `Matter #`, `Matter`, `Client`, `Status`, and `Responsible user`.
6. Add a light filter, such as `Status is not blank`.
7. Run the custom report.
8. Export the custom report to CSV.

Expected results:

- Each built-in report opens as an individual report with its own rows and columns.
- The basic report builder can produce a simple filtered list without needing a saved report definition.
- CSV export downloads for both built-in and custom reports.

## 13B. Time Approval, Locking, Codes, And Timer

Purpose: confirm the v1 time-entry approval and billing-data rules work together.

Steps:

1. Open or create a matter with `Requires time approval` off and a 6-minute or 15-minute increment.
2. Record time for that matter with a client narrative, internal notes, phase, and task, then submit it.
3. Confirm the entry auto-approves.
4. Open the entry detail page and confirm it is locked from normal editing after approval.
5. Open or create a matter with `Requires time approval` on and a lead partner.
6. Record time for that matter and submit it.
7. Confirm the entry remains Submitted until the matter lead partner approves it.
8. As the lead partner with time approval permission, open the Matter Partner dashboard and confirm the waiting time row has an `Approve` action.
9. Open `/Time?status=Submitted` and confirm approvable rows also expose an `Approve` action.
10. Approve the submitted entry from the dashboard, the time list, or the detail page.
11. Export approved time from `/Time` and reopen the exported entry.
12. Open `/Time/Create`, confirm the hours field displays and accepts two decimal places, and use the numeric stepper after selecting matters with actual-time, 6-minute, and 15-minute rules.
13. Use the timer Start and Stop controls, assign the elapsed time to a matter, and save the entry.
14. With a 6-minute matter selected, apply an elapsed timer value that rounds to `0.20` hours and submit without touching the numeric stepper.

Expected results:

- Matter increment rules control the time-entry step, minimum, rounded minutes, and two-decimal hours display.
- Submitted time auto-approves only when the matter does not require approval.
- Approval-required submitted time waits for the matter lead partner.
- Lead partners with time approval rights can open submitted entries waiting on them even if another user recorded the time.
- Approved and exported entries are read-only from the normal edit path.
- Client narrative appears in export/reporting outputs while internal notes stay available in-app.
- Phase and task options come from the selected matter's time code set.
- The local timer fills elapsed time into the time-entry form using the selected matter's increment rule and a two-decimal hour value.
- Timer-applied values such as `0.20` pass browser number validation immediately instead of requiring an up/down stepper nudge.

## 14. System Settings

Purpose: confirm operational settings exist, are grouped clearly, and are protected in demo mode.

Steps:

1. In a non-demo/local environment, open `System -> Settings`.
2. Review the Data Access, Conflicts, Address Lookup, Email, and Inbound Email settings.
3. Confirm CSV export batch size is shown as `1000` and is disabled/read-only.
4. Confirm SQL access availability, SQL Entra principal, and SQL connection string are disabled/read-only.
5. Confirm SQL access copy says Enterprise is required unless the active plan includes customer SQL data access.
6. Confirm SMTP host, port, username, and password settings are not exposed as tenant-editable fields.
7. Save a harmless non-secret change, such as live conflict preview enabled/disabled, then change it back.
8. In the live demo environment, confirm Settings is not shown in the System dropdown.
9. In the live demo environment, browse directly to `/System/Settings`.

Expected results:

- Non-demo admins can update settings.
- CMIForge-controlled data access settings are visible but not customer-editable.
- Demo mode shows Settings as read-only and blocks saving.
- Blank optional settings can be left blank while saving unrelated changes.
- Tenant settings do not require customers to provide SMTP infrastructure; SMTP transport is a platform/SaaS configuration concern.
- Entra remains the expected place for password resets, verification, MFA, and sign-in policy.

### 13A. Enhancement Requests

Purpose: confirm product ideas behave like a shared firm queue instead of a private personal list.

Steps:

1. Open `System -> Enhancement Requests`.
2. Create a new request with a clear title and description.
3. Confirm the request appears in the firm request queue.
4. Use the Support button on an existing request.
5. Click the same request's support control again to remove your support vote.
6. As an admin, update request status or internal notes.

Expected results:

- Active enhancement requests are visible to signed-in users in the same tenant/firm.
- Each user can support a request once, and support can be toggled off.
- The support count updates after each toggle.
- Newly submitted requests automatically count the submitting user as supporting the idea.
- Admin-only status/internal-note controls remain protected.

## 15. Demo Mode Guardrails

Purpose: confirm the public demo stays safe, disposable, and clearly labeled.

Steps:

1. Open the live demo app.
2. Confirm the demo banner appears at the top of the page.
3. Open `System -> Demo Mode`.
4. Create a harmless client named `Reset Sentinel Demo Client`.
5. Click `Reset demo now`.
6. Confirm the sentinel client disappears and starter clients, matters, parties, forms, workflows, users, and time entries return.
7. Confirm the recent reset run history shows a successful manual or scheduled reset and that `Last reset` uses the latest successful run.
8. Try to create a client with obvious abusive language in the name.
9. Try a blocked admin action, such as creating a team from `/Security/Teams`.

Expected results:

- The demo app points at the separate `cmiforge-demo` database.
- Demo reset clears visitor-created data and reseeds starter records.
- Scheduled reset catches up when the app starts overdue instead of waiting a fresh interval after every cold start.
- Bad content redirects to the demo-blocked page and creates no record.
- Admin/destructive demo actions redirect to the demo-blocked page.
- User `00000001` remains protected.

## 16. Optional Entra Login Review

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

## 17. Signup, Demo Reset, and Onboarding

Purpose: verify the public-to-admin provisioning path and the safer public demo reset path.

Steps:

1. Open `/Signup` without signing in.
2. Confirm the terms checkbox is present and links to `/System/Terms`.
3. Open the terms link in a private/anonymous browser context and confirm it loads without requiring sign-in.
4. Try to submit a workspace request with a firm name, first admin, admin email, plan, and optional domain/subdomain without checking the agreement box.
5. Confirm validation blocks submission and explains that the agreement must be accepted.
6. Check the agreement box and submit the same request.
7. In a non-demo admin environment, open `System -> Signup Requests`.
8. Confirm the request appears with firm, admin, plan, notes, and status.
9. Confirm the database has a related `LegalAgreementAcceptances` row with customer name, signer name/email, agreement key/version/title, product version, accepted timestamp, IP address, and user agent.
10. Change the request status to `Contacted` or `Provisioning`, add internal notes, and save.
11. Open `System -> Onboarding`.
12. Review the checklist and open several linked setup pages.
13. In the public demo environment, open `System -> Demo Mode`.
14. Run `Reset demo now`.
15. Confirm the recent reset runs table records the manual reset attempt.

Expected results:

- `/Signup` is available anonymously even when the customer app uses Entra login.
- `/System/Terms` is available anonymously from the signup link.
- Signup cannot proceed until the current CMIForge SaaS Terms, Legal Use, and License Agreement is accepted.
- Accepted signup records create a durable legal agreement acceptance row tied to the tenant provisioning request.
- Signup requests are stored for admin triage outside the public marketing site.
- Signup status changes are audited.
- The onboarding checklist gives a clear first-customer setup path.
- Demo reset attempts persist in reset history with status, timing, deleted row count, and message.

## 17A. Local Build and Migration Health

Purpose: confirm code, EF migrations, and the model snapshot agree before deployment.

For normal Codex work, deployment is part of the completion loop unless Gabe explicitly says not to deploy. Use `-SkipDatabaseUpdate` only for changes that do not need schema updates or when migrations have already been handled separately.

Cloud tenant migrations should use the dedicated VNet-integrated migrator WebJob. Local SQL tools can still be used for controlled maintenance, but Azure SQL public access is disabled by default, so the operator must either run from a private-network path or use an explicit temporary public-access maintenance switch that restores access afterward.

Steps:

1. Run:

```powershell
dotnet restore
dotnet build
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --no-build
```

2. If testing against a local or disposable database, run:

```powershell
dotnet tool run dotnet-ef database update
```

3. Review the migration list and confirm the latest expected migrations are present:
   - performance indexes
   - lead partner and entity audit history
   - contact archive and system archive
   - conflict archive index
   - enhancement requests
   - legal agreement acceptances
   - conflict result escalations
   - enhancement request votes and client aliases
   - external invite email tracking
   - workflow draft/publish state
   - entity note impersonation attribution
   - dashboard assignments

Expected results:

- Restore and build complete without errors.
- EF reports no pending model changes after the latest migrations.
- Database update applies cleanly in the intended environment.
- The app starts after migration and the dashboard loads.

## 18. Cloud Domains, DNS, and Email Smoke

Purpose: confirm the current public architecture works after DNS, hosting, and mail changes.

Steps:

1. Open `https://cmiforge.com`.
2. Confirm the marketing site loads and the primary CTAs point to:
   - `https://demo.cmiforge.com`
   - `https://app.cmiforge.com`
   - `https://app.cmiforge.com/Signup`
3. Open `https://demo.cmiforge.com`.
4. Confirm the public demo loads and shows the demo banner.
5. Open `https://app.cmiforge.com`.
6. Confirm Customer 0 redirects to Microsoft Entra sign-in.
7. Open `https://app.cmiforge.com/Signup`.
8. Confirm the signup page is accessible for request capture.
9. In PowerShell, check DNS:

```powershell
Resolve-DnsName cmiforge.com -Type NS
Resolve-DnsName cmiforge.com -Type MX
Resolve-DnsName cmiforge.com -Type TXT
Resolve-DnsName demo.cmiforge.com
Resolve-DnsName app.cmiforge.com
```

10. Confirm expected DNS shape:
   - Nameservers are Azure DNS servers.
   - `demo` points to `cmiforge-dev-web-06161223.azurewebsites.net`.
   - `app` points to `cmiforge-customer0-web.azurewebsites.net`.
   - Email currently uses Microsoft 365 / Exchange Online MX and SPF records.

Expected results:

- `cmiforge.com`, `demo.cmiforge.com`, and `app.cmiforge.com` resolve without browser certificate errors after DNS propagation.
- Demo and Customer 0 remain separate environments.
- Public request access routes to Customer 0 `/Signup`.
- Demo and Customer 0 continue loading after SQL and Blob public network access are disabled.
- Azure SQL `gwmatterforge` and Blob storage `cmiforgeattachasgmt7` should be reached by the apps through private endpoints, not public access.
- Email DNS shows `cmiforge-com.mail.protection.outlook.com` as the MX target and `include:spf.protection.outlook.com` in SPF.
- `gabe@cmiforge.com` should be licensed as needed, and `support@cmiforge.com` should exist as a shared mailbox delegated to Gabe when support mail is active.

## 18A. Brand Asset Smoke

Purpose: confirm business/profile assets are available from the current app logo source.

Steps:

1. Open `wwwroot/img/cmiforge-logo.png`.
2. Confirm it is the current app/page logo source and visually matches the supplied square CMIForge product logo.
3. Open the generated assets in `artifacts/brand/`:
   - `cmiforge-linkedin-logo-400.png`
   - `cmiforge-linkedin-logo-1200.png`
   - `cmiforge-linkedin-logo-2400.png`
   - `cmiforge-logo-source.png`
4. Confirm the 1200px PNG is crisp, centered, square, and suitable for a LinkedIn business page logo.

Expected results:

- The raster product logo is the canonical current logo for app chrome, public-site chrome, favicons, touch icons, and social previews.
- The PNG exports are square, high-resolution, and visually match the in-app/public-site mark.

## 19. Suggested Smoke Regression Pass

Use this as the short “did we break anything obvious?” sweep after future changes:

- [ ] Marketing site loads
- [ ] Demo domain loads
- [ ] Customer app domain redirects to Entra
- [ ] Signup page loads
- [ ] Customer provisioning WhatIf shows `cmiforge-vnet/appsvc-integration`
- [ ] Signup requires legal agreement acceptance
- [ ] Terms page loads anonymously
- [ ] Dashboard loads
- [ ] Dashboard charts render
- [ ] Help loads
- [ ] System menu opens
- [ ] System Archive loads in non-demo admin environments
- [ ] Forms list loads
- [ ] Forms list shows client-link counts and Send actions for published forms
- [ ] Recent sends dashboard shows queued, sent, opened, and completed status
- [ ] Resend creates a fresh tracked invite and revokes the previous uncompleted invite
- [ ] Contact/client/matter detail pages show recent secure-link sends
- [ ] Secure external form invite link loads anonymously and submits into the normal submission queue
- [ ] Completed external form invite link cannot be reused
- [ ] Create form works
- [ ] Edit form publishes a new version
- [ ] Submit form works
- [ ] Lead partner picker appears on submission and conversion flows
- [ ] Submissions list uses compact one-line rows
- [ ] Floating conflict preview appears while typing client/matter names
- [ ] Submission detail loads
- [ ] Attachment upload works
- [ ] Inbound email settings and log page load
- [ ] Inbound email with `Client:` and blank `Matter:` creates a submission with no matter value
- [ ] Inbound email from a disallowed domain is rejected without creating a submission
- [ ] Workflow queue loads
- [ ] Workflow definition editor shows approval and notification step types
- [ ] Workflow queue pagination works with high task volume
- [ ] Workflow action updates status
- [ ] Approved submission converts to client/matter
- [ ] Approved submission with blank matter converts to client-only
- [ ] Client detail loads
- [ ] Client notes and archive controls work
- [ ] Client audit history appears when available
- [ ] Matter detail loads
- [ ] Matter lead partner displays correctly
- [ ] Matter notes and archive controls work
- [ ] Matter audit history appears when available
- [ ] Party notes and archive controls work
- [ ] Party audit history appears when available
- [ ] Contact create/detail loads and links to a client or matter
- [ ] Contact archive/restore works
- [ ] User detail/edit loads
- [ ] User Partner role toggle affects lead-partner pickers
- [ ] Entity approvals page loads and shows pending/recent requests
- [ ] Entity list pagination works
- [ ] Conflict search runs
- [ ] Conflict search term examples in Help match current behavior
- [ ] Large conflict searches page results at 50 rows per page
- [ ] Conflict review saves
- [ ] Conflict result filters narrow visible rows
- [ ] Conflict multi-select clearance updates selected rows
- [ ] Import templates download
- [ ] Client photo OCR reads image text and drafts client fields
- [ ] CSV validation works
- [ ] CSV import works
- [ ] Security pages load
- [ ] Security dashboard access page loads and shows user/team/role grant controls
- [ ] Security impersonation starts, shows a banner, affects permissions/routing, and stops
- [ ] Security audit log loads
- [ ] System settings page loads in non-demo or is read-only in demo
- [ ] Attachment upload/download still works after storage key rotation
- [ ] Visible timestamps match the browser's local timezone
- [ ] Reports built-in catalog and basic builder load
- [ ] Time entry approval, locking, phase/task, and timer flows work
- [ ] Plan page loads
- [ ] `dotnet build` passes
- [ ] EF reports no pending model changes

## 19A. Customer 0 Volume Data Check

Purpose: confirm the load-test dataset is present and still distinguishable from real data.

Expected Customer 0 volume counts:

- 700 generated active users with realistic names and no Entra IDs
- 10,000 generated clients with realistic company and individual names
- 10,000 generated parties with organization, individual, and government names
- 20,000 generated matters with realistic matter names and practice areas
- 19,600 generated matter-party links for relationship and search-load testing
- 1,000 submissions with `"volumeTest": true` in the submission JSON
- 300 open volume workflow tasks

Suggested checks:

- Run `tools/seed-customer0-big-volume-data.ps1 -AllowTemporarySqlPublicAccess` without `-Apply` and confirm all large generated counts are at target with zero planned additions.
- Run `tools/rebuild-conflict-search-documents.ps1 -AllowTemporarySqlPublicAccess` after big-volume reseeds/imports and confirm document counts plus sample full-text hits are reported.
- Confirm Azure AI Search entity-directory index counts after setup or reseed: `cmiforge-demo-entity-directory` should have 57 documents in the current demo seed, and `cmiforge-customer0-entity-directory` should have 40,081 documents in the current Customer 0 volume seed.
- Query the entity-directory indexes directly for sample terms such as `manufacturing`, `walker`, and `alder`; confirm results include mixed `sourceType` values rather than separate indexes per entity table.
- Open `/Entities/Clients`, `/Entities/Matters`, and `/Entities/Parties` and search for sample generated names such as `Brightline`, `Granite`, `Lucas`, or `City of Austin`.
- Open `/Submissions` and confirm the larger list still loads and pages/filtering remain responsive.
- Open a high-hit conflict search and confirm result pages show 50 rows at a time.
- Open `/Workflow/Queue` and confirm the seeded open workflow tasks do not make the queue unusably slow.
- Open the dashboard and confirm the charts render with the larger dataset.

## 20. What To Notice While Testing

This is the “buyer brain” part of the walkthrough. As you test, pay attention to:

- Does the product tell a coherent story from intake to workflow to matter creation?
- Do the linked records make the app feel like one system instead of separate modules?
- Does the conflicts workflow feel attached to operational context, not just a search box?
- Does the platform feel more like firm software than a demo CRUD app?
- Which screens feel “beta but useful” versus “next obvious polish target”?

## 21. Notes Template

Use this if you want to jot reactions as you go:

```text
Section:
What I tested:
What worked well:
What felt confusing:
What I would change next:
Any bug or rough edge:
```
