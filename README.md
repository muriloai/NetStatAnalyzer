# NetStatAnalyzer

![NetStatAnalyzer Preview](https://github.com/user-attachments/assets/368c5f97-d946-4bb3-89ec-536cd5ab050f)

**NetStatAnalyzer** é uma ferramenta moderna, leve e eficiente para Windows que permite visualizar, filtrar e monitorar em tempo real todas as conexões de rede ativas no seu sistema, identificando instantaneamente os processos associados.

---

## 🚀 Recursos Principais

- **Monitoramento de Conexões em Tempo Real:** Visualize conexões TCP/UDP ativas com endereços locais, remotos, portas e estados.
- **Identificação Visual de Processos:** Extração automática de ícone, nome do executável e PID do processo associado.
- **Badges de Estado Coloridos:** Identificação visual rápida de conexões (`ESTABLISHED`, `LISTENING`, `TIME_WAIT`, `CLOSE_WAIT`, etc.).
- **Métricas Instantâneas:** Painel superior com contagem de conexões Totais, Estabelecidas e em Escuta.
- **Busca Global Instantânea:** Filtre em tempo real por nome do processo, PID, endereço IP, porta ou protocolo.
- **Menu de Contexto & Ações Rápidas:**
  - 📁 *Abrir Local do Arquivo* (no Windows Explorer ou duplo clique na linha)
  - 📋 *Copiar Detalhes da Conexão*
  - 📋 *Copiar Endereço IP / Porta*
  - 📋 *Copiar PID*
  - 🔄 *Recarregamento Assíncrono* (sem travar a interface)

---

## 🛠️ Requisitos do Sistema

- **Sistema Operacional:** Windows 10 / Windows 11 (64-bit)
- **Runtime:** .NET 8.0 Runtime (ou utilize o build single-file executável)
- **Privilégios:** Recomendado executar como Administrador para listar detalhes de processos de sistema.

---

## 📦 Publicação e Executáveis Portáteis

Você pode gerar o executável de duas formas:

### 1. 100% Portátil (Self-Contained) — *Recomendado para distribuição*
Inclui o runtime do .NET 8 embutido e compactado. Não necessita que o usuário final instale nada:
```bash
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./dist
```
*Executável gerado:* `./dist/NetStatAnalyzer.exe`

### 2. Ultra Compacto (Framework-Dependent)
Apenas ~2 MB, ideal para máquinas que já possuem o .NET 8 instalado:
```bash
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```
*Executável gerado:* `./publish/NetStatAnalyzer.exe`

---

## 📄 Licença

Distribuído sob a licença [MIT](LICENSE).
