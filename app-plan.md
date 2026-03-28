# Kava — CalDAV + CardDAV Personal Information App Plan

## Overview

Build a Windows desktop app in C# with WinUI 3 that provides native-feeling calendar and contacts management backed by CalDAV and CardDAV. The app fills the gap left by Windows retiring its built-in Mail & Calendar and People apps, giving users a lightweight, self-hosted-first way to manage their schedule and contacts on Windows.

The app has two interaction surfaces:

1. **Flyout** — a compact tray-anchored panel for quick calendar access, resembling the native Windows clock/calendar flyout.
2. **Main window** — a lightweight standalone window for contact management, event editing, account settings, and anything that needs more space than the flyout provides.

A Windows widget provides a third, glanceable surface for upcoming events.

The initial release targets CalDAV for calendars, with CardDAV for contacts following shortly after on the same infrastructure. Synology NAS is the primary target server.

## Product Goals

- Provide a highly native-feeling Windows flyout experience for calendar viewing.
- Provide full contact management (browse, search, add, edit, delete) via CardDAV.
- Support CalDAV and CardDAV, starting with Synology Calendar and Contacts.
- Open instantly from the tray with minimal friction.
- Show upcoming events clearly in a compact agenda layout.
- Resolve event attendees against synced contacts for richer calendar context.
- Expose upcoming events through a Windows widget for glanceable access.
- Match Windows 11 visual language across both the flyout and main window.
- Keep the app lightweight, responsive, and local-first in day-to-day use.
- Replace the need for separate calendar and contacts apps for self-hosted users.

## Non-Goals For Initial Release

- No Microsoft Graph, Google Calendar API, or Exchange support in v1.
- No task management, reminders engine, or notification system in v1.
- No team collaboration features.
- No macOS or Linux UI targets in v1. Windows x64 and arm64 are both in scope. However, core libraries must be cross-platform from the start to leave the door open.
- No full desktop calendar application with complex multi-day/week/month editing views in v1.
- No reliance on the Windows OS appointment store or OS-level account system.
- No CardDAV support in the initial calendar-focused MVP. CardDAV phases in after calendar sync is stable.

## Architecture Principle: Cross-Platform Core, Platform-Specific Shell

The app ships on Windows first, but all domain logic, sync engines, protocol implementations, and data access must live in portable .NET libraries that carry no Windows dependency.

The split:

| Layer | Target | Windows dependency |
|---|---|---|
| Domain models, business rules | net8.0 (or later, no OS suffix) | None |
| CalDAV provider | net8.0 | None |
| CardDAV provider | net8.0 | None |
| Future providers (Graph, Google, etc.) | net8.0 | None |
| Provider abstractions / interfaces | net8.0 | None |
| Persistence (SQLite cache, settings) | net8.0 | None |
| Windows shell (tray, flyout, main window) | net8.0-windows10.x | Yes — WinUI 3, Windows App SDK |
| Windows widget | net8.0-windows10.x | Yes — Windows App SDK Widgets |
| Credential storage | Abstracted interface; Windows implementation uses Credential Locker | Interface is portable, implementation is per-platform |

This means a future macOS app, Linux app, or .NET MAUI target can consume the same domain, sync, and protocol libraries. Only the UI shell and platform integration swap out.

## Architecture Principle: Provider Abstraction

CalDAV is the first calendar and contact provider, but it must not be the only one the architecture supports. The domain layer should define provider-agnostic interfaces:

- `ICalendarProvider` — discover calendars, sync events, create/update/delete events
- `IContactProvider` — discover address books, sync contacts, create/update/delete contacts
- `IAccountAuthenticator` — handle auth flow for a given account type

CalDAV/CardDAV is one implementation. Future implementations could include:
- Microsoft Graph (Outlook.com, Microsoft 365)
- Google Calendar API + Google People API
- Apple iCloud (CalDAV/CardDAV with Apple-specific auth)
- Local / offline-only provider

The domain and UI layers consume these interfaces. They never reference CalDAV types directly. This keeps provider-specific code isolated and makes adding new providers a contained task.

This does not mean building elaborate abstractions upfront. It means:
- Define the interfaces early, shaped by what CalDAV actually needs.
- Implement CalDAV/CardDAV behind those interfaces.
- Don't let CalDAV-specific concepts leak into domain models or UI code.
- When a second provider arrives, the interfaces may need to evolve — that's expected and fine.

## Windows OS Calendar Integration: Not Viable

Windows exposes appointment and user-data-account APIs (`AppointmentStore`, `UserDataAccountStore`) that in theory allow apps to read calendar data from OS-configured accounts (Outlook.com, Gmail, etc.) and even register as custom sync providers.

In practice, these APIs are effectively abandoned:

- `AppointmentStore` has been broken and unreliable for years, with no Microsoft response to bug reports (see [WindowsAppSDK#1852](https://github.com/microsoft/WindowsAppSDK/issues/1852)).
- The built-in Windows Mail & Calendar app, which backed the sync engine for these APIs, was retired at end of 2024 in favor of the new Outlook for Windows.
- The new Outlook app does not expose its data through `AppointmentStore` or the old account infrastructure.
- Registering a custom account type (e.g. CalDAV) requires the `userDataAccountsProvider` restricted capability, which is not available to normal third-party apps.
- No replacement API has been announced in Windows App SDK.

Consequently, this app will not depend on OS-level account or calendar integration. All calendar data access will go through direct protocol implementations (CalDAV now, and potentially Microsoft Graph or Google Calendar API in the future).

## UX Direction

### Inspiration

The target experience is a custom flyout that closely resembles the Windows calendar surface shown in the reference app and screenshot:

- Compact panel anchored near the bottom-right taskbar area.
- Dark, glassy, low-noise visual treatment.
- Header area with month context and quick navigation.
- A horizontal strip or compact date selector for fast day switching.
- Main agenda area showing the selected day’s events.
- Strong visual hierarchy: date first, then event titles, then time ranges and metadata.
- Optional subtle color accents taken from event/calendar color.

### Native Feel Principles

- Borderless or minimally framed flyout window.
- Rounded corners consistent with Windows 11.
- Proper shadows, spacing, and animation timing.
- Use WinUI typography, controls, and composition effects where they help.
- Support light and dark mode, but tune dark mode first because the reference style depends on it.
- Keep interaction density high without feeling cramped.
- Avoid web-app-in-a-desktop-shell visuals.

## App Surfaces

The app has two primary window types plus a widget:

### Flyout

- Compact tray-anchored panel for calendar agenda and quick contact lookup.
- Optimized for glancing and lightweight interaction.
- Not suitable for editing forms or data management.

### Main Window

- Lightweight standalone window that opens on demand from the flyout or tray icon.
- Hosts contact management (browse, search, add, edit, delete).
- Hosts event editing when that feature is added.
- Hosts account settings, calendar/address book selection, and sync diagnostics.
- Can close independently of the flyout.
- Should feel like a companion panel, not a full desktop application.

### Widget

- Windows widget for glanceable upcoming events.
- Read-only. Activates the flyout or main window when clicked.

## Core MVP Features

### Flyout Shell

- System tray icon with click to open and dismiss.
- Flyout appears near the taskbar clock area.
- Dismiss on outside click or Escape.
- Restore previous size and preferred position behavior where appropriate.
- Fast startup and fast reopen.
- Right-click tray icon offers: open flyout, open main window, sync now, settings, exit.

### Calendar Views

- Month header and compact calendar strip.
- Selected date state.
- Agenda list for the selected day.
- Clear empty-state when there are no events.
- Visual event color markers.

### Event Data

- Read events from one or more CalDAV calendars.
- Show:
  - title
  - start time
  - end time
  - all-day state
  - location if present
  - meeting URL if present
  - calendar color/category if available

### Account and Sync

- Add one CalDAV account in v1, with room to expand to multiple accounts.
- Store server URL, username, and credential securely.
- Discover all calendars from the CalDAV server, including shared/delegated calendars.
- Allow enabling or disabling individual calendars.
- Respect per-calendar permissions: detect read-only vs read-write from the server and reflect that in the UI.
- Assign distinct colors per calendar so events from personal vs shared calendars are visually distinguishable.
- Cache events locally for fast flyout rendering.
- Background or on-demand sync with a simple, understandable model.

### Basic Event Actions

- Open event details inside a compact details pane or secondary window.
- Open meeting URL when present.
- Optional stretch goal: open an edit URL in browser if Synology exposes one cleanly.
- Full event create/edit/delete can wait until after the read-focused MVP unless implementation turns out to be simple and reliable.

### Widget Surface

- Include a Windows widget backed by the same cached calendar data as the flyout.
- Show a concise upcoming agenda such as today, next event, or next few events.
- Open the flyout or main app when the user clicks into the widget.
- Keep the widget read-only at first.
- Defer richer widget interactions until the flyout and sync foundations are stable.

### Contact Management (Post-Calendar MVP)

- Full contact CRUD via the main window.
- Browse: scrollable alphabetical list with search.
- View: contact card showing all synced fields (name, email, phone, org, address, notes, photo).
- Source indicator: each contact card displays a small provider icon or glyph (e.g. a Synology logo, a stylized "G" for Google, an Outlook icon) so the user can tell at a glance which account owns the contact. The glyph is configurable per account in settings—pick from built-in provider icons or assign a custom label/color.
- Add: new contact form with common fields. Choose which address book to save to.
- Edit: same form, pre-filled from existing contact.
- Delete: with confirmation, synced back to CardDAV server.
- Photo: display if present, optional set/change.
- Address book selection: discover and list all address books from the server, allow enabling/disabling.
- Sync: push create/update/delete back to the CardDAV server.

### Calendar–Contact Integration

- Resolve event attendee email addresses against synced contacts.
- Display resolved contact name and photo in event detail views instead of raw email.
- Contact card accessible from event attendees: click to see full details.
- Quick actions from contact cards: compose email (via default mail handler), copy email, copy phone number.
- Integration is opportunistic: if a contact isn't found, the raw email address is shown without error.

### Multi-Provider Views & Source Indicators

When multiple accounts/providers are configured, the default view is **mixed**: events and contacts from all enabled sources appear together in a unified list. Each item carries a small source badge so provenance is always visible.

The user can switch views via a filter control (e.g. a segmented button or dropdown):
- **Mixed** (default) — all accounts combined.
- **Per-account** — e.g. "Synology", "Google", "Outlook" — shows only items from that account.
- The active filter persists per surface (flyout remembers its own, main window remembers its own).
- Filter applies to both the calendar/agenda and the contact list in that surface.

Source badges on items:
- **Contacts:** a provider glyph on the contact card (corner or inline badge).
- **Events:** a colored left-border or small icon on agenda items, keyed to the source account.
- **Calendars:** the calendar color already distinguishes sources; the source badge is secondary.

Badge configuration per account in settings:
- Built-in glyphs for known provider types (CalDAV generic, Synology, Google, Microsoft, iCloud).
- Fallback: first letter of account display name in a colored circle.
- Optional: user can override the glyph or pick a custom accent color.

### Default Save Targets

When the user creates a new contact or event and has multiple accounts, the app needs to know where to save it:
- **Default address book:** configurable in settings — pick which account + address book receives new contacts by default.
- **Default calendar:** configurable in settings — pick which account + calendar receives new events by default.
- The "Add" form pre-selects the default target but allows the user to switch before saving.
- If only one account exists, it is the implicit default with no extra configuration needed.

## Technical Stack

- Language: C#
- UI (Windows): WinUI 3 via Windows App SDK
- Future UI options: .NET MAUI, Avalonia, or platform-native shells consuming the same core libraries
- Secondary surface (Windows): Windows Widgets integration via Windows App SDK
- Packaging (Windows): Prefer packaged app (MSIX) for cleaner install, identity, and widget support
- Core libraries target: net8.0 (no OS suffix) for full portability
- Windows app target: net8.0-windows10.0.19041.0 or later
- Local storage: SQLite via Microsoft.Data.Sqlite (cross-platform)
- HTTP/WebDAV transport: WebDAVClient (cross-platform)
- XML/WebDAV handling: built-in .NET XML APIs (cross-platform)
- ICS parsing and recurrence handling: Ical.Net (cross-platform)
- vCard parsing and serialization: evaluate MixERP.Net.VCards or similar; hand-roll if the field set is narrow enough (cross-platform)
- Credential storage: abstracted interface; Windows implementation uses Credential Locker or DPAPI-backed secure storage

### Library Decisions

- Use Ical.Net from the start for iCalendar parsing, serialization, recurrence rules, and timezone-aware event handling. It targets netstandard/net6.0+ and is fully cross-platform.
- Use WebDAVClient from the start for WebDAV transport and request plumbing. It serves both CalDAV and CardDAV since they share the same WebDAV foundation. It targets net8.0 and is cross-platform.
- Keep direct XML handling in the app for CalDAV/CardDAV-specific discovery, REPORT payloads, sync-token parsing, and server-specific interoperability work.
- Choose a vCard library or write minimal parsing for the fields we need (name, email, phone, org, address, photo, notes). Evaluate before committing.
- All protocol and domain libraries must target net8.0 without a Windows TFM suffix. No WinUI, Windows App SDK, or Windows-specific NuGet references allowed in these projects.
- Do not depend on a niche end-to-end CalDAV or CardDAV abstraction as the core of the app.

## Architecture

### Portability Boundary

The architecture has a hard portability boundary. Everything below the UI and shell layers must be platform-agnostic:

```
┌─────────────────────────────────────────────┐
│  Platform-specific (Windows in v1)          │
│  ┌─────────┐ ┌───────────┐ ┌─────────────┐ │
│  │ Flyout  │ │Main Window│ │   Widget    │ │
│  │ (WinUI) │ │ (WinUI)   │ │ (WinAppSDK)│ │
│  └────┬────┘ └─────┬─────┘ └──────┬──────┘ │
├───────┴────────────┴──────────────┴─────────┤
│  Portable .NET libraries (net8.0)           │
│  ┌──────────────────────────────────────┐   │
│  │ Domain models, business rules        │   │
│  │ Provider interfaces                  │   │
│  │ CalDAV provider (ICalendarProvider)  │   │
│  │ CardDAV provider (IContactProvider)  │   │
│  │ Persistence / SQLite cache           │   │
│  │ Credential storage interface         │   │
│  └──────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

### High-Level Components

#### 1. Shell Layer (platform-specific)

Responsible for tray integration, flyout window behavior, activation, positioning, and dismissal logic.

Responsibilities:
- tray icon lifecycle
- flyout open/close
- taskbar-corner placement
- window z-order behavior
- keyboard handling
- theme integration

This layer is Windows-specific in v1. A future macOS or Linux shell would replace it entirely while consuming the same core libraries.

#### 2. UI Layer (platform-specific)

WinUI 3 views and view models for:
- flyout container
- date selector
- agenda list
- event detail pane
- main window frame
- contact list / search
- contact card / detail view
- contact add / edit form
- settings / account pages

Recommended pattern:
- MVVM with a simple, explicit state model
- avoid over-engineered abstractions early
- share view models between flyout and main window where the data is the same
- view models may live in a portable project if they don't reference WinUI types directly

#### 3. Domain Layer (portable, net8.0)

Models and business rules:
- calendar account (provider-agnostic)
- calendar source
- event instance
- recurrence expansion rules
- address book source
- contact instance
- attendee-to-contact resolution logic
- sync state
- selected date / visible range logic

Provider interfaces defined here:
- `ICalendarProvider` — calendar discovery, event sync, event CRUD
- `IContactProvider` — address book discovery, contact sync, contact CRUD
- `IAccountAuthenticator` — auth flow for a given provider type
- `ICredentialStore` — secure credential storage (platform-specific implementations)

#### 4. CalDAV Provider (portable, net8.0)

Implements `ICalendarProvider`. Responsible for CalDAV server communication.

Responsibilities:
- principal discovery
- calendar-home-set discovery
- calendar discovery
- REPORT/PROPFIND requests
- ETag handling
- sync-token handling if supported
- event retrieval and update mapping

Implementation notes:
- use WebDAVClient for request transport and authentication plumbing
- use built-in XML parsing for CalDAV request and response documents
- use Ical.Net to parse and normalize event payloads returned by the server

#### 5. CardDAV Provider (portable, net8.0)

Implements `IContactProvider`. Responsible for CardDAV server communication.

Responsibilities:
- principal discovery (shared with CalDAV where possible)
- addressbook-home-set discovery
- address book discovery
- REPORT/PROPFIND requests for contacts
- ETag handling
- sync-token handling if supported
- contact retrieval, creation, update, and deletion
- vCard parsing and serialization

Implementation notes:
- use WebDAVClient for request transport (same as CalDAV layer)
- use built-in XML parsing for CardDAV request and response documents
- use a vCard library or minimal parser for contact payloads
- share principal discovery logic with CalDAV layer since both protocols start from the same server

#### 6. Persistence Layer (portable, net8.0)

Local cache and settings:
- account configuration
- enabled calendars and address books
- sync metadata for both calendars and address books
- cached events
- cached contacts
- recurrence-expanded instances where appropriate
- attendee-to-contact resolution index (email → contact lookup)

#### 7. Widget Integration Layer (platform-specific)

Responsible for publishing compact, glanceable calendar content to a Windows widget.

Responsibilities:
- map cached agenda data into widget-friendly models
- drive widget refreshes after sync or significant local changes
- handle widget activation back into the flyout or app
- keep widget rendering independent from live network requests

## Proposed Data Model

### Account

- AccountId
- ProviderType (CalDAV, future: MicrosoftGraph, Google, iCloud, etc.)
- DisplayName
- ServerBaseUrl
- Username
- CredentialReference
- LastSyncUtc
- SyncToken
- IsEnabled
- SupportsCalendars
- SupportsContacts

### Calendar

- CalendarId
- AccountId
- DisplayName
- CalDavUrl
- Color
- IsReadOnly
- IsEnabled
- ETag or ctag metadata

### Event

- EventId
- CalendarId
- RemoteUid
- RemoteResourceUrl
- ETag
- Title
- Description
- Location
- StartUtc
- EndUtc
- TimeZoneId
- IsAllDay
- RecurrenceRule
- RecurrenceExceptions
- MeetingUrl
- RawICalendarPayload
- LastSeenUtc

### AddressBook

- AddressBookId
- AccountId
- DisplayName
- CardDavUrl
- IsReadOnly
- IsEnabled
- ETag or ctag metadata

### Contact

- ContactId
- AddressBookId
- RemoteUid
- RemoteResourceUrl
- ETag
- FullName
- FirstName
- LastName
- Organization
- Emails (list)
- PhoneNumbers (list)
- Addresses (list)
- PhotoUri or PhotoBlob
- Notes
- RawVCardPayload
- LastSeenUtc

## CalDAV Scope For MVP

### Supported

- Account connection to Synology Calendar via CalDAV
- Calendar discovery
- Read events in a bounded date range
- Incremental sync where server support exists
- Basic recurrence support
- Time zone aware display
- Read-only agenda experience

### Deferred

- Full recurring-event editing
- Attendee management
- alarms/reminders sync
- offline conflict resolution for edits
- attachments
- invitation workflow

## Synology-Specific Expectations

Synology Calendar and Contacts should work as standards-based CalDAV/CardDAV servers, but implementation details still need validation.

Assumptions:
- standard CalDAV discovery works
- standard CardDAV discovery works (`.well-known/carddav`, principal, addressbook-home-set)
- sync-token may or may not be available depending on version/configuration
- recurring events follow standard iCalendar data
- contacts follow vCard 3.0 or 4.0
- auth may require app-specific credentials or normal NAS user credentials
- same credentials work for both CalDAV and CardDAV

Validation tasks:
- confirm CalDAV discovery endpoints
- confirm CardDAV discovery endpoints
- confirm auth behavior (shared credentials for both protocols)
- confirm recurring event payloads
- confirm server behavior for changed/deleted events
- confirm server behavior for changed/deleted contacts
- confirm discovery of shared/delegated calendars alongside personal calendars
- confirm discovery of shared address books
- confirm per-calendar and per-address-book permission detection (read-only vs read-write)
- confirm vCard format version and field coverage
- confirm contact photo handling (embedded vs URI)
- confirm performance with multiple calendars and address books syncing concurrently

## Flyout Window Behavior Plan

This is a critical part of the product.

### Desired Behavior

- Open from tray icon click.
- Position near the bottom-right screen edge or taskbar anchor.
- Stay above normal windows while active.
- Close when focus is lost, unless interacting with submenus/dialogs.
- Support keyboard navigation.
- Animate in subtly, not theatrically.
- Reopen to the last selected date.

### Technical Considerations

WinUI 3 can provide the content and styling, but some behavior may need Win32 interop:
- precise popup positioning
- non-activating or lightly activating window behavior
- outside-click dismissal
- taskbar-aware placement
- optional exclusion from Alt+Tab depending on UX choice

This area should be treated as a first-class engineering task, not polish added at the end.

## UI Layout Proposal

The flyout is a single compact panel with three distinct vertical zones: the date strip at the top, the agenda below it, and the system tray anchoring it to the bottom edge of the screen. Every element should feel like it belongs in the Windows 11 shell.

### Overall Flyout Frame

- Fixed width, roughly 340-380px. Height adapts to content but has a comfortable max before scrolling.
- Dark background using the Windows 11 system dark surface color, not pure black. Slightly translucent or mica-backed if feasible.
- Rounded corners on the top edges of the flyout. The bottom edge sits flush against the taskbar with no visible gap.
- No title bar, no window chrome, no close button. The flyout dismisses on outside click or Escape.
- Subtle drop shadow on the top and sides to lift it off the desktop.

### Date Strip

- A compact horizontal row of day numbers across the top of the flyout.
- Shows approximately one week of dates, centered on or including today.
- Each date is a plain number in a small, regular-weight font.
- The selected date has a small filled dot or pill indicator directly below the number, not a full highlight circle.
- Dates with events could show a smaller secondary dot above or below to hint at content, but this should be subtle enough to not clutter the strip.
- Tapping a date scrolls the agenda below to that day.
- The strip should support horizontal swiping or arrow-key navigation to move through weeks.

### Date Heading and Actions

- Below the date strip, a left-aligned heading shows the full date of the selected day, e.g. "September 20".
- A small "+" icon button sits right-aligned on the same row. In MVP this can be hidden or non-functional, but the layout should reserve space for it.
- The heading uses a medium-weight font at roughly 16-18px equivalent, white or near-white on dark.

### Agenda Event List

- Below the heading, events are stacked vertically with consistent spacing between cards.
- Each event is a single row or compact card, not boxed or bordered. No visible card background distinct from the flyout background; separation comes from spacing alone.
- Left edge of each event has a thin vertical color bar (roughly 3-4px wide, with rounded ends) representing the calendar color. This is the primary visual accent per event.
- Event layout within each row, left to right:
  - Color bar, vertically spanning the full height of the row.
  - Small gap.
  - Text block:
    - First line: event title in regular weight, white/near-white, roughly 14-15px.
    - Second line: time range in a lighter gray, smaller size, e.g. "9:30 AM - 10:00 AM". If the event is recurring, a small recurrence icon sits inline after the time.
    - Optional third line: supplementary text like "Microsoft Teams Meeting" or a location, in the same muted gray, slightly smaller.
  - Right side: optional icon for meeting source (e.g. a Teams icon, or a generic video-call icon) or calendar provider badge, right-aligned and vertically centered.
- All-day events should appear at the top of the list for the selected day, styled slightly differently (no time range, possibly a subtle full-width accent instead of a side bar).

### Event Context Menu

- Right-clicking an event opens a compact context menu with rounded corners, matching the Windows 11 menu style.
- Menu items should include:
  - "Open in browser" with a globe icon (opens the event on the CalDAV server's web UI if available).
  - "Edit" with a pencil icon (deferred in MVP, but reserve the slot).
  - "Delete" with a trash icon (deferred in MVP, but reserve the slot).
- Each row has a small leading icon and a text label. No keyboard shortcut hints needed initially.
- The menu background matches the flyout dark surface, with a slightly elevated or bordered feel to distinguish it from the flyout body.
- Hover state on menu items uses a subtle lighter fill, consistent with WinUI 3 default menu styling.

### Empty State

- When the selected day has no events, show a single line of muted text centered in the agenda area, e.g. "No events for this day".
- No illustration, no icon, no call-to-action button. Keep it minimal and consistent with the overall low-noise aesthetic.

### Scrolling Behavior

- If there are more events than fit in the visible area, the agenda section scrolls vertically.
- The date strip and heading remain fixed at the top.
- Scrollbar should be thin, auto-hiding, and styled to match the dark theme.

### Settings Surface

- Accessed via a small gear icon in the date heading row or via right-click on the tray icon.
- Can open as a secondary panel within the flyout or as a separate small window.
- Contains:
  - account connection fields (server URL, username, password)
  - calendar list with toggles to show/hide each calendar
  - sync status indicator and manual refresh button
  - theme preference (follow system, force dark, force light)

### Visual Reference Summary

Key visual attributes derived from the reference screenshots:

| Element | Treatment |
|---|---|
| Background | Dark gray, near-black, slightly warm or neutral. Not pure #000. |
| Text primary | White or very light gray for titles and headings. |
| Text secondary | Medium gray for times, locations, subtitles. |
| Color bars | Thin vertical accents on event left edge: red, blue, pink, green, etc. per calendar. |
| Icons | Small, monochrome or lightly tinted, right-aligned in event rows. |
| Corners | Rounded on the flyout frame and context menus. |
| Spacing | Generous vertical gaps between events. Tight but not cramped horizontal padding. |
| Font | System font (Segoe UI Variable on Windows 11), with size hierarchy for title > time > subtitle. |
| Interaction | Hover highlight on events and menu items is subtle, not high-contrast. |

## Widget Plan

The widget should complement the flyout rather than replace it.

### Widget Goals

- Let the user see upcoming events without opening the flyout.
- Reinforce the app as part of the Windows shell experience.
- Reuse the same local cache and sync pipeline as the flyout.

### Initial Widget Scope

- Show current day and the next few upcoming events.
- Display event title and time range with restrained visual density.
- Open the flyout or app to the selected day when invoked.
- Support empty, loading, and stale-data states cleanly.

### Widget Constraints

- The widget should be read-only initially.
- The widget must never depend on live CalDAV calls for rendering.
- The widget should degrade gracefully when sync is stale or offline.
- Widget work should not block the first usable flyout release.

## Implementation Phases

### Phase 0: Discovery and Technical Validation

- Verify Synology CalDAV behavior against a test account.
- Verify Synology CardDAV behavior against the same account.
- Establish the baseline stack with Ical.Net and WebDAVClient.
- Evaluate vCard parsing libraries.
- Prove tray + flyout shell behavior in WinUI 3.
- Prove main window open/close lifecycle alongside the flyout.
- Confirm secure credential storage approach.
- Decide packaging approach.

Deliverable:
- short technical spike app proving tray icon, flyout, main window, CalDAV discovery, and CardDAV discovery

### Phase 1: Flyout Shell MVP

- Build tray icon and flyout window.
- Implement native-feeling positioning and dismiss behavior.
- Create static mock UI with realistic sample events.
- Tune spacing, typography, color, and shadows.

Deliverable:
- non-functional but visually representative flyout

### Phase 2: Read-Only CalDAV Sync MVP

- Add account setup
- Discover calendars
- Sync and cache events
- Render agenda for selected date
- Add manual refresh
- Handle loading, empty, and error states

Deliverable:
- working read-only calendar flyout for Synology via CalDAV

### Phase 3: Reliability and Polish

- Add recurrence expansion improvements
- Improve timezone handling
- Add incremental sync optimization
- Improve startup and reopen performance
- Add better error diagnostics and reconnect flows

Deliverable:
- stable daily-driver v1 candidate

### Phase 4: CardDAV Contacts

- Add CardDAV discovery and sync for address books.
- Cache contacts locally.
- Build contact list, search, and contact card views in the main window.
- Implement contact add, edit, and delete with sync back to server.
- Resolve event attendees against synced contacts in the flyout.
- Add quick actions from contact cards (email, copy, etc.).

Deliverable:
- working contact management via CardDAV with calendar–contact integration

### Phase 5: Windows Widget

- Add a read-only Windows widget powered by cached agenda data.
- Design a compact glanceable layout for upcoming events.
- Support activation from widget into the flyout or full app.
- Tune refresh behavior so widget data stays current without over-syncing.

Deliverable:
- working widget experience that complements the flyout

### Phase 6: Post-MVP Features

Possible next steps:
- multiple accounts
- additional providers: Microsoft Graph (Outlook.com / M365), Google Calendar + People API, iCloud CalDAV/CardDAV
- event creation and editing from the main window
- notifications/reminders
- quick join buttons for meeting URLs
- richer agenda grouping
- week view or upcoming view
- startup/background behavior options
- richer widget interactions (contacts widget, combined view)
- contact groups / favorites
- contact photo management
- macOS, Linux, or MAUI app targets consuming the same core libraries

## Risks

### 1. Scope Risk

Combining calendar and contacts increases the surface area. The phased approach mitigates this, but discipline is needed to avoid shipping nothing while building everything.

Mitigation:
- calendar flyout ships first as a standalone usable product
- contacts layer on top of proven infrastructure
- resist adding features to both surfaces simultaneously

### 2. Flyout Behavior Risk

WinUI 3 gives the visual stack, but not a turnkey system flyout primitive. Getting the window to feel truly native may require Win32 interop and iterative tuning.

Mitigation:
- prototype this first
- isolate windowing code behind a shell service

### 3. CalDAV/CardDAV Protocol Variance

Even standards-based CalDAV servers vary in sync behavior, recurrence representation, auth, and metadata.

Mitigation:
- target Synology first
- keep protocol logic modular
- capture raw payloads for diagnostics

### 4. Recurrence Complexity

Recurring events, exceptions, time zones, and all-day handling are where calendar apps usually get subtle bugs.

Mitigation:
- start read-only
- test recurrence thoroughly
- avoid custom parser logic unless necessary

### 5. Performance Risk

A flyout must feel instant. Network-bound rendering would make the product feel bad.

Mitigation:
- render from local cache
- sync in background or on explicit refresh
- minimize work on open

## Acceptance Criteria For MVP

- User can add a Synology CalDAV account successfully.
- App discovers available calendars.
- User can choose which calendars to display.
- Flyout opens from tray in under one second under normal conditions.
- Agenda for selected date renders from cache immediately after initial sync.
- Events display correct local times and all-day state.
- Basic recurrence works for common cases.
- App survives offline mode by showing cached data.
- UI feels consistent with Windows 11 and close in spirit to the reference flyout.

## Suggested Project Structure

Portable libraries (net8.0, no Windows dependency):
- src/Core.Domain — models, interfaces, business rules
- src/Core.Providers.CalDav — ICalendarProvider implementation
- src/Core.Providers.CardDav — IContactProvider implementation
- src/Core.Persistence — SQLite cache, settings, credential store interface
- tests/Core.Tests — tests for all portable code

Windows app (net8.0-windows10.x):
- src/Windows.App — app entry point, packaging, tray lifecycle
- src/Windows.Shell — flyout window behavior, Win32 interop
- src/Windows.UI — WinUI 3 views and view models for flyout and main window
- src/Windows.Widget — Windows widget implementation
- src/Windows.Platform — platform-specific implementations (credential store, etc.)

Future platform targets would add parallel shell/UI projects:
- src/Maui.App, src/Mac.App, src/Linux.App, etc.
- All consuming Core.* libraries unchanged.

Shared:
- docs/

A simpler single-solution start is reasonable. The critical constraint is that Core.* projects never reference Windows-specific packages. Enforce this via TFM (net8.0, not net8.0-windows).

## Open Questions

- Packaged or unpackaged for v1?
- Read-only MVP, or include event creation from day one?
- Single account only, or multiple CalDAV/CardDAV accounts in the first usable build?
- Should the flyout appear in Alt+Tab? Should the main window?
- Do we want startup-on-login in v1 or later?
- Is the tray icon always visible, or only in the overflow area by default?
- (Decided) App name: **Kava**. Icon/logo direction: kite-inspired geometric mark.
- Should contacts be searchable from the flyout, or only from the main window?
- How much contact detail should appear in the flyout vs requiring the main window?
- (Decided) Multi-provider default view is mixed with a filter to switch to per-account views. Each item carries a configurable source icon/glyph. User can set a default save target (account + address book / calendar) for new items.

## Recommended Initial Decision Set

- Use C# + WinUI 3
- Package the app
- Start with one account (CalDAV + CardDAV share the same account)
- Multi-provider: default-to-mixed with filterable per-account views and configurable default save targets
- Focus on read-only calendar agenda first
- Add CardDAV contacts after the calendar flyout is stable
- Treat Synology as the reference server for both protocols
- Build tray + flyout shell before protocol depth
- Build main window as the home for contact management and settings
- Add the widget after flyout and contacts are stable
- Keep all domain and protocol code in portable net8.0 libraries from day one
- Define provider interfaces early; implement CalDAV/CardDAV behind them
- Never allow Windows-specific references in core libraries
- Prioritize cache-backed performance over broad feature scope

## First Concrete Milestones

1. Create a WinUI 3 packaged desktop app skeleton with flyout and main window scaffolding.
2. Implement tray icon and open/close flyout shell.
3. Reproduce the target flyout look using sample data.
4. Build CalDAV discovery against a Synology test account.
5. Cache events in SQLite and render a real agenda.
6. Harden recurrence, timezone, and sync behavior.
7. Build CardDAV discovery and contact sync against the same Synology account.
8. Implement contact browse, search, add, edit, delete in the main window.
9. Wire up attendee-to-contact resolution in the flyout.
10. Add a read-only Windows widget backed by cached agenda data.
11. Prepare packaging, settings, and diagnostics for distribution.

## Summary

Kava fills the gap Windows created by abandoning its built-in calendar and contacts infrastructure. It is a native-feeling Windows flyout plus a lightweight main window, backed by CalDAV and CardDAV engines — not an attempt to hook into the defunct OS calendar or People apps.

The flyout is the daily-driver surface for calendar access. The main window is the home for contact management and settings. A Windows widget extends the experience as a glanceable surface.

The icon/logo direction is a kite-inspired geometric mark — simple enough to read at tray-icon size, evoking lightness, connection, and the "ka" in Kava.

Delivery is phased: calendar flyout first, then contacts, then widget. The immediate success criteria are visual fidelity, instant open behavior, and reliable Synology CalDAV reading. Once those are solid, CardDAV contacts layer onto the same infrastructure with shared sync, shared auth, and calendar–contact integration. The combined product gives self-hosted users a single, lightweight Windows app for their schedule and their people.
