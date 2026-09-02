namespace BonusService;

public class ValidationErrorResponse : ErrorResponse
{
    public Dictionary<string, string> Errors { get; set; }

    public ValidationErrorResponse(string message, Dictionary<string, string> errors)
        : base(message)
    {
        Errors = errors;
    }
}
