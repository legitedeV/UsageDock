# UsageDock design

UsageDock is an Operate interface: a native Windows 11 tool for inspecting several AI connections without losing track of which allowance or budget belongs to which account. Version 0.3.0 extends the approved compact dark-and-mint visual world across all four tabs. The dashboard targets 1128 x 756, supports 960 x 580, and keeps a separate 268 x 548 pinned widget. Content adapts or scrolls at smaller sizes instead of shrinking text.

## Shared visual language

Segoe UI, restrained mint accents, dark neutral surfaces, quiet separators, and aligned numeric data form the application identity. Use clear heading, section, body and supporting-text levels; spacing groups related controls and separates distinct tasks. The light theme carries the same hierarchy and adequate contrast. Interactive controls have visible keyboard focus, hover, selection and disabled states. Icons share a consistent stroke. Avoid decorative graphs, glass effects, redundant containers and dashboard numbers with no useful meaning.

The reference palette remains background #111B21, header #161E25, surface #1C272F, primary text #E3E8EB, secondary #A2ADB8 and accent #3CD3AD. Teal, amber and red communicate usage or status only alongside text. Color alone must never communicate availability or failure.

## Accounts

Keep a compact account table with distinguishable names, provider, allowance or cost, reset or budget, tokens and actions. Longer names must not hide their distinguishing suffixes unnecessarily; ellipsis has a full-name tooltip. Retain missing values as unavailable. Separate an initial failed read from an outdated previously successful read, and offer a useful recovery action. API budgets are local user thresholds. Native search and account actions remain accessible when the window narrows.

## Statistics

This is a report of the latest available snapshots, not a fictional history. Present connection health followed by structured allowance rows for each subscription and a separate API spending view. Include all reported windows, known percentages, reset times, stale status and retrieval context. Missing percentages have no filled zero-value bar. Do not add overlapping organization costs or imply a general ChatGPT conversation quota. Empty and failed-read states explain the next useful action.

## History

Use a current-session event list with timestamps, account context, event type and status. Record meaningful changes in connection state and refresh results; a generic controller notification is not itself a useful history item. Filter reads and problems, clear the session list, and retain at most 100 events. No raw server responses, credentials or secret-bearing exceptions enter the event list. Clearly state that history is not persisted between launches.

## Settings

Group appearance, refresh and notifications, startup and widget controls into designed setting rows. Each row pairs a concise label and explanation with the corresponding native control. Provide visible unsaved, invalid and saved feedback, explicit save and revert actions, and inline interval validation. Appearance is an exception: the main-window and widget theme controls apply and persist only the theme immediately; unrelated draft preferences remain untouched. Draft edits survive background refresh and tab navigation. Demo mode never modifies startup registration or real stored preferences.

## Verification and scope

Render every tab in dark, light and minimum-size windows using the actual WPF visual tree and synthetic fixtures. Inspect empty, unavailable, invalid and long-content states. Pair one complete inspection with one corrective batch and confirmation. In-process interaction checks complement Core tests; neither should be described as full external mouse automation. The original screenshot comparison belongs to version 0.2.0 and is not evidence of pixel accuracy for the expanded tab designs.

Credentials remain in the existing Windows DPAPI store. This UI refinement neither modifies provider authentication nor migrates connected accounts. No private connection names or credentials enter public screenshots or release artifacts.
