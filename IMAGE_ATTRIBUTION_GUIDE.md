# ?? Image Attribution Implementation Guide

## Summary

Your image carousel currently lacks **photographer attribution**, which is required for production use with **Unsplash** (appreciated) and **Pexels** (appreciated). This guide shows you how to add proper attribution.

---

## ? What's Required

### **Unsplash** (Strongly Encouraged)
- **Not legally required**, but strongly encouraged
- Format: `Photo by [Photographer] on Unsplash`
- Include UTM parameters: `utm_source=your_app&utm_medium=referral`
- Link to photographer's profile

### **Pexels** (Appreciated)
- **Not required**, but appreciated
- Format: `Photo by [Photographer] from Pexels`
- Link to photographer's profile

---

## ??? Implementation Steps

### 1. Update Domain Models

**File**: `Domain/Model/TrendShoes/UnsplashPhoto.cs`

```csharp
public class UnsplashPhoto
{
    public string id { get; set; }
    public UnsplashUrls urls { get; set; }
    public string alt_description { get; set; }
    public UnsplashUser user { get; set; }              // ? ADD
    public UnsplashLinks links { get; set; }            // ? ADD
}

public class UnsplashUser
{
    public string name { get; set; }
    public string username { get; set; }
    public UnsplashUserLinks links { get; set; }
}

public class UnsplashUserLinks
{
    public string html { get; set; }
}

public class UnsplashLinks
{
    public string download_location { get; set; }
}
```

**Files for Pexels** (check if they exist):
- `Domain/Model/TrendShoes/PexelsPhoto.cs`
- `Domain/Model/TrendShoes/PexelsSrc.cs`
- `Domain/Model/TrendShoes/PexelsResponse.cs`

Add photographer info to Pexels models:
```csharp
public class PexelsPhoto
{
    public int id { get; set; }
    public PexelsSrc src { get; set; }
    public string photographer { get; set; }            // ? ADD
    public string photographer_url { get; set; }        // ? ADD
    public string url { get; set; }                     // ? ADD (link to photo on Pexels)
}
```

### 2. Update TrendImageDto

**File**: `Domain/Model/TrendShoes/TrendImageDto.cs`

```csharp
public record TrendImageDto(
    int Id,
    string ImageUrl,
    string Source,                          // "unsplash" | "pexels"
    string? PhotographerName = null,        // ? ADD
    string? PhotographerUrl = null,         // ? ADD
    string? SourceUrl = null                // ? ADD (link to Unsplash or Pexels)
);
```

### 3. Update UnsplashService

**File**: `Application/TrendShoes/UnsplashService.cs`

```csharp
public async Task<List<UnsplashPhoto>> SearchImages(string query, int count)
{
    var client = _httpFactory.CreateClient();
    var key = _config["Unsplash:AccessKey"];
    var appName = _config["Unsplash:AppName"] ?? "trendplus";

    var url = $"https://api.unsplash.com/search/photos?query={query}&per_page={count}&client_id={key}";
    var response = await client.GetFromJsonAsync<UnsplashResponse>(url);

    // ? ADD UTM parameters
    var photos = response!.results.Select(p => 
    {
        if (p.user?.links?.html != null)
        {
            p.user.links.html = AddUtmParameters(p.user.links.html, appName);
        }
        return p;
    }).ToList();

    return photos;
}

private string AddUtmParameters(string url, string appName)
{
    var separator = url.Contains('?') ? "&" : "?";
    return $"{url}{separator}utm_source={appName}&utm_medium=referral";
}
```

### 4. Update PexelsService

**File**: `Application/TrendShoes/PexelsService.cs`

```csharp
public async Task<List<PexelsPhoto>> Search(string query, int count)
{
    var url = $"https://api.pexels.com/v1/search?query={query}&per_page={count}";
    var res = await _http.GetFromJsonAsync<PexelsResponse>(url);
    return res?.Photos ?? new List<PexelsPhoto>();
}
```

### 5. Update API Endpoint

**File**: `Trendplus2/Endpoints/AllEndpoints.cs`

Find the `/api/trends/seasonal-images` endpoint and update it:

```csharp
app.MapGet("/api/trends/seasonal-images", async (
    [FromServices] UnsplashService unsplash,
    [FromServices] PexelsService pexels,
    ILogger<Program> logger) =>
{
    try
    {
        var query = "women platform sandals fashion";

        // Parallel API calls
        var unsplashTask = unsplash.SearchImages(query, 10);
        var pexelsTask = pexels.Search(query, 10);
        await Task.WhenAll(unsplashTask, pexelsTask);

        var images = new List<TrendImageDto>();
        var appName = "trendplus";

        // ? Map Unsplash with attribution
        images.AddRange(
            unsplashTask.Result.Select((photo, i) =>
                new TrendImageDto(
                    i + 1,
                    photo.urls.regular,
                    "unsplash",
                    photo.user?.name,
                    photo.user?.links?.html,
                    $"https://unsplash.com?utm_source={appName}&utm_medium=referral"
                ))
        );

        // ? Map Pexels with attribution
        images.AddRange(
            pexelsTask.Result.Select((photo, i) =>
                new TrendImageDto(
                    images.Count + i + 1,
                    photo.src.medium,
                    "pexels",
                    photo.photographer,
                    photo.photographer_url,
                    photo.url
                ))
        );

        // Shuffle for variety
        var shuffled = images.OrderBy(_ => Guid.NewGuid()).Take(20);
        return Results.Ok(shuffled);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Seasonal images FAILED");
        return Results.Problem(title: "Image providers failed", detail: ex.Message);
    }
});
```

### 6. Update Frontend Component

**File**: `Klijent/clientapp/src/components/trendshoes/SeasonalImageCarousel.tsx`

```typescript
type ImageItem = {
    id: number;
    imageUrl: string;
    source: string;
    photographerName?: string | null;
    photographerUrl?: string | null;
    sourceUrl?: string | null;
};

export default function SeasonalImageCarousel() {
    const [images, setImages] = useState<ImageItem[]>([]);
    // ...existing code...

    return (
        <div style={{ position: "relative", marginTop: 24 }}>
            {/* ...existing carousel buttons... */}

            <div ref={containerRef} className="carousel-strip" /* ...existing props... */>
                {images.map(img => (
                    <div key={img.id} style={{ position: "relative" }}>
                        <img
                            src={img.imageUrl}
                            alt="Trend model"
                            loading="lazy"
                            className="carousel-img"
                        />
                        {/* ? ADD Attribution overlay */}
                        {img.photographerName && (
                            <div style={{
                                position: "absolute",
                                bottom: 8,
                                left: 8,
                                background: "rgba(0,0,0,0.6)",
                                color: "white",
                                padding: "4px 8px",
                                borderRadius: 4,
                                fontSize: "0.75rem"
                            }}>
                                Photo by{" "}
                                <a
                                    href={img.photographerUrl || "#"}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    style={{ color: "white", textDecoration: "underline" }}
                                >
                                    {img.photographerName}
                                </a>
                                {" "}on{" "}
                                <a
                                    href={img.sourceUrl || "#"}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    style={{ color: "white", textDecoration: "underline" }}
                                >
                                    {img.source === "unsplash" ? "Unsplash" : "Pexels"}
                                </a>
                            </div>
                        )}
                    </div>
                ))}
            </div>

            {/* ...existing carousel buttons... */}
        </div>
    );
}
```

### 7. Add Configuration

**File**: `appsettings.json`

```json
{
  "Unsplash": {
    "AccessKey": "YOUR_KEY_HERE",
    "AppName": "trendplus"
  },
  "Pexels": {
    "ApiKey": "YOUR_KEY_HERE"
  }
}
```

---

## ?? Styling Options

### Option 1: Subtle Overlay (Recommended)
```css
.image-attribution {
    position: absolute;
    bottom: 8px;
    left: 8px;
    background: rgba(0, 0, 0, 0.6);
    color: white;
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 0.75rem;
}
```

### Option 2: Below Image
```tsx
<div>
    <img src={img.imageUrl} alt="..." />
    <p style={{ fontSize: "0.75rem", color: "#666", marginTop: 4 }}>
        Photo by <a href={...}>{img.photographerName}</a> on {img.source}
    </p>
</div>
```

### Option 3: Hover Effect
```css
.image-attribution {
    opacity: 0;
    transition: opacity 0.3s;
}

.carousel-img:hover + .image-attribution {
    opacity: 1;
}
```

---

## ? Production Checklist

- [ ] Update all domain models with photographer info
- [ ] Update `TrendImageDto` with attribution fields
- [ ] Update `UnsplashService` to add UTM parameters
- [ ] Update `PexelsService` to return full photo objects
- [ ] Update API endpoint to map attribution data
- [ ] Update frontend component to display attribution
- [ ] Test with real API keys
- [ ] Verify links work correctly
- [ ] Check mobile responsiveness
- [ ] Verify attribution is visible but not intrusive

---

## ?? References

- [Unsplash API Guidelines](https://unsplash.com/documentation#guidelines--crediting)
- [Unsplash License](https://unsplash.com/license)
- [Pexels API Documentation](https://www.pexels.com/api/documentation/)
- [Pexels License](https://www.pexels.com/license/)

---

## ?? Example Attribution

### Unsplash
```
Photo by [John Doe](https://unsplash.com/@johndoe?utm_source=trendplus&utm_medium=referral) on [Unsplash](https://unsplash.com?utm_source=trendplus&utm_medium=referral)
```

### Pexels
```
Photo by [Jane Smith](https://www.pexels.com/@janesmith) from [Pexels](https://www.pexels.com)
```

---

## ?? Quick Implementation

If you want me to implement these changes for you, I can update all the files. Just let me know!

The key changes are:
1. Add photographer data to models ?
2. Update services to return full photo objects ?
3. Update API to map attribution ?
4. Update frontend to display attribution ?

---

**Need Help?** Reply with "implement attribution" and I'll make all the changes for you!
