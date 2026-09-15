# IPConflictMonitor UX Contract

## Product context

- Audience: técnicos de campo em notebooks Windows.
- Primary jobs: analisar a rede local, interpretar provas de conflito IPv4 e manter a ferramenta atualizada.
- Target market: distribuição em português do Brasil.
- Active locale: `pt-BR`, preservando termos técnicos padronizados.
- Accessibility target: controles nativos de teclado e foco, contraste equivalente a WCAG 2.2 AA e escala DPI do Windows.

## Business-context sources

| Domain / scope | Authoritative source | Source type | Reviewed date |
|---|---|---|---|
| Detecção e estados | `README.md` | Contrato do produto | 2026-09-15 |
| Atualização e recuperação | `README.md` | Contrato do produto | 2026-09-15 |
| Configuração segura | `config/config.json` | Configuração distribuída | 2026-09-15 |

Não existem permissões, cobrança, exclusão de dados pessoais ou texto regulatório neste fluxo.

## Visual contract

- Project design: `DESIGN.md`.
- Token ownership: código de execução canônico; documentação espelha valores aceitos.
- Runtime source for new surfaces: `FieldTheme` e componentes em `launcher/IPConflictMonitor.UpdateExperience.cs`.
- Supported theme: escuro operacional.
- Visual verification: `dist/IPConflictMonitor-dashboard.png` e `dist/IPConflictMonitor-update.png`.

## Canonical UI Map

| Capability | Canonical owner | Source of truth | Verification |
|---|---|---|---|
| Atualização assistida | `UpdateExperiencePage` | Este contrato + `UpdateCoordinator` | build, Pester, capturas e simulação |
| Progresso em etapas | `UpdateStageRail` + `UpdateProgressBar` | `DESIGN.md` | estado determinado e indeterminado |
| Ação de atualização | `UpdateActionButton` | `DESIGN.md` | teclado, ocupado e duplicidade |
| Tabela de diagnóstico | `NetworkOperationsForm` | `README.md` | captura e testes visuais |

## Flow ledger

| Operation | Trigger | Pending | Success destination | Success feedback | Failure recovery | Focus outcome |
|---|---|---|---|---|---|---|
| Verificar atualização | `Verificar atualização` | página interna, etapa Consulta | mesma página | `Sistema atualizado` ou versão disponível | `Tentar novamente` / `Voltar ao painel` | ação primária da página |
| Baixar atualização | `Atualizar para {versão}` | percentual real de bytes | etapa Integridade | pacote validado | instalação atual preservada + tentar novamente | ação de recuperação |
| Instalar atualização | confirmação na página | etapas Instalação e Reinício | painel reiniciado | `Atualização instalada` antes de abrir | rollback do `.previous`, tentar novamente ou fechar | painel reiniciado |
| Cancelar antes da instalação | `Agora não` | nenhum | painel original | nenhuma | não aplicável | botão `Verificar atualização` |

## Overlays and feedback

A atualização substitui a célula principal do dashboard e mantém a barra lateral como contexto. Não usa caixas de mensagem nativas. O processo auxiliar reutiliza a mesma experiência visual porque o executável principal precisa ser fechado para a substituição. Durante download, cópia e validação, o fechamento pelo usuário é bloqueado e a justificativa aparece na própria página.

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

A página usa a ordem visual como ordem de foco. Botões têm rótulos completos, alvo mínimo de 44 px, estados ocupado/desabilitado e foco nativo. Estado é comunicado por título, descrição, símbolo e cor. A preferência de animação do Windows controla o movimento indeterminado. Ao voltar, o foco retorna ao botão que abriu a atualização.

## Verification

- Build: `./Build-Executable.ps1`.
- Testes: `Invoke-Pester -Path ./tests -PassThru`.
- Cenários internos: `./dist/IPConflictMonitor.exe -SelfTestDetection`.
- Capturas: dashboard e página de atualização em 1440×900.
- Fluxo crítico: consulta, versão encontrada, download real, validação, substituição temporária, backup, rollback e reinício.
- Auditoria estática de interface: script `audit_project.py` em modo estrito e buscas do catálogo de anti-padrões.
