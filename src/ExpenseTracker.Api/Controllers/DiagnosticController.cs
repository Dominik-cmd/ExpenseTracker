using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/diagnostic")]
public sealed class DiagnosticController(IDiagnosticService diagnosticService) : ApiControllerBase
{
  [HttpPost("parse-sms")]
  public ActionResult<DiagnosticParseResponse> ParseSms(
    [FromBody] DiagnosticParseRequest request)
  {
    var result = diagnosticService.ParseSms(request.Text);
    if (!result.Success)
    {
      return BadRequest(result);
    }
    return Ok(result);
  }
}
