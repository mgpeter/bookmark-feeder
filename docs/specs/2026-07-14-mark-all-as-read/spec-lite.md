# Spec Summary (Lite)

Add a "Mark all as read" action to the bookmark list that marks every bookmark matching the
current search and filters as read across all pages, not just the visible page. A new
`POST /api/bookmarks/mark-read` reuses the list endpoint's filter composition and updates matches
in a single statement; the UI guards it with a confirmation dialog stating the affected count and
warning when no filters are active. No undo.
