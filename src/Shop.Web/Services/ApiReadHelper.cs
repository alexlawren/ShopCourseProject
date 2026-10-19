using System.Net;
using System.Net.Http.Json;

namespace Shop.Web.Services;

public static class ApiReadHelper
{
    public static async Task<T?> GetAsync<T>(HttpClient client, string url,
        CancellationToken cancellationToken = default, bool allowNotFound = false)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                return default;
            if (!response.IsSuccessStatusCode)
            {
                var error = await ApiErrorReader.ReadAsync(response, cancellationToken);
                throw new HttpRequestException(error.ErrorMessage, null, response.StatusCode);
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("Сервис вернул некорректный ответ. Повторите попытку.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null)
        {
            throw new HttpRequestException("Не удалось получить данные. Проверьте соединение и повторите попытку.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new HttpRequestException("Сервис вернул некорректный ответ. Повторите попытку.", ex);
        }
    }
}
