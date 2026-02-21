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
# Reindexing an Index
To reindex an index in PGVector, you can use the `REINDEX` command. This command rebuilds the specified index, which can help improve performance if the index has become fragmented or if there have been significant changes to the underlying data. Here's how you can use it:
```sql
REINDEX INDEX code_chunk_embedding_index;
```
# Vacuuming an Index
To vacuum an index in PGVector, you can use the `VACUUM` command. This command helps to clean up and optimize the index by removing dead tuples and reclaiming storage space. Here's how you can use it:
```sql
VACUUM ANALYZE code_chunk_embedding_index;
```