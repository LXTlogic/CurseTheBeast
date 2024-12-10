using CurseTheBeast.Api.Curseforge.Model;
using CurseTheBeast.Services;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace CurseTheBeast.Api.Curseforge;


public class CurseforgeApiClient : BaseApiClient
{
    protected override void OnConfigureHttpClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.curseforge.com/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", HttpConfigService.CurseforgeKey);
    }

    public async ValueTask<ModFile[]> GetFilesAsync(IEnumerable<long> fileIds, CancellationToken ct)
    {
        return await PostJsonAsync<ModFile[]>(new Uri($"v1/mods/files", UriKind.Relative), new JsonObject
        {
            ["fileIds"] = new JsonArray(fileIds.Select(id => JsonValue.Create(id)).ToArray()),
        }, Contexts.ModFileArrayContext.Default.GenericRspModFileArray, ct);
    }

    public async ValueTask<MatchResult> MatchFilesAsync(IEnumerable<long> fingerprints, CancellationToken ct)
    {
        return await PostJsonAsync<MatchResult>(new Uri($"v1/fingerprints/432", UriKind.Relative), new JsonObject
        {
            ["fingerprints"] = new JsonArray(fingerprints.Select(f => JsonValue.Create(f)).ToArray()),
        }, Contexts.MatchResultContext.Default.GenericRspMatchResult, ct);
    }

    protected async override Task<TRsp> CallJsonAsync<TRsp>(HttpMethod method, Uri uri, Func<HttpContent>? contentProvider, JsonTypeInfo? type, CancellationToken ct)
    {
        return (await base.CallJsonAsync<GenericRsp<TRsp>>(method, uri, contentProvider, type, ct)).data;
    }
}
