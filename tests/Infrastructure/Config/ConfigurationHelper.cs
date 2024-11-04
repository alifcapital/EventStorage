
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EventStorage.Tests.Infrastructure.Config;

public static class ConfigurationHelper
{
    private static readonly string DefaultEnvironmentVariable = Environments.Development;
        
    public static IConfiguration LoadConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT") ?? DefaultEnvironmentVariable;

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}