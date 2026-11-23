# Treino semanal e desenvolvimento — versão 0.5

Modelo demonstrativo. Cada rodada representa uma semana de seis sessões e uma partida; não é um calendário oficial por datas. Configurações, XP, atributos e relatórios pertencem ao snapshot JSON da carreira.

## Planejamento

Sessões: Passe, Finalização, Defesa, Físico, Goleiro, Recuperação ou Descanso. Cada plano contém seis dias e pelo menos um de Recuperação ou Descanso. A intensidade global é Leve, Normal ou Alta. O plano se repete até ser alterado. Modelos técnico, físico e recuperação preenchem o editor e precisam ser salvos.

O foco individual pode seguir a Equipe ou redirecionar todas as sessões de atributo para um atributo específico. Ele nunca substitui descanso/recuperação. Apenas goleiros recebem foco individual Goleiro. Quando seguem a equipe, goleiros convertem sessões de Finalização e Defesa em treino de Goleiro. Sessão coletiva de Goleiro não concede XP a jogadores de linha; ainda conta como carga do dia. Para um grupo de goleiros, prefira o foco individual sem retirar treino dos atletas de linha.

A rota antiga `actions` com `type: training` continua aceitando Técnico, Físico e Recuperação e seleciona o modelo correspondente. O plano personalizado aparece no campo legado como Personalizado.

## Aplicação e evolução

O treino é aplicado uma vez por temporada/rodada, após validar disponibilidade e antes de simular. Condição e habilidade treinadas entram no snapshot da partida. Requisições com revisão antiga não repetem a rodada; o estado também guarda a última semana aplicada. Consultas, escolha de foco e salvamento do plano não concedem XP nem recuperação.

Os cinco atributos começam iguais à habilidade existente do jogador. Isso conserva a base de dados e não inventa características detalhadas anteriores. Cada sessão de atributo concede XP com esta regra:

`arredondar(pontosPorIdade × multiplicadorDeIntensidade × (0,5 + condiçãoInicial/200))`, mínimo 1.

- Idade até 21: 8 pontos; 22–26: 6; 27–30: 4; acima de 30: 2.
- Leve: multiplicador 0,75; Normal: 1; Alta: 1,25.
- A condição usada é a do início da semana, para todas as sessões.
- Cada 100 XP concede +1 no atributo e conserva o restante. No teto de 99, XP daquele atributo fica em zero.

A evolução da habilidade geral corresponde à diferença da média ponderada dos atributos antes/depois dos ganhos, com arredondamento para o inteiro mais próximo. Pesos somam dez:

| Posição | Passe | Finalização | Defesa | Físico | Goleiro |
| --- | ---: | ---: | ---: | ---: | ---: |
| GOL | 1 | 0 | 1 | 2 | 6 |
| ZAG | 2 | 1 | 5 | 2 | 0 |
| LAT | 3 | 1 | 3 | 3 | 0 |
| MEI | 5 | 2 | 1 | 2 | 0 |
| ATA | 2 | 5 | 1 | 2 | 0 |

Um ganho de atributo pode não mudar imediatamente a habilidade geral, devido aos pesos e ao arredondamento. O motor posicional v2 recebe a habilidade geral e a condição; ainda não usa cada atributo diretamente para sua respectiva ação. Valor de mercado e salário não são recalculados pelo treino.

## Carga e disponibilidade

Antes da partida, cada dia Descanso repõe 2 de condição e Recuperação repõe 1. Cada sessão de atributo custa 1 em Leve, 2 em Normal ou 3 em Alta. O resultado fica entre 25 e 100. A recuperação após o jogo é separada: +12 para quem jogou, menos 0,16 por minuto; +18 para quem não jogou. Carga elevada pode exigir semanas de recuperação para evitar desgaste contínuo.

Atletas lesionados no início da semana não recebem XP nem carga; seguem a regra de recuperação de lesão ao final da rodada. Suspensos treinam normalmente. Uma lesão ocorrida no jogo não desfaz as sessões já realizadas antes da partida. O treino não cria lesões diretamente; a condição resultante participa do risco de lesão já existente no motor v2.

A partir da versão 0.6, jovens da base seguem o plano e o foco individual do clube. Eles aparecem com grupo Base nos relatórios e preservam atributos/XP quando promovidos. Não disputam partidas; recebem a recuperação prevista para quem não joga.

Adversários usam o plano Técnico/Normal e evoluem pela mesma regra, sem focos individuais. Agentes livres têm perfis, mas não treinam. Transferências conservam atributos e XP; o foco salvo acompanha o atleta, mas só é usado quando o clube é controlado pelo usuário. A virada do ano preserva perfis e relatórios, aumenta idade pela regra já existente e permite o treino da nova temporada.

## Relatórios e compatibilidade

São preservados os últimos doze relatórios do clube, incluindo temporada, rodada, plano aplicado e dados individuais. Condição antes/depois refere-se ao treino pré-jogo, sem o desgaste posterior da partida. Relatórios não mudam se o plano futuro for editado; nomes de atletas que saíram permanecem no histórico do clube.

Saves anteriores inicializam perfis a partir da habilidade atual e preservam o antigo foco de treino por meio de um modelo. Nenhum treino, ganho ou relatório é gerado retroativamente. Partidas anteriores guardam snapshots próprios e não são recalculadas. `positional-v1` e `positional-v2` permanecem preservados; esta versão modifica preparação e entradas de novas partidas, sem alterar o algoritmo de replay.

Ainda faltam potencial individual, especialidades, comissão técnica, instalações, declínio de atributos por idade e calendários reais. O teto é comum a todos e o único efeito atual da idade no treino é a velocidade de XP.

## API

Todas as rotas são autenticadas e restritas ao dono da carreira:

- `GET /api/careers/{id}/training/`: plano, opções, modelos, perfis do próprio elenco, relatórios e revisão.
- `POST /api/careers/{id}/training/plan`: `{ revision, plan: { days: [seis sessões], intensity } }`.
- `POST /api/careers/{id}/training/players/{playerId}/focus`: `{ revision, focus }`.

XP, atributos e condição enviados pelo cliente são ignorados; apenas plano e foco são aceitos. Escritas usam revisão concorrente e auditoria. Não há migração relacional adicional. Validação local usa EF InMemory; execução contra PostgreSQL e publicação permanecem pendentes.
