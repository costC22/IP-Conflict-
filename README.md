# IPConflictMonitor 3.1 — Field Edition

Aplicativo portátil para Windows que identifica indícios e confirmações de dois equipamentos usando o mesmo endereço IPv4 na rede local. A ferramenta foi escrita em C# nativo e possui uma interface voltada aos técnicos de campo.

## Recursos principais

- varredura IPv4 da rede local com seleção automática da interface;
- leitura da tabela ARP pela API nativa do Windows;
- captura ARP adicional quando TShark/Npcap está disponível;
- correlação histórica entre endereço IP e endereços MAC;
- classificação visual em `NORMAL`, `SUSPECT` e `CONFIRMED`;
- relatórios CSV e log técnico no perfil local do usuário;
- atualização manual e validada a partir das releases deste repositório.

## Uso pelo técnico

1. Baixe e extraia `IPConflictMonitor-Windows.zip` da página de releases.
2. Abra `IPConflictMonitor.exe`.
3. Clique em **Analisar agora** para executar uma varredura única.
4. Clique em **Monitorar** para repetir as análises enquanto o painel estiver aberto.
5. Use **Parar** para encerrar o monitoramento da sessão.

Nenhuma instalação é necessária. O campo de busca filtra por IP, hostname, MAC, status ou diagnóstico.

## Atualização pelo aplicativo

O botão **Verificar atualização** consulta a release mais recente do GitHub somente quando o usuário clica nele. Quando existe uma versão mais nova, o aplicativo:

1. mostra a versão e as notas da release;
2. solicita confirmação antes do download;
3. baixa `IPConflictMonitor-Windows.zip` e `IPConflictMonitor.exe.sha256` por HTTPS;
4. compara o SHA-256 do novo executável;
5. substitui o executável atual e reinicia a ferramenta;
6. restaura a versão anterior se a substituição ou a validação falhar.

Não há token ou credencial dentro do executável. O atualizador aceita somente os arquivos publicados em `costC22/IP-Conflict-` e bloqueia URLs fora do GitHub. Como o programa é portátil, mantenha a pasta em um local no qual o usuário possua permissão de escrita.

## Como a detecção funciona

1. Seleciona a interface IPv4 física ativa ou o índice configurado.
2. Lê a tabela ARP diretamente pela API `iphlpapi.dll`.
3. Faz varredura ICMP paralela para provocar a resolução dos vizinhos.
4. Quando disponível, usa TShark/Npcap para observar respostas ARP diretamente.
5. Mantém uma janela de evidências por IP e MAC.
6. Classifica o resultado como normal, suspeito ou confirmado.

Um conflito é confirmado quando dois MACs aparecem na mesma captura, respondem às sondagens no mesmo ciclo ou alternam repetidamente com evidências suficientes. Uma troca isolada permanece como suspeita para reduzir falsos positivos.

No modo portátil comum, sem privilégios administrativos, a análise usa a tabela de vizinhos e o histórico. Para máxima precisão em uma ocorrência difícil, abra manualmente o aplicativo como Administrador e instale Wireshark/TShark com Npcap. O aplicativo nunca solicita elevação sozinho.

## Segurança e privacidade

- não cria serviço, tarefa agendada ou inicialização automática;
- não se copia para pastas do sistema;
- não executa scripts durante o uso ou a atualização;
- não coleta telemetria nem envia resultados da rede;
- grava os diagnósticos apenas no perfil local do usuário;
- contém somente a configuração JSON como recurso incorporado.

Uma assinatura Authenticode válida é recomendada para consolidar a reputação do editor no SmartScreen e em antivírus corporativos. O SHA-256 confirma a integridade do download, mas não substitui uma assinatura de código emitida para o publicador.

## Arquivos e relatórios

```text
%LocalAppData%\IPConflictMonitor\logs\monitor.log
%LocalAppData%\IPConflictMonitor\logs\updater.log
%LocalAppData%\IPConflictMonitor\reports\snapshot.csv
%LocalAppData%\IPConflictMonitor\reports\conflicts.csv
%LocalAppData%\IPConflictMonitor\data\state.json
```

## Configuração

Edite `config\config.json` ao lado do EXE:

- `CIDR`: vazio usa a rede da interface ativa; exemplo: `192.168.15.0/24`;
- `InterfaceIndex`: zero seleciona automaticamente;
- `MaxHosts`: limita a quantidade de endereços da varredura;
- `ExcludedIPs` e `ExcludedMACs`: itens ignorados;
- `TrustedPairs`: pares permitidos no formato `IP|MAC`;
- `EvidenceWindowMinutes`: janela usada para correlacionar mudanças;
- `MinObservationsPerMac` e `MinMacTransitions`: limiares da confirmação histórica;
- `PacketCaptureEnabled`: usa TShark quando disponível;
- `ActiveArpProbeEnabled`: habilita sondagem ativa quando o processo já foi aberto como Administrador.

## Linha de comando

```powershell
.\IPConflictMonitor.exe -Worker -Once
.\IPConflictMonitor.exe -Worker
.\IPConflictMonitor.exe -Status
.\IPConflictMonitor.exe -CheckUpdate
.\IPConflictMonitor.exe -ValidateConfiguration -ConfigPath .\config\config.json
```

## Gerar o EXE

No Windows com .NET Framework:

```powershell
.\Build-Executable.ps1
```

Para assinar durante o build, informe o thumbprint de um certificado instalado:

```powershell
.\Build-Executable.ps1 -CertificateThumbprint 'THUMBPRINT_DO_CERTIFICADO'
```

O build valida a versão, a configuração, os recursos incorporados, a presença do atualizador e a renderização da interface. Os artefatos são gravados em `dist`.

## Publicar uma versão

1. Atualize o número de versão do assembly e da interface.
2. Execute `Build-Executable.ps1` e os testes.
3. Crie e envie uma tag no formato `v3.1.0`.
4. O fluxo de release recompila e publica EXE, ZIP, SHA-256 e imagem do painel.

## Limites

- o monitor enxerga somente o segmento de camada 2 alcançável pela interface escolhida;
- dispositivos silenciosos podem aparecer após tráfego ou varreduras posteriores;
- switches com isolamento, VLANs ou segurança de porta podem limitar a observação;
- um executável sem assinatura pode receber alerta baseado em baixa reputação.
