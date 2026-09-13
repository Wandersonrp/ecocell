using System.Reflection;
using Ecocell.Api.Extensions;
using Ecocell.Api.Services.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Extensions;

public class DependencyInjectionExtensionsTests
{
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
}
