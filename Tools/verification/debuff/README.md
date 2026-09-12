# Debuff checks

Run from the repository root:

```powershell
dotnet run --project tools/verification/debuff/DebuffChecks.csproj
```

Requires the locally installed .NET 9 SDK. No test packages are downloaded; NuGet.Config clears external sources. The project links the actual game HP, damage, debuff, binding and parser source files. Unity serialization attributes and the unrelated legacy Mathf rounding symbol are the only Unity stubs.

These checks cover fixed-point precision/range, overkill, ordinary damage coefficients, fractional/minimum burn damage, last-tick remainder, refresh starvation, expiration, source sharing, death cleanup, isolated targets and CSV definition/binding validation. They do **not** execute Unity MonoBehaviours, DI scopes, prefab serialization, projectile flight, UI animation or Google Sheets networking.

See `docs/technical/debuff-sheet-guide.md` for configuration and Unity verification steps.
