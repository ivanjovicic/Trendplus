-- Create analytics database on container init
-- This script will be executed by the official postgres image on first run

-- Create the analytics database
CREATE DATABASE analytics;

-- You can create any required extensions here, for example pgvector if needed later:
-- NOTE: pgvector requires DB-level install; uncomment if you add the extension to image
-- \c analytics
-- CREATE EXTENSION IF NOT EXISTS vector;
