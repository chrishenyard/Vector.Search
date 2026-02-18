CREATE EXTENSION IF NOT EXISTS vector;

SELECT 'CREATE DATABASE vector_search'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'vector_search')\gexec
