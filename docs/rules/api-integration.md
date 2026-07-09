---
description: API Integration Rules
globs: *.cs
---
# API Integration Rules

Rules for maintaining API integration standards.

<rule>
name: api_integration
description: Enforces API integration and service standards
filters:
  - type: file_extension
    pattern: "\\.(cs|json)$"
  - type: content
    pattern: "HttpClient|OpenAI|API|endpoint|IClient|IService"
  - type: event
    pattern: "file_create|file_modify"

actions:
  - type: reject
    conditions:
      - pattern: "new HttpClient\\(\\)"
        message: "Use IHttpClientFactory for HTTP clients"
      - pattern: "apiKey = \"[^\"]+\""
        message: "Use proper configuration for API keys"
      - pattern: "await httpClient\\.GetAsync"
        message: "Use typed API clients instead of raw HTTP calls"

  - type: suggest
    message: |
      API Integration Standards:

      1. Project Organization:
         ```
         Core/
         ├── Abstractions/
         │   ├── Services/              # Service abstractions
         │   │   ├── IExternalService.cs
         │   │   └── IIntegrationService.cs
         │   └── Clients/               # Client abstractions
         │       ├── IApiClient.cs
         │       └── IExternalClient.cs
         └── Models/
             └── Api/                   # API DTOs and requests
         
         Integration.Provider/          # Provider-specific implementations
         ├── Services/
         │   └── ExternalService.cs
         ├── Clients/
         │   └── ApiClient.cs
         └── DependencyInjection.cs     # Extension methods for DI
         ```

      2. Abstraction Standards:
         - Define interfaces/abstract classes in Core
         - Keep abstractions focused and minimal
         - Use proper abstraction levels
         - Follow ISP (Interface Segregation)
         - Document abstraction contracts

      3. Implementation Standards:
         - Implement in provider-specific projects
         - Use IHttpClientFactory
         - Handle provider-specific logic
         - Implement retry policies
         - Use circuit breakers

      4. Configuration:
         - Use options pattern
         - Keep provider settings separate
         - Use user secrets for local dev
         - Validate configurations
         - Use proper DI registration

      5. Security & Performance:
         - Secure API keys per provider
         - Implement provider-specific caching
         - Handle rate limits per service
         - Monitor API usage
         - Implement proper logging

examples:
  - input: |
      // Bad: Implementation in Core project and wrong namespace
      namespace Core.Interfaces {
          public class ApiClient : IApiClient { }
      }

      // Good: Interface in Core Abstractions, implementation in Integration
      // Core/Abstractions/Clients/IApiClient.cs
      namespace Core.Abstractions.Clients {
          public interface IApiClient {
              Task<Response> SendRequest(Request request);
          }
      }

      // Integration.Provider/Clients/ApiClient.cs
      namespace Integration.Provider.Clients {
          public class ApiClient : IApiClient { }
      }
    output: "Correctly separated abstraction and implementation"

  - input: |
      // Bad: Direct configuration
      services.AddHttpClient<ApiClient>();

      // Good: Using extension methods
      // Integration.Provider/DependencyInjection.cs
      public static IServiceCollection AddProviderServices(
          this IServiceCollection services,
          IConfiguration configuration)
      {
          services.Configure<ProviderOptions>(
              configuration.GetSection("Provider"));
          
          services.AddHttpClient<IApiClient, ApiClient>()
              .AddPolicyHandler(GetRetryPolicy())
              .AddPolicyHandler(GetCircuitBreakerPolicy());
              
          return services;
      }
    output: "Correctly implemented service registration"

metadata:
  priority: high
  version: 1.0
</rule> 