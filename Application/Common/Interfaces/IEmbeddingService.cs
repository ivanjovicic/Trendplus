namespace Application.Common.Interfaces;

/// <summary>
/// Service for generating embeddings from images using AI models (CLIP, etc.)
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate a 512-dimensional embedding vector from an image file
    /// </summary>
    /// <param name="imagePath">Full path to the image file</param>
    /// <returns>512-dimensional float array representing the image</returns>
    Task<float[]> GetEmbeddingAsync(string imagePath, CancellationToken ct = default);
    
    /// <summary>
    /// Find products with similar images based on vector similarity
    /// </summary>
    /// <param name="embedding">Query embedding vector</param>
    /// <param name="threshold">Similarity threshold (0-1, default 0.8)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of similar products with similarity scores</returns>
    Task<List<SimilarProduct>> FindSimilarProductsAsync(
        float[] embedding, 
        float threshold = 0.8f, 
        int limit = 10, 
        CancellationToken ct = default);
}

public record SimilarProduct(
    int ProductId,
    string ProductName,
    string ImageFileName,
    float Similarity
);
