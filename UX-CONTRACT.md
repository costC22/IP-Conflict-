# IPConflictMonitor UX Contract

## Product context

- Version: 3.4.0.
- Audience: técnicos de campo em notebooks Windows.
- Primary jobs: analisar a rede local, interpretar provas de conflito IPv4, ajustar o perfil ativo e manter a ferramenta atualizada.
- Target market: distribuição em português do Brasil.
- Active locale: `pt-BR`, preservando termos técnicos padronizados.
- Accessibility target: controles nativos de teclado e foco, contraste equivalente a WCAG 2.2 AA e escala DPI do Windows.
- Product posture: console técnico dedicado, com identidade de appliance e sem decoração circular ornamental.

## Business-context sources

| Domain / scope | Authoritative source | Source type | Reviewed date |
|---|---|---|---|
| Detecção e estados | `README.md` | Contrato do produto | 2026-09-15 |
| Atualização e recuperação | `README.md` | Contrato do produto | 2026-09-15 |
| Configuração segura | `config/config.json` | Configuração distribuída | 2026-09-15 |
| Persistência do perfil | `ConfigurationStore` | Contrato de execução | 2026-09-15 |

Não existem permissões, cobrança, exclusão de dados pessoais ou texto regulatório neste fluxo.

## Visual contract

- Project design: `DESIGN.md`.
- Token ownership: código de execução canônico; documentação espelha valores aceitos.
- Runtime source for shared surfaces: `FieldTheme` e componentes em `launcher/IPConflictMonitor.UpdateExperience.cs`.
- Typography: Bahnschrift para títulos e métricas, Segoe UI para interface e Consolas para dados técnicos.
- Supported theme: escuro operacional.
- Geometry: módulos retangulares, linhas, colchetes e cantos contidos; sem pontos ou círculos decorativos.
- Visual verification: `dist/IPConflictMonitor-dashboard.png`, `dist/IPConflictMonitor-update.png` e `dist/IPConflictMonitor-settings.png`.

## Canonical UI Map

| Capability | Canonical owner | Source of truth | Verification |
|---|---|---|---|
| Dashboard operacional | `NetworkOperationsForm` | `README.md` | captura e testes visuais |
| Configuração integrada | `SettingsExperiencePage` | este contrato + `ConfigurationStore` | build, Pester e captura |
| Persistência da configuração | `ConfigurationStore` | `config/config.json` + regras Strict Evidence | validação, round-trip e falha segura |
| Atualização assistida | `UpdateExperiencePage` | este contrato + `UpdateCoordinator` | build, Pester, capturas e simulação |
| Progresso em etapas | `UpdateStageRail` + `UpdateProgressBar` | `DESIGN.md` | estado determinado e indeterminado |
| Tabela de diagnóstico | `NetworkOperationsForm` | `README.md` | captura e testes visuais |

## Flow ledger

| Operation | Trigger | Pending | Success destination | Success feedback | Failure recovery | Focus outcome |
|---|---|---|---|---|---|---|
| Abrir configuração | `Configuração` | carregamento local breve | página integrada | perfil e caminho ativos visíveis | erro na faixa da página + voltar | primeira seção ou primeiro erro |
| Salvar ajuste comum | `Salvar alterações` | ação bloqueada durante gravação | mesma página | `Alterações salvas` | original preservado + corrigir/tentar novamente | ação de salvar |
| Salvar exceção ou autorização | primeira ativação | estado `Revise as exceções` | mesma página | nenhuma gravação antes da confirmação | editar ou voltar | `Confirmar e salvar` |
| Confirmar exceção ou autorização | `Confirmar e salvar` | validação + gravação atômica | mesma página | backup criado; próxima análise informada | original preservado + erro contextual | ação de salvar |
| Descartar e voltar | `Descartar e voltar` | nenhum | dashboard | alterações não gravadas são descartadas | não aplicável | navegação Configuração |
| Verificar atualização | `Verificar atualização` | página interna, etapa Consulta | mesma página | sistema atualizado ou versão disponível | `Tentar novamente` / `Voltar ao painel` | ação primária da página |
| Baixar atualização | `Atualizar para {versão}` | percentual real de bytes | etapa Integridade | pacote validado | instalação atual preservada + tentar novamente | ação de recuperação |
| Instalar atualização | confirmação na página | etapas Instalação e Reinício | painel reiniciado | `Atualização instalada` antes de abrir | rollback do `.previous`, tentar novamente ou fechar | painel reiniciado |

Enquanto a página de atualização estiver aberta, a navegação lateral fica desabilitada e nenhuma segunda página pode ocupar a célula principal. Alterações não salvas na Configuração bloqueiam a abertura da atualização e permanecem visíveis para decisão do técnico.

## Integrated settings behavior

A opção Configuração substitui apenas a célula principal do dashboard. A barra lateral permanece visível, o editor de arquivo externo não é iniciado e erros não dependem de caixas de mensagem.

A página organiza o perfil em Rede, Exceções, Coleta, Verificação, Integrações e Saída. O seletor de interface é um `ComboBox` nativo em modo `DropDownList`; o popup pertence ao Windows, pode ultrapassar os limites do contêiner, mantém navegação padrão por teclado e desenha seus itens com a paleta escura do console.

As proteções `DetectionMode=StrictEvidence`, `RequireCapturedArpRequest=true`, `RequireCorrelatedArpResponses=true`, `FailClosedWithoutCapture=true` e `MaxConcurrentVerifications=1` são apresentadas como bloqueadas. A página nunca gera uma configuração que desative essas garantias.

Mudanças em `ExcludedIPs`, `ExcludedMACs`, `TrustedPairs`, `TrustedVirtualIps` ou `TrustedMacs` exigem duas ativações: a primeira arma a confirmação e explica que alertas podem ser suprimidos; somente a segunda grava.

## Configuration persistence

- Caminho canônico: `%LOCALAPPDATA%\IPConflictMonitor\config\config.json`.
- Primeiro uso: copiar o sidecar `config/config.json` distribuído; se ausente, usar o recurso interno.
- Depois do seed: launcher, worker e interface usam o mesmo arquivo local.
- A validação de domínio ocorre antes de qualquer substituição.
- A serialização é gravada em arquivo temporário na mesma pasta e recebe `Flush(true)`.
- O temporário é reaberto e validado antes da promoção.
- A substituição é atômica e preserva o perfil anterior em `config.json.previous`.
- Falha de leitura, validação, permissão ou substituição mantém o perfil original.
- Temporários remanescentes são removidos em bloco de limpeza.
- Se um worker estiver ativo, o salvamento é permitido, mas a página informa que os novos valores serão aplicados somente quando uma nova análise for iniciada.
- `Output.Directory` é resolvido pela mesma função no motor, dashboard, Relatórios e comando `-Status`.

Campos reservados sem consumidor operacional não devem aparecer como recursos ativos.

## Validation and feedback

A validação é inline e leva o foco à primeira seção com erro. IPv4 e MACs válidos são canonicalizados; entradas duplicadas são removidas. CIDR e `MaxHosts` são verificados em conjunto. Webhook habilitado exige HTTPS ou loopback. Caminho de TShark inexistente permite salvamento com aviso, pois pode ser provisionado depois.

Mensagens distinguem:

- alteração ainda não gravada;
- configuração inválida;
- exceção aguardando confirmação;
- perfil salvo com backup;
- perfil salvo, mas aguardando próximo worker;
- falha de permissão ou gravação.

## Update overlays and feedback

A atualização também substitui a célula principal e mantém a barra lateral como contexto. O processo auxiliar reutiliza a experiência visual porque o executável principal precisa ser fechado para a substituição. Durante download, cópia e validação, o fechamento pelo usuário é bloqueado e a justificativa aparece na página.

## Async and resilience

- Estratégia pessimista: nenhuma instalação é declarada antes de download, tamanho, SHA-256 e versão interna serem confirmados.
- Ativação duplicada é bloqueada pelo estado ocupado.
- HTTP usa timeout de 30 segundos; a espera pelo processo principal é limitada a 45 segundos.
- Download grava em `.part`, força flush e só então move para o nome final.
- Percentual é determinado apenas quando o total de bytes é conhecido; as demais etapas são nomeadas e indeterminadas.
- Falha preserva a instalação atual e mantém tentativa novamente disponível.
- Antes da cópia, a versão anterior é preservada em `.previous`; falha na verificação final restaura o backup.
- Se o arquivo validado já estiver instalado, a aplicação é idempotente e segue para o reinício.

## Navigation and accessibility

Páginas usam a ordem visual como ordem de foco. Botões têm rótulos completos, alvo mínimo de 44 px, estados ocupado/desabilitado e foco nativo. Estado é comunicado por título, descrição, código e cor. Ao voltar, o foco retorna à ação que abriu a página.

A lista de interfaces mantém comportamento nativo do Windows, incluindo abertura por teclado, setas, Enter, Escape e rolagem do popup. Nenhum menu suspenso é recortado ou simulado por painéis internos.

## Verification

- Build: `./Build-Executable.ps1`.
- Testes: `Invoke-Pester -Path ./tests -PassThru`.
- Cenários internos: `./dist/IPConflictMonitor.exe -SelfTestDetection`.
- Capturas: dashboard, atualização e configuração em 1440×900.
- Configuração crítica: seed, load, validação, confirmação dupla, flush, backup, substituição, falha segura e aplicação no próximo worker.
- Atualização crítica: consulta, versão encontrada, download real, validação, substituição temporária, backup, rollback e reinício.
- Auditoria estática: ausência de editor externo, círculos decorativos e caminhos divergentes de configuração/saída.
