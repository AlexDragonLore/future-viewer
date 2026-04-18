# Parallel animation + streaming AI interpretation

## Context

Today the flow on `ReadingView` is strictly sequential: shuffle anim → **await full AI response** (stage `'loading'`) → deal anim → flip anim → navigate to `ResultView`, which then reveals the already-complete text with a local typewriter. The AI call blocks the animation for several seconds and the user stares at "Карты зовут…" with no motion, and then the text on the result page is faked — it's not really arriving from the model, just a client-side reveal of a pre-fetched blob.

The user wants two changes:
1. The card animation (shuffle → deal → flip) must kick off the moment the begin button is pressed and play continuously, while the backend request runs in parallel.
2. The AI text must actually stream from the model all the way to the UI, so characters appear as OpenAI generates them — not as a client-side fake.

## Approach

Use **one streaming endpoint** that emits an SSE-style stream with three kinds of events: `cards` (full card data + reading id), `chunk` (interpretation delta), `done`. The frontend consumes it with `fetch` + `ReadableStream` (EventSource can't carry the JWT header). Animation runs independently of the stream — card identities are only needed at flip time, so the shuffle/deal can start immediately.

## Backend changes

### 1. `IAIInterpreter` — add streaming variant
`backend/src/FutureViewer.DomainServices/Interfaces/IAIInterpreter.cs`

Add:
```csharp
IAsyncEnumerable<string> InterpretStreamAsync(
    Spread spread, string question, IReadOnlyList<ReadingCard> cards,
    CancellationToken ct = default);
```
Keep the existing non-streaming method for `GetAsync` backfill paths.

### 2. `OpenAIInterpreter.InterpretStreamAsync`
`backend/src/FutureViewer.Infrastructure/AI/OpenAIInterpreter.cs:25`

Implement with `_chat.CompleteChatStreamingAsync(messages, cancellationToken: ct)` which returns `AsyncCollectionResult<StreamingChatCompletionUpdate>`. Iterate `await foreach` and `yield return` every `update.ContentUpdate[i].Text` that is non-empty. Reuse `BuildPrompt` and the existing system prompt verbatim.

### 3. `ReadingService.CreateStreamAsync`
`backend/src/FutureViewer.DomainServices/Services/ReadingService.cs:24`

New method signature:
```csharp
public async IAsyncEnumerable<ReadingStreamEvent> CreateStreamAsync(
    CreateReadingRequest request, Guid? userId,
    [EnumeratorCancellation] CancellationToken ct = default)
```
Logic:
- Build spread + draw cards (reuse existing `_deck.DrawAsync` + `ReadingCard` projection from lines 29–40).
- Create the `Reading` entity with empty `AiInterpretation` and persist it immediately (`_repo.AddAsync`) so we have an id to return.
- `yield return new ReadingStreamEvent.Cards(Map(reading, spread))` — reuse the existing `Map` helper.
- Accumulate chunks into a `StringBuilder` while iterating `_interpreter.InterpretStreamAsync(...)`; for each chunk `yield return new ReadingStreamEvent.Chunk(delta)`.
- After the loop: set `reading.AiInterpretation = sb.ToString()`, `reading.AiModel = <model>`, save via new repo method `UpdateAsync` (or `SaveChangesAsync` if the tracked entity allows it — check `IReadingRepository`), and `yield return new ReadingStreamEvent.Done()`.

Add a small discriminated type:
```csharp
public abstract record ReadingStreamEvent {
    public sealed record Cards(ReadingResult Reading) : ReadingStreamEvent;
    public sealed record Chunk(string Delta) : ReadingStreamEvent;
    public sealed record Done : ReadingStreamEvent;
}
```
Place it next to `ReadingResult` in `FutureViewer.DomainServices/DTOs/`.

`InterpretationService` gets a pass-through `InterpretStreamAsync` method mirroring the existing `InterpretAsync`.

If `IReadingRepository` doesn't already expose an update path, add `Task UpdateAsync(Reading reading, CancellationToken ct)` and implement it in the EF repository as `_db.Readings.Update(reading); await _db.SaveChangesAsync(ct);`. Verify the current interface before adding — it may already suffice.

### 4. New endpoint `POST /api/readings/stream`
`backend/src/FutureViewer.Host/Endpoints/ReadingEndpoints.cs:14`

Add alongside the existing POST (keep the old one for compatibility with `GetAsync`/history). The handler writes newline-delimited JSON events (simpler than strict SSE and works cleanly with `fetch` + `ReadableStream`):

```csharp
group.MapPost("/stream", async (
    CreateReadingRequest request,
    IValidator<CreateReadingRequest> validator,
    ReadingService service,
    HttpContext ctx,
    CancellationToken ct) =>
{
    await validator.ValidateAndThrowAsync(request, ct);
    var userId = GetUserId(ctx.User);

    ctx.Response.Headers.ContentType = "application/x-ndjson";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    await foreach (var evt in service.CreateStreamAsync(request, userId, ct))
    {
        var payload = evt switch {
            ReadingStreamEvent.Cards c => new { type = "cards", reading = c.Reading },
            ReadingStreamEvent.Chunk ch => new { type = "chunk", delta = ch.Delta },
            ReadingStreamEvent.Done    => new { type = "done" },
            _ => throw new InvalidOperationException()
        };
        await JsonSerializer.SerializeAsync(ctx.Response.Body, (object)payload, cancellationToken: ct);
        await ctx.Response.Body.WriteAsync("\n"u8.ToArray(), ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
});
```
NDJSON avoids the SSE `data: ` / empty-line framing and is trivial to parse on the client with a line buffer.

## Frontend changes

### 5. `readingApi.createStream` — fetch + ReadableStream
`frontend/src/api/readingApi.ts:4`

Add:
```ts
type StreamHandlers = {
  onCards: (r: Reading) => void
  onChunk: (delta: string) => void
  onDone: () => void
  onError?: (e: unknown) => void
}
async createStream(spreadType, question, h: StreamHandlers, signal?: AbortSignal)
```
Use `fetch('/api/readings/stream', { method: 'POST', headers: { Authorization, Content-Type }, body, signal })`, then read `res.body!.getReader()`, decode with `TextDecoder`, split on `\n`, parse each line as JSON, and dispatch to the right handler. Reuse `httpClient.defaults.baseURL` for the URL and pull the JWT the same way `httpClient.ts` does (line 8–14).

### 6. `useReadingStore` — streaming state
`frontend/src/stores/useReadingStore.ts:22`

Add reactive state:
- `streamingText = ref('')` — grows as chunks arrive
- `streamingDone = ref(false)`
- `cardsReady = ref(false)`

Add `createStream(spreadType, question)` that calls `readingApi.createStream` with handlers that set `current.value = r; cardsReady.value = true` on cards, append to `streamingText` on chunk, set `streamingDone.value = true` on done. Returns a `{ cardsPromise, donePromise }` pair so `ReadingView` can await card data before flip. Reset all three flags in `reset()`.

### 7. `ReadingView` — parallel animation
`frontend/src/views/ReadingView.vue:49`

Rewrite `startReading`:
```ts
stage.value = 'shuffling'
playShuffle()
const { cardsPromise } = store.createStream(pendingSpread.value, pendingQuestion.value)
await new Promise(r => shuffleDeck(deckEl, () => r()))

// deal happens regardless — cards show backs via CardFlip default
stage.value = 'dealing'
// ...existing deal setup, but use placeholders.value as-is until cards land
dealCards(targets, async () => {
  await cardsPromise  // ensure real card data before flipping faces
  placeholders.value = store.current!.cards
  stage.value = 'flipping'
  // ...existing flip loop
  router.push({ name: 'result' })
})
```
Drop the `'loading'` stage entirely (and its label in the template at line 123). The headline goes straight shuffling → dealing → flipping. If `cardsPromise` rejects, transition back to `'idle'` and surface the store error.

Note: `computeSlots` only needs count + board rect, both known locally, so slot computation does not depend on backend data.

### 8. `ResultView` — live streaming display
`frontend/src/views/ResultView.vue:12`

Replace the typewriter:
- Remove `useTypewriter` import and usage.
- `interpretationSource = toRef(() => store.streamingText)` — bind directly to the reactive growing string.
- `typedHtml = computed(() => marked.parse(store.streamingText) as string)`.
- Show the blinking caret only while `!store.streamingDone`.
- `onMounted`: if `!store.current` navigate home (unchanged). No other changes; the store is already populated by the time we navigate here, and chunks continue to flow into `streamingText` regardless of which view is active.

Delete `frontend/src/composables/useTypewriter.ts` once no other view imports it (grep to confirm — it's small and only used here per exploration).

## Files to modify

Backend:
- `backend/src/FutureViewer.DomainServices/Interfaces/IAIInterpreter.cs`
- `backend/src/FutureViewer.Infrastructure/AI/OpenAIInterpreter.cs`
- `backend/src/FutureViewer.DomainServices/Services/InterpretationService.cs`
- `backend/src/FutureViewer.DomainServices/Services/ReadingService.cs`
- `backend/src/FutureViewer.DomainServices/DTOs/ReadingStreamEvent.cs` (new)
- `backend/src/FutureViewer.Host/Endpoints/ReadingEndpoints.cs`
- (possibly) `IReadingRepository` + EF impl for `UpdateAsync`

Frontend:
- `frontend/src/api/readingApi.ts`
- `frontend/src/stores/useReadingStore.ts`
- `frontend/src/views/ReadingView.vue`
- `frontend/src/views/ResultView.vue`
- `frontend/src/composables/useTypewriter.ts` (delete)

## Verification

1. `dotnet build` at `backend/` — catches streaming signature issues.
2. Run backend + `npm run dev` in `frontend/`.
3. Home page → type a question → "Начать расклад" → tap deck. Confirm:
   - Shuffle animation starts instantly, with no "Карты зовут…" pause.
   - Deal + flip play without stalling even if the network tab shows the POST still in flight.
   - On `/result`, text appears progressively (not all-at-once), synchronized with tokens streaming from OpenAI — check the network tab to see the `readings/stream` response streaming in real time.
   - Caret blinks during stream, disappears once `done` arrives.
4. Throttle network to Slow 3G in DevTools and repeat — the animation must still play to completion regardless of stream speed; flipping should gracefully wait if cards haven't landed yet.
5. Refresh on `/result` → should redirect to home (no regression in `onMounted` guard).
6. `GET /api/readings/{id}` (the non-streaming detail endpoint) must still return the final interpretation because `ReadingService.CreateStreamAsync` persists the accumulated text before emitting `done`.
