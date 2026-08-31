# NetStatAnalyzer

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0%20Windows-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Release](https://img.shields.io/badge/Release-v1.2.0-emerald.svg)](https://github.com/muriloai/NetStatAnalyzer/releases)
[![Architecture](https://img.shields.io/badge/Arch-x64-orange.svg)](#)

<br/>

**NetStatAnalyzer** é um monitor visual de conexões de rede para Windows. Ele mapeia em tempo real todas as portas e sockets ativos (TCP e UDP), identificando instantaneamente o executável, PID, caminho em disco e ícone de cada processo.

<br/>

![NetStatAnalyzer Preview](https://github.com/user-attachments/assets/aa7690da-5567-4da4-8c92-9d73b0d2b82c)

</div>

---

## Por que o NetStatAnalyzer foi criado?

Inspecionar conexões de rede no Windows pelo terminal usando o netstat tradicional costuma ser um processo lento e pouco intuitivo. Identificar qual aplicativo abriu determinada porta exige cruzar tabelas de texto com o Gerenciador de Tarefas manualmente.

O NetStatAnalyzer resolve esse problema entregando:
- Visibilidade imediata de processos e portas sem travar a interface gráfica.
- Filtro rápido para separar conexões normais do sistema de conexões desconhecidas ou suspeitas.
- Exportação prática dos dados para auditoria e análise de segurança.

---

## Recursos Principais

- **Monitoramento Contínuo:** Leitura rápida de sockets TCP e UDP com atualização assíncrona em segundo plano.
- **Identificação de Processos:** Extração automática de ícone, nome do executável, PID e caminho em disco.
- **Badges de Estado:** Cores distintas para estados como ESTABLISHED, LISTENING, TIME_WAIT, CLOSE_WAIT e SYN_SENT.
- **Painel de Métricas:** Contadores em tempo real para o total de conexões, conexões estabelecidas, portas em escuta e itens confiáveis.
- **Busca Instantânea:** Filtro global por nome do programa, PID, endereço IP local, endereço remoto ou porta.
- **Seleção Múltipla Avançada:** Suporte a intervalos contínuos com Shift + Clique e seleção descontínua com Ctrl + Clique.
- **Integração com o Windows Explorer:** Duplo clique em qualquer linha para abrir a pasta do executável diretamente.

---

## Sistema de Conexões Confiáveis

Para facilitar a triagem de tráfego de rede, o aplicativo permite cadastrar conexões conhecidas como confiáveis. A regra associa estritamente o **Nome do Processo** ao **Endereço IP**.

```mermaid
flowchart LR
    A[Conexão Detectada] --> B{Processo + IP estão na lista?}
    B -- Sim --> C[Conexão Confiável com Badge Verde]
    B -- Não --> D[Tráfego Não Mapeado]
    
    C --> E[Filtro: Ocultar Confiáveis]
    D --> E
    E --> F[Foco Imediato nas Conexões Suspeitas]
```

### Como utilizar na prática
1. Selecione uma ou mais conexões na tabela principal e clique em **Marcar como Confiável**.
2. Alterne o filtro de confiança para **Ocultar Confiáveis** para visualizar apenas o tráfego não mapeado.
3. Acesse a janela **Conexões Confiáveis** para gerenciar, pesquisar, excluir, importar ou exportar suas regras em JSON.

---

## Arquitetura do Software

O projeto foi estruturado seguindo os princípios de **Clean Architecture** e **SOLID**, mantendo uma separação clara entre regras de domínio, orquestração de casos de uso, adaptadores de sistema operacional e a interface WPF.

```mermaid
flowchart TD
    subgraph Presentation_Layer [Apresentação: WPF e MVVM]
        V[Views: MainWindow e AllowlistManagerWindow]
        VM[ViewModels: MainViewModel e AllowlistManagerViewModel]
        CMD[AsyncRelayCommand e Data Binding]
    end

    subgraph Application_Layer [Aplicação: Casos de Uso e Contratos]
        UC1[ScanConnectionsUseCase]
        UC2[ManageAllowlistUseCase]
        UC3[ExportConnectionsUseCase]
        PORTS[Portas: INetworkScanner, IProcessResolver, IAllowlistRepository, IConnectionExporter]
    end

    subgraph Domain_Layer [Domínio: Entidades e Políticas]
        E1[NetworkConnection e AllowlistRule]
        EN[Enums: NetworkProtocol e ConnectionState]
        POL[TrustEvaluationPolicy]
    end

    subgraph Infrastructure_Layer [Infraestrutura: Adaptadores e I/O]
        INF1[NetStatCliScanner]
        INF2[Win32ProcessResolver]
        INF3[JsonFileAllowlistRepository com Fallback AppData]
        INF4[FileConnectionExporter: CSV, TXT e JSON]
    end

    V --> VM
    VM --> UC1 & UC2 & UC3
    UC1 & UC2 & UC3 --> PORTS
    UC1 & UC2 & UC3 --> E1 & EN & POL
    INF1 & INF2 & INF3 & INF4 -. Implementam .-> PORTS
```

### Decisões Técnicas de Engenharia

- **Concorrência e Thread-Safety:** A coleta e o parseamento de rede rodam em threads em segundo plano via tarefas assíncronas. Os ícones extraídos do Windows são convertidos e congelados em memória com o método Freeze, garantindo consumo seguro na UI Thread sem problemas de thread affinity.
- **Cache de Recursos em Memória:** Para evitar leituras repetitivas em disco ao encontrar dezenas de conexões do mesmo aplicativo (como navegadores), o `ProcessIconCache` utiliza um dicionário concorrente em memória, reduzindo o I/O de disco em mais de noventa por cento.
- **Inversão de Dependência:** O motor de varredura e a persistência são isolados atrás de interfaces. Isso permite evoluir a leitura de sockets para chamadas nativas de P/Invoke da API do Windows sem alterar a camada de apresentação.
- **Persistência Resiliente:** Caso o aplicativo seja executado a partir de pastas protegidas sem permissão de escrita local, o repositório detecta a restrição e grava as configurações automaticamente no diretório AppData do usuário.

---

## Atalhos e Navegação

| Ação | Como Executar |
| :--- | :--- |
| Selecionar linha | Clique simples |
| Selecionar intervalo contínuo | Shift + Clique |
| Seleção múltipla alternada | Ctrl + Clique |
| Abrir pasta do executável | Duplo clique na linha ou menu de contexto |
| Menu de contexto | Clique com o botão direito na tabela |
| Recarregar conexões | Botão Recarregar ou tecla F5 |

---

## Formatos de Exportação

Você pode exportar a lista de conexões exibidas na tela a qualquer momento nos seguintes formatos:

- **JSON (.json):** Dados estruturados prontos para scripts, integrações e ferramentas de segurança.
- **CSV (.csv):** Compatível com Excel, Power BI e Google Sheets para relatórios e filtros em planilhas.
- **Texto (.txt):** Relatório formatado em colunas alinhadas com cabeçalho e resumo de métricas.

---

## Como Compilar e Executar

### Pré-requisitos
- Windows 10 (versão 19041+) ou Windows 11 (64-bit).
- SDK do .NET 8.0 instalado na máquina de desenvolvimento.

### 1. Publicação Autocontida Portátil (Recomendada)
Gera um único executável independente de cerca de 70 MB com o runtime embutido, pronto para rodar em qualquer máquina sem instalar nada:

```powershell
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./dist
```
> Executável gerado em: `./dist/NetStatAnalyzer.exe`

### 2. Publicação Dependente do Framework
Gera um executável enxuto de apenas 2 MB para computadores que já possuem o .NET 8 Desktop Runtime:

```powershell
dotnet publish NetStatAnalyzer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```
> Executável gerado em: `./publish/NetStatAnalyzer.exe`

---

## Estrutura do Projeto

```text
NetStatAnalyzer/
├── assets/
│   ├── icon.ico                           # Ícone do aplicativo para Windows
│   ├── icon.png                           # Logotipo em alta resolução
│   ├── icon-dark.ico                      # Ícone variante para temas escuros
│   └── icon-dark.png                      # Logotipo variante
├── Domain/
│   ├── Entities/
│   │   ├── AllowlistRule.cs               # Entidade de regra de confiança
│   │   └── NetworkConnection.cs           # Entidade pura de conexão de rede
│   ├── Enums/
│   │   ├── ConnectionState.cs             # Estados tipados de sockets TCP
│   │   └── NetworkProtocol.cs             # Protocolos de rede (TCP, UDP)
│   └── Policies/
│       └── TrustEvaluationPolicy.cs       # Regras de validação e normalização de IP
├── Application/
│   ├── Contracts/
│   │   ├── IAllowlistRepository.cs        # Contrato de persistência de regras
│   │   ├── IConnectionExporter.cs         # Contrato de exportação de dados
│   │   ├── INetworkScanner.cs             # Contrato de varredura de sockets
│   │   └── IProcessResolver.cs            # Contrato de resolução de processos
│   ├── DTOs/
│   │   └── RawSocketInfo.cs               # DTO de leitura bruta de sockets
│   └── UseCases/
│       ├── ExportConnectionsUseCase.cs    # Orquestração de exportação de arquivos
│       ├── ManageAllowlistUseCase.cs      # Gestão de regras e eventos
│       └── ScanConnectionsUseCase.cs      # Varredura e enriquecimento de conexões
├── Infrastructure/
│   ├── Exporting/
│   │   └── FileConnectionExporter.cs      # Gerador de relatórios CSV, TXT e JSON
│   ├── Persistence/
│   │   └── JsonFileAllowlistRepository.cs # Persistência JSON com fallback AppData
│   ├── Processes/
│   │   └── Win32ProcessResolver.cs        # Introspecção Win32 de processos do Windows
│   └── Scanning/
│       └── NetStatCliScanner.cs           # Leitura e parsing de comandos de rede
├── Presentation/
│   ├── Common/
│   │   ├── AsyncRelayCommand.cs           # Comando assíncrono para MVVM
│   │   ├── RelayCommand.cs                # Comando síncrono para MVVM
│   │   └── ViewModelBase.cs               # Base com suporte a PropertyChanged
│   ├── Converters/
│   │   └── EmptyStringToVisibilityConverter.cs # Conversores de valor XAML
│   ├── Extensions/
│   │   └── DataGridExtensions.cs          # Ajuste automático de largura de colunas
│   ├── Services/
│   │   └── ProcessIconCache.cs            # Cache thread-safe de ícones em memória
│   └── ViewModels/
│       ├── AllowlistManagerViewModel.cs   # ViewModel do gerenciador de regras
│       ├── ConnectionItemViewModel.cs     # ViewModel de linha e badges visuais
│       └── MainViewModel.cs               # ViewModel da janela principal
├── AllowlistManagerWindow.xaml            # Interface do gerenciador de regras
├── AllowlistManagerWindow.xaml.cs
├── App.xaml                               # Inicialização e estilos globais
├── App.xaml.cs
├── AssemblyInfo.cs                        # Informações de assembly
├── MainWindow.xaml                        # Interface da janela principal
├── MainWindow.xaml.cs                     # Inicialização da view e bindings
├── NetStatAnalyzer.csproj                 # Configuração de build do .NET 8
└── LICENSE                                # Licença MIT
```

---

## Licença

Este projeto é distribuído sob os termos da licença [MIT](LICENSE). Consulte o arquivo para obter todos os detalhes.
