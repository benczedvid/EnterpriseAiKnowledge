# Enterprise AI Knowledge

RAG-alapú vállalati tudásasszisztens belső SharePoint-dokumentumok visszakereséséhez, forráshivatkozással támogatott magyar válaszokkal.

## Állapot

Ez a repository az **EAK-001 – Git repository és könyvtárstruktúra létrehozása** feladat eredménye. Az alkalmazásprojektek és integrációk még nincsenek implementálva.

## Tervezett technológiai irány

- backend: ASP.NET Core Web API (.NET 8);
- frontend: Angular;
- adatbázis és vektoros keresés: PostgreSQL + pgvector;
- lokális LLM és embedding: Ollama;
- elsődleges dokumentumforrás: SharePoint dokumentumtár, csak olvasási kapcsolattal;
- prototípus-futtatás: Docker Compose.

## Könyvtárstruktúra

```text
enterprise-ai-knowledge/
├── src/
│   ├── EnterpriseAI.Api/
│   ├── EnterpriseAI.Application/
│   ├── EnterpriseAI.Domain/
│   ├── EnterpriseAI.Infrastructure/
│   └── EnterpriseAI.Web/
├── tests/
│   ├── EnterpriseAI.UnitTests/
│   └── EnterpriseAI.IntegrationTests/
├── docker/
├── docs/
├── samples/
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── EnterpriseAI.sln
├── docker-compose.yml
├── global.json
└── README.md
```

## Helyi ellenőrzés

A solution a repository-struktúra ellenőrzésére szolgál; az alkalmazásprojektek létrehozása a következő backlog-tétel része.

```bash
dotnet --version
dotnet build EnterpriseAI.sln
```

## Következő fejlesztési lépés

A backend projektek létrehozása után a módosított iterációs sorrend szerint a SharePoint read-only kapcsolat és a document discovery következik. Parser, embedding és Angular funkció csak a tényleges SharePoint-fájlok és metaadatok feltérképezése után készül.

## Biztonsági alapelvek

- Titok, kliensazonosítóhoz tartozó secret vagy jelszó nem kerül a repositoryba.
- Fejlesztésben .NET User Secrets vagy helyi secret store használandó.
- A SharePoint-integráció minimális, csak olvasási jogosultságot kap.
