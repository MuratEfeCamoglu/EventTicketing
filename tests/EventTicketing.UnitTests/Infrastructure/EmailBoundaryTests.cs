namespace EventTicketing.UnitTests.Infrastructure;

public sealed class EmailBoundaryTests
{
    [Fact]
    [Trait("AC", "AC-17.5")]
    public void ApiAssembly_References_NoMailKitAndNoEmailSender()
    {
        var api = typeof(Program).Assembly;

        api.GetReferencedAssemblies().Select(a => a.Name).ShouldNotContain(name => name == "MailKit" || name == "MimeKit");
        api.GetTypes().ShouldNotContain(type => type.Name == "IEmailSender");
    }
}
