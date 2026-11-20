# EAK-001 repository baseline

## Cél

A fejlesztéshez szükséges Git repository, solution-fájl és kezdeti könyvtárstruktúra létrehozása anélkül, hogy a későbbi backlog-elemek implementációját előrehoznánk.

## Elfogadási ellenőrzések

- a Git repository alapértelmezett ága `main`;
- a tervezett backend-, frontend- és tesztkönyvtárak léteznek;
- a `EnterpriseAI.sln` érvényes Visual Studio solution-formátumot használ;
- a központi buildbeállítás célkeretrendszere `net8.0`;
- a repository nem tartalmaz hitelesítési adatot vagy helyi secretet;
- a strukturális ellenőrzés a `./scripts/verify-structure.sh` paranccsal futtatható;
- a CI .NET 8 SDK-val Release buildet futtat.

## Scope-határ

Az EAK-001 nem hoz létre alkalmazás- vagy tesztprojektet. Ezek a következő backlog-tételhez tartoznak. A solution ezért ebben a commitban szándékosan nem tartalmaz fordítandó projektet.
