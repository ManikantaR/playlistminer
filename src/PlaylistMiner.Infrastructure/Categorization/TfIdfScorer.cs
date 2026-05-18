namespace PlaylistMiner.Infrastructure.Categorization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

public class TfIdfScorer(
    PlaylistMinerDbContext db,
    IOptions<CategorizationOptions> options,
    IMemoryCache cache) : ITfIdfScorer
{
    private const string CacheKey = "tfidf_corpus";

    private sealed record CorpusData(
        Dictionary<string, float> Idf,
        Dictionary<int, (string Name, Dictionary<string, float> Centroid)> Tags);

    public async Task BuildCorpusAsync(CancellationToken ct = default)
    {
        // Load all manually-tagged videos
        var videoTags = await db.VideoTags
            .Where(vt => vt.Source == TagSource.Manual)
            .Include(vt => vt.Video)
            .Include(vt => vt.Tag)
            .ToListAsync(ct);

        if (videoTags.Count == 0)
        {
            cache.Remove(CacheKey);
            return;
        }

        // Group by tag: tagId -> list of documents
        var tagGroups = videoTags
            .GroupBy(vt => vt.TagId)
            .ToDictionary(
                g => g.Key,
                g => (Name: g.First().Tag.Name,
                      Docs: g.Select(vt => $"{vt.Video.Title} {vt.Video.Description}").ToList()));

        // All documents across all tags (for IDF calculation)
        var allDocuments = tagGroups.Values.SelectMany(g => g.Docs).ToList();
        int N = allDocuments.Count;

        // Tokenize all documents
        var tokenizedDocs = allDocuments.Select(doc => StopWords.Tokenize(doc).ToList()).ToList();

        // Compute document frequencies
        var df = new Dictionary<string, int>();
        foreach (var tokens in tokenizedDocs)
        {
            foreach (var token in tokens.Distinct())
            {
                df.TryGetValue(token, out var count);
                df[token] = count + 1;
            }
        }

        // Compute IDF: log((1+N)/(1+df(t))) + 1
        var idf = df.ToDictionary(
            kv => kv.Key,
            kv => (float)(Math.Log((1.0 + N) / (1.0 + kv.Value)) + 1.0));

        // Build tag centroids
        var tagCentroids = new Dictionary<int, (string Name, Dictionary<string, float> Centroid)>();
        int docOffset = 0;
        foreach (var (tagId, (tagName, docs)) in tagGroups)
        {
            var centroid = new Dictionary<string, float>();
            var tagDocs = tokenizedDocs.Skip(docOffset).Take(docs.Count).ToList();
            docOffset += docs.Count;

            foreach (var tokens in tagDocs)
            {
                if (tokens.Count == 0) continue;
                var tfidfVec = ComputeTfIdf(tokens, idf);
                foreach (var (term, weight) in tfidfVec)
                {
                    centroid.TryGetValue(term, out var sum);
                    centroid[term] = sum + weight;
                }
            }

            // Average
            int numDocs = tagDocs.Count;
            foreach (var term in centroid.Keys.ToList())
                centroid[term] /= numDocs;

            tagCentroids[tagId] = (tagName, centroid);
        }

        var corpusData = new CorpusData(idf, tagCentroids);
        cache.Set(CacheKey, corpusData);
    }

    public Task<List<TagSuggestion>> ScoreAsync(VideoContext video, CancellationToken ct = default)
    {
        if (!cache.TryGetValue(CacheKey, out CorpusData? corpus) || corpus is null)
            return Task.FromResult(new List<TagSuggestion>());

        var threshold = options.Value.TfIdfThreshold;
        var tokens = StopWords.Tokenize($"{video.Title} {video.Description}").ToList();

        if (tokens.Count == 0)
            return Task.FromResult(new List<TagSuggestion>());

        var queryVec = ComputeTfIdf(tokens, corpus.Idf);
        var results = new List<TagSuggestion>();

        foreach (var (tagId, (tagName, centroid)) in corpus.Tags)
        {
            var similarity = CosineSimilarity(queryVec, centroid);
            if (similarity >= threshold)
                results.Add(new TagSuggestion(tagId, tagName, similarity, TagSource.TfIdf));
        }

        return Task.FromResult(results);
    }

    private static Dictionary<string, float> ComputeTfIdf(List<string> tokens, Dictionary<string, float> idf)
    {
        var tf = new Dictionary<string, int>();
        foreach (var token in tokens)
        {
            tf.TryGetValue(token, out var c);
            tf[token] = c + 1;
        }

        var result = new Dictionary<string, float>();
        foreach (var (term, count) in tf)
        {
            if (!idf.TryGetValue(term, out var idfVal))
                continue;
            result[term] = (float)count / tokens.Count * idfVal;
        }
        return result;
    }

    private static float CosineSimilarity(Dictionary<string, float> a, Dictionary<string, float> b)
    {
        float dot = 0f, normA = 0f, normB = 0f;

        foreach (var (term, wa) in a)
        {
            normA += wa * wa;
            if (b.TryGetValue(term, out var wb))
                dot += wa * wb;
        }

        foreach (var wb in b.Values)
            normB += wb * wb;

        if (normA == 0f || normB == 0f) return 0f;
        return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
