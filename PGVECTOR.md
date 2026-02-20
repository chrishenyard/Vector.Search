# PGVector Indexing and Search

# Monitoring Index Creation Progress Query
To monitor the progress of index creation in PGVector, you can query the `pg_stat_progress_create_index` view. This view provides information about the progress of index creation operations. Here's how you can use it:
```sql
SELECT	phase,
	round(100.0 * (blocks_done / nullif(blocks_total, 0)), 2) AS percent_complete,
FROM	pg_stat_progress_create_index;
```

# Drop and create index
To drop and create an index in PGVector, you can use the following SQL commands:
```sql
DROP INDEX IF EXISTS hnsw_products_embedding;
CREATE INDEX hnsw_products_embedding ON products USING hnsw (embedding vector_cosine_ops);
```