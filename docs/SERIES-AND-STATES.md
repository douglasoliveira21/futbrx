# Clubes por série e estado

Estado desta etapa (23/11/2026): o jogo tem 156 clubes reais das Séries A a D de 2026, a escolha de clube
filtra por divisão, estado e nome, e as Séries A, B, C e D são jogáveis.

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

## Mata-mata de ida e volta (etapa 2)

`ClubCompetition.Legs` (1 ou 2) e `LegGap` (semanas entre ida e volta) definem o formato; o padrão segue em jogo único.

- Cada confronto gera duas partidas (`CupMatch.Tie`, `Leg` 1 e 2): a volta fica na semana seguinte, com o mando invertido.
- A ida pode terminar empatada, sem pênaltis. A volta decide e soma a ida (`MatchSetup.AggregateLead`): o motor só
  cobra pênaltis se o agregado estiver empatado. Não há gol fora de casa.
- O vencedor é gravado nas duas partidas; a fase só avança (ou o campeão só é definido) quando todos os confrontos
  estão decididos. Transmissão, tela inicial e aba de copas mostram "Ida"/"Volta" e o agregado.
- O Estadual demonstrativo passou a ser em ida e volta; a Copa do Brasil continua em jogo único.

Ainda não existe: estaduais reais e uma tela de calendário agrupada por competição. A simulação leve de jogos de
outros clubes existe na Série D.

## Série D (jogável)

Formato (regulamento de 2026, lido na página da edição; fases e grupos ainda a conferir):

- 96 clubes em 16 grupos de 6, ida e volta, 10 rodadas. Os 4 primeiros de cada grupo (64 clubes) avançam.
- Mata-matas de ida e volta em seis fases (64, 32, 16, quartas, semifinais e final), uma por quinzena: semanas 11 a 22.
- Sobem 6 para a Série C: os 2 finalistas, os 2 semifinalistas eliminados e os 2 vencedores da repescagem entre os
  4 eliminados nas quartas (repescagem de ida e volta, jogada junto com as semifinais).
- Não há rebaixamento; início em abril e fim em setembro (o jogo usa as mesmas semanas das outras séries).

Como o jogo faz isso (`backend/src/Simulation/SerieD.cs`):

- Cada grupo é uma versão de dados com seis clubes (`serie-d-2026-grupo-01-v1` a `-16-v1`); a versão de cada clube vem
  no diretório (`group`, `version`). O catálogo da Série D tem seis clubes em vez de vinte.
- **Grupos formados pelo projeto**, por proximidade geográfica (ordem dos estados do norte ao sul), porque os grupos
  oficiais ainda não foram lidos. Trocar para os oficiais é só reordenar a lista no diretório.
- A carreira joga o grupo de seis (`SeasonCalendar` gera 10 rodadas para seis clubes). Os outros 90 clubes existem só
  como nome e força (`GameState.SerieD.Others`); **os elencos deles só são criados quando o clube enfrenta o
  adversário** (`SerieD.EnsureSquad`), então o save não cresce.
- Ao terminar o grupo, os outros 15 grupos são simulados de uma vez pela força dos clubes, guardando só a tabela
  final; os 64 classificados entram em ordem de campanha (melhor contra pior, chave padrão).
- Jogos do mata-mata entre outros clubes (`ClubCompetition.Light`) saem de gols sorteados pela força dos dois, sem
  elencos nem transmissão, com agregado e pênaltis como nos demais mata-matas. Só os jogos do clube são transmitidos.
- Clube eliminado passa as semanas restantes com "Avançar semana" até o fim da temporada.
- Economia em 10% da Série A; meta da diretoria: ficar entre os 4 primeiros do grupo. Tela de Competições mostra as
  16 tabelas e quem subiu.

Limites: o acesso e o descenso entre séries ainda não mudam a divisão do clube na temporada seguinte (a carreira
recomeça no mesmo grupo), a força dos clubes ainda não altera os elencos, e a Série D não disputa Copa do Brasil nem
copas continentais; o Estadual continua demonstrativo.
