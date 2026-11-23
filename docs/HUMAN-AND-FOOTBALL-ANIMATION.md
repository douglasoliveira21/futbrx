# Jogador humano e animações de futebol — 0.8

Atualização: novas partidas da 0.9 usam v4; a biblioteca visual permanece compatível. Ver [regras e gestão](RULES-MARKET-AND-2026.md).

Entrega da arte final para a direção humana estilizada do jogo. O modelo anterior foi substituído por geometria original com mandíbula, crânio, nariz, boca, olhos, sobrancelhas, orelhas, três cabelos, uniforme, chuteiras com travas e luvas. Pele e uniforme variam por atleta; a camisa recebe número na frente e nas costas. Não representa atletas reais e não usa captura de movimento.

## Biblioteca e inspeção

O menu **Estúdio do jogador** mostra os mesmos assets usados nas partidas, com rotação/zoom, exame do rosto, aparência, pausa, reinício e seleção de quadro em passos de 1/30 s.

| Ação | Clipe | Origem |
|---|---|---|
| Repouso | Football_Idle | Projeto |
| Corrida, arrancada e entrada | Football_Run / Sprint / Walk | Quaternius, CC0 |
| Condução | Football_Dribble | Projeto |
| Passe por dentro | Football_Pass | Projeto |
| Chute com peito do pé | Football_Shot | Projeto |
| Cruzamento | Football_Cross | Projeto |
| Cabeceio com salto | Football_Header | Projeto |
| Prontidão e encaixe | Football_KeeperReady / Catch | Projeto |
| Defesas para os dois lados | Football_SaveLeft / SaveRight | Projeto |
| Comemoração | Football_Celebrate | Projeto |

As onze coreografias originais são amostradas em trilhas de ossos a 30 Hz e exportadas para o GLB. O corpo tem 6.568 triângulos; cabelos e luvas são acessórios adicionais. Cada cabelo é uma única malha para reduzir chamadas de desenho. O pacote fica abaixo de 1,5 MB. Três estilos ficam no arquivo, mas só um aparece por atleta.

## Partidas e sincronização

Novos snapshots usam `positional-v3`. O servidor distingue condução, passe, cruzamento, cabeceio, chute, prontidão, encaixe e defesas laterais. `actionSecond` marca o contato; o renderer antecipa o preparo do movimento conforme o offset do clipe. A defesa se prepara antes da chegada da bola. Cruzamentos têm trajetória aérea e podem gerar cabeceios. Os resultados continuam decididos pelo servidor.

O relógio visual é absoluto: pausar ou buscar um instante amostra a mesma pose, sem acumular animações de perna por frame. O replay começa em 30×; selecione **1×** para examinar movimentos em segundos reais. A linha do tempo aceita passos de um segundo de jogo; o estúdio permite 1/30 s.

`positional-v1` e `positional-v2` continuam despachados para os motores congelados. A adaptação visual de ações genéricas antigas não recalcula placares nem eventos. O novo motor mantém disciplina, lesões, substituições e estatísticas.

## Fonte e direitos

Fontes editáveis: `scripts/assets/human-footballer.mjs` e `football-clips.mjs`. Reconstrução: `python scripts/assets/fetch-player-source.py` e `node scripts/assets/build-footballer.mjs`. O download da base verifica seu hash antes do uso. Depois de alterações visuais, revisar e atualizar o hash do GLB no manifesto.

Rig e três clipes de locomoção: [Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html), CC0. O texto recebido acompanha `SOURCE-LICENSE-CC0.txt`. Geometria humana e onze coreografias são arte original do projeto; o manifesto não concede direitos adicionais sobre elas. `LICENSE.txt` explica o escopo de cada componente. Sem novas dependências, fotografias ou samples externos nesta etapa.

## Verificação e alcance

- Build TypeScript/Vite e build .NET sem erros.
- Teste de GLB: 14 clipes, ossos/trilhas válidos, posições finitas, deformação limitada, limite de chão, salto do cabeceio, chute distinto e defesas opostas. Esqueletos independentes, acessórios, tamanho, hash e créditos conferidos.
- Teste do motor: oito sementes com resumo/replay consistentes, todas as ações específicas, bola aérea, timestamps, determinismo e dispatch v2; a suíte existente também cobre v1 e temporada completa.
- Suítes de API, integração HTTP de temporada e inspeção no navegador.

A direção final é estilizada. Regras físicas completas, carrinhos, colisões, quedas, variações de pé esquerdo e motion capture não fazem parte desta biblioteca. O perfil Leve conserva fallback geométrico; os demais usam o humano. Medições locais de render não certificam aparelhos reais. A situação dos elencos e escudos de 2026 segue separada em `VISUAL-AND-REAL-DATA.md`.
