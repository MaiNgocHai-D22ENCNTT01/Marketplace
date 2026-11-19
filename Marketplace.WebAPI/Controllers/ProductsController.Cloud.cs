using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Marketplace.WebAPI.Controllers;

public sealed partial class ProductsController
{
    private sealed record CloudFeatures(double[] Embedding, string Label, double Probability);
    private sealed class CloudCandidate
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = "";
        public string Name { get; set; } = "";
        public double Distance { get; set; }
    }

    private async Task<Prediction> SearchCloudCatalog(CloudFeatures? features, CancellationToken ct)
    {
        if (features?.Embedding is not { Length: 1024 } vector || !vector.All(double.IsFinite) ||
            !double.IsFinite(features.Probability) || vector.Sum(v => v * v) < 1e-12)
            throw new JsonException("AI returned an invalid embedding.");
        // Neon transaction pooler: SET LOCAL and query must use the same transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = strict_order", ct);
        var rows = await db.Database.SqlQueryRaw<CloudCandidate>("""
            SELECT p.id AS "Id", c.name AS "Category", p.name AS "Name",
                   i.feature_vector_ann <=> CAST(@query AS vector) AS "Distance"
            FROM products p JOIN categories c ON c.id=p.category_id
            JOIN shops s ON s.id=p.shop_id JOIN product_images i ON i.product_id=p.id
            WHERE p.status='ACTIVE' AND c.status='ACTIVE' AND s.status='ACTIVE'
              AND i.feature_vector_ann IS NOT NULL AND i.image_url LIKE '/images/%'
            ORDER BY i.feature_vector_ann <=> CAST(@query AS vector)
            LIMIT 64
            """, new NpgsqlParameter("query", JsonSerializer.Serialize(vector))).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        rows = rows.DistinctBy(r => r.Id).ToList();
        if (rows.Count == 0) throw new HttpRequestException("No indexed catalog images available.");
        double Score(CloudCandidate r) => Math.Clamp(1 - r.Distance, -1, 1);
        var votes = rows.Take(5).GroupBy(r => r.Category)
            .Select(g => new { Category = g.Key, Weight = g.Sum(r => Math.Exp((Score(r) - Score(rows[0])) / .05)) })
            .OrderByDescending(g => g.Weight).ToList();
        string category = votes[0].Category;
        double confidence = votes[0].Weight / votes.Sum(v => v.Weight);
        if (Score(rows[0]) < .75)
        {
            string[] terms = features.Label switch
            {
                "dumbbell" => ["tạ tay", "tạ đơn"], "barbell" => ["tạ đòn"],
                "backpack" => ["balo", "ba lô"], "mouse" => ["chuột"],
                "water bottle" => ["bình nước"], "coffee mug" => ["ly", "cốc"],
                "binder" => ["bìa còng"], "jersey" => ["áo thể thao", "áo thun"], _ => []
            };
            var sameType = features.Probability >= .25
                ? rows.Where(r => terms.Any(t => (" " + r.Name.ToLowerInvariant() + " ").Contains(" " + t + " "))).ToList()
                : [];
            if (sameType.Count > 0)
            {
                rows = sameType;
                category = rows[0].Category;
                confidence = features.Probability;
            }
            else rows = rows.Where(r => r.Category == category).ToList();
        }
        return new Prediction(category, confidence, rows.Take(12).Select(r => new Hit(r.Id, Score(r))).ToList());
    }
}
