create extension if not exists vector;

select 'create database vector_search'
where not exists (select from pg_database WHERE datname = 'vector_search');

create table if not exists public.code_chunk (
	id UUID PRIMARY KEY,
	path VARCHAR(255),
	language VARCHAR(50),
	content TEXT,
	hash VARCHAR(64),
	embedding vector(768)
);

create index if not exists idx_code_chunk_embedding ON public.code_chunk
using hnsw (embedding vector_cosine_ops);
