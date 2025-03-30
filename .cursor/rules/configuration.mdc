---
description: Configuration and Secrets Management Rules
globs: *.json,*.cs
---
# Configuration and Secrets Management Rules

Rules for maintaining configuration and secrets management standards.

<rule>
name: configuration
description: Enforces configuration and secrets management standards
filters:
  - type: file_extension
    pattern: "\\.(json|cs)$"
  - type: content
    pattern: "appsettings|IConfiguration|Options|UserSecrets|KeyVault"
  - type: event
    pattern: "file_create|file_modify"

actions:
  - type: reject
    conditions:
      - pattern: "apiKey = \"[^\"]+\""
        message: "Never hardcode API keys or secrets"
      - pattern: "appsettings\\.json.*\"ApiKey\""
        message: "Never store secrets in appsettings.json"
      - pattern: "IConfiguration\\[\\\"[^\"]+\\\"\\]"
        message: "Use strongly-typed configuration with Options pattern"
      - pattern: "new ConfigurationBuilder\\(\\)"
        message: "Use dependency injection for configuration"

  - type: suggest
    message: |
      Configuration and Secrets Standards:

      1. Configuration Structure:
         ```
         Core/
         ├── Configuration/
         │   ├── ServiceOptions.cs
         │   └── IntegrationOptions.cs
         └── DependencyInjection.cs

         WebApi/
         ├── appsettings.json
         ├── appsettings.Development.json
         └── appsettings.Example.json
         ```

      2. Options Pattern:
         ```csharp
         // Core/Configuration/ServiceOptions.cs
         public class ServiceOptions
         {
             public string ApiKey { get; set; }
             public string Provider { get; set; }
             public int MaxItems { get; set; }
         }

         // Core/DependencyInjection.cs
         public static IServiceCollection AddCoreServices(
             this IServiceCollection services,
             IConfiguration configuration)
         {
             services.Configure<ServiceOptions>(
                 configuration.GetSection("Service"));
             
             services.AddScoped<IExternalService, ExternalService>();
             return services;
         }

         // Service implementation
         public class ExternalService : IExternalService
         {
             private readonly IOptions<ServiceOptions> _options;
             
             public ExternalService(IOptions<ServiceOptions> options)
             {
                 _options = options;
             }
         }
         ```

      3. Secrets Management:
         - Development:
           ```
           // Use User Secrets
           dotnet user-secrets init
           dotnet user-secrets set "Service:ApiKey" "your-key"
           ```
         - Production:
           ```
           // Use Azure Key Vault
           services.AddAzureKeyVault(
               new Uri($"https://{configuration["KeyVaultName"]}.vault.azure.net/"),
               new DefaultAzureCredential());
           ```

      4. Configuration Files:
         ```json
         // appsettings.Example.json
         {
           "Service": {
             "Provider": "ExternalApi",
             "MaxItems": 100
           },
           "KeyVaultName": "your-keyvault"
         }

         // appsettings.json (no secrets)
         {
           "Logging": {
             "LogLevel": {
               "Default": "Information"
             }
           }
         }
         ```

      5. Validation:
         ```csharp
         public class ServiceOptions
         {
             [Required]
             public string ApiKey { get; set; }

             [Range(1, 1000)]
             public int MaxItems { get; set; }
         }

         // In Startup.cs
         services.Configure<ServiceOptions>(
             configuration.GetSection("Service"))
             .ValidateDataAnnotations();
         ```

examples:
  - input: |
      // Bad: Direct configuration access
      private readonly IConfiguration _config;
      private string ApiKey => _config["Service:ApiKey"];

      // Good: Using Options pattern
      private readonly IOptions<ServiceOptions> _options;
      private string ApiKey => _options.Value.ApiKey;
    output: "Correctly implemented configuration access"

  - input: |
      // Bad: Hardcoded secret
      private const string ApiKey = "secret-key";

      // Good: Using User Secrets
      // In appsettings.Example.json
      {
        "Service": {
          "ApiKey": "your-key-here"
        }
      }
      // In User Secrets
      {
        "Service:ApiKey": "actual-secret-key"
      }
    output: "Correctly implemented secrets management"

metadata:
  priority: high
  version: 1.0
</rule> 