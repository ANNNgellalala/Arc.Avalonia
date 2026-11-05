# Samples

Three minimal, offline-buildable Avalonia desktop samples that consume the Arc design system through
`ProjectReference` (they are not part of `ArcUI.slnx` and are never packed):

| Project | Shows |
|---|---|
| `Arc.Avalonia.Sample.Login` | `ArcInput`, `ArcPasswordField`, `ArcCheckbox`, `ArcButton`, `ArcCard` |
| `Arc.Avalonia.Sample.Settings` | `ArcSwitch` rows inside an `ArcCard` |
| `Arc.Avalonia.Sample.Data` | `ArcSortableDataTable` with typed columns and selection |

Each app has exactly one line of Arc setup in `App.axaml`:

```xml
<arc:ArcTheme />
```

Build one of them from the repository root (the environment needs single-node MSBuild):

```powershell
pwsh -NoProfile tools/build.ps1 -Project samples/Arc.Avalonia.Sample.Login/Arc.Avalonia.Sample.Login.csproj
dotnet run --project samples/Arc.Avalonia.Sample.Login/Arc.Avalonia.Sample.Login.csproj
```

Once the packages are packed (see the repository README), the same screens work by replacing the
`ProjectReference` with a single `PackageReference`:

```xml
<PackageReference Include="Arc.Avalonia" Version="0.9.0-alpha" />
```