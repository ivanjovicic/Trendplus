"""
Trendplus Image Embedding Service
FastAPI service for generating image embeddings using CLIP or SigLIP models
"""

import torch
import numpy as np
from PIL import Image
from io import BytesIO
from typing import Literal
from fastapi import FastAPI, File, UploadFile, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from transformers import (
    CLIPProcessor, 
    CLIPModel,
    AutoProcessor, 
    AutoModel
)
import logging

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# FastAPI app
app = FastAPI(
    title="Trendplus Image Embedding Service",
    description="Generate image embeddings using CLIP or SigLIP models",
    version="1.0.0"
)

# CORS configuration
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:8080", "http://localhost:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Model configuration
MODEL_TYPE: Literal["clip", "siglip"] = "siglip"  # Change to "clip" to use CLIP instead
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

logger.info(f"Using device: {DEVICE}")
logger.info(f"Selected model: {MODEL_TYPE}")

# Load models on startup
if MODEL_TYPE == "clip":
    logger.info("Loading CLIP model...")
    model = CLIPModel.from_pretrained("openai/clip-vit-base-patch32").to(DEVICE)
    processor = CLIPProcessor.from_pretrained("openai/clip-vit-base-patch32")
    EMBEDDING_DIM = 512
else:  # siglip
    logger.info("Loading SigLIP model...")
    model = AutoModel.from_pretrained("google/siglip-base-patch16-256").to(DEVICE)
    processor = AutoProcessor.from_pretrained("google/siglip-base-patch16-256")
    EMBEDDING_DIM = 768

logger.info(f"Model loaded successfully! Embedding dimension: {EMBEDDING_DIM}")

# Set model to evaluation mode
model.eval()


def process_image(image_bytes: bytes) -> torch.Tensor:
    """
    Process image bytes and return embedding tensor
    """
    try:
        # Open image
        image = Image.open(BytesIO(image_bytes))
        
        # Convert to RGB if needed
        if image.mode != "RGB":
            image = image.convert("RGB")
        
        # Process image
        inputs = processor(images=image, return_tensors="pt").to(DEVICE)
        
        # Generate embedding
        with torch.no_grad():
            if MODEL_TYPE == "clip":
                embedding = model.get_image_features(**inputs)
            else:  # siglip
                embedding = model(**inputs).image_embeds
        
        return embedding[0]
    
    except Exception as e:
        logger.error(f"Error processing image: {str(e)}")
        raise HTTPException(status_code=400, detail=f"Invalid image: {str(e)}")


@app.get("/")
async def root():
    """Health check endpoint"""
    return {
        "service": "Trendplus Image Embedding Service",
        "status": "running",
        "model": MODEL_TYPE,
        "embedding_dim": EMBEDDING_DIM,
        "device": DEVICE
    }


@app.get("/health")
async def health():
    """Detailed health check"""
    return {
        "status": "healthy",
        "model_loaded": model is not None,
        "processor_loaded": processor is not None,
        "device": DEVICE,
        "cuda_available": torch.cuda.is_available(),
        "model_type": MODEL_TYPE,
        "embedding_dimension": EMBEDDING_DIM
    }


@app.post("/embed")
async def embed_image(
    file: UploadFile = File(...),
    normalize: bool = Query(True, description="Normalize embedding vector")
):
    """
    Generate embedding for an uploaded image
    
    Args:
        file: Image file (JPEG, PNG, etc.)
        normalize: Whether to normalize the embedding vector (default: True)
    
    Returns:
        JSON with embedding vector and metadata
    """
    try:
        logger.info(f"Processing image: {file.filename} ({file.content_type})")
        
        # Validate content type
        if not file.content_type.startswith("image/"):
            raise HTTPException(
                status_code=400, 
                detail=f"Invalid content type: {file.content_type}. Must be an image."
            )
        
        # Read image bytes
        image_bytes = await file.read()
        
        # Generate embedding
        embedding = process_image(image_bytes)
        
        # Normalize if requested
        if normalize:
            embedding = embedding / embedding.norm(dim=-1, keepdim=True)
        
        # Convert to list
        embedding_list = embedding.cpu().numpy().tolist()
        
        logger.info(f"Successfully generated embedding with {len(embedding_list)} dimensions")
        
        return {
            "success": True,
            "embedding": embedding_list,
            "dimension": len(embedding_list),
            "model": MODEL_TYPE,
            "normalized": normalize,
            "filename": file.filename
        }
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error generating embedding: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error generating embedding: {str(e)}")


@app.post("/embed-batch")
async def embed_batch(files: list[UploadFile] = File(...)):
    """
    Generate embeddings for multiple images
    
    Args:
        files: List of image files
    
    Returns:
        JSON with list of embeddings
    """
    try:
        logger.info(f"Processing batch of {len(files)} images")
        
        embeddings = []
        
        for file in files:
            try:
                image_bytes = await file.read()
                embedding = process_image(image_bytes)
                
                # Normalize
                embedding = embedding / embedding.norm(dim=-1, keepdim=True)
                
                embeddings.append({
                    "filename": file.filename,
                    "embedding": embedding.cpu().numpy().tolist(),
                    "success": True
                })
            except Exception as e:
                logger.error(f"Error processing {file.filename}: {str(e)}")
                embeddings.append({
                    "filename": file.filename,
                    "error": str(e),
                    "success": False
                })
        
        successful = sum(1 for e in embeddings if e["success"])
        
        logger.info(f"Batch processing complete: {successful}/{len(files)} successful")
        
        return {
            "success": True,
            "total": len(files),
            "successful": successful,
            "failed": len(files) - successful,
            "embeddings": embeddings
        }
    
    except Exception as e:
        logger.error(f"Error in batch processing: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Batch processing error: {str(e)}")


@app.post("/similarity")
async def compute_similarity(
    file1: UploadFile = File(...),
    file2: UploadFile = File(...)
):
    """
    Compute cosine similarity between two images
    
    Args:
        file1: First image
        file2: Second image
    
    Returns:
        Similarity score (0-1, higher is more similar)
    """
    try:
        logger.info(f"Computing similarity between {file1.filename} and {file2.filename}")
        
        # Process both images
        bytes1 = await file1.read()
        bytes2 = await file2.read()
        
        emb1 = process_image(bytes1)
        emb2 = process_image(bytes2)
        
        # Normalize
        emb1 = emb1 / emb1.norm(dim=-1, keepdim=True)
        emb2 = emb2 / emb2.norm(dim=-1, keepdim=True)
        
        # Compute cosine similarity
        similarity = torch.nn.functional.cosine_similarity(emb1, emb2, dim=0)
        
        similarity_score = similarity.item()
        
        logger.info(f"Similarity score: {similarity_score:.4f}")
        
        return {
            "success": True,
            "similarity": similarity_score,
            "file1": file1.filename,
            "file2": file2.filename,
            "model": MODEL_TYPE
        }
    
    except Exception as e:
        logger.error(f"Error computing similarity: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error computing similarity: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    
    logger.info("Starting Trendplus Image Embedding Service...")
    
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=8000,
        log_level="info"
    )
