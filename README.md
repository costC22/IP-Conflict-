# IPConflictMonitor 3.4.0 — Strict Evidence Detection

Ferramenta portátil para Windows destinada a verificar conflitos IPv4 no mesmo domínio de camada 2. A versão 3.4.0 mantém a política conservadora de detecção, apresenta um console técnico renovado e incorpora configuração e atualização dentro do painel: informação ambígua nunca é apresentada como conflito.

> Um conflito somente é confirmado quando requisições ARP geradas pelo monitor recebem respostas contemporâneas, correlacionadas, repetidas e consistentes de dois endereços MAC distintos para o mesmo IPv4.

## Estados

- `NORMAL`: nenhuma prova contemporânea de conflito;
- `UNVERIFIED`: mudança ou informação incompleta sem prova suficiente;
- `MONITORING_LIMITED`: captura, interface ou visibilidade técnica insuficiente;
- `CONFIRMED`: todas as condições do Strict Evidence foram aprovadas.

`UNVERIFIED` e `MONITORING_LIMITED` não são incidentes de IP duplicado e nunca geram alerta crítico.

## Arquitetura de detecção

### Fase 1 — Discovery

Descobre hosts e candidatos usando tabela ARP do Windows, ICMP auxiliar, histórico e pacotes ARP observados. Esses dados servem somente para descoberta, visualização e seleção de candidatos. Nenhum resultado desta fase pode produzir `CONFIRMED`.

### Fase 2 — Strict Verification

Para cada candidato, o monitor:

1. inicia uma captura TShark na interface selecionada;
2. limpa apenas a entrada ARP do IPv4 alvo;
3. gera uma requisição ARP ativa com o IPv4 local como origem;
4. exige que a própria requisição apareça na captura;
5. aceita somente respostas posteriores, dentro da janela configurada, para o alvo e interface corretos;
6. rejeita MAC broadcast, multicast, nulo ou malformado;
7. exige o mesmo par de MACs em pelo menos 2 de 3 rodadas;
8. exige repetição da prova em 2 ciclos consecutivos;
9. bloqueia a confirmação em caso de Proxy ARP, MAC de gateway ou associação autorizada;
10. gera `EvidenceId` e SHA-256 da evidência normalizada.

Na versão 3.3, o discovery força nova resolução apenas das entradas ARP dinâmicas já conhecidas enquanto a captura está ativa. Isso faz conflitos silenciosos aparecerem como candidatos sem apagar entradas estáticas. A verificação também exige que a requisição tenha o MAC da interface selecionada, que cada resposta seja destinada a esse mesmo MAC e que endereços excluídos não participem da prova. Se três ou mais equipamentos responderem, o engine procura um par estável repetido nas rodadas e preserva todos os respondentes persistentes no relatório.

Existe uma única função de decisão (`EvaluateConflict`). A interface gráfica apenas exibe a decisão produzida pelo engine.

## O que não confirma conflito

- mudança histórica de MAC;
- dois MACs existentes apenas no cache ARP;
- alternância ou flapping anterior;
- ping, timeout, TTL ou disponibilidade;
- pontuação de confiança;
- ARP espontâneo ou gratuitous ARP isolado;
- duas identidades vistas em uma captura não correlacionada;
- resultado parcial após falha do TShark.

## Requisitos para confirmação

- Windows com .NET Framework 4.x;
- TShark instalado com Npcap funcional;
- interface correta e operacional;
- visibilidade de camada 2 do IPv4 monitorado;
- permissão suficiente para capturar na interface;
- CIDR local alcançável pela interface selecionada.

Wireshark/TShark e Npcap não são empacotados com a ferramenta. Instale-os pelo canal oficial do fornecedor. A GUI e os logs informam quando a verificação estrita não está pronta.

## Uso pelo técnico

1. Baixe `IPConflictMonitor-Windows.zip` na página de releases.
2. Extraia o pacote em uma pasta com permissão de escrita.
3. Abra `IPConflictMonitor.exe`.
4. Use **Analisar rede** para uma execução única ou **Monitorar** para ciclos contínuos.
5. Consulte a prova no painel: interface, requisição observada, rodadas, ciclos, respostas e Evidence ID.

Para captura em ambientes restritos, pode ser necessário abrir manualmente o aplicativo como Administrador. A ferramenta nunca solicita elevação por conta própria.

## Configuração integrada e segura

Use **Configuração** na barra lateral. A página permanece dentro do sistema e organiza o perfil em **Rede**, **Exceções**, **Coleta**, **Verificação**, **Integrações** e **Saída**. O seletor de interface usa a lista nativa do Windows; os campos exibem limites e erros no próprio painel.

O pacote ainda contém `config\config.json`, mas ele funciona como seed da primeira execução. O perfil ativo canônico fica em:

```text
%LocalAppData%\IPConflictMonitor\config\config.json
```

Na ausência de um perfil local, o sistema copia o sidecar distribuído; se ele também estiver ausente, usa o default incorporado ao EXE. Depois disso, painel e worker leem o mesmo arquivo local.

Defaults essenciais:

```json
{
  "DetectionMode": "StrictEvidence",
  "VerificationRounds": 3,
  "RequiredPositiveRounds": 2,
  "RequiredConfirmedCycles": 2,
  "ArpResponseWindowMs": 1500,
  "RequireCapturedArpRequest": true,
  "RequireCorrelatedArpResponses": true,
  "FailClosedWithoutCapture": true,
  "DetectProxyArp": true,
  "MaxConcurrentVerifications": 1,
  "ArpProbeRateLimitMs": 250
}
```

As três proteções fundamentais e a serialização da verificação não podem ser desativadas. Uma configuração insegura é recusada com `STRICT DETECTION SAFETY DISABLED`.

O salvamento valida os valores, grava em arquivo temporário, força flush em disco e substitui o perfil atomicamente. A versão anterior permanece em `config.json.previous`. Se uma análise estiver em andamento, o perfil é salvo normalmente e passa a valer somente quando uma nova análise for iniciada.

`Output.Directory` é aplicado de forma consistente ao motor, ao dashboard, à opção **Relatórios** e ao comando `-Status`.

### Autorizações explícitas

- `TrustedPairs`: pares `IP|MAC` permitidos;
- `TrustedVirtualIps`: VIPs/endereços de HA explicitamente autorizados;
- `TrustedMacs`: MACs globais autorizados;
- `ExcludedIPs` e `ExcludedMACs`: itens fora do escopo.

Como essas entradas podem suprimir alertas, qualquer alteração nelas exige duas ações conscientes: **Salvar alterações** e, após a revisão exibida na página, **Confirmar e salvar**. Toda exceção permanece visível e auditável no perfil.

## Seleção de interface

O seletor prioriza Ethernet/Wi-Fi físicos, gateway válido, velocidade e compatibilidade com o CIDR. VPN, TAP/TUN, Hyper-V, VMware, VirtualBox, WSL, Docker, Bluetooth, ZeroTier e Tailscale são penalizados automaticamente, salvo escolha explícita por `InterfaceIndex`.

Os logs registram:

```text
SelectedInterfaceName
SelectedInterfaceIndex
SelectedInterfaceIPv4
SelectedInterfaceMac
SelectedInterfaceCidr
SelectionReason
```

Se o IPv4 local não pertencer ao CIDR configurado, o estado passa a `MONITORING_LIMITED` com a mensagem `configured network not reachable through selected interface`.

## Evidências, logs e relatórios

Os dados ficam somente no perfil local:

```text
%LocalAppData%\IPConflictMonitor\logs\monitor.log
%LocalAppData%\IPConflictMonitor\logs\updater.log
%LocalAppData%\IPConflictMonitor\reports\snapshot.csv
%LocalAppData%\IPConflictMonitor\reports\conflicts.csv
%LocalAppData%\IPConflictMonitor\data\state.json
```

`snapshot.csv` registra estado, MACs, interface, monitor IP, requisição observada, rodadas positivas, ciclos, respostas correlacionadas, Proxy ARP, gateway, associação autorizada, saúde da captura, Evidence ID, EvidenceHash e motivo.

`state.json` e `snapshot.csv` são gravados por arquivo temporário, flush em disco e substituição atômica. O histórico permanece separado do estado atual e nunca mantém um conflito sem prova presente.

## Recuperação

Quando a prova deixa de existir, o estado atual sai de `CONFIRMED`. Se somente um MAC permanece e a captura está saudável, o engine registra `CONFLICT_RESOLVED` e volta para `NORMAL`. O último conflito pode continuar no histórico para auditoria, sem afetar a decisão atual.

## Self-test

O executável contém 24 cenários sintéticos e não depende da rede real:

```powershell
.\IPConflictMonitor.exe -SelfTestDetection
```

Resultado obrigatório:

```text
24 passed
0 failed
```

O build é interrompido se qualquer cenário falhar.

## Compilação

Em um Windows com .NET Framework:

```powershell
.\Build-Executable.ps1
```

O fluxo executa:

```text
validação estática da política
→ compilação limpa
→ 24 self-tests
→ validação da configuração
→ validação do binário
→ renderização do dashboard, da página de atualização e da página de configuração
→ pacote ZIP e SHA-256
```

Para assinar com um certificado instalado:

```powershell
.\Build-Executable.ps1 -CertificateThumbprint 'THUMBPRINT_DO_CERTIFICADO'
```

## Atualização pelo aplicativo

O botão **Verificar atualização** abre uma página na área principal do próprio sistema. O fluxo apresenta cinco etapas reais: **Consulta**, **Download**, **Integridade**, **Instalação** e **Reinício**. O download mostra percentual somente quando o total de bytes é conhecido; as demais etapas usam progresso indeterminado e texto descritivo.

O download usa HTTPS, confere o tamanho publicado, grava primeiro em arquivo `.part`, força flush em disco e valida o SHA-256 e a versão interna antes de instalar. O processo auxiliar mantém a mesma identidade visual enquanto o executável principal é fechado, cria o backup `.previous`, verifica novamente o arquivo instalado e restaura a versão anterior se a confirmação final falhar.

Erros permanecem na página com **Tentar novamente** e **Voltar ao painel**. Durante uma etapa crítica, o fechamento é temporariamente bloqueado e a justificativa aparece na própria interface. Nenhuma credencial é incorporada ao EXE.

## Troubleshooting

### MONITORING_LIMITED — TShark indisponível

Instale Wireshark/TShark e confirme o caminho em `Integrations.TSharkPath` se a descoberta automática não funcionar.

### MONITORING_LIMITED — Npcap/interface indisponível

Confirme a instalação do Npcap, permissões de captura e se o TShark lista a interface correta com `tshark -D`.

### CIDR não alcançável

Revise `Network.CIDR` e `InterfaceIndex`. A distribuição oficial prefere não confirmar a produzir um incidente em uma interface incorreta.

### Mudança de MAC aparece como NÃO VERIFICADO

Esse é o comportamento esperado. DHCP, failover, VIP, migração de VM e substituição de equipamento podem trocar a associação sem conflito simultâneo.

## Limitações técnicas

- Strict ARP verification requer visibilidade Layer 2 do IPv4 monitorado;
- redes roteadas remotas não podem ser verificadas universalmente por ARP;
- private VLAN, port isolation e segurança de switch podem ocultar um dos respondentes;
- Proxy ARP, gateway e ambientes HA recebem tratamento conservador;
- ausência de prova suficiente produz `UNVERIFIED` ou `MONITORING_LIMITED`, nunca conflito;
- o binário sem assinatura pode receber alerta de reputação mesmo após varredura antivírus limpa.

## Segurança operacional

- sem serviço, tarefa agendada ou inicialização automática;
- sem scripts no fluxo de execução ou atualização;
- sem telemetria de rede;
- processos externos usam timeout, captura de saída e `UseShellExecute = false`;
- apenas uma verificação do mesmo processo ocorre por vez;
- sondagens possuem limite de frequência para evitar tráfego excessivo.
