using System.Reflection;
using Ecocell.Api.Extensions;
using Ecocell.Api.Jobs;
using Ecocell.Api.Services.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Extensions;

public class DependencyInjectionExtensionsTests
{
    [Fact]
    public void AddScoreProcessing_ShouldNotRegisterHostedService_WhenEnvironmentIsTesting()
    {
        var services = InvokeAddScoreProcessing("Testing");

        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(CreditScoreJob)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(CreditScoreDispatcher)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddScoreProcessing_ShouldRegisterHostedService_WhenEnvironmentIsDevelopment()
    {
        var services = InvokeAddScoreProcessing(Environments.Development);

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddServices_ShouldRegisterLoggingEmailSender_WhenEnvironmentIsDevelopment()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);

        var method = typeof(DependencyInjectionExtensions).GetMethod(
            "AddServices",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        // Act
        method!.Invoke(null, new object[] { services, environment.Object });
        using var provider = services.BuildServiceProvider();
        var emailSender = provider.GetRequiredService<IEmailSender>();

        // Assert
        emailSender.ShouldBeOfType<LoggingEmailSender>();
    }

    private static IServiceCollection InvokeAddScoreProcessing(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        var method = typeof(DependencyInjectionExtensions).GetMethod(
            "AddScoreProcessing",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        method!.Invoke(null, new object[] { services, environment.Object });
        return services;
    }
}
