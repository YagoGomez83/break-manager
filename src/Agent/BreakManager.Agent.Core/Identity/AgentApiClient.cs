using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BreakManager.Contracts.Agent;

namespace BreakManager.Agent.Core.Identity;

/// <summary>Resultado de una llamada a la API: el valor, o el código HTTP con el error del servidor.</summary>
#pragma warning disable CA1000 // Fábricas estáticas en tipo genérico: parte de la API, como Result<T> en Domain.
public sealed record AgentApiResult<T>(bool IsSuccess, T? Value, int StatusCode, AgentError? Error)
{
    public static AgentApiResult<T> Ok(T value) => new(true, value, 200, null);

    public static AgentApiResult<T> Fail(int statusCode, AgentError error) => new(false, default, statusCode, error);
}
#pragma warning restore CA1000

/// <summary>API del servidor para el agente (03-arquitectura.md §5).</summary>
public interface IAgentApi
{
    Task<AgentApiResult<EnrollResponse>> EnrollAsync(Uri server, EnrollRequest request, CancellationToken cancellationToken);

    Task<AgentApiResult<LoginResponse>> StartLoginAsync(DeviceCredential credential, StartLoginRequest request, CancellationToken cancellationToken);

    Task<AgentApiResult<LoginResponse>> CompleteLoginAsync(DeviceCredential credential, CompleteLoginRequest request, CancellationToken cancellationToken);

    Task<AgentApiResult<bool>> LogoutAsync(DeviceCredential credential, string sessionToken, CancellationToken cancellationToken);
}

/// <summary>
/// Cliente HTTP de la API del agente. Solo HTTPS, y nunca se desactiva la validación del certificado.
/// </summary>
public sealed class AgentApiClient(HttpClient http) : IAgentApi
{
    private static readonly AgentError ConnectionError =
        new("Agent.ConnectionError", "No se pudo conectar con el servidor. Revisá la red y volvé a intentar.");

    private static readonly AgentError UnexpectedResponse =
        new("Agent.UnexpectedResponse", "El servidor respondió algo inesperado. Avisale al administrador.");

    public Task<AgentApiResult<EnrollResponse>> EnrollAsync(Uri server, EnrollRequest request, CancellationToken cancellationToken) =>
        SendAsync<EnrollResponse>(Build(HttpMethod.Post, server, AgentApiRoutes.Enrollment, request), cancellationToken);

    public Task<AgentApiResult<LoginResponse>> StartLoginAsync(
        DeviceCredential credential,
        StartLoginRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<LoginResponse>(Authorized(credential, Build(HttpMethod.Post, credential?.ServerUrl, AgentApiRoutes.Sessions, request)), cancellationToken);

    public Task<AgentApiResult<LoginResponse>> CompleteLoginAsync(
        DeviceCredential credential,
        CompleteLoginRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<LoginResponse>(
            Authorized(credential, Build(HttpMethod.Post, credential?.ServerUrl, AgentApiRoutes.SessionCompletion, request)),
            cancellationToken);

    public async Task<AgentApiResult<bool>> LogoutAsync(DeviceCredential credential, string sessionToken, CancellationToken cancellationToken)
    {
        var message = Authorized(credential, Build(HttpMethod.Delete, credential?.ServerUrl, AgentApiRoutes.CurrentSession, body: null));
        message.Headers.Add(AgentHeaders.SessionToken, sessionToken);
        var result = await SendAsync<object>(message, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? AgentApiResult<bool>.Ok(true)
            : AgentApiResult<bool>.Fail(result.StatusCode, result.Error!);
    }

    private static HttpRequestMessage Build(HttpMethod method, Uri? server, string path, object? body)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (server.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("El servidor tiene que usar HTTPS.", nameof(server));
        }

        var message = new HttpRequestMessage(method, new Uri(server, path));
        if (body is not null)
        {
            message.Content = JsonContent.Create(body, body.GetType());
        }

        return message;
    }

    private static HttpRequestMessage Authorized(DeviceCredential? credential, HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(credential);
        message.Headers.Authorization = new AuthenticationHeaderValue(AgentHeaders.DeviceScheme, credential.DeviceToken);
        return message;
    }

    private async Task<AgentApiResult<T>> SendAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using (message)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return AgentApiResult<T>.Fail(0, ConnectionError);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AgentApiResult<T>.Fail(0, ConnectionError);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                try
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var value = response.Content.Headers.ContentLength is 0 || typeof(T) == typeof(object)
                            ? default
                            : await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
                        return new AgentApiResult<T>(true, value, status, null);
                    }

                    var error = await response.Content.ReadFromJsonAsync<AgentError>(cancellationToken).ConfigureAwait(false);
                    return AgentApiResult<T>.Fail(status, error ?? UnexpectedResponse);
                }
                catch (JsonException)
                {
                    return AgentApiResult<T>.Fail(status, UnexpectedResponse);
                }
                catch (NotSupportedException)
                {
                    return AgentApiResult<T>.Fail(status, UnexpectedResponse);
                }
            }
        }
    }
}
