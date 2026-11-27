# Guia de uso do NetStatAnalyzer

[Voltar ao projeto](../README.md) · [Capturas de tela](images/README.md)

## Consultar os dados

| Aba | O que aparece | Como filtrar |
| --- | --- | --- |
| Conexões de rede | Sockets TCP/UDP, endereços e portas locais e remotas, PID, processo e, quando disponível, o caminho do executável. O estado do socket é exibido para TCP. | Pesquise por processo, PID, IP ou porta. Use os filtros da aba para restringir os resultados. |
| Processos e consumo | Conexões agrupadas por processo, CPU e taxas de leitura e escrita. Os contadores de I/O incluem operações além da rede. | Use mínimos de CPU (`1%`, `5%`, `10%`, `25%`, `50%`) e de I/O (`10 KB/s`, `100 KB/s`, `1 MB/s`, `10 MB/s`). Na interface, o filtro de I/O aparece como **Banda total**. |
| Tarefas agendadas | Tarefas que a consulta local consegue listar, com estado, comando, autor e horários quando disponíveis. | Veja apenas tarefas habilitadas ou use o filtro de tarefas suspeitas ou sem identificação da Microsoft. |
| Itens de inicialização | Entradas do Registro e das pastas Inicializar, com origem, comando e indicadores sobre o arquivo associado. | Filtre pelo tipo de origem ou pelos itens sinalizados. |
| Serviços do Windows | Serviços locais (`services.msc`) com nome, nome de exibição, status (Em Execução, Parado, etc.), inicialização, PID e assinatura. Possui sub-abas: **Meus Serviços Fixados** (itens salvos para monitoramento direto) e **Pesquisar e Selecionar** (catálogo completo com destaque verde nos fixados). | Pesquise por nome ou exibição e filtre por status. Marque a caixa para fixar (salva automaticamente). Clique com botão direito no status ou em múltiplos itens para Iniciar, Parar, Pausar, Continuar e Reiniciar (com elevação UAC em lote único quando necessário). |

O botão **Atualizar dados** consulta as cinco abas. A leitura contínua usa um intervalo de dois segundos nas abas de conexões e processos, com uma coleta por vez. Pause a leitura contínua para voltar à atualização manual. A barra de status mostra o andamento da coleta, os dados da aba e um resumo da seleção.

Os filtros de tag, confiança e alertas valem para todas as abas. A busca e os demais filtros de cada aba valem para a tabela ativa. O tema claro ou escuro é salvo nas preferências locais.

## Tags e confiança

1. Selecione um ou mais registros. Clique com o botão direito ou abra **Mais ações** para adicionar uma tag. Você pode digitar uma nova tag, reutilizar uma já salva ou deixar o campo vazio para remover a tag da seleção.
2. Use **Marcar como confiável** para registrar itens conhecidos. Nas conexões, a regra combina o nome do processo com o endereço remoto ou, em portas de escuta, com o endereço local. Quando a regra inclui um hash SHA-256, o executável também precisa corresponder a ele.
3. Use o filtro de confiança para ocultar itens já revisados. **Não verificado** indica que o item ainda está sem uma classificação de confiança.

A janela **Conexões confiáveis** permite pesquisar, excluir, importar e exportar as regras da lista. O aplicativo guarda regras, tags, confiança e preferências em `allowlist.json`, ao lado do executável quando a pasta permite gravar. A alternativa é `%APPDATA%\NetStatAnalyzer\allowlist.json`.

## Exportar os resultados

Em **Mais ações**, exporte os itens exibidos após aplicar os filtros. O JSON registra a versão do aplicativo, o tipo de dado, os horários da coleta e da exportação e a quantidade de itens. Dependendo da aba, o arquivo pode conter IPs, caminhos locais, nomes de processos e comandos. Revise esses campos antes de compartilhar o arquivo.

## Atalhos do Windows

| Ferramenta | O que você pode fazer nela |
| --- | --- |
| Monitor de Recursos (`resmon.exe`) | Consultar atividade de rede, CPU e disco por processo. |
| Agendador de Tarefas (`taskschd.msc`) | Conferir gatilhos e ações de uma tarefa. |
| Aplicativos de Inicialização (`ms-settings:startupapps`) | Gerenciar os aplicativos listados nas Configurações do Windows. |
| Gerenciador de Tarefas (`taskmgr.exe`) | Conferir processos e consumo de recursos. |
| Pasta Inicializar (`shell:startup`) | Inspecionar arquivos e atalhos que rodam ao entrar na sessão. |
| Editor do Registro (`regedit.exe`) | Conferir a chave de origem de um item. |
| Serviços do Windows (`services.msc`) | Gerenciar, configurar e inspecionar serviços do sistema. |
