# Product Mission

## Pitch

BookmarkFeeder is a privacy-first, self-hosted bookmark manager that helps
privacy-conscious professionals and self-hosting enthusiasts collect, organize,
and rediscover their browser bookmarks by keeping all data on their own
infrastructure and (soon) auto-organizing it with AI.

## Users

### Primary Customers

- **Privacy-conscious professionals**: developers, researchers, and knowledge
  workers who want full ownership of their bookmark data instead of trusting a
  third-party cloud service.
- **Self-hosting enthusiasts**: home-lab and NAS owners (e.g. Synology) who run
  their own services via Docker and want a "Pocket alternative" they control.

### User Personas

**Alex Chen** (28-40 years old)
- **Role:** Senior Software Engineer
- **Context:** Runs a home lab; deeply values data ownership and open standards.
- **Pain Points:** Cloud read-later services mine data and can shut down; bookmarks scattered across browsers and machines.
- **Goals:** Self-host a bookmark service in under 30 minutes; sync selected browser folders; keep everything on his own hardware.

**Dr. Sarah Martinez** (35-55 years old)
- **Role:** Academic Researcher
- **Context:** Collects hundreds of papers, articles, and references per project.
- **Pain Points:** Manual tagging is tedious; finding a saved source months later is slow.
- **Goals:** Fast full-text search across a large collection; AI-assisted categorization; reliable long-term storage.

**Michael Thompson** (30-50 years old)
- **Role:** Marketing Consultant
- **Context:** Curates competitor sites, campaigns, and inspiration for multiple clients.
- **Pain Points:** No good way to group and revisit bookmarks by topic or client.
- **Goals:** Folder-based sync, categories and tags, quick browsing from any device on his network.

## The Problem

### Bookmarks are trapped and disorganized

Browser bookmarks pile up in nested folders that are never revisited, and the
convenient alternatives (Pocket, Instapaper) are cloud services that own your
data and can disappear. There is no self-hosted option that combines easy
collection, real organization, and search.

**Our Solution:** A self-hosted API + web app + browser extension that syncs
selected bookmark folders to your own PostgreSQL database, with tagging,
categories, search, and AI-assisted organization.

### Manual organization does not scale

Tagging and categorizing hundreds of bookmarks by hand is tedious, so most
collections stay a flat, unsearchable dump.

**Our Solution:** AI categorization (planned) suggests tags and categories from
each bookmark's title, URL, and description, with a user-approval workflow.

## Differentiators

### Complete data ownership

Unlike Pocket, Instapaper, or Raindrop's cloud tiers, BookmarkFeeder runs
entirely on your own infrastructure - no data leaves your network. This results
in true privacy and no risk of a service shutting down your access.

### Browser-native collection, not manual entry

Unlike generic bookmark apps that require manual saving, BookmarkFeeder's MV3
extension syncs whole selected browser folders in a couple of clicks, so
existing bookmarks flow in without re-entry.

### Open, standard, self-hostable stack

Unlike closed SaaS products, BookmarkFeeder is built on .NET, PostgreSQL, React,
and Docker - deployable on a Synology NAS or any Docker host, and extensible.

## Key Features

### Collection

- **Folder-based sync:** Select browser folders in the extension and sync all
  their bookmarks (recursively) to your server.
- **Batch ingestion with de-duplication:** The API accepts batches and skips
  URLs it already has.

### Organization

- **Tags:** Colored, case-insensitively de-duplicated tags, resolved-or-created by name.
- **Hierarchical categories:** Nest categories and reassign on delete.
- **Read/unread tracking:** Mark bookmarks read or unread.

### Discovery

- **Filtering & sorting:** Filter by search text, tags, category, source folder,
  and read state; sort by date or title.
- **Full-text search (planned):** PostgreSQL-backed ranked search with highlights.

### Intelligence (planned)

- **AI categorization:** LLM-suggested tags and categories with confidence
  scoring and an approve/auto-apply workflow.

### Access

- **Web app:** A React interface to browse, search, edit, and organize the collection.
- **Privacy-preserving favicons:** Domain monograms with an optional same-origin
  favicon - never a third-party favicon service.
