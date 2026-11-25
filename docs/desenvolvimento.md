# Compilação, testes e distribuição

[Voltar ao projeto](../README.md) · [Arquitetura](arquitetura.md)

Use Windows 10 (build 19041 ou posterior) ou Windows 11 em x64. Para compilar, instale o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Executar a partir do código

No PowerShell, a partir da raiz do repositório:

```powershell
dotnet run --project NetStatAnalyzer.csproj
```

## Rodar os testes

```powershell
dotnet test NetStatAnalyzer.sln
```

A [suíte xUnit](../NetStatAnalyzer.Tests) cobre regras de confiança, associação de conexões a processos, métricas, filtros e exportação. Veja as execuções no [GitHub Actions](https://github.com/muriloai/NetStatAnalyzer/actions/workflows/tests.yml).

## Gerar o executável portátil

```powershell
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

O executável `NetStatAnalyzer.exe` é gerado em `publish`. O runtime do .NET vai junto no binário. Para consultar os binários distribuídos pelo projeto, use a página de [Releases](https://github.com/muriloai/NetStatAnalyzer/releases).
