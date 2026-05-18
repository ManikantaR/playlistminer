---
name: categorization-debug
description: Debug why a PlaylistMiner video was categorized incorrectly or not categorized, by tracing through all 3 pipeline layers.
---

# Debug Video Categorization

Investigate unexpected categorization results for a video.

## Step 1: Get Video Details

Find the video in the database and check its current tags, then check which keyword rules match its title/description.

## Step 2: Trace the Pipeline

1. **Layer 1 (Keywords):** Check `tag_rules` table for keyword matches against the video's title and description. Sum weights per tag — if total < 0.7 threshold, no suggestion from this layer.

2. **Layer 2 (TF-IDF):** Check if the TF-IDF corpus has been built (requires manually-tagged videos). If corpus is empty, this layer produces nothing.

3. **Layer 3 (Ollama):** Only runs if Layers 1+2 produce zero suggestions AND Ollama container is running.

## Step 3: Common Issues

- **No keyword rules match:** Add manual tag → triggers self-learning → creates rules
- **Weight too low:** Rules exist but total weight < threshold (0.7 default)
- **TF-IDF corpus empty:** Need more manually-tagged videos to build meaningful vectors
- **Ollama offline:** Check if pm-ollama container is running

## Step 4: Fix

- Tag manually (preferred — trains the engine)
- Or add keyword rule directly in Tags management UI
- Or lower categorization thresholds in Settings
