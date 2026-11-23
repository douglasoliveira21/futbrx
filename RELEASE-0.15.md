# 0.15 — Um clube vivo

Os bastidores agora afetam a carreira. As decisões são calculadas na API e salvas no snapshot da conta; alterar uma tela não altera resultados ou replays anteriores.

| Sistema | Comportamento implementado |
|---|---|
| Diretoria | Meta conforme o clube, confiança por resultados e posição, teto salarial e orçamento de transferências, cobranças formais, pedido de aporte, demissão e contratação por outro clube. |
| Jogadores | Personalidade, moral, ambição, liderança, relacionamento, capitão e conversas. Prometer espaço exige 120 minutos em três rodadas; cumprir ou descumprir muda a relação. Moral e análise técnica entram no próximo snapshot da equipe. |
| Empresários | Agências fictícias, ambição influenciando salário, comissão calculada pelo servidor, revisão antes da confirmação e lançamento separado no caixa. Propostas e contrapropostas existentes continuam sendo usadas. |
| Médico | Tratamentos padrão, conservador e intensivo, custo, tempo de recuperação e risco simulado de recaída após retorno. Preparação física e fisioterapia influenciam condição e risco. |
| Base | Sub-15, Sub-17 e Sub-20, peneiras de quatro candidatos, captação paga, progressão por idade e promoção a partir dos dezesseis anos. Mantida a captação regional anterior. |
| Olheiros | Observação de profissionais em duas rodadas, relatórios com estimativas de potencial e precisão conforme o especialista. O potencial exato fica no servidor; o intervalo inclui incerteza e viés. |
| Finanças | Bilheteria variável, ingresso configurável, três opções de patrocínio com metas, salários, operação, comissão técnica, premiações e dívida em doze parcelas com custo total explícito. O ledger registra as movimentações. |
| Torcida | Popularidade, apoio, sequência de derrotas, protestos e rival gerado nesta carreira. Rivalidades ampliam repercussão; popularidade influencia receita. A apresentação visual mantém arquibancadas cheias. |
| Imprensa | Coletivas com respostas, repercussão dos resultados, notícias médicas e da diretoria e rumores ligados a negociações realmente abertas na carreira. |
| Competições | Liga existente e mata-matas demonstrativos de estadual, Copa do Brasil e Libertadores ou Sul-Americana. Sorteios, mandos, fases, resultados, campeão, prêmio e replays próprios. |
| Calendário | Pausas de seleções geradas, viagens estimadas, remarcação logística e desgaste por viagem e pouco descanso. Datas são da temporada simulada, não uma reprodução do calendário oficial. |
| Comissão | Auxiliar, preparador, analista, fisioterapeuta, olheiro e coordenação da base, com qualidade, salários, contratação e troca de profissionais. |

## Transmissão das copas

Partidas eliminatórias do treinador entram antes do próximo jogo da liga. Concluir uma copa não adianta a rodada do Brasileirão. Não há atalho para o resultado: velocidade, banco, táticas e intervalo continuam na transmissão obrigatória.

O motor V8 mantém o futebol do V7 e acrescenta disputas de pênaltis calculadas à medida que o relógio avança. As cobranças têm bola, cobrador, goleiro, animação e placar próprio. Gols de disputa não contam como gols do tempo regulamentar. Estatísticas de copa ficam nos seus jogos e não contaminam a artilharia da liga. Replays V1–V7 continuam em seus respectivos motores.

## Saves e continuidade

A migração conserva elencos, contas, caixa e partidas disputadas. Novas copas só começam se sua primeira fase ainda estiver disponível; uma carreira já avançada pode receber o estadual apenas na temporada seguinte. Partidas já iniciadas continuam em sua versão original. Aposentar um treinador não apaga o save: ele pode assumir outro clube. Elenco, base e economia do clube anterior são preservados.

O início local agora abre o Vite diretamente pelo Node, sem depender da política de execução de scripts do Windows PowerShell. URL: http://127.0.0.1:5173/ .

## Escopo dos formatos

As copas são formatos simplificados do jogo, não regulamentos oficiais completos. O estadual é um circuito de quatro clubes disponíveis no catálogo; a Copa do Brasil usa dezesseis; o circuito continental usa oito, incluindo quatro clubes internacionais fictícios. A classificação continental inicial usa a força do clube e, nas temporadas seguintes, a posição anterior. Disciplina e suspensões ainda compartilham regras simplificadas entre as partidas.

Rival, distâncias de viagem, agências, profissionais e notícias são gerados na carreira. Não representam fatos ou relações oficiais. Ainda não há um ecossistema completo de imprensa livre ou negociação individual com cada agente. Os escudos e elencos reais pendentes da versão anterior continuam pendentes; esta versão não concede licenças em nome de terceiros.

## Validação

Testes cobrem persistência, isolamento entre contas, revisão de ações, encargos calculados no servidor, orçamento, promessas, contratação, tratamentos, peneiras, potencial oculto, demissão, novo emprego, copas, pênaltis incrementais e replays. A bateria anterior foi ajustada para conferir as novas comissões e as receitas pelo ledger.
