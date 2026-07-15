# Spec Requirements Document

> Spec: Mark All As Read
> Created: 2026-07-14
> Status: Completed
> Completed: 2026-07-14

## Overview

Add a "Mark all as read" action to the bookmark list that marks every bookmark matching the
current search and filters as read - not just the page on screen - behind a confirmation dialog
that states exactly how many bookmarks will be affected.

## User Stories

### Clear out a search in one action

As someone who has just searched or filtered down to a set of bookmarks I've finished with, I
want to mark the whole result set as read in one action, so that I don't page through and mark
each one individually.

The user narrows the list (search term, tags, category, source folder, dates, read state), clicks
"Mark all as read" in the list header, and sees a dialog naming the count - "Mark all 137 matching
bookmarks as read?". On confirm, every match across all pages is marked read, the list refreshes,
and a toast reports how many were updated.

### Understand the blast radius before confirming

As a user about to change many records at once, I want the confirmation to tell me how many
bookmarks it will touch and whether my filters are narrowing it, so that I don't sweep my whole
collection by accident.

When no search or filter is active, the action still works but the dialog says so explicitly -
"Mark all 4,312 bookmarks as read? This affects your entire collection." - so the count and the
wording are the safeguard.

## Spec Scope

1. **Bulk mark-read endpoint** - `POST /api/bookmarks/mark-read` accepting the same filter
   parameters as the list endpoint and setting `IsRead` on every match in a single statement.
2. **Shared filter composition** - Extract the `BookmarkQuery` filtering from `GetBookmarksAsync`
   into one helper used by both list and bulk-update, so what's marked always matches what's shown.
3. **Confirmation dialog** - An `AlertDialog` stating the affected count, with wording that
   distinguishes a filtered set from the whole collection.
4. **List header action + feedback** - A "Mark all as read" button next to "Add bookmark",
   disabled when the result set is empty, with a toast reporting the number actually updated and a
   refreshed list.

## Out of Scope

- Undo (the confirmation dialog with an explicit count is the only guard).
- Marking as *un*read in bulk from the UI (the endpoint takes `isRead`, so the capability exists,
  but no UI surfaces the unread direction).
- Per-bookmark selection / checkboxes and a selection-based bulk bar.
- Any other bulk operation (bulk delete, bulk retag, bulk recategorize).
- A background job or progress UI for very large sets (the update is one SQL statement).

## Expected Deliverable

1. With a search or filter active, "Mark all as read" marks every match across all pages - not
   just the current page - and the list reflects it after confirming.
2. The confirmation dialog states the correct count before the action, and cancelling changes
   nothing.
3. With no filters active, the dialog warns that the entire collection is affected, and the action
   still completes.
