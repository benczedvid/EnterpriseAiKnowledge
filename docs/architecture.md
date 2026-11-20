# Architecture baseline

```text
SharePoint Document Library (read-only)
        ↓
SharePoint connector and document discovery
        ↓
Document processing and chunking
        ↓
Embedding
        ↓
PostgreSQL + pgvector
        ↓
Retrieval
        ↓
RAG / Ollama
        ↓
ASP.NET Core API
        ↓
Angular UI
```

EAK-001 only establishes the repository boundary and folders. No runtime component is implemented yet.
