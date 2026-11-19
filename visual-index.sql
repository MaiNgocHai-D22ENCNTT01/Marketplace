-- Run with psql -v ON_ERROR_STOP=1 after ONNX backfill of legacy 2304-D vectors.
-- Requires pgvector >= 0.8 on the PostgreSQL 17 server. Separate DDL from busy traffic.
-- psql script: if the extension is not installed on this machine, keep text storage
-- and the Python exact-search fallback. No vectors are deleted or truncated.
SELECT EXISTS (SELECT FROM pg_available_extensions WHERE name = 'vector') AS vector_available \gset
\if :vector_available
CREATE EXTENSION IF NOT EXISTS vector;
ALTER TABLE product_images ADD COLUMN IF NOT EXISTS feature_vector_ann vector(1024)
    GENERATED ALWAYS AS (feature_vector::vector(1024)) STORED;
CREATE INDEX CONCURRENTLY IF NOT EXISTS product_images_cosine_hnsw
    ON product_images USING hnsw (feature_vector_ann vector_cosine_ops);
ANALYZE product_images;
\else
\echo 'pgvector unavailable: keeping exact-search fallback. Not suitable for million-row load tests.'
\endif
