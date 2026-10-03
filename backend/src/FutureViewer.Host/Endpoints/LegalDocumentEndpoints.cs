using FutureViewer.Infrastructure.Compliance;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Host.Endpoints;

public static class LegalDocumentEndpoints
{
    public static IEndpointRouteBuilder MapLegalDocuments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/legal-documents", async (AppDbContext db, HttpContext context, CancellationToken ct) =>
        {
            var active = await db.LegalDocuments.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
            var documents = PublishedLegalDocuments.All
                .Select(content => (Content: content, Record: active.SingleOrDefault(
                    record => record.DocumentType == content.Type && record.Version == content.Version
                              && record.ContentHash == content.ContentHash)))
                .Where(entry => entry.Record is not null)
                .Select(entry => entry.Content.ToResponse(entry.Record!.EffectiveAt));
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { documents });
        }).WithTags("Public");
        return app;
    }
}
