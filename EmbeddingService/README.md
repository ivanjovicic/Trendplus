# Trendplus Image Embedding Service

AI-powered image embedding service for similarity search using CLIP or SigLIP models.

## 🚀 Quick Start

### Prerequisites
- Python 3.9+
- pip

### Installation

```bash
# Create virtual environment
python -m venv venv

# Activate virtual environment
# Windows:
venv\Scripts\activate
# Linux/Mac:
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt
```

### Running the Service

```bash
# Start server
python app.py

# Or with uvicorn directly:
uvicorn app:app --reload --host 0.0.0.0 --port 8000
```

The service will be available at:
- API: http://localhost:8000
- Docs: http://localhost:8000/docs
- Health: http://localhost:8000/health

## 📊 Model Comparison

### SigLIP (Default)
- **Model**: `google/siglip-base-patch16-256`
- **Embedding Dim**: 768
- **Pros**: Better performance, more recent, optimized for retrieval
- **Cons**: Larger model size

### CLIP
- **Model**: `openai/clip-vit-base-patch32`
- **Embedding Dim**: 512
- **Pros**: Smaller, faster, well-documented
- **Cons**: Slightly lower accuracy

**To switch models**, edit `app.py` line 35:
```python
MODEL_TYPE = "clip"  # or "siglip"
```

## 🔌 API Endpoints

### 1. Generate Embedding
```bash
POST /embed
Content-Type: multipart/form-data

curl -X POST "http://localhost:8000/embed" \
  -F "file=@shoe.jpg"
```

**Response:**
```json
{
  "success": true,
  "embedding": [0.123, -0.456, ...],
  "dimension": 768,
  "model": "siglip",
  "normalized": true
}
```

### 2. Batch Embeddings
```bash
POST /embed-batch
Content-Type: multipart/form-data

curl -X POST "http://localhost:8000/embed-batch" \
  -F "files=@shoe1.jpg" \
  -F "files=@shoe2.jpg"
```

### 3. Image Similarity
```bash
POST /similarity
Content-Type: multipart/form-data

curl -X POST "http://localhost:8000/similarity" \
  -F "file1=@shoe1.jpg" \
  -F "file2=@shoe2.jpg"
```

**Response:**
```json
{
  "success": true,
  "similarity": 0.87,
  "file1": "shoe1.jpg",
  "file2": "shoe2.jpg"
}
```

### 4. Health Check
```bash
GET /health

curl http://localhost:8000/health
```

## 🔗 Integration with .NET Backend

Update your `appsettings.json`:
```json
{
  "EmbeddingService": {
    "BaseUrl": "http://localhost:8000",
    "Timeout": 30
  }
}
```

The .NET backend will automatically call this service when uploading images.

## 🐳 Docker Deployment (Optional)

```dockerfile
FROM python:3.9-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py .
EXPOSE 8000

CMD ["python", "app.py"]
```

Build and run:
```bash
docker build -t trendplus-embedding .
docker run -p 8000:8000 trendplus-embedding
```

## 📈 Performance

- **CPU**: ~200ms per image (CLIP), ~300ms (SigLIP)
- **GPU**: ~50ms per image (CLIP), ~80ms (SigLIP)
- **Memory**: ~2GB (CLIP), ~3GB (SigLIP)

## 🔧 Configuration

### Environment Variables

```bash
# Model selection
MODEL_TYPE=siglip  # or "clip"

# Server config
HOST=0.0.0.0
PORT=8000

# GPU
CUDA_VISIBLE_DEVICES=0
```

### Advanced Options

Edit `app.py` to customize:
- Model checkpoint
- Image preprocessing
- Normalization
- Batch size
- Device (CPU/GPU)

## 🧪 Testing

```bash
# Test with curl
curl -X POST "http://localhost:8000/embed" \
  -F "file=@test_image.jpg" \
  -o embedding.json

# Test similarity
curl -X POST "http://localhost:8000/similarity" \
  -F "file1=@image1.jpg" \
  -F "file2=@image2.jpg"
```

## 📝 Logs

Logs are printed to stdout with format:
```
2024-01-25 12:00:00 - INFO - Model loaded successfully!
2024-01-25 12:00:05 - INFO - Processing image: shoe.jpg
```

## 🚨 Troubleshooting

### GPU not detected
```bash
# Install CUDA-enabled PyTorch
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
```

### Out of memory
- Use CPU instead: Set `CUDA_VISIBLE_DEVICES=""`
- Switch to CLIP (smaller model)
- Reduce batch size

### Slow performance
- Enable GPU
- Use smaller images (resize before upload)
- Consider model quantization

## 📚 References

- [CLIP Paper](https://arxiv.org/abs/2103.00020)
- [SigLIP Paper](https://arxiv.org/abs/2303.15343)
- [FastAPI Docs](https://fastapi.tiangolo.com/)
- [Transformers Library](https://huggingface.co/docs/transformers)

## 📄 License

MIT License - Part of Trendplus Inventory Management System
