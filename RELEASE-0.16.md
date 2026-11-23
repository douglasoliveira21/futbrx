# 0.16 — Carreira de treinador e mercado de técnicos

A carreira continua depois da saída de um clube. Não existe mais contratação por escolha livre de escudo: uma mudança exige vaga, seleção da diretoria, proposta oficial vigente e assinatura de termos calculados no servidor.

## Fluxo jogável

- **Diretoria:** confiança contextual, sete faixas de avaliação, personalidade por clube, detalhamento dos fatores e ultimatos com progresso e prazo em jogos. Resultados são ponderados pela força dos adversários, projeto, campanhas recentes de 5/10/20 jogos, finanças, base, moral, relacionamento, torcida e evolução em relação à temporada anterior.
- **Saída:** demissão contextual, término do contrato ou pedido de demissão. O comunicado preserva campanha, datas, motivo, classificação, títulos e indenização. Sem contrato, nenhuma ação de gestão pode modificar o ex-clube.
- **Treinador livre:** avanço de um dia ou uma semana; jogos, copas, calendário, temporadas e mercado continuam pela IA. Enquanto empregado, esse avanço é bloqueado e a transmissão obrigatória permanece.
- **Mercado:** vagas com prazo, candidaturas, concorrentes da IA, sondagens, entrevistas, propostas espontâneas, recusa e expiração. Uma candidatura pode ser rejeitada. Uma carreira regional não recebe automaticamente uma contratação de elite.
- **Contratos:** salário semanal, duração, objetivo, orçamento e cláusula de rescisão negociáveis em até três rodadas. A diretoria aceita, rejeita ou contrapropõe. Multas são pagas pelo novo clube ao anterior. Renovação e proposta de permanência encerram o vínculo anterior antes de criar o próximo.
- **Minha carreira:** contratos, demissões, pedidos de saída, jogos, V/E/D, aproveitamento por vínculo, invencibilidade, placares extremos, títulos, prêmio da simulação, reputação e linha do tempo.
- **Técnicos da IA:** nomes gerados a partir da semente, vínculos exclusivos, formações e estilos usados nas próximas partidas, avaliações, demissões, pedidos de saída, contratos, transferências, renovações, desemprego e aposentadoria.

As notícias são emitidas por decisões e resultados realmente registrados. Não há manchetes de negociações inexistentes. As personalidades, técnicos e regras são dados dinâmicos desta simulação, não uma base oficial de vínculos profissionais. Sem ligas internacionais ou seleções jogáveis, não são oferecidos empregos nesses mercados. Os quatro adversários continentais possuem técnicos da IA, mas não vagas de emprego jogáveis.

## Integração e integridade

`ClubLifeState.Coaches` faz parte do snapshot JSON existente, persistido pelo EF Core no banco configurado (SQLite no ambiente local). A revisão concorrente da carreira e o `SaveChanges` atômico abrangem contratos, vaga, pagamentos e auditoria. Uma revisão antiga recebe HTTP 409. Não é possível ter dois contratos ativos para o mesmo técnico ou dois técnicos no mesmo clube.

`GET /api/careers/{id}/coach` fornece a visão do dono da carreira; `POST .../coach/actions` processa as decisões. Valores de confiança e reputação enviados pelo navegador não são utilizados. Ao aceitar, salário, prazo, orçamento e multa vêm da proposta persistida. Todas as mutações são bloqueadas durante uma transmissão.

Na mudança de clube, o elenco anterior, a base, a comissão, o caixa e o livro financeiro são conservados. O antigo clube segue pela IA e abre seleção para o cargo. O novo clube assume a interface de gestão. Com duas divisões ativadas, a mudança troca corretamente a liga observada; partidas e snapshots existentes não são reconstruídos. Estatísticas são vinculadas ao contrato que comandava o clube naquela partida.

A migração importa os resultados disponíveis da temporada atual e das copas. Ela não inventa contratos anteriores desconhecidos. Novas avaliações e eventos são idempotentes, com chaves por partida/temporada; ler uma carreira não recalcula ou adianta resultados. Os motores e snapshots de replays antigos são preservados.

## Balanceamento

O operador pode configurar a seção `CoachCareer` de `backend/src/Api/appsettings.json` ou as variáveis de ambiente equivalentes. Os parâmetros são copiados para cada nova carreira; saves existentes mantêm suas regras para preservar a reprodução da simulação.

```json
{
  "CoachCareer": {
    "ConfidenceBands": [90, 75, 60, 45, 30, 15],
    "UltimatumThreshold": 30,
    "DismissalThreshold": 20,
    "CriticalThreshold": 10,
    "CriticalDismissalChance": 90,
    "BaseDismissalChance": 35,
    "UltimatumGames": 4,
    "UltimatumWins": 2,
    "OfferValidityDays": 14,
    "VacancySelectionDays": 14,
    "ReputationTolerance": 18,
    "ApplicationChance": 60,
    "InformalInterestChance": 55,
    "AiRetirementAge": 70
  }
}
```

Os números são parâmetros de jogo e precisam de calibração conforme mais temporadas forem jogadas. As probabilidades usam semente e estado salvos; não representam previsão estatística do futebol real.

## Validação

Os testes de carreira cobrem exclusividade de contrato e vaga, cláusulas, pagamentos, negociação, rejeição, expiração, bloqueio da troca livre, desemprego, comunicado, continuidade do mundo, IA, reputação, migração, virada de temporada, persistência e reprodução determinística. A integração HTTP verifica isolamento entre contas, revisão concorrente, termos forjados, bloqueio da transmissão pulada e recuperação das permissões ao assinar.

O calendário permanece gerado, conforme a preferência do projeto. Regulamentos simplificados de copas, elencos reais completos e licenças de escudos pendentes continuam no escopo documentado da versão anterior.

Conferência desta entrega: 37 verificações específicas de carreira, 35 dos bastidores, 136 verificações HTTP/persistência, 279 da regressão geral e oito testes do frontend passaram. A transmissão foi conferida com 32 sementes fixas, incluindo laterais, impedimentos, cartões e pênaltis. O build da API e do frontend passou. No navegador, foram verificados comunicado de saída, calendário livre, movimentação da IA, candidatura recusada, retorno à carreira anterior e layout de 390 px sem transbordamento horizontal. Os testes de mercado usaram uma carreira separada da carreira anterior do navegador.
