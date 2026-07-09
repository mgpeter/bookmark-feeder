---
description: Git Commit Message Rules
alwaysApply: true
---
# Git Commit Message Rules

Rules for maintaining consistent and informative Git commit messages.

<rule>
name: git_commits
description: Enforces Git commit message standards and best practices
filters:
  - type: file_extension
    pattern: "\\.git/COMMIT_EDITMSG$"
  - type: event
    pattern: "file_create"

actions:
  - type: reject
    conditions:
      - pattern: "^.{73,}"
        message: "Subject line must not exceed 72 characters"
      - pattern: "^[a-z]"
        message: "Subject line must start with a capital letter"
      - pattern: "^.*\\.$"
        message: "Subject line must not end with a period"
      - pattern: "^[A-Za-z]+: "
        message: "Do not use conventional commit types (feat:, fix:, etc.)"
      - pattern: "^\\s+"
        message: "Subject line must not start with whitespace"
      - pattern: "^[A-Za-z]+\\s*$"
        message: "Subject line must be a complete sentence"

  - type: suggest
    message: |
      Git Commit Message Standards:

      1. Subject Line (First Line):
         - Maximum 72 characters
         - Start with a capital letter
         - No period at the end
         - Use imperative mood ("Add" not "Added")
         - Complete sentence
         - Examples:
           ```
           Add user authentication service
           Implement caching mechanism
           Fix database connection timeout
           ```

      2. Body (After Blank Line):
         - Separate from subject with blank line
         - Wrap at 72 characters
         - Explain what and why, not how
         - Reference issues when relevant
         - Examples:
           ```
           Add user authentication service

           Implement JWT-based authentication service to handle user
           login and session management. This enables secure access
           to protected endpoints.

           - Add JWT token generation
           - Configure proper secret management
           - Implement token validation
           - Add session handling

           Closes #123
           ```

      3. Breaking Changes:
         - Start with "BREAKING CHANGE:"
         - Explain what changed and why
         - Example:
           ```
           Update authentication flow

           BREAKING CHANGE: Move authentication to a separate
           microservice for better scalability. This change
           requires updating client configurations.
           ```

      4. Common Patterns:
         - Feature additions: "Add [feature]"
         - Bug fixes: "Fix [issue]"
         - Refactoring: "Refactor [component]"
         - Performance: "Optimize [component]"
         - Documentation: "Update [docs]"

examples:
  - input: |
      feat: add auth

      // Bad: Missing body, uses conventional commit type
    output: "Incorrect commit message format"

  - input: |
      Add user authentication service

      Implement JWT-based authentication service to handle user
      login and session management. This enables secure access
      to protected endpoints.

      - Add JWT token generation
      - Configure proper secret management
      - Implement token validation
      - Add session handling

      Closes #123
    output: "Correctly formatted commit message"

metadata:
  priority: high
  version: 1.0
</rule> 