using System.Text.Json;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandle_AnswersForbiddenWithTheErrorCodeForAnUnverifiedMype()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, new MypeNotVerifiedException(), default);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("MYPE_NOT_VERIFIED", body.RootElement.GetProperty("errorCode").GetString());
    }
}
