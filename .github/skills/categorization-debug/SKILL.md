---
name: categorization-debug
description: 'Debug why a video was categorized incorrectly or not categorized at all in PlaylistMiner.'
---

# Debug Video Categorization

Investigate why the categorization engine produced unexpected results for a video.

## Step 1: Get Video Details

```sql
SELECT v.id, v.title, v.description, v.status
FROM videos v
WHERE v.youtube_id = '<VIDEO_ID>' OR v.title ILIKE '%<SEARCH>%';
```

## Step 2: Check Current Tags

```sql
SELECT t.name, vt.source, vt.confidence, vt.created_at
FROM video_tags vt
JOIN tags t ON t.id = vt.tag_id
WHERE vt.video_id = <VIDEO_ID>
ORDER BY vt.confidence DESC;
```

## Step 3: Check Keyword Matches

```sql
SELECT tr.keyword, tr.field, tr.weight, t.name as tag_name, tr.is_learned
FROM tag_rules tr
JOIN tags t ON t.id = tr.tag_id
WHERE LOWER('<VIDEO_TITLE>') LIKE '%' || LOWER(tr.keyword) || '%'
   OR LOWER('<VIDEO_DESCRIPTION>') LIKE '%' || LOWER(tr.keyword) || '%'
ORDER BY tr.weight DESC;
```

## Step 4: Check Why Tag Was Not Suggested

- **No keyword rules match:** Need to add rules or manually tag to trigger learning
- **Weight too low:** Rules exist but total weight < 0.7 threshold
- **TF-IDF miss:** Video description too different from corpus
- **Ollama offline:** Layer 3 skipped, no fallback

## Step 5: Fix

- Add manual tag → triggers self-learning → creates keyword rules
- Or manually add keyword rule:
  ```sql
  INSERT INTO tag_rules (tag_id, keyword, field, weight, is_learned)
  VALUES (<TAG_ID>, '<keyword>', 'both', 0.5, false);
  ```

## Step 6: Reprocess

Trigger re-categorization for the video via the API or UI.
