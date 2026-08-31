# NetStatAnalyzer

![NetStatAnalyzer Preview](https://github.com/user-attachments/assets/aa7690da-5567-4da4-8c92-9d73b0d2b82c)

**NetStatAnalyzer** é uma ferramenta moderna, leve e eficiente para Windows que permite visualizar, filtrar e monitorar em tempo real todas as conexões de rede ativas no seu sistema, identificando instantaneamente os processos associados.

---

## 🚀 Recursos Principais (v1.1.0)

- **Monitoramento de Conexões em Tempo Real:** Visualize conexões TCP/UDP ativas com endereços locais, remotos, portas e estados.
- **Identificação Visual de Processos:** Extração automática de ícone, nome do executável e PID do processo associado.
- **Seleção Múltipla Avançada por Blocos:**
  - Suporte completo a seleção nativa com **Shift + Clique** (intervalos contínuos para cima e para baixo).
  - Suporte a **Ctrl + Clique** e **Ctrl + Shift + Clique** para selecionar múltiplos blocos descontínuos simultaneamente.
  - Ações em lote e cópia estruturada para todos os itens selecionados.
- **🛡️ Sistema Inteligente de Conexões Confiáveis (IP + Processo):**
  - Associação estrita entre o **IP Remoto/Local** e o **Nome do Executável/Processo**.
  - Reconhece e categoriza o tráfego legítimo do sistema para auditoria e isolamento de conexões desconhecidas ou anômalas.
  - **Destaque Visual:** Badges verdes estilizados na listagem para conexões identificadas (`🛡️ Confiável`).
  - **Filtro Rápido:** Alterne entre *Todas*, *Apenas Confiáveis* e *Ocultar Confiáveis* (ideal para auditoria e triagem de conexões suspeitas/desconhecidas).
  - **Persistência Automática:** A lista de conexões confiáveis é salva e carregada automaticamente (`allowlist.json`).
  - **Exportação & Importação JSON:** Compartilhe listas em formato JSON padronizado, incluindo metadados de versão (`1.1.0`) para interoperabilidade.
  - **Gerenciador de Conexões Confiáveis:** Janela dedicada para buscar, remover regras individuais, limpar tudo, importar e exportar.
- **Badges de Estado Coloridos:** Identificação visual rápida de conexões (ESTABLISHED, LISTENING, TIME_WAIT, CLOSE_WAIT, etc.).
- **Métricas Instantâneas:** Painel superior com contagem de conexões Totais, Estabelecidas, Em Escuta e Confiáveis.
- **Busca Global Instantânea:** Filtre em tempo real por nome do processo, PID, endereço IP, porta ou protocolo.
- **📤 Exportação Completa de Conexões Vigentes:**
  - Exporte todas as conexões encontradas/filtradas na tabela principal com 1 clique.
  - Suporte a múltiplos formatos: **JSON estruturado**, **CSV (planilha Excel)** e **Relatório TXT formatado**.
  - Inclui todos os dados: Nome do Processo, PID, Caminho Executável, Protocolo, Endereço Local, Endereço Remoto, Estado e Status de Confiança.
- **Menu de Contexto & Ações Rápidas:**
  - 🛡️ *Marcar Selecionado(s) como Confiável / Remover das Conexões Confiáveis*
  - 📤 *Exportar Conexões Exibidas...*
  - 📁 *Abrir Local do Arquivo* (no Windows Explorer ou duplo clique na linha)
  - 📋 *Copiar Detalhes da(s) Linha(s)*
  - 📋 *Copiar Endereço(s) Remoto(s) / Local(is)*
  - 📋 *Copiar PID(s)*
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
`ash
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./dist
`
*Executável gerado:* ./dist/NetStatAnalyzer.exe

### 2. Ultra Compacto (Framework-Dependent)
Apenas ~2 MB, ideal para máquinas que já possuem o .NET 8 instalado:
`ash
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
`
*Executável gerado:* ./publish/NetStatAnalyzer.exe

---

## 📄 Licença

Distribuído sob a licença [MIT](LICENSE).
