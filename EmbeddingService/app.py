"""
Trendplus Image Embedding Service
FastAPI service for generating image embeddings using CLIP or SigLIP models
"""

import asyncio
import logging
import os
from io import BytesIO
from typing import Literal

import torch
from fastapi import FastAPI, File, UploadFile, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from PIL import Image
from transformers import (
    AutoModel,
    AutoProcessor,
    CLIPModel,
    CLIPProcessor,
)


def _read_bool_env(name: str, default: bool) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default

    normalized = raw.strip().lower()
    if normalized in {"1", "true", "yes", "on"}:
        return True
    if normalized in {"0", "false", "no", "off"}:
        return False

    raise RuntimeError(f"{name} must be a boolean value.")


def _read_positive_int_env(name: str, default: int, minimum: int, maximum: int) -> int:
    raw = os.getenv(name)
    if raw is None:
        value = default
    else:
        try:
            value = int(raw)
        except ValueError as exc:
            raise RuntimeError(f"{name} must be an integer.") from exc

    if value < minimum or value > maximum:
        raise RuntimeError(f"{name} must be between {minimum} and {maximum}.")

    return value


def _read_model_type() -> Literal["clip", "siglip"]:
    raw = os.getenv("MODEL_TYPE", "siglip").strip().lower()
    if raw not in {"clip", "siglip"}:
        raise RuntimeError('MODEL_TYPE must be either "clip" or "siglip".')

    return raw  # type: ignore[return-value]


# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


SERVICE_ENABLED = _read_bool_env("EMBEDDING_SERVICE_ENABLED", False)
if not SERVICE_ENABLED:
    raise RuntimeError(
        "Embedding service is disabled. Set EMBEDDING_SERVICE_ENABLED=true to start it."
    )

MODEL_TYPE = _read_model_type()
MAX_UPLOAD_BYTES = _read_positive_int_env(
    "MAX_UPLOAD_BYTES",
    default=10 * 1024 * 1024,
    minimum=1,
    maximum=50 * 1024 * 1024,
)
MAX_BATCH_FILES = _read_positive_int_env(
    "MAX_BATCH_FILES",
    default=8,
    minimum=1,
    maximum=32,
)
EMBEDDING_TIMEOUT_SECONDS = _read_positive_int_env(
    "EMBEDDING_TIMEOUT_SECONDS",
    default=30,
    minimum=1,
    maximum=120,
)
HOST = os.getenv("HOST", "0.0.0.0")
PORT = _read_positive_int_env("PORT", default=8000, minimum=1, maximum=65535)

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

logger.info("Using device: %s", DEVICE)
logger.info("Selected model: %s", MODEL_TYPE)
logger.info("Upload limit: %s bytes", MAX_UPLOAD_BYTES)
logger.info("Batch limit: %s files", MAX_BATCH_FILES)
logger.info("Inference timeout: %s seconds", EMBEDDING_TIMEOUT_SECONDS)


# FastAPI app
app = FastAPI(
    title="Trendplus Image Embedding Service",
    description="Generate image embeddings using CLIP or SigLIP models",
    version="1.0.0",
)


# CORS configuration
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:8080", "http://localhost:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# Load models on startup
if MODEL_TYPE == "clip":
    logger.info("Loading CLIP model...")
    model = CLIPModel.from_pretrained("openai/clip-vit-base-patch32").to(DEVICE)
    processor = CLIPProcessor.from_pretrained("openai/clip-vit-base-patch32")
    EMBEDDING_DIM = 512
else:
    logger.info("Loading SigLIP model...")
    model = AutoModel.from_pretrained("google/siglip-base-patch16-256").to(DEVICE)
    processor = AutoProcessor.from_pretrained("google/siglip-base-patch16-256")
    EMBEDDING_DIM = 768

logger.info("Model loaded successfully! Embedding dimension: %s", EMBEDDING_DIM)

# Set model to evaluation mode
model.eval()


def _load_image(image_bytes: bytes) -> Image.Image:
    if not image_bytes:
        raise HTTPException(status_code=400, detail="Empty image upload.")

    if len(image_bytes) > MAX_UPLOAD_BYTES:
        raise HTTPException(
            status_code=413,
            detail=f"Image exceeds maximum upload size of {MAX_UPLOAD_BYTES} bytes.",
        )

    try:
        with Image.open(BytesIO(image_bytes)) as image:
            image.verify()

        with Image.open(BytesIO(image_bytes)) as image:
            return image.convert("RGB")
    except HTTPException:
        raise
    except Exception:
        logger.warning("Rejected invalid image upload", exc_info=True)
        raise HTTPException(status_code=400, detail="Invalid image file.")


def process_image(image_bytes: bytes) -> torch.Tensor:
    """Process image bytes and return embedding tensor."""
    try:
        image = _load_image(image_bytes)

        inputs = processor(images=image, return_tensors="pt").to(DEVICE)

        with torch.no_grad():
            if MODEL_TYPE == "clip":
                embedding = model.get_image_features(**inputs)
            else:
                embedding = model(**inputs).image_embeds

        return embedding[0]
    except HTTPException:
        raise
    except Exception:
        logger.error("Error processing image", exc_info=True)
        raise HTTPException(status_code=500, detail="Image embedding failed.")


async def _generate_embedding(image_bytes: bytes) -> torch.Tensor:
    try:
        return await asyncio.wait_for(
            asyncio.to_thread(process_image, image_bytes),
            timeout=EMBEDDING_TIMEOUT_SECONDS,
        )
    except asyncio.TimeoutError:
        logger.warning(
            "Embedding generation timed out after %s seconds",
            EMBEDDING_TIMEOUT_SECONDS,
        )
        raise HTTPException(
            status_code=504,
            detail="Embedding generation timed out.",
        )


async def _read_limited_upload(file: UploadFile) -> bytes:
    image_bytes = await file.read(MAX_UPLOAD_BYTES + 1)
    if not image_bytes:
        raise HTTPException(status_code=400, detail="Empty image upload.")
    if len(image_bytes) > MAX_UPLOAD_BYTES:
        raise HTTPException(
            status_code=413,
            detail=f"Image exceeds maximum upload size of {MAX_UPLOAD_BYTES} bytes.",
        )
    return image_bytes


@app.get("/")
async def root():
    """Health check endpoint."""
    return {
        "service": "Trendplus Image Embedding Service",
        "status": "running",
        "model": MODEL_TYPE,
        "embedding_dim": EMBEDDING_DIM,
        "device": DEVICE,
        "enabled": SERVICE_ENABLED,
    }


@app.get("/health")
async def health():
    """Detailed health check."""
    return {
        "status": "healthy",
        "model_loaded": model is not None,
        "processor_loaded": processor is not None,
        "device": DEVICE,
        "cuda_available": torch.cuda.is_available(),
        "model_type": MODEL_TYPE,
        "embedding_dimension": EMBEDDING_DIM,
        "max_upload_bytes": MAX_UPLOAD_BYTES,
        "max_batch_files": MAX_BATCH_FILES,
        "timeout_seconds": EMBEDDING_TIMEOUT_SECONDS,
    }


@app.post("/embed")
async def embed_image(
    file: UploadFile = File(...),
    normalize: bool = Query(True, description="Normalize embedding vector"),
):
    """Generate embedding for an uploaded image."""
    try:
        logger.info("Processing image: %s (%s)", file.filename, file.content_type)

        content_type = (file.content_type or "").lower()
        if not content_type.startswith("image/"):
            raise HTTPException(
                status_code=400,
                detail="Unsupported file type. Upload an image file.",
            )

        image_bytes = await _read_limited_upload(file)
        embedding = await _generate_embedding(image_bytes)

        if normalize:
            embedding = embedding / embedding.norm(dim=-1, keepdim=True)

        embedding_list = embedding.cpu().numpy().tolist()

        logger.info(
            "Successfully generated embedding with %s dimensions",
            len(embedding_list),
        )

        return {
            "success": True,
            "embedding": embedding_list,
            "dimension": len(embedding_list),
            "model": MODEL_TYPE,
            "normalized": normalize,
            "filename": file.filename,
        }
    except HTTPException:
        raise
    except Exception:
        logger.error("Error generating embedding", exc_info=True)
        raise HTTPException(status_code=500, detail="Error generating embedding.")


@app.post("/embed-batch")
async def embed_batch(files: list[UploadFile] = File(...)):
    """Generate embeddings for multiple images."""
    try:
        if not files:
            raise HTTPException(status_code=400, detail="At least one image is required.")

        if len(files) > MAX_BATCH_FILES:
            raise HTTPException(
                status_code=413,
                detail=f"Batch size exceeds maximum of {MAX_BATCH_FILES} files.",
            )

        logger.info("Processing batch of %s images", len(files))

        embeddings = []

        for file in files:
            try:
                content_type = (file.content_type or "").lower()
                if not content_type.startswith("image/"):
                    raise HTTPException(
                        status_code=400,
                        detail="Unsupported file type. Upload an image file.",
                    )

                image_bytes = await _read_limited_upload(file)
                embedding = await _generate_embedding(image_bytes)

                embedding = embedding / embedding.norm(dim=-1, keepdim=True)

                embeddings.append(
                    {
                        "filename": file.filename,
                        "embedding": embedding.cpu().numpy().tolist(),
                        "success": True,
                    }
                )
            except HTTPException as exc:
                logger.warning("Rejected %s: %s", file.filename, exc.detail)
                embeddings.append(
                    {
                        "filename": file.filename,
                        "error": exc.detail,
                        "success": False,
                    }
                )
            except Exception:
                logger.error("Error processing %s", file.filename, exc_info=True)
                embeddings.append(
                    {
                        "filename": file.filename,
                        "error": "Embedding generation failed.",
                        "success": False,
                    }
                )

        successful = sum(1 for item in embeddings if item["success"])

        logger.info("Batch processing complete: %s/%s successful", successful, len(files))

        return {
            "success": True,
            "total": len(files),
            "successful": successful,
            "failed": len(files) - successful,
            "embeddings": embeddings,
        }
    except HTTPException:
        raise
    except Exception:
        logger.error("Error in batch processing", exc_info=True)
        raise HTTPException(status_code=500, detail="Batch processing error.")


@app.post("/similarity")
async def compute_similarity(
    file1: UploadFile = File(...),
    file2: UploadFile = File(...),
):
    """Compute cosine similarity between two images."""
    try:
        logger.info(
            "Computing similarity between %s and %s",
            file1.filename,
            file2.filename,
        )

        content_type_1 = (file1.content_type or "").lower()
        content_type_2 = (file2.content_type or "").lower()
        if not content_type_1.startswith("image/") or not content_type_2.startswith("image/"):
            raise HTTPException(
                status_code=400,
                detail="Unsupported file type. Upload image files only.",
            )

        bytes1 = await _read_limited_upload(file1)
        bytes2 = await _read_limited_upload(file2)

        emb1 = await _generate_embedding(bytes1)
        emb2 = await _generate_embedding(bytes2)

        emb1 = emb1 / emb1.norm(dim=-1, keepdim=True)
        emb2 = emb2 / emb2.norm(dim=-1, keepdim=True)

        similarity = torch.nn.functional.cosine_similarity(emb1, emb2, dim=0)
        similarity_score = similarity.item()

        logger.info("Similarity score: %.4f", similarity_score)

        return {
            "success": True,
            "similarity": similarity_score,
            "file1": file1.filename,
            "file2": file2.filename,
            "model": MODEL_TYPE,
        }
    except HTTPException:
        raise
    except Exception:
        logger.error("Error computing similarity", exc_info=True)
        raise HTTPException(status_code=500, detail="Error computing similarity.")


if __name__ == "__main__":
    import uvicorn

    logger.info("Starting Trendplus Image Embedding Service...")

    uvicorn.run(
        app,
        host=HOST,
        port=PORT,
        log_level="info",
    )
