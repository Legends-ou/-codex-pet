# Contributing

## Development loop

1. Create a branch for one focused change.
2. Run restore, build, and the complete test suite:

   ```powershell
   dotnet restore
   dotnet build PetDesktop.sln -c Release --no-restore
   dotnet test PetDesktop.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName!~RealPetContractTests'
   ```

3. Do not commit `bin/`, `obj/`, `.tools/`, or `artifacts/`.
4. For release changes, run both scripts documented in the README and attach the generated files to a GitHub Release.

## Compatibility contract

Pet package support is a core contract. Changes must retain generic Codex v1/v2 parsing:

- Read `pet.json` manifests without relying on one pet's identifier or filename.
- Support PNG and WebP sprite sheets.
- Keep bundled resource discovery and portable resource discovery working.
- Add or update focused tests for format and animation changes.

## Pull requests

Keep pull requests small, describe user-visible behavior, and include the verification
commands you ran. Do not include generated release binaries in commits.
