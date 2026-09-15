---
version: alpha
name: "IPConflictMonitor Field Interface"
description: "Console operacional de campo que apresenta diagnóstico IPv4 e atualização segura como instrumentos verificáveis."
colors:
  primary: "#2BD5ED"
  canvas: "#060C17"
  sidebar: "#08111F"
  surface: "#0D192C"
  surface-raised: "#112036"
  stroke: "#253A52"
  text-primary: "#EDF5FD"
  text-secondary: "#8EA6C3"
  text-muted: "#567492"
  accent-cyan: "#2BD5ED"
  accent-blue: "#4B7DFF"
  accent-purple: "#9E66FF"
  success: "#37DE97"
  warning: "#F9B844"
  danger: "#FF5871"
typography:
  sans:
    fontFamily: "Segoe UI, Arial, sans-serif"
  data:
    fontFamily: "Consolas, Cascadia Mono, monospace"
rounded:
  DEFAULT: "9px"
  sm: "6px"
  md: "9px"
  lg: "10px"
spacing:
  shell-padding: "20px"
  panel-gap: "12px"
  control-height: "44px"
components:
  button: { }
  panel: { }
  table: { }
  stage-rail: { }
  progress: { }
---

# IPConflictMonitor Field Interface Design System

## Overview

### Creative North Star

A interface deve parecer um instrumento de diagnóstico de rede usado em uma central de operações: alta densidade informacional, contraste confiável, hierarquia por linhas e superfícies, e cor reservada para estado técnico. Não é um tema “hacker”; é um equipamento de campo legível.

### Product context and register

- **Audience and primary job:** técnicos de campo que precisam encontrar conflitos IPv4, validar evidências e atualizar a ferramenta sem treinamento especializado.
- **Target market and evidence:** distribuição Windows em português do Brasil, conforme o README e os textos mantidos no executável.
- **Locale and language policy:** interface própria em `pt-BR`; termos técnicos consolidados como IPv4, MAC, ARP, SHA-256, Npcap e TShark permanecem inalterados.
- **Usage scene:** notebook Windows em atendimento de rede, com urgência moderada, telas a partir de 1160×740 e alta densidade de dados.
- **Register:** produto operacional; clareza, estabilidade e prova têm prioridade sobre expressão promocional.
- **Memorable signature:** a trilha de integridade em cinco etapas para trabalhos longos: consulta, download, integridade, instalação e reinício.
- **Restraint:** tabelas, mensagens de falha, botões e ações frequentes permanecem familiares e sem animação ornamental.
- **Anti-references:** estética gamer neon, terminal fictício, instalador genérico do Windows, vidro excessivo e cartões promocionais com números sem função.
- **Token ownership/runtime mapping:** o código C# é a fonte de execução. `FieldTheme` em `launcher/IPConflictMonitor.UpdateExperience.cs` é o proprietário canônico para novas superfícies; os valores existentes em `NetworkOperationsForm` são compatíveis e devem migrar para `FieldTheme` quando forem tocados. Este arquivo espelha e explica esses valores.

## Colors

`canvas`, `sidebar`, `surface` e `surface-raised` criam profundidade por tom, sem sombras pesadas. `stroke` delimita cartões e áreas de dados. Ciano identifica ação ou etapa ativa; azul apoia informação; roxo é reservado à transição de instalação. Verde significa conclusão comprovada, âmbar pede atenção recuperável e vermelho indica falha ou conflito. Estado nunca depende apenas da cor: ícone, título e texto acompanham cada tom.

## Typography

Segoe UI é a família de interface e mantém leitura nativa no Windows. Consolas é usada somente para versões, endereços, hashes, horários e telemetria. Títulos usam peso semibold; corpo evita caixa alta. Etiquetas técnicas curtas podem usar caixa alta com tamanho menor. Textos longos devem truncar apenas quando o valor completo continua disponível em uma área de detalhe.

## Layout

A aplicação usa barra lateral fixa de 226 px e uma área principal fluida. A margem de conteúdo é 20 px e os painéis usam intervalo visual próximo de 12 px. A atualização substitui somente a área principal e mantém a barra lateral como contexto. Indicadores assíncronos reservam altura estável; controles não mudam de dimensão entre estados. O mínimo suportado é 1160×740 com escala DPI do Windows.

## Elevation & Depth

A hierarquia vem de superfícies tonais e bordas de 1 px. Gradiente é permitido no cabeçalho e em uma ação primária. Halos aparecem apenas no ponto ativo ou em métricas críticas. Sombras, transparência intensa e desfoque não são usados em tabelas, formulários nem mensagens de recuperação.

## Shapes

Controles usam raio de 6–9 px; painéis principais usam 9–10 px ou borda reta compatível com o dashboard legado. Indicadores circulares são reservados a etapas, métricas e estados. Botões sempre mantêm texto explícito; ícones sem rótulo não são usados em ações críticas.

## Components

### Foundational visual states

Controles interativos oferecem estado padrão, hover, foco nativo visível, pressionado, desabilitado e ocupado. O indicador padrão é uma barra própria: determinada somente quando bytes totais são conhecidos e indeterminada nos demais trabalhos. Falha permanece na superfície com explicação e recuperação; sucesso é anunciado sem retirar o foco à força.

### Buttons and actions

Uma área de decisão possui uma ação primária sólida. Ação secundária usa superfície elevada e borda. Durante consulta, download ou instalação, ativação duplicada é bloqueada sem alterar a geometria. Rótulos descrevem o resultado: “Atualizar para 3.3.1”, “Tentar novamente” e “Voltar ao painel”.

### Navigation and data display

A barra lateral preserva a orientação durante a atualização. A trilha de integridade é a representação canônica de trabalhos com várias etapas; etapas concluídas, atual e futura têm texto e símbolo além de cor. Dados de rede continuam em tabela densa, com detalhes técnicos em área própria.

### Forms and overlays

A atualização é uma página dentro da área principal, não uma caixa de mensagem nativa. Quando o executável precisa substituir a si próprio, o processo auxiliar reutiliza a mesma página em uma janela compacta. Durante etapas críticas, o fechamento é bloqueado com explicação na própria superfície. Erros oferecem tentativa novamente ou saída segura.

### Iconography

Símbolos Unicode simples acompanham rótulos; não substituem texto em ações críticas. Escudo, marca de verificação, alerta e seta devem conservar o mesmo significado em todo o produto.

### Motion

Movimento comunica atividade: o segmento da barra avança durante duração desconhecida. O sistema respeita a preferência de animação do Windows; quando desativada, a posição permanece estática e o texto continua comunicando progresso. A confirmação concluída permanece visível antes do reinício.

### Content and data visualization

A voz é direta, operacional e em português. Mensagens dizem o que está acontecendo, o que foi preservado e qual ação resolve a falha. Percentual aparece somente para download mensurável. Hashes, IPs, MACs, versões e horários usam fonte de dados.

## Do's and Don'ts

- **Do:** explicar a evidência, a etapa atual e a recuperação disponível.
- **Do:** preservar a barra lateral, a geometria e o vocabulário entre estados.
- **Don't:** inventar percentual durante validação, instalação ou espera de processo.
- **Don't:** usar pop-ups nativos, animação contínua ornamental ou vermelho para uma condição apenas informativa.


