using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WenMind.Daily;

internal sealed record Quote(string Text, string Author, string Work, string Provider)
{
    public string Attribution => string.Join("  /  ",
        new[] { Author, Work.Equals(Author, StringComparison.Ordinal) ? "" : Work }
            .Where(value => value.Length > 0));
}

internal sealed record DailyQuote(DateOnly Date, Quote Quote, DateTimeOffset? RefreshedAt = null)
{
    public bool NeedsRefresh(DateTimeOffset now, int? minutes) =>
        minutes is { } interval
            ? RefreshedAt is not { } refreshed || refreshed > now || now - refreshed >= TimeSpan.FromMinutes(interval)
            : Date != DateOnly.FromDateTime(now.DateTime);
}

internal sealed class QuoteService(LocalState state) : IDisposable
{
    private const string CacheName = "daily.json";
    private const int RecentLimit = 8;
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(2);
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Queue<string> recent = new(RecentLimit);
    private long lastRequest;

    private static readonly Quote[] LocalQuotes =
    [
        new("千里之行，始于足下。", "老子", "《道德经》", "本地句库"),
        new("路漫漫其修远兮，吾将上下而求索。", "屈原", "《离骚》", "本地句库"),
        new("天行健，君子以自强不息。", "", "《周易》", "本地句库"),
        new("天生我材必有用，千金散尽还复来。", "李白", "《将进酒》", "本地句库"),
        new("竹杖芒鞋轻胜马，谁怕？一蓑烟雨任平生。", "苏轼", "《定风波》", "本地句库"),
        new("长风破浪会有时，直挂云帆济沧海。", "李白", "《行路难》", "本地句库"),
        new("不积跬步，无以至千里。", "荀子", "《劝学》", "本地句库"),
        new("学而不思则罔，思而不学则殆。", "", "《论语》", "本地句库"),
        new("莫愁前路无知己，天下谁人不识君。", "高适", "《别董大》", "本地句库"),
        new("会当凌绝顶，一览众山小。", "杜甫", "《望岳》", "本地句库"),
        new("博观而约取，厚积而薄发。", "苏轼", "《稼说送张琥》", "本地句库"),
        new("纸上得来终觉浅，绝知此事要躬行。", "陆游", "《冬夜读书示子聿》", "本地句库"),
        new("君子和而不同。", "", "《论语》", "本地句库"),
        new("山重水复疑无路，柳暗花明又一村。", "陆游", "《游山西村》", "本地句库"),
        new("如切如磋，如琢如磨。", "", "《诗经·淇奥》", "本地句库"),
        new("凡益之道，与时偕行。", "", "《周易》", "本地句库"),
        new("三人行，必有我师焉。", "", "《论语·述而》", "本地句库"),
        new("往者不可谏，来者犹可追。", "", "《论语·微子》", "本地句库"),
        new("读书破万卷，下笔如有神。", "杜甫", "《奉赠韦左丞丈二十二韵》", "本地句库"),
        new("问渠那得清如许？为有源头活水来。", "朱熹", "《观书有感·其一》", "本地句库"),
        new("海内存知己，天涯若比邻。", "王勃", "《送杜少府之任蜀州》", "本地句库"),
        new("欲穷千里目，更上一层楼。", "王之涣", "《登鹳雀楼》", "本地句库"),
        new("盛年不重来，一日难再晨。", "陶渊明", "《杂诗·其一》", "本地句库"),
        new("穷则独善其身，达则兼善天下。", "", "《孟子·尽心上》", "本地句库"),
    ];

    public DailyQuote? ReadCache(DateTimeOffset now)
    {
        DailyQuote? saved = state.Read<DailyQuote>(CacheName);
        if (saved?.Quote is not { Text.Length: > 0, Author: not null, Work: not null, Provider: not null })
            return null;

        if (saved.RefreshedAt is null && saved.Date == DateOnly.FromDateTime(now.DateTime))
        {
            saved = saved with { RefreshedAt = now };
            state.Write(CacheName, saved);
        }

        Remember(saved.Quote.Text);
        return saved;
    }

    public void Save(DailyQuote quote)
    {
        Remember(quote.Quote.Text);
        state.Write(CacheName, quote);
    }

    private void Remember(string text)
    {
        if (recent.Count == RecentLimit)
            recent.Dequeue();
        recent.Enqueue(text);
    }

    public async Task<Quote> FetchNextAsync(bool daily, CancellationToken cancellation)
    {
        string[] shown = recent.ToArray();
        Quote? online = await FetchAsync(cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        if (online is not null && !shown.Contains(online.Text))
            return online;

        Quote dailyFallback = LocalQuotes[DateOnly.FromDateTime(DateTime.Now).DayNumber % LocalQuotes.Length];
        if (daily && !shown.Contains(dailyFallback.Text))
            return dailyFallback;

        Quote[] choices = LocalQuotes.Where(quote => !shown.Contains(quote.Text)).ToArray();
        return choices[Random.Shared.Next(choices.Length)];
    }

    private async Task<Quote?> FetchAsync(CancellationToken cancellation)
    {
        try
        {
            TimeSpan delay = MinimumRequestInterval - Stopwatch.GetElapsedTime(lastRequest);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellation).ConfigureAwait(false);

            lastRequest = Stopwatch.GetTimestamp();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(client.Timeout);
            using HttpResponseMessage response = await client.GetAsync(
                "https://v1.hitokoto.cn/?c=d&c=k&encode=json&max_length=60", timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            string text = ReadString(root, "hitokoto");
            return text.Length is > 0 and <= 500
                ? new Quote(text, ReadString(root, "from_who"), ReadString(root, "from"), "一言 Hitokoto")
                : null;
        }
        catch (Exception ex) when (!cancellation.IsCancellationRequested &&
            ex is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    public void Dispose() => client.Dispose();
}
