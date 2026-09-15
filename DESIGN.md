---
version: "3.4.0"
name: "IPConflictMonitor Field Interface"
description: "Console técnico para diagnóstico IPv4, configuração segura e atualização verificável em campo."
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
  display:
    fontFamily: "Bahnschrift, Segoe UI, Arial, sans-serif"
  sans:
    fontFamily: "Segoe UI, Arial, sans-serif"
  data:
    fontFamily: "Consolas, Cascadia Mono, monospace"
rounded:
  DEFAULT: "4px"
  sm: "2px"
  md: "4px"
  lg: "6px"
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
  settings-page: { }
  native-combobox: { }
---

# IPConflictMonitor Field Interface Design System

## Overview

### Creative North Star

A interface 3.4.0 deve parecer um console técnico dedicado, próximo de um appliance de diagnóstico de rede: estrutura precisa, leitura rápida, superfícies contidas e sinais operacionais inequívocos. A identidade vem de tipografia, grade, bordas, códigos curtos e dados reais — não de ornamentos.

### Product context and register

- **Audience and primary job:** técnicos de campo que precisam localizar conflitos IPv4, conferir evidências, ajustar o perfil ativo e manter a ferramenta atualizada sem treinamento extenso.
- **Target market:** distribuição Windows em português do Brasil.
- **Locale and language policy:** interface própria em `pt-BR`; IPv4, MAC, ARP, SHA-256, Npcap e TShark permanecem inalterados.
- **Usage scene:** notebook Windows em atendimento de rede, com telas a partir de 1160×740, escala DPI do sistema e alta densidade de dados.
- **Register:** console operacional; clareza, estabilidade e prova têm prioridade sobre expressão promocional.
- **Memorable signature:** módulos identificados por códigos técnicos entre colchetes, linhas de estado e a trilha de integridade em cinco etapas.
- **Restraint:** tabelas, formulários, mensagens e ações frequentes permanecem familiares e sem animação ornamental.
- **Anti-references:** estética gamer, terminal fictício, vidro excessivo, cartões promocionais genéricos, pontos luminosos soltos e indicadores circulares decorativos.
- **Token ownership/runtime mapping:** o código C# é a fonte de execução. `FieldTheme` em `launcher/IPConflictMonitor.UpdateExperience.cs` é o proprietário canônico das superfícies compartilhadas. Este arquivo espelha e explica os valores aceitos.

## Colors

`canvas`, `sidebar`, `surface` e `surface-raised` criam profundidade por tom. `stroke` delimita módulos e áreas de dados. Ciano identifica seleção ou ação; azul apoia informação; roxo é reservado à transição de instalação. Verde significa conclusão comprovada, âmbar pede atenção recuperável e vermelho indica falha ou conflito.

Estado nunca depende apenas da cor. Título, texto e geometria acompanham cada tom. Brilhos difusos e halos não fazem parte do sistema.

## Typography

- **Bahnschrift SemiCondensed:** títulos de página, números de métricas e cabeçalhos de módulo.
- **Segoe UI:** navegação, formulários, botões, ajuda e textos corridos.
- **Consolas:** IPv4, MAC, CIDR, versões, hashes, caminhos, horários e telemetria.

Títulos usam peso semibold ou bold. Corpo evita caixa alta. Etiquetas técnicas curtas podem usar caixa alta em tamanho menor. O sistema deve manter fallback para Segoe UI quando Bahnschrift não estiver disponível.

## Layout

A aplicação usa barra lateral fixa de 226 px e área principal fluida. A margem de conteúdo é 20 px e os módulos usam intervalo próximo de 12 px. Dashboard, configurações e atualização ocupam a mesma célula principal e mantêm a barra lateral como contexto.

A configuração usa uma segunda navegação vertical compacta para Rede, Exceções, Coleta, Verificação, Integrações e Saída. Indicadores assíncronos e mensagens reservam altura estável. O mínimo suportado é 1160×740 com escala DPI do Windows.

## Elevation & Depth

A hierarquia vem de superfícies tonais, divisores e bordas de 1 px. Gradiente é permitido apenas em cabeçalhos e na ação primária. Sombras, transparência intensa e desfoque não são usados em tabelas, formulários nem mensagens de recuperação.

## Shapes

O console privilegia retângulos, linhas, colchetes e cantos de 2–6 px. Status e etapas usam barras, segmentos, códigos ou marcas angulares. Indicadores circulares, pontos de navegação e círculos desenhados não são permitidos. Botões sempre mantêm texto explícito; ícones sem rótulo não são usados em ações críticas.

## Components

### Foundational visual states

Controles interativos oferecem estado padrão, hover, foco nativo visível, pressionado, desabilitado e ocupado. Falha permanece na própria superfície com explicação e recuperação. Sucesso é anunciado sem retirar o foco à força.

### Buttons and actions

Uma área de decisão possui uma ação primária sólida. Ação secundária usa superfície elevada e borda. Ativação duplicada é bloqueada sem alterar a geometria. Rótulos descrevem o resultado: “Atualizar para 3.4.0”, “Salvar alterações”, “Confirmar e salvar” e “Voltar ao painel”.

### Navigation and data display

A barra lateral preserva orientação entre as páginas. Seleção é indicada por faixa lateral, contraste e rótulo; nunca por um ponto isolado. Dados de rede permanecem em tabela densa, com detalhes técnicos em área própria. Separadores textuais preferem barras, colchetes ou espaçamento.

### Settings

A opção Configuração abre `SettingsExperiencePage` dentro da área principal. O formulário não abre editor de texto externo.

- O seletor de interface usa `ComboBox` nativo com `DropDownList`; o popup pertence ao sistema operacional, mantém teclado e rolagem padrão, enquanto os itens acompanham a paleta escura do console.
- Campos numéricos usam `NumericUpDown` e limites coerentes com a validação de domínio.
- Proteções Strict Evidence são exibidas como bloqueadas.
- Exceções e autorizações exigem uma segunda ativação consciente antes da gravação.
- Erros e sucesso aparecem na faixa de estado da página.
- Alterações salvas durante uma análise passam a valer somente no próximo worker.

### Configuration persistence

O perfil canônico fica em `%LOCALAPPDATA%\IPConflictMonitor\config\config.json`. Na primeira execução, ele é semeado pelo sidecar distribuído; na ausência deste, usa o recurso interno. Depois do seed, o arquivo local é a única fonte ativa.

O salvamento valida o modelo e o arquivo temporário, força flush em disco e usa substituição atômica com backup `.previous`. O caminho configurado em `Output.Directory` deve ser respeitado de maneira idêntica pelo motor, dashboard, Relatórios e comando de status.

### Update progress

A trilha de integridade representa Consulta, Download, Integridade, Instalação e Reinício. Percentual aparece somente quando o total de bytes é conhecido. Nos demais trabalhos, uma barra segmentada comunica atividade sem inventar progresso.

### Forms and overlays

Configuração e atualização são páginas dentro da área principal, não caixas de mensagem. Quando o executável precisa substituir a si próprio, o processo auxiliar reutiliza a mesma linguagem visual em janela compacta. Durante etapas críticas, o fechamento é bloqueado com explicação na superfície.

### Iconography

Códigos como `[NW]`, `[TR]`, `[CL]`, `[VF]`, `[IN]` e `[IO]` identificam módulos. Setas, escudo, marca de verificação e alerta só acompanham rótulos explícitos. Símbolos redondos isolados não comunicam estado.

### Motion

Movimento comunica apenas atividade real. O sistema respeita a preferência de animação do Windows. Quando animações estão desativadas, posição, texto e contraste continuam comunicando o estado.

### Content and data visualization

A voz é direta, operacional e em português. Mensagens dizem o que está acontecendo, o que foi preservado e qual ação resolve a falha. Hashes, IPs, MACs, versões, caminhos e horários usam fonte de dados.

## Do's and Don'ts

- **Do:** explicar evidência, configuração ativa, etapa atual e recuperação disponível.
- **Do:** preservar barra lateral, geometria, foco e vocabulário entre estados.
- **Do:** usar tipografia, linhas e códigos técnicos para construir identidade.
- **Don't:** inventar percentual durante validação, instalação ou espera.
- **Don't:** usar editor externo, pop-ups desnecessários, animação ornamental ou círculos decorativos.
- **Don't:** apresentar um campo reservado como se já tivesse efeito operacional.

