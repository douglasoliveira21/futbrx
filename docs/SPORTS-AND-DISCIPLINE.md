# Gestão esportiva e disciplina — versão 0.4

Regras de um jogo demonstrativo, sem compromisso com o regulamento oficial de uma competição.

## Seleção e banco

Uma partida exige onze titulares disponíveis; o banco automático contém até doze reservas. Lesão e suspensão impedem participação. A escalação automática preserva titulares aptos quando recompõe indisponíveis, ou seleciona novamente quando acionada pelo treinador. Sem goleiro apto, um atleta de linha atua improvisado com habilidade reduzida. Caso o clube do usuário não tenha onze disponíveis, é necessário recompor o elenco antes de avançar. A IA pode convocar reservas temporários de habilidade 45 para não bloquear a rodada; eles não são contratações persistentes.

Na tela Táticas, programe até cinco trocas com minuto de 1 a 89, saída de um titular e entrada de um reserva. Um atleta só pode aparecer uma vez em cada papel; goleiro só pode ser trocado por goleiro no plano. O servidor valida a escalação, reserva, limite e revisão da carreira. Os planos são consumidos na rodada seguinte; mudar titulares ou usar escalação automática limpa os planos anteriores.

Trocas programadas acontecem na primeira oportunidade sem bola em voo depois do minuto desejado. Trocas automáticas opcionais atendem lesões e avaliam desgaste aos 60/70/80 minutos quando a bola não está em voo. Ambas compartilham o limite de cinco. Não há regra de três janelas de substituição. Trocas que perderem o atleta ou a reserva são canceladas, sem reinserir expulsos. Uma lesão sem troca reduz a equipe; um atleta de linha pode assumir o gol. Não há retorno de substituídos.

## Disponibilidade

Cada rodada representa sete dias. Lesões antigas recuperam sete dias ao final da rodada. Lesões novas recebem 7, 14, 21 ou 28 dias e começam a recuperar na rodada seguinte. O motor limita a uma lesão por equipe por partida; menor condição aumenta o risco. São eventos fictícios, não uma simulação clínica.

Três amarelos acumulados ou uma expulsão geram uma partida de suspensão. O contador de amarelos volta ao restante módulo três. Segundo amarelo contabiliza dois amarelos e um vermelho no jogo. Uma mesma partida gera no máximo um novo jogo de punição, mesmo combinando acúmulo e expulsão. Punições antigas são cumpridas na rodada; punições novas valem a partir da próxima partida. Agentes livres não cumprem suspensão enquanto sem clube. O ano seguinte zera amarelos, preserva lesões e suspensões.

Faltas e cartões são probabilísticos e determinísticos pela seed. O motor impede novas remoções abaixo de sete jogadores ativos; não há encerramento por W.O., acréscimos, impedimento, colisões físicas ou arbitragem completa. Goleiros não são escolhidos como infratores na rotina simplificada de faltas.

A partir da versão 0.5, a carga do microciclo altera a condição antes da partida. Ao final, quem joga recebe 12 pontos de recuperação e perde 0,16 por minuto; quem não joga recebe 18. A condição fica entre 25 e 100. O treino por atributo e os modelos de recuperação estão descritos em [TRAINING-AND-DEVELOPMENT.md](TRAINING-AND-DEVELOPMENT.md).

## Estatísticas e replay

A partida persiste estatísticas individuais junto do resultado, incluindo jogadores substituídos. Gols, chutes, passes completos, interceptações, faltas e cartões vêm dos mesmos eventos que geram o replay; minutos usam os ticks efetivamente ativos e são arredondados para baixo. O agregado da temporada acompanha o atleta pelo ID; após transferência, o clube exibido é o último clube pelo qual ele atuou e os números incluem sua temporada inteira. Não há assistências ou divisão dos agregados por passagem em clubes.

O arquivo anual preserva estatísticas e a rodada inicial de cobertura. Saves anteriores começam a contabilizar no primeiro avanço com a versão nova. Partidas antigas permanecem inalteradas; seus números individuais não são reconstruídos. Replays `positional-v1` usam `LegacyPositionalV1`, enquanto novos snapshots usam `positional-v2`. Frames contêm identidade e estado ativo de cada posição; o cliente oculta jogadores removidos e usa o substituto no mesmo espaço de renderização. O replay não altera saúde, punições ou estatísticas da carreira.

## API autenticada

- `POST /api/careers/{id}/sports/plan`: `revision`, `substitutions` (minute/outPlayerId/inPlayerId), `autoSubstitute`, `autoReplaceUnavailable`.
- `POST /api/careers/{id}/sports/lineup/auto`: `revision`.
- `GET /api/careers/{id}/sports/statistics?sort=goals|minutes|passes|cards&club=pal`: até 100 atletas, temporada e `fromRound`.

Apenas o dono acessa sua carreira. Escritas usam a mesma revisão concorrente e auditoria das negociações. As alterações são campos do JSON de carreira, sem migração relacional adicional. PostgreSQL/containers e publicação pública continuam sem validação neste ambiente; testes locais usam EF InMemory.
