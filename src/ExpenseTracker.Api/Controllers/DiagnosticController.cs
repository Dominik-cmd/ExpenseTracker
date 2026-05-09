using ExpenseTracker.Api.Models;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers
{


[Route("diagnostic")]
public sealed class DiagnosticController(OtpBankaSmsParser parser, ILogger<DiagnosticController> logger) : ApiControllerBase
{
    [HttpPost("parse-sms")]
    public ActionResult<DiagnosticParseResponse> ParseSms([FromBody] DiagnosticParseRequest request)
    {
        try
        {
            var parsed = parser.Parse(request.Text);
            return parsed is null
                ? BadRequest(new DiagnosticParseResponse(false, null, "Unable to parse SMS payload."))
                : Ok(new DiagnosticParseResponse(true, parsed, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diagnostic SMS parsing failed.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to parse SMS payload.");
        }
    }
}
}

