#!/usr/bin/env python3
"""
Test script for Trendplus Embedding Service
Tests all API endpoints with sample images
"""

import requests
import sys
from pathlib import Path

# Service URL
BASE_URL = "http://localhost:8000"

def test_health():
    """Test health endpoint"""
    print("🔍 Testing health endpoint...")
    try:
        response = requests.get(f"{BASE_URL}/health")
        response.raise_for_status()
        data = response.json()
        
        print(f"✅ Service is healthy")
        print(f"   Model: {data['model_type']}")
        print(f"   Embedding dimension: {data['embedding_dimension']}")
        print(f"   Device: {data['device']}")
        print()
        return True
    except Exception as e:
        print(f"❌ Health check failed: {e}")
        return False


def test_embed(image_path):
    """Test single image embedding"""
    print(f"🔍 Testing embedding for: {image_path}")
    
    if not Path(image_path).exists():
        print(f"❌ Image not found: {image_path}")
        return False
    
    try:
        with open(image_path, 'rb') as f:
            files = {'file': f}
            response = requests.post(f"{BASE_URL}/embed", files=files)
        
        response.raise_for_status()
        data = response.json()
        
        print(f"✅ Embedding generated")
        print(f"   Dimension: {data['dimension']}")
        print(f"   Model: {data['model']}")
        print(f"   First 5 values: {data['embedding'][:5]}")
        print()
        return True
    except Exception as e:
        print(f"❌ Embedding failed: {e}")
        return False


def test_similarity(image1_path, image2_path):
    """Test image similarity"""
    print(f"🔍 Testing similarity between:")
    print(f"   {image1_path}")
    print(f"   {image2_path}")
    
    if not Path(image1_path).exists() or not Path(image2_path).exists():
        print(f"❌ One or both images not found")
        return False
    
    try:
        with open(image1_path, 'rb') as f1, open(image2_path, 'rb') as f2:
            files = [
                ('file1', f1),
                ('file2', f2)
            ]
            response = requests.post(f"{BASE_URL}/similarity", files=files)
        
        response.raise_for_status()
        data = response.json()
        
        similarity = data['similarity']
        print(f"✅ Similarity computed: {similarity:.4f}")
        
        if similarity > 0.8:
            print("   → Very similar images! 🎯")
        elif similarity > 0.6:
            print("   → Moderately similar 👍")
        else:
            print("   → Different images 🤔")
        
        print()
        return True
    except Exception as e:
        print(f"❌ Similarity test failed: {e}")
        return False


def main():
    """Run all tests"""
    print("=" * 50)
    print("Trendplus Embedding Service - Test Suite")
    print("=" * 50)
    print()
    
    # Test health
    if not test_health():
        print("❌ Service is not running. Please start it first:")
        print("   python app.py")
        sys.exit(1)
    
    # Test with sample images (if available)
    test_images = [
        "test_shoe1.jpg",
        "test_shoe2.jpg",
        "../wwwroot/product-images/sample.jpg"
    ]
    
    available_images = [img for img in test_images if Path(img).exists()]
    
    if not available_images:
        print("ℹ️  No test images found. Creating a test image...")
        from PIL import Image
        import numpy as np
        
        # Create a random test image
        test_img = Image.fromarray(np.random.randint(0, 255, (256, 256, 3), dtype=np.uint8))
        test_img.save("test_image.jpg")
        available_images = ["test_image.jpg"]
    
    # Test embedding
    if available_images:
        test_embed(available_images[0])
    
    # Test similarity (if we have 2+ images)
    if len(available_images) >= 2:
        test_similarity(available_images[0], available_images[1])
    
    print("=" * 50)
    print("✅ All tests completed!")
    print("=" * 50)


if __name__ == "__main__":
    main()
