# Prompt 04: Categorization Engine

## Context
PlaylistMiner auto-categorizes videos using a 3-layer pipeline: keyword matching → TF-IDF scoring → optional Ollama fallback. Tags are always suggested, never auto-applied. The engine self-learns from user tagging decisions. TDD approach with xUnit.

## Prompt 04a: Keyword Matcher (TDD)

```
TDD — write ALL tests first, run them (Red), then implement (Green):

KeywordMatcherTests.cs:
- Test_Match_FindsExactKeywordInTitle
- Test_Match_FindsSubstringInDescription
- Test_Match_IsCaseInsensitive
- Test_Match_ReturnsWeightedScores
- Test_Match_AggregatesMultipleRuleWeights
- Test_Match_FieldFilter_TitleOnly_IgnoresDescription
- Test_Match_FieldFilter_DescriptionOnly_IgnoresTitle
- Test_Match_NoRules_ReturnsEmpty
- Test_Match_BelowThreshold_ExcludesTag
- Test_Match_AboveThreshold_IncludesTag
- Test_Match_MultipleTagsCanMatch

Then implement in PlaylistMiner.Core/Categorization/:

IKeywordMatcher interface:
- Task<List<TagSuggestion>> MatchAsync(VideoContext video, CancellationToken ct)

KeywordMatcher implementation:
- Load active tag rules from ITagRuleRepository
- For each rule, check if keyword appears in the relevant field (title/description/both)
- Aggregate weights per tag
- Return TagSuggestion(TagId, TagName, Score, Source=RuleBased) for tags above threshold

TagSuggestion record: TagId, TagName, Confidence (float), Source (enum)
VideoContext record: Title, Description (pre-processed: lowercased, trimmed)

Threshold is configurable via IOptions<CategorizationOptions> (default: 0.7)
```

## Prompt 04b: TF-IDF Scorer (TDD)

```
TDD — tests first:

TfIdfScorerTests.cs:
- Test_Score_EmptyCorpus_ReturnsEmpty
- Test_Score_SingleDocument_ComputesSimilarity
- Test_Score_MultipleDocuments_RanksCorrectly
- Test_Score_NewVideoSimilarToTag_ReturnsHighConfidence
- Test_Score_UnrelatedVideo_ReturnsLowConfidence
- Test_BuildCorpus_GroupsDocumentsByTag
- Test_BuildCorpus_UsesManuallyTaggedVideosOnly

Then implement in PlaylistMiner.Core/Categorization/:

ITfIdfScorer interface:
- Task BuildCorpusAsync(CancellationToken ct)
- Task<List<TagSuggestion>> ScoreAsync(VideoContext video, CancellationToken ct)

TfIdfScorer implementation:
- Build TF-IDF corpus from descriptions of all manually-tagged videos, grouped by tag
- Compute centroid vector per tag
- For a new video, compute cosine similarity against each tag centroid
- Return TagSuggestion(TagId, TagName, Similarity, Source=TfIdf) for tags above threshold

Use ML.NET's text featurization or implement lightweight TF-IDF:
- Tokenize: split on whitespace/punctuation, lowercase, remove stop words
- TF: term frequency in document
- IDF: log(total docs / docs containing term)
- Cosine similarity between vectors

Corpus is built on startup and rebuilt when manual tags change (use IMemoryCache with manual invalidation).
Threshold configurable via IOptions<CategorizationOptions> (default: 0.5)
```

## Prompt 04c: Ollama Integration (TDD)

```
TDD — tests first:

OllamaCategorizerTests.cs:
- Test_Categorize_SendsCorrectPrompt
- Test_Categorize_ParsesResponse_ExtractsTags
- Test_Categorize_OllamaUnavailable_ReturnsEmpty (graceful degradation)
- Test_Categorize_InvalidResponse_ReturnsEmpty
- Test_IsAvailable_ReturnsTrueWhenReachable
- Test_IsAvailable_ReturnsFalseWhenUnreachable

Then implement:

IOllamaCategorizer interface:
- Task<List<TagSuggestion>> CategorizeAsync(VideoContext video, IEnumerable<string> availableTags, CancellationToken ct)
- Task<bool> IsAvailableAsync(CancellationToken ct)

OllamaCategorizer implementation:
- HTTP client to Ollama API (configurable base URL, default http://pm-ollama:11434)
- Model: mistral (configurable)
- Prompt template:
  "Given a YouTube video with the following details:
   Title: {title}
   Description: {description}
   
   Available tags: {comma-separated tag list}
   
   Select the most relevant tags for this video. Return ONLY a JSON array of objects with 'tag' and 'confidence' (0-1) fields. Example: [{"tag": "React", "confidence": 0.9}]"
- Parse JSON response, map to TagSuggestion(Source=Ollama)
- Timeout: 30 seconds
- If Ollama is unreachable or returns invalid JSON, return empty list (never throw)
```

## Prompt 04d: Categorization Pipeline (TDD)

```
TDD — tests first:

CategorizationPipelineTests.cs:
- Test_Pipeline_RunsKeywordMatcherFirst
- Test_Pipeline_RunsTfIdfSecond
- Test_Pipeline_RunsOllamaOnlyIfNoSuggestions
- Test_Pipeline_MergesSuggestions_KeepsHighestConfidence
- Test_Pipeline_DeduplicatesTags
- Test_Pipeline_SavesSuggestionsToDatabase
- Test_Pipeline_SkipsAlreadyTaggedVideos

Then implement:

ICategorizationPipeline interface:
- Task<List<TagSuggestion>> CategorizeAsync(int videoId, CancellationToken ct)
- Task CategorizeNewVideosAsync(CancellationToken ct) // batch process

CategorizationPipeline implementation:
- Layer 1: Run KeywordMatcher
- Layer 2: Run TfIdfScorer
- Merge results, keep highest confidence per tag
- If zero suggestions: Layer 3: Run OllamaCategorizer (if available)
- Save all suggestions as VideoTag records with Source and Confidence
- Do NOT auto-apply — all saved as pending suggestions
```

## Prompt 04e: Self-Learning Service (TDD)

```
TDD — tests first:

SelfLearningServiceTests.cs:
- Test_OnTagAccepted_ExtractsKeywordsFromTitle
- Test_OnTagAccepted_CreatesNewLearnedRules
- Test_OnTagAccepted_IncrementsExistingRuleWeight
- Test_OnTagAccepted_CapsWeightAt1
- Test_OnTagRejected_DecrementsRuleWeights
- Test_OnTagRejected_RemovesRulesAtZeroWeight
- Test_OnTagAccepted_InvalidatesTfIdfCorpus

Then implement:

ISelfLearningService interface:
- Task OnTagAcceptedAsync(int videoId, int tagId, CancellationToken ct)
- Task OnTagRejectedAsync(int videoId, int tagId, CancellationToken ct)

SelfLearningService implementation:
- On accept: extract significant words from video title/description (exclude stop words, < 3 chars)
- For each word: if no existing rule for this tag+keyword, create with weight 0.3, is_learned=true
- For each word: if existing learned rule, increment weight by 0.1 (cap 1.0)
- On reject: find matching learned rules, decrement weight by 0.1, delete if weight <= 0
- After any change: invalidate TF-IDF corpus cache
```

## Verification
- All unit tests pass following Red-Green-Refactor
- Categorization pipeline processes a sample video and produces suggestions
- Self-learning creates new rules when tags are accepted
- Ollama fallback degrades gracefully when container is not running
