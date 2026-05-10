using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IDiagnosticService
{
  DiagnosticParseResponse ParseSms(string text);
}
