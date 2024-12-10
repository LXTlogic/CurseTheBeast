using CurseTheBeast.Api.Curseforge;
using CurseTheBeast.Services.Model;

namespace CurseTheBeast.Services;


public class CurseforgeService
{
    private static readonly CurseforgeApiClient Api = new CurseforgeApiClient();

    public static async Task<IEnumerable<FTBFileEntry>> GetFilesWithIncorrectMetadata(IEnumerable<FTBFileEntry> allFiles, CancellationToken ct = default)
    {
        return await Focused.StatusAsync($"检查 Curseforge FileID", async ctx =>
        {
            var dict = allFiles.Where(file => file.Curseforge != null)
                .ToDictionary(f => f.Curseforge!.FileId);
            if (dict.Count == 0)
                return [];

            var count = dict.Count;

            var progressed = 0;
            foreach (var batch in dict.Keys.Chunk(50).ToArray())
            {
                ctx.Status = Focused.Text($"检查 Curseforge FileID - {progressed}/{count}");

                var rsp = await Api.GetFilesAsync(batch, ct);
                foreach (var rspFile in rsp.DistinctBy(f => f.id))
                {
                    if (!dict.TryGetValue(rspFile.id, out var file))
                        continue;

                    if (string.IsNullOrWhiteSpace(file.Sha1) || file.Sha1 != rspFile.hashes.Where(h => h.algo == 1).FirstOrDefault()?.value)
                        continue;

                    dict.Remove(rspFile.id);
                }

                progressed += rsp.Length;
            }

            return dict.Values as IEnumerable<FTBFileEntry>;
        });
    }
    public static async Task FetchModInfo(IEnumerable<FTBFileEntry> modFiles, CancellationToken ct = default)
    {
        await Focused.StatusAsync($"获取 Curseforge 模组信息", async ctx =>
        {
            var fileDict = modFiles.ToDictionary(f => f.Sha1!);
            if (fileDict.Count == 0) 
                return;
            var result = await Api.MatchFilesAsync(fileDict.Values.Select(f => f.CFMurmur), ct);
            foreach (var matchedFile in result.exactMatches)
            {
                var sha1 = matchedFile.file.hashes.FirstOrDefault(h => h.algo == 1)?.value;
                if (sha1 != null && fileDict.TryGetValue(sha1, out var file))
                {
                    file.WithCurseforgeInfo(matchedFile.file.modId, matchedFile.file.id);
                }
            }
        });
    }
}
