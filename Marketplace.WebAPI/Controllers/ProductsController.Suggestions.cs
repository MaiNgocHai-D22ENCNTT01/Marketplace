using System.Text;
using Marketplace.Application.Catalog;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

public sealed partial class ProductsController
{
    private const string Accents = "aàáảãạăằắẳẵặâầấẩẫậeèéẻẽẹêềếểễệiìíỉĩịoòóỏõọôồốổỗộơờớởỡợuùúủũụưừứửữựyỳýỷỹỵđ";
    private static readonly string Source = Accents + Accents.ToUpperInvariant() + "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string Target = string.Concat(Source.Select(c => char.ToLowerInvariant(c) == 'đ'
        ? 'd' : char.ToLowerInvariant(c.ToString().Normalize(NormalizationForm.FormD)[0])));

    private static string AppSearchKey(string text) => string.Concat(text.Select(c =>
    {
        var position = Source.IndexOf(c);
        return position >= 0 ? Target[position] : char.ToLowerInvariant(c);
    }));

    [HttpGet("suggestions")]
    public async Task<ActionResult<List<ProductSummary>>> Suggestions(string? q, CancellationToken ct)
    {
        var term = q?.Trim().Normalize();
        if (term is null || term.Length < 2 || term.Length > 256) return new List<ProductSummary>();
        var key = AppSearchKey(term);
        var matches = from p in db.AvailableProducts()
                      let name = AppDbContext.Translate(p.Name.ToLower(), Source, Target)
                      let category = AppDbContext.Translate(p.Category.Name.ToLower(), Source, Target)
                      where name.Contains(key) || category.Contains(key)
                      orderby name == key descending, name.StartsWith(key) descending, name.Contains(key) descending, p.Name, p.Id
                      select p;
        return await matches.Take(6).Summaries().ToListAsync(ct);
    }
}
