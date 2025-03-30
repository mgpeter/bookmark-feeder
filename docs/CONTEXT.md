# CONTEXT.md

## 🔖 Overview

This project is a self-hosted bookmark management platform intended to replace services like Pocket. It allows users to collect, categorize, and review bookmarks from specific browser bookmark folders. The system is composed of three main components:

- A **Chrome extension** that integrates with Edge to send user bookmarks to the backend
- A **.NET 9 Web API** backend to receive, process, and store bookmarks
- An **Angular 19 frontend** to display and manage bookmarks
- A **PostgreSQL database** for persistent storage

---

## 🎯 Core Features

1. **Send Specific Bookmark Directories**
   - User selects one or more bookmark folders in their browser (Edge/Chrome)
   - Bookmarks from these folders are sent to the backend upon user request
   - Backend stores these bookmarks

2. **Manage Categories/Tags**
   - Users can manually assign categories and tags to bookmarks
   - Bookmarks may belong to multiple tags

3. **Auto-Categorization with OpenAI**
   - Backend integrates with OpenAI to suggest categories/tags for new bookmarks
   - Categories based on bookmark title, URL, and optionally extracted metadata/content

4. **Review & Edit Bookmarks**
   - Users can browse all stored bookmarks via the frontend
   - Tags/categories can be updated manually

5. **Search Functionality**
   - Search by URL, title, category, or tag
   - Full-text or tag-based filtering

---

## 🧩 Project Structure

### 1. **Chrome Extension**

- Reads browser bookmarks via the `chrome.bookmarks` API
- Allows configuration of backend server address
- UI to select specific bookmark folders (e.g. "Research", "Tools")
- Sends selected bookmarks to backend via REST API
- Authentication may be token-based or session-less for MVP
- Authentication not required for MVP

### 2. **Backend: BookmarkFeeder.WebApi (.NET 9)**

- **Endpoints:**
  - `POST /api/bookmarks` – Receive and store bookmarks
  - `GET /api/bookmarks` – Retrieve bookmarks for display/search
  - `PUT /api/bookmarks/{id}` – Update tags/categories
  - `POST /api/bookmarks/categorize` – Trigger OpenAI auto-categorization

- **Features:**
  - Duplicate detection (based on URL or URL + title hash)
  - OpenAI integration for categorization (GPT API or local LLM)
  - Input validation and sanitization
  - Tag normalization (no case-sensitive duplicates)

### 3. **Frontend: BookmarkFeeder.Angular**

- UI for:
  - Viewing bookmarks (card or list layout)
  - Searching and filtering by tag/category
  - Editing tags and metadata
- Responsive and mobile-friendly
- Consumes the WebAPI endpoints

### 4. **PostgreSQL Database**

- Tables:
  - `bookmarks` (id, url, title, description, date_added, etc.)
  - `tags` (id, name)
  - `bookmark_tags` (bookmark_id, tag_id)
  - `sources` (optional, to track source folder or device)

---

## 🔐 Deployment Considerations

- All services are deployable via Docker (extension excluded)
- Synology NAS runs the backend and frontend via Docker Compose

---

## 📦 Future Enhancements

- Import/export functionality (e.g. HTML bookmark files)
- Offline/PWA support for frontend
- Background sync on schedule
- Personal metrics dashboard (most visited tags, etc.)
- Support for notes/highlights per bookmark
- Markdown rendering for descriptions/notes

---

## 🤖 AI Integration Details

- Use OpenAI GPT API for tag/category suggestions
- Input: Bookmark title + URL + optional description
- Output: List of suggested tags or categories
- Configurable confidence threshold or allow user to approve suggestions

---

## 📝 Tech stack

- Angular 19
  - <https://angular.dev/overview>
- .NET 9
  - <https://dotnet.microsoft.com/en-us/download/dotnet/9.0>
- PostgreSQL
  - <https://www.postgresql.org/>
- OpenAI GPT API
  - <https://platform.openai.com/docs/introduction>
  - <https://platform.openai.com/api-reference/introduction>
  - <https://github.com/openai/openai-dotnet>
- Tailwind CSS v4
  - <https://ui.shadcn.com/docs/tailwind-v4>
- Shadcn UI
  - <https://ui.shadcn.com/>
- Docker
  - <https://www.docker.com/>
- Docker Compose
  - <https://docs.docker.com/compose/>

### 📝 Tech notes

- Use Tailwind CSS v4 for styling
- Use Shadcn UI for components
- Use Docker for containerization
- Use Docker Compose for multi-container applications
- Use PostgreSQL for database
- Use OpenAI GPT API for AI integration
- Use dotnet OpenAI nuget package for OpenAI API integration

### Authentication Strategy

- Implement proper authentication
- Options to evaluate:
  - Auth0 integration: <https://auth0.com/blog/backend-for-frontend-pattern-with-auth0-and-dotnet/>
  - ASP.NET Core Identity
- Secure token management via BFF pattern

---

## 📌 Summary

This project empowers users to take control of their bookmarks, enhancing them with AI-driven categorization, tag management, and cross-device synchronization — all self-hosted and privacy-respecting.
