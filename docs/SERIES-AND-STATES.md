# Clubes por série e estado

Estado desta etapa (23/11/2026): o jogo tem 156 clubes reais das Séries A a D de 2026, a escolha de clube
filtra por divisão, estado e nome, e as Séries A, B e C são jogáveis. A Série D está listada, mas ainda não é jogável.

## Dados

`data/real-2026/series-2026.json` é o diretório. Cada clube tem `id`, `series`, `name`, `city`, `uf`, `short`,
`color`, `strength` e a origem desses atributos (`attributes`). Fontes registradas no próprio arquivo:

- Série A: lista da CBF já conferida em `clubs.json`; cidades e estados a conferir.
- Séries B, C e D: tabelas de participantes da Wikipédia (fatos públicos), pesquisadas em 23/11/2026.
- Série D de 2026 tem **96 clubes** (ampliada de 64), em 16 grupos de 6.
- Pendente de conferência: linhas marcadas com `check` (lidas de resumo automático), nomes oficiais completos, grupos
  A1 a A16 da Série D e as cores e forças das Séries B, C e D (estimativas do projeto).

Regras de id: até 20 caracteres (limite do catálogo). A Série A mantém os ids já usados (`pal`, `fla`...).

## O que o jogo faz com isso

- `GET /api/directory` entrega o diretório e as versões de dados de cada série (`versions`).
- Séries B e C têm catálogos próprios (`serie-b-2026-clubes-reais-v1`, `serie-c-2026-clubes-reais-v1`), semeados na
  subida da API com data de publicação antiga: a versão mais recente continua sendo o padrão de novas carreiras, e as
  bases B e C são escolhidas de forma explícita na tela de escolha de clube.
- Tela de escolha: divisão, estado e busca por clube ou cidade; bases importadas por administradores ficam em
  "Outras bases de dados".
- Economia por série (`Game.EconomyFactor`): caixa inicial e caixa dos adversários em 100% na A, 45% na B e 20% na C.
- Meta da diretoria nas Séries B e C: pela força do clube dentro da própria liga (os 4 mais fortes miram o acesso).
  A Série A segue a regra antiga por limites de força.
- Nome da liga na interface e na transmissão vem da base de dados da carreira (`leagueName`, `LeagueTitle`).
- Escudos: só os 18 com licença revisada (ver `RULES-MARKET-AND-2026.md`). Os demais usam o escudo genérico nas cores
  do clube.

## Limites atuais

- Séries B e C usam, por enquanto, o formato da Série A (38 rodadas, pontos corridos). Os formatos reais, o acesso e
  o descenso entre as séries e os estaduais ainda não existem.
- A força dos clubes de B e C só afeta diretoria, mercado de técnicos e caixa; o resultado das partidas vem das notas
  dos jogadores (elencos simulados, iguais para todos os clubes).

## Calendário por competição (etapa 1: fundação)

A rodada continua sendo a **semana** da temporada. Cada competição ocupa algumas semanas e o clube pode ter zero,
um ou dois jogos em uma mesma semana (como já acontecia com as copas).

- `GameState.SeasonRounds` (padrão 38) é o tamanho da temporada; `GameState.LeagueStart` (padrão 1) é a semana em que
  a liga principal começa. Saves antigos não guardam os campos e seguem com 38 e 1. A API expõe os dois no estado.
- `SeasonCalendar.Generate(..., startRound)` cria a liga em qualquer semana e `SeasonCalendar.DateOfRound` dá a data de
  uma semana sem depender de existir jogo de liga nela (usada por copas e pela carreira de técnico).
- Os `38` e `39` que eram "tamanho da temporada" e "pré-temporada" passaram a `SeasonRounds` e `SeasonRounds + 1`.
- Semana sem jogo do clube (estadual em outra semana, liga que ainda não começou, folga): `Game.AdvanceWeek` e
  `POST /api/careers/{id}/advance-week` avançam a semana sem transmissão, jogando os demais jogos. Se há jogo do clube
  (liga ou copa devida), o servidor recusa e exige a transmissão; a transmissão também recusa semana sem jogo.
- Na tela inicial, o botão vira "Avançar semana" quando o próximo jogo está em outra semana; a barra superior mostra
  "Rodada X de N" com N da carreira.

Ainda não existe: competições com formato próprio (grupos, mata-mata de ida e volta, estaduais reais), uma tela de
calendário agrupada por competição e a simulação leve dos jogos de outros clubes.

## Série D (desenho aprovado, ainda não implementado)

Formato lido na página da edição de 2026 (parte do regulamento; fases e grupos ainda a conferir):

- 96 clubes em 16 grupos de 6, por distribuição geográfica; ida e volta, 10 rodadas.
- Os 4 primeiros de cada grupo avançam (64 clubes) para mata-matas de ida e volta até a fase final.
- Sobem 6 para a Série C: os 2 finalistas, os 2 semifinalistas e 2 vencedores de playoffs entre eliminados nas quartas.
- Não há rebaixamento: a cada ano os clubes entram pelos campeonatos estaduais e pelos rebaixados da Série C.
- Início em abril e fim em setembro.

Decisão (opção A): seguir o regulamento, com o grupo real de 6 clubes e mata-matas de ida e volta. Os outros 15 grupos
são simulados em segundo plano guardando só a classificação, e os elencos dos adversários são gerados sob demanda,
para o save não crescer. Depende de um **calendário por competição** (grupos, mata-mata de ida e volta, estaduais),
que é o próximo passo, seguido dos estaduais e da Série D.
