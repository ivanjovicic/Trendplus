const API = import.meta.env.VITE_API_BASE_URL;

export interface UploadImageResponse {
    success: boolean;
    fileName: string;
    imageUrl: string;
    productId?: number;
    message: string;
}

/**
 * Upload an image to the server
 * @param formData FormData containing the image file (key: "image")
 * @param productId Optional product ID to associate with the image
 * @returns Upload response with image details
 */
export async function uploadImage(
    formData: FormData,
    productId?: number
): Promise<UploadImageResponse> {
    let url = `${API}/api/upload-image`;
    
    if (productId) {
        url += `?productId=${productId}`;
    }

    const response = await fetch(url, {
        method: "POST",
        body: formData,
    });

    if (!response.ok) {
        const error = await response.json().catch(() => ({
            message: "Failed to upload image",
        }));
        throw new Error(error.message || "Failed to upload image");
    }

    return response.json();
}

/**
 * Delete product image
 * @param productId The product ID
 * @returns Success response
 */
export async function deleteProductImage(productId: number): Promise<{ success: boolean; message: string }> {
    const response = await fetch(`${API}/api/product-images/${productId}`, {
        method: "DELETE",
    });

    if (!response.ok) {
        const error = await response.json().catch(() => ({
            message: "Failed to delete image",
        }));
        throw new Error(error.message || "Failed to delete image");
    }

    return response.json();
}

/**
 * Get the full URL for a product image
 * @param fileName The image file name
 * @returns Full URL to the image
 */
export function getImageUrl(fileName: string | null | undefined): string | null {
    if (!fileName) return null;
    return `${API}/product-images/${fileName}`;
}
