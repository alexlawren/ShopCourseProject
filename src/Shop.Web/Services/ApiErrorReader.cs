using System.Net.Http.Json;
using Shop.Web.Models.Auth;

namespace Shop.Web.Services;

public static class ApiErrorReader
{
    public static async Task<(string ErrorMessage, string? ErrorCode)> ReadAsync(
        HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsModel>(cancellationToken: cancellationToken);
            if (problem != null)
            {
                problem.Status = (int)response.StatusCode;
                return (problem.GetDisplayMessage(), problem.GetErrorCode());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A proxy may return HTML, plain text or an invalid body. Never display it.
        }
        return (ProblemDetailsModel.GetStatusMessage((int)response.StatusCode), null);
    }
}
