# Contratos e transferências · versão 0.3

## Fluxo de jogo

1. Consulte a proposta de valores no Mercado ou escolha Renovar na tela Contratos.
2. Ofereça taxa, salário por rodada e duração. Agentes livres e renovações exigem taxa zero.
3. Aguarde resposta ou contraproposta na aba Negociações. Propostas entre clubes são respondidas no próximo avanço de rodada; na rodada final da janela, a resposta é imediata. Renovações e agentes livres respondem imediatamente.
4. Confira os termos finais, a saída/entrada de caixa e o salário antes de Confirmar operação. Nenhuma quantia foi debitada antes dessa etapa.

O servidor determina o valor pedido, a demanda salarial, a resposta e as luvas. Valores oferecidos pelo treinador são propostas, não instruções para atribuir dinheiro ou determinar o resultado da negociação. Confirmar aceita apenas o ID da proposta e a revisão da carreira; campos financeiros adicionais são ignorados.

## Regras demonstrativas

- Janela entre clubes: rodadas 1–6 e 20–23. Agentes livres e renovações são permitidos a qualquer momento. Esses períodos são regras do jogo, não um calendário oficial da CBF.
- Duração: uma a cinco temporadas, contando a temporada atual. Um contrato de três temporadas assinado em 2026 termina ao final de 2028. Se a temporada já terminou (rodada 39), uma contratação precisa cobrir ao menos dois anos no seletor para alcançar o ano seguinte. Renovação precisa estender o vencimento atual; não é possível renovar antecipadamente além do limite de cinco temporadas.
- Salário é pago por rodada, não por mês. A confirmação muda a folha das rodadas seguintes. Não há pagamento retroativo de salário.
- Taxa pedida por clubes usa o valor estimado do atleta e seu status de titular. Propostas abaixo de 80% das exigências são recusadas; propostas intermediárias recebem contraproposta.
- Luvas: seis salários por rodada para agentes livres e quatro para transferências/renovações, com mínimo de R$ 50 mil. Os termos finais sempre são exibidos antes da confirmação.
- Propostas não reservam dinheiro. Na confirmação o servidor verifica novamente caixa, janela, propriedade do atleta, revisão do contrato e capacidade dos elencos. Uma proposta aceita pode não ser mais executável se os recursos mudaram.
- Uma negociação ativa por atleta. Para enviar novos termos, encerre a proposta anterior. Recusas e encerramentos não cobram dinheiro. Propostas encerradas continuam no histórico.
- Clubes não liberam jogadores se ficarem abaixo de 18 atletas, dois goleiros ou dezesseis jogadores de linha. O elenco de destino não pode ultrapassar 40 jogadores. Fim de contrato pode deixar o elenco abaixo desses mínimos; reforce-o antes de negociar novas saídas.
- Colocar à venda permite receber propostas de clubes com caixa e espaço. Não garante comprador. Retirar da lista não cancela propostas já recebidas: encerre-as na aba Negociações.
- Venda remove o atleta e transfere a taxa do comprador para o vendedor; o comprador também paga as luvas. Se um titular for vendido, o treinador deve ajustar sua escalação antes da próxima partida.
- Contratos vencem no encerramento da temporada. Sem renovação, o atleta sai sem taxa para o mercado livre. O caixa não é reiniciado. Os adversários renovam seus vínculos com uma política simplificada para manter a competição jogável.

## Histórico e compatibilidade

O campo `ManagementVersion=1` identifica a estrutura de gestão introduzida em 0.3. Ao carregar um save anterior, são inicializados contratos e caixas dos clubes adversários sem debitar o saldo do usuário. IDs adversários que conflitam em saves 0.1 são ajustados somente no elenco atual; snapshots de partidas já disputadas não são reescritos.

O catálogo continua independente: operações de carreira não alteram `DataReleases`, `ClubData` ou `PlayerData`. Contratos, propostas e arquivos históricos são persistidos no JSON da própria carreira, protegidos pela revisão de concorrência do EF. Não é necessária migração relacional adicional nesta versão. Não existem dados compartilhados de dinheiro/elenco entre carreiras.

Antes de iniciar o ano seguinte, `PastSeasons` guarda a classificação final e os 380 fixtures, com seeds, eventos e escalações usadas. O endpoint da visão geral retorna somente o resumo do arquivo; a tela Histórico baixa o ano selecionado. A reprodução usa o snapshot daquele ano, e não o elenco/tática atual. Anos já descartados por saves de versões anteriores não podem ser reconstruídos.

Novos lançamentos financeiros incluem `Season`. Lançamentos antigos sem ano permanecem identificados como Legado na tabela e não são atribuídos a um ano que não pode ser comprovado. O saldo não é alterado por essa adaptação.

## Rotas

Todas as rotas são autenticadas e filtram a carreira pelo usuário do JWT.

| Rota | Operação |
| --- | --- |
| `GET /api/careers/{id}/market` | Mercado paginado de 20 itens; filtros `search`, `position`, `club`, `maxFee`, `sort`, `page`. `club=__free` seleciona agentes livres. |
| `GET /api/careers/{id}/market/quote/{playerId}?kind=inbound&years=3` | Consulta os valores pedidos. `kind=renewal` consulta renovação. |
| `POST /api/careers/{id}/market/offers` | Envia `revision`, `playerId`, `fee`, `wage`, `years`. |
| `POST /api/careers/{id}/contracts/{playerId}/renew` | Envia `revision`, `wage`, `years`. |
| `POST /api/careers/{id}/market/list/{playerId}` | Altera a lista de venda com `revision`, `listed`. |
| `POST /api/careers/{id}/market/offers/{offerId}/accept` | Confirma termos armazenados usando somente `revision`. |
| `POST /api/careers/{id}/market/offers/{offerId}/withdraw` | Encerra a proposta usando `revision`. |
| `GET /api/careers/{id}/history/{season}` | Consulta tabela e jogos da temporada arquivada. |
| `GET /api/careers/{id}/matches/{round}/{home}/replay?season=2026` | Reproduz uma partida do ano informado. |

As antigas ações diretas `buy` e `sell` são rejeitadas para impedir o contorno da negociação. Valores negativos, salários fora dos limites, IDs de terceiros e revisões antigas também são rejeitados. Caixa, elenco, contrato, proposta, livro caixa e auditoria são gravados juntos na operação EF; a validação transacional em PostgreSQL real ainda precisa ser executada na infraestrutura de destino.

## Limites desta etapa

Não há empréstimos, pagamento parcelado, cláusulas de rescisão, agentes, leilões entre vários compradores, impostos, bônus por atuação ou uma IA econômica avançada. O motor esportivo permanece `positional-v1`, preservando compatibilidade dos replays 0.2. Seu equilíbrio esportivo e o desgaste dos elencos precisam de calibração em uma versão futura do motor. O arquivo é carregado sob demanda na interface, mas ainda pertence ao snapshot JSON completo no backend; separar esse armazenamento será necessário para muitas temporadas/usuários.
