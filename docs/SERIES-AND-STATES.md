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

- Séries B e C têm formatos próprios, o campeonato estadual segue o estado do clube e o acesso e o descenso entre as
  séries existem (ver abaixo). O regulamento das federações estaduais não é reproduzido.
- A força dos clubes molda as notas dos elencos (ver "Força dos clubes nos elencos"); o restante do jogo (caixa,
  diretoria, mercado de técnicos) já a usava.

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
- **Grupos oficiais A1 a A16**, lidos das tabelas da página da edição de 2026 (campo `group` no diretório; o número do
  grupo é o número oficial). Se o diretório perder o campo, o jogo volta a formar grupos por proximidade geográfica.
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

## Formatos de B e C (edição de 2026)

`SeriesFormats` (`backend/src/Simulation/SeriesFormats.cs`):

- **Série B**: 38 rodadas; ao fim, dois playoffs de ida e volta (3º×6º e 4º×5º, semanas 39 e 40), com a segunda partida
  na casa do mais bem colocado. Empate no agregado dá a vaga ao mais bem colocado: não há prorrogação nem pênaltis.
- **Série C**: primeira fase em turno único (19 rodadas); os 8 melhores formam dois grupos de 4 (1º, 4º, 5º, 8º e 2º, 3º,
  6º, 7º) em ida e volta (semanas 20 a 25); os dois primeiros de cada grupo sobem e os líderes decidem a final em ida e
  volta (semanas 26 e 27). A página não diz como o agregado empatado da final é decidido; o jogo usa pênaltis.
- Calendário da carreira: A 38 semanas, B 40, C 27, D 22 (`SeriesFormats.SeasonRoundsFor`). Fases em grupos usam
  `ClubCompetition.GroupStage` (todos contra todos, sem vencedor por jogo; a tabela decide).

## Campeonato estadual

Carreiras com dados reais disputam o campeonato do **estado do clube** (`SeriesFormats.CreateState`): oito clubes da base
daquele estado (os mais fortes; estados com poucos clubes na base completam com os dos estados mais próximos), turno
único (semanas 2 a 8), semifinais e final em ida e volta entre os quatro primeiros (semanas 9 a 12). Os demais clubes
não têm elenco: seus jogos saem da força (`Light`) e o elenco de um adversário só é criado quando o clube o enfrenta
(`GameState.KnownClubs`). O formato é simplificado e **não reproduz o regulamento de cada federação**; carreiras
demonstrativas continuam com o circuito genérico de quatro clubes.

## Premiação da Copa do Brasil (cotas da CBF para 2026)

Valores informados pelo usuário a partir das cotas divulgadas pela CBF (cumulativas por fase; em R$ mi):

| Fase | Série A | Série B | Séries C, D e outros |
|---|---|---|---|
| 1ª fase | — | — | 0,4 |
| 2ª fase | — | 1,38 | 0,83 |
| 3ª fase | — | 1,53 | 0,95 |
| 4ª fase | — | 1,68 | 1,07 |
| 5ª fase | 2 | 2 | 2 |
| Oitavas | 3 | 3 | 3 |
| Quartas | 4 | 4 | 4 |
| Semifinal | 9 | 9 | 9 |
| Vice-campeão | 34 | 34 | 34 |
| Campeão | 78 | 78 | 78 |

O jogo disputa só os 16 últimos clubes. A cota de cada fase é paga ao clube do jogador quando ele decide o seu jogo da
fase (`ClubCompetition.StagePrizes`, `RunnerUpPrize` e `Prize`); na primeira partida o clube recebe também as cotas das
fases anteriores que a série dele teria disputado (A: 5ª fase; B: 2ª a 5ª; demais: 1ª a 5ª). Tudo é cumulativo: o campeão
da A recebe 96 mi, da B 100,59 mi e da C 99,25 mi; o vice da A, 52 mi. A Série D ainda não disputa a Copa do Brasil.
Os demais prêmios (libertadores, sul-americana, estadual, liga e Série D) seguem com valores do projeto, iguais para todas
as séries; a liga paga `(21 − posição) × 500 mil`.

## Premiações da temporada

Tabelas informadas pelo usuário (as de liga são propostas de balanceamento do jogo, não pagamentos oficiais):

- **Liga**: Série A por posição, de R$ 54 mi (1º) a R$ 13 mi (16º), nada do 17º ao 20º; Série B de R$ 6 mi (campeão) a R$ 600 mil (8º). Série C paga por fase (grupo: R$ 900 mil aos
  dois primeiros e R$ 350 mil aos outros dois; final: campeão R$ 2 mi no total, vice R$ 1,5 mi) e Série D pelo mata-mata (campeão R$ 800 mil, vice R$ 500 mil, semifinalistas R$ 275 mil, eliminados nas
  quartas R$ 93,75 mil). Carreiras demonstrativas mantêm a tabela antiga.
- **Supercopa**: campeão R$ 11,64 mi, vice R$ 6,35 mi, entre o campeão do Brasileirão e o da Copa do Brasil da temporada anterior (só aparece quando o clube é um dos dois).
- **Copa do Nordeste** (clubes do Nordeste) e **Copa Verde** (Norte, Centro-Oeste e ES): mata-mata de oito clubes, com a cota de participação do grupo do ranking nacional (A = grupo 1 ... D = grupo 4),
  cotas por fase, vice e campeão. Máximo acumulado: R$ 4,6 mi e R$ 950 mil.
- **Estaduais**: campeão e vice por estado (R$ 5 mi/1,65 mi no Paulista, até R$ 10 mi/5,5 mi no Carioca, ... R$ 50 mil/25 mil no Amapaense) e cota de participação dos clubes da Série A (Paulista R$ 35 mi;
  Carioca R$ 8,3 mi, ponto médio de R$ 6,6 a 10 mi). O Ceará não estava na tabela fornecida e usa R$ 400 mil/200 mil (estimativa).
- **Libertadores**: Pré R$ 2 mi, 2,5 mi e 3 mi por fase; grupos R$ 15 mi + R$ 1,7 mi por vitória; oitavas R$ 6,25 mi; quartas R$ 8,5 mi; semifinal R$ 11,5 mi; vice R$ 35 mi; campeão R$ 125 mi.
- **Sul-Americana**: grupos R$ 4,5 mi + R$ 625 mil por vitória; playoffs R$ 2,5 mi; oitavas R$ 3 mi; quartas R$ 3,5 mi; semifinal R$ 4 mi; vice R$ 12,5 mi; campeão R$ 50 mi. A primeira fase (só para
  outros países) e a pré-temporada de outros clubes não existem no jogo.

## Vagas internacionais (Libertadores, Pré-Libertadores e Sul-Americana)

Motor central em `Qualification.cs` (função pura `Resolve`), com o regulamento por temporada em `Qualification.Rules` (2026: 4 vagas diretas pela liga, 1 vaga na Pré, 6 na Sul-Americana, campeões
continentais com vaga adicional). Resolve, no fim de cada temporada de uma carreira na Série A, a partir da tabela final, do campeão e do vice da Copa do Brasil e dos campeões continentais:

- vagas diretas do 1º ao 4º; campeão da Copa fora desse grupo ocupa vaga direta; se já estava no grupo, a vaga direta da Copa passa ao vice (e a vaga do vice na Pré vai à tabela);
- campeão continental que já tem vaga libera a vaga ao próximo da tabela (a zona se expande, do G5 ao G9); fora do grupo, ocupa vaga adicional;
- Pré-Libertadores: 5º colocado (ou o próximo elegível) e vice da Copa; Sul-Americana: os seis melhores seguintes, sempre ignorando quem já está classificado;
- clubes rebaixados (quatro últimos) não participam; ninguém ocupa duas vagas (testado em 400 combinações aleatórias).

A origem de cada vaga fica em `GameState.Qualification` (vagas da temporada e histórico) e é mostrada na tabela da Série A: cores (verde-escuro Libertadores, verde-claro Pré, azul Sul-Americana, cinza
permanência, vermelho rebaixamento) e uma estrela com a origem para quem conquistou a vaga fora do campeonato. O treinador é notificado ao garantir a vaga, quando a vaga de outro clube é remanejada e
quando a classificação é garantida matematicamente (G4, G5 e G11). Na primeira temporada, sem resultados anteriores, as vagas seguem a força dos clubes. Quem acabou de subir à Série A não tem vaga.

Competições (`Continental.cs`): Pré-Libertadores (três fases de jogo único; o vencedor vai aos grupos da Libertadores e o eliminado na terceira fase à Sul-Americana), fase de grupos (grupo de quatro com
adversários ilustrativos de outros países; os outros sete grupos saem da força dos clubes), mata-mata (final em jogo único) e, na Sul-Americana, playoffs contra terceiros da Libertadores, com os líderes
entrando nas oitavas; o terceiro colocado do grupo da Libertadores é transferido aos playoffs da Sul-Americana. Limites: os adversários estrangeiros são fictícios, não há sorteio por ranking
continental nem Recopa, e os bônus financeiros de classificação de contratos não existem no jogo.

## Força dos clubes nos elencos

`Game.Players(offset, strength)` desloca a nota dos atletas pela força do clube em relação a 70 (a Série A, que fica como
era): em média A 72, B 68, C 58 e D 44; dentro de uma série o clube mais forte tem o elenco melhor. Vale para os
catálogos das séries, para elencos criados depois (adversários do estadual, Série D, mudança de divisão) e para o
elenco inicial do clube do jogador. A base demonstrativa não muda.

## Acesso e descenso entre as séries

`SeriesMovement` (`backend/src/Simulation/SeriesMovement.cs`), chamado em `TransferMarket.NextSeason` com a classificação
final, move só o **clube do jogador**; os demais clubes da divisão são os da base de dados de cada série.

- Série A: os 4 últimos (17º a 20º) caem para a B. Série B: os 2 primeiros sobem direto e os vencedores dos playoffs
  3º×6º e 4º×5º também; caem os 4 últimos. Série C: sobem os 2 primeiros de cada grupo da segunda fase (4 clubes) e
  caem os 2 últimos da primeira fase. Série D: sobem os 6 do mata-mata (ver acima); não há descenso.
  Fonte: páginas das edições de 2026 (Wikipédia, lidas em 24/11/2026); a quantidade de rebaixados da Série A (4) foi
  confirmada pelo usuário, porque o texto lido da página não a informa. Na B, o 1º colocado é o campeão e os quatro
  acessos são: 1º, 2º e os dois vencedores dos playoffs (3º×6º e 4º×5º).
- Quem sobe toma o lugar do clube mais fraco da nova divisão; quem cai toma o lugar do mais forte. Se o clube volta a
  uma divisão da qual saiu, retoma a própria vaga.
- Quem cai para a D entra no grupo oficial do seu estado (o grupo com mais clubes do mesmo estado; se o clube já era da
  D, volta ao próprio grupo).
- A carreira passa à versão de dados da nova divisão (20 clubes e 38 rodadas; na D, 6 clubes e 22 semanas), os elencos da
  divisão antiga são descartados e os dos novos adversários são criados. Copas, rival, meta da diretoria e economia dos
  adversários seguem a nova divisão. Rebaixamento tira 20 pontos de confiança da diretoria.
- Carreiras demonstrativas e bases importadas por administradores não têm escada de divisões.

Limites: o regulamento de cada federação estadual não é reproduzido, e a Série D não disputa Copa do Brasil nem
copas continentais; o Estadual continua demonstrativo.
