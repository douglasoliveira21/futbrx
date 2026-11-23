# Estado da implementação e continuidade

## Entregue na versão 0.13 — contatos e apresentação

- Motor v6 com proteção de bola, disputa de posse, desarme em pé/carrinho, desequilíbrio e disputa aérea. V5 congelada e transmissões antigas retomadas na própria versão.
- Vinte clipes (seis novos), detalhes de mãos/roupa e passada por distância; caminhada na entrada e corte para a formação.
- Vantagem e retorno à falta, impedimento na participação, obstrução da visão e checagem antes de confirmar gols; contato de mão antinatural simplificado. Arbitragem integral continua pendente.
- Departamento médico, estatísticas, contratos, histórico do mercado, extrato financeiro, jovens e relatórios de treino convertidos para cartões; perfis de qualidade mantidos e atualização da torcida limitada a 12/24 Hz.
- Gráfico financeiro sob demanda, JavaScript principal de 892 kB para 480 kB e inicialização React separada dos componentes.
- Coleta aberta novamente limitada por HTTP 429; seis escudos incluídos, catorze pendentes e elencos reais não publicados.
- Ver [validação e limites](RELEASE-0.13.md).

## Histórico — entregue na versão 0.12

- Cálculo incremental v5 durante a transmissão, reconstrução determinística após reiniciar, movimentação com aceleração e marcação coletiva.
- SQLite local persistente e chave de autenticação persistida; testes de reinício durante a partida.
- Elenco em cartões, prancheta com trocas e mercado visual.
- Estádio reconstruído, torcida reativa, bandeiras animadas, bola de 22 cm, traves e rede flexível; cinco gravações CC0 com fontes e hashes.
- Seis escudos incluídos; elencos reais completos e outros escudos continuam pendentes.
- Ver [entrega e validação 0.12](RELEASE-0.12.md).

## Histórico — entregue na versão 0.11 — dia de jogo

- Transmissão obrigatória de cada rodada do clube: entrada das equipes, placar e lances progressivos, câmera de TV e acompanhamento a 12× sem adiantar ou pular.
- Sessão persistida no snapshot, retomada após recarregar, pausas sem crédito de tempo, relógio compartilhado entre abas e resultado registrado somente no final.
- API recusa avanço instantâneo, encerramento precoce e mudanças na carreira durante a partida; entrega apenas frames e eventos já liberados.
- Visual compartilhado de estádio/vestiário em todas as telas, com nova identidade esportiva e chamada principal para entrar em campo.
- Gramado procedural, redes, marcações, túnel, bandeiras, estruturas de cobertura e árbitro humano no perfil com rig. Alternativa tática quando WebGL falha.
- Testes de domínio, API, integração HTTP, reprodução e conferência no navegador. Ver `LIVE-BROADCAST.md`.

## Histórico — entregue na versão 0.10 — calendário por temporada

- Gerador determinístico por carreira, ano e divisão: 380 jogos, turno/returno, 19 mandos por clube, descanso entre rodadas e última rodada simultânea.
- Novas tabelas nos anos futuros e calendário próprio nas duas divisões e playoffs. Datas simuladas permitem avançar no ritmo do jogador.
- Migração preenche datas futuras ausentes sem substituir confrontos, partidas disputadas ou replays.
- Calendário oficial e remarcações reais retirados do escopo por decisão do usuário. Ver `SEASON-CALENDAR.md`.

## Histórico — entregue na versão 0.9 — regras e gestão

- Motor v4 com impedimento por posição, separação cinemática, tiros livres/pênaltis, saídas/reposições, limite de três paradas de substituição e acréscimos com replay de duração variável. V1/v2/v3 preservados.
- Desempates, duas divisões demonstrativas optativas, playoffs no formato 2026, acesso/descenso mantendo o clube controlado e histórico completo.
- Empréstimos de entrada e saída com propriedade preservada, vaga reservada e retorno; multas rescisórias; até 24 parcelas com obrigações entre temporadas e liquidação idempotente.
- Calendário CC0 de 380 jogos, fontes/hashes, cinco escudos incluídos e licença individual revisada em dezoito. Downloads restantes sob HTTP 429; dois arquivos sem origem/licença aprovada.
- 1.820 linhas candidatas de atletas para os vinte clubes; nenhuma base incompleta publicada. Identidades limitadas em 500 pelo provedor. Elencos reais completos continuam pendentes.
- Testes da simulação/API, calendário, finanças, vagas, empréstimos, divisões e replays; conferência no navegador.

Ver `RULES-MARKET-AND-2026.md`. Arbitragem integral, física realista e uma Série B com elencos reais não fazem parte desta expansão demonstrativa.

## Histórico — entregue na versão 0.8 — jogador humano e futebol

- Geometria humana original estilizada com rosto, três cortes de cabelo, tons de pele, uniforme numerado, chuteiras e luvas. GLB local com 14 clipes e fonte de arte reprodutível.
- Onze coreografias esqueléticas originais: repouso, condução, passe, chute, cruzamento, cabeceio, prontidão, encaixe, duas defesas e comemoração. Três clipes de locomoção CC0 com créditos separados.
- Motor `positional-v3`, ações/timestamps autoritativos, bola aérea e adaptação visual dos replays antigos; v1/v2 congelados.
- Estúdio interativo para inspecionar rosto, aparência e movimentos, com pausa e quadro a quadro. Regressões de modelo, deformação, animação, determinismo, API e temporada.
- Arte final para a direção estilizada adotada; não é captura de movimento nem reprodução de atletas reais. Ver `HUMAN-AND-FOOTBALL-ANIMATION.md`.

## Histórico — entregue na versão 0.7 — visual e levantamento de fontes

- Avatar GLB local, esqueleto independente por atleta e sete clipes CC0 selecionados da biblioteca Quaternius. Uniformes/pele por máscaras de vértices; animações de corrida, repouso e comemoração, com chute procedural sobre o esqueleto. É um manequim estilizado, não um pacote completo de futebol profissional.
- Assentos por instancing, bancos, túnel, bandeirinhas e telão. Áudio HRTF sintetizado, controle de volume e suspensão com pausa/fora da visibilidade.
- Perfis Leve/Equilibrada/Alta, seleção automática conservadora, redução por FPS observado, câmera perto do campo e contadores de render. Sem certificação de desempenho em GPUs/dispositivos diversos.
- Tela Dados e licenças, hashes/licença dos materiais e participantes reais de 2026 conferidos na CBF. Mapeamento Wikidata dos vinte clubes e coleta parcial de evidências de atletas.
- Coletor com cache/consulta em lote e respeito a indisponibilidade, mais gerador que bloqueia catálogos incompletos ou vínculos não revisados. Nenhum elenco real incompleto foi publicado e nenhum save foi substituído.
- **Pendente nesta frente:** elencos 2026 completos e conferidos e arquivos de escudos. A arte humana e as animações específicas foram entregues na 0.8. Fontes abertas não garantem vínculo atual; download de escudos foi bloqueado e a consulta completa do provedor falhou.

## Entregue na versão 0.6

- Elenco de base separado do profissional, seis jovens iniciais, limite de dezoito, manutenção por rodada e ingresso de até três jovens por temporada.
- Observação por região/posição com taxa, espera de duas rodadas, seis candidatos e validade limitada. Captação individual com conferência de caixa e vaga.
- Promoção com luvas, salário e contrato profissional, preservando identidade e evolução. Conferência dos termos antes de confirmar e movimentação financeira registrada.
- Integração da base ao microciclo e foco individual; relatórios distinguem Base e Profissional. Atletas não promovidos saem como livres ao completar 21 anos, sem receita fictícia.
- Upgrade idempotente de saves, IDs próprios, limite de elencos, auditoria e revisão concorrente. Testes de prazos, saldo, cobranças repetidas, treino, promoção, virada anual e isolamento HTTP.
- Regras e limites em [YOUTH-AND-SCOUTING.md](YOUTH-AND-SCOUTING.md). Não há competição de base nesta entrega.

## Entregue na versão 0.5

- Calendário de seis sessões de preparação por rodada, intensidade leve/normal/alta, modelos de treino e exigência de um dia de descanso ou recuperação.
- Desenvolvimento de passe, finalização, defesa, físico e goleiro; XP por idade/condição/carga, ganho a cada 100 XP e teto de 99. Foco individual e habilidade geral ponderada por posição.
- Carga aplicada uma vez antes da partida, com reflexo na condição do snapshot. Lesionados não treinam e suspensos podem participar. Os adversários usam um plano padrão.
- Últimos doze relatórios com temporada/rodada, plano aplicado, XP, atributos e condição antes do jogo; consulta isolada por conta e escrita com revisão concorrente.
- Upgrade dos saves sem evolução retroativa, perfis que acompanham atletas nas transferências e preservação de replays anteriores. Campos adicionais no JSON, sem migração relacional.
- Testes de validação, aplicação única, limite de atributos, idade, lesão, carga, descanso, persistência, isolamento HTTP e tentativa de conceder XP pelo cliente.
- Regras e limites descritos em [TRAINING-AND-DEVELOPMENT.md](TRAINING-AND-DEVELOPMENT.md).

## Entregue na versão 0.4

- Motor `positional-v2` com faltas, amarelos, segundo amarelo, expulsão, lesões, cinco substituições e troca de lados no intervalo. A implementação `positional-v1` foi preservada para replays antigos.
- Banco de até doze reservas, planos para a próxima partida, trocas automáticas opcionais e recomposição de atletas indisponíveis. Saídas e entradas aparecem nos eventos e nos frames do replay.
- Recuperação de lesões por rodada, suspensão de um jogo por expulsão ou três amarelos, disponibilidade no elenco e departamento médico.
- Estatísticas individuais de jogos, titularidade, minutos, gols, chutes, passes, interceptações, faltas e cartões; artilharia e arquivo por temporada. Saves antigos indicam a rodada inicial da cobertura, sem inventar dados retroativos.
- Testes do ciclo de punição/recuperação, identidade e minutos após trocas, limite de cinco, frames após expulsão, permissões HTTP, revisão concorrente e compatibilidade do dispatch legado.
- Regras e limites demonstrativos descritos em [SPORTS-AND-DISCIPLINE.md](SPORTS-AND-DISCIPLINE.md).

## Entregue na versão 0.3

- Contratos por jogador/clube, vencimento, salário por rodada, luvas e duração de uma a cinco temporadas. Renovações estendem o vínculo e impactam o caixa somente após confirmação.
- Propostas entre clubes, contrapropostas, recusas, prazos e confirmação final. Clubes compradores e vendedores têm seus próprios caixas dentro da carreira; uma venda não cria uma receita sem contraparte.
- Lista de transferências e ofertas recebidas; mercado paginado com filtros de posição, clube, nome, taxa e ordenação. Agentes livres não cobram taxa, mas negociam salário/luvas.
- Janelas demonstrativas nas rodadas 1–6 e 20–23, aplicadas pelo servidor na proposta e no fechamento; agentes livres e renovações em toda a temporada.
- Final de contrato libera atletas ao iniciar o próximo ano. Há aviso antes da virada, caixa preservado e recomposição da escalação.
- Arquivo de temporadas com campeão, tabela e partidas/replays. Jogos históricos são baixados separadamente da visão geral. As informações financeiras novas incluem a temporada e não se misturam nos gráficos de anos seguintes.
- Compatibilidade automática dos saves 0.1/0.2, com preservação de caixa e snapshots já disputados. Diálogos de negociação usam foco modal nativo, Escape e revisão explícita dos termos.
- Testes de conservação dos valores, dupla confirmação, revisão antiga, payload adulterado, isolamento de contas, janela, vencimentos e replays após mudança de temporada.

## Entregue na versão 0.2

- Catálogos relacionais versionados de clubes/jogadores, rascunhos, importação JSON/CSV, validação e metadados de fonte/licença. Editor administrativo com edição tabular, exportação, duplicação, revisão concorrente e publicação imutável.
- Administrador autorizado por ID de conta na configuração do servidor. Cadastro e requisições do navegador não atribuem esse acesso. Toda rota administrativa aplica a política no backend.
- Nova carreira escolhe uma base publicada e copia seus dados, incluindo elencos adversários. Atualizações do catálogo preservam carreiras existentes.
- Motor C# positional-v1: ticks de dois segundos, formações 4-3-3/4-4-2/3-5-2, mentalidade, deslocamento, posse, passes, interceptações e finalizações. API fornece a timeline de posições; o cliente interpola os frames.
- Snapshot de escalação/tática/condição por partida e seed persistida. Replay regenera movimentos e verifica placar; mudanças futuras na tática/elenco não reescrevem jogos anteriores.
- Quatro câmeras, busca na linha do tempo e reprodução legada para partidas antigas sem snapshot. Frames só são materializados ao abrir um replay.
- Gestão de sessões com revogação imediata de JWTs, rotação de refresh com controle de concorrência, auditoria de cadastro/login/logout/revogação e limpeza horária de sessões expiradas.
- Migração EF incremental e SQL idempotente. Testes de importação, permissões, isolamento, snapshots, replay, interpolação e temporada completa.

Os registros versionados de técnicos, estádios e competições ainda não têm catálogo administrativo. Fonte/licença são informações declaradas pelo operador, não uma verificação automática de direitos.

## Próximas entregas de gestão esportiva

1. Ampliar empréstimos com opções/obrigações de compra e percentuais salariais, bônus condicionais, intermediários e decisões mais elaboradas da IA. Empréstimos, parcelas e multa básica foram entregues na 0.9.
2. Aprofundar desenvolvimento com comissão técnica, potencial individual, declínio por idade, instalações e planejamento por datas reais. Treino por atributo e microciclo demonstrativo já foram implementados.
3. Assistências e critérios adicionais de competições futuras. Desempates de 2026 foram integrados na 0.9.
4. Competições de base, relatórios de potencial, competições adicionais e divisões com elencos reais. Acesso/descenso demonstrativo e calendário aberto de 2026 foram integrados na 0.9.

## Próximas entregas do motor 3D

1. Completar arbitragem: vantagem com cartões diferidos em todos os contextos, mão com anatomia física, desafios/interferência sem toque além da linha de visão, VAR de pênalti/vermelho/identidade e abandono/W.O. A v6 já inclui vantagem simples, retorno à falta, impedimento na recepção, visão obstruída e checagem de gols.
2. Refinar qualidade profissional, contatos por anatomia, variações com perna esquerda e física articulada. A v6 e os novos clipes da 0.13 já incluem carrinhos, quedas e disputas aéreas. Modelo humano final estilizado e movimentos de passe, chute, condução, cruzamento, cabeceio e goleiro foram entregues na 0.8.
3. Ampliar variedade de cânticos e coreografias. Torcida reativa, bandeiras de tecido, arquibancadas contínuas, bola proporcional, traves/rede e cinco gravações CC0 foram entregues na 0.12.
4. LOD, texturas KTX2, compressão Draco/Meshopt, CDN/S3 e carregamento progressivo. Instancing, DPR, sombras e suspensão fora da visibilidade já existem, mas faltam medições de 30/60 FPS em dispositivos reais.
5. SignalR autenticado para transmissão de ticks e eventos quando houver partidas ao vivo/streaming. Hoje a transmissão já usa cálculo incremental e entrega por HTTP; SignalR é uma alternativa de transporte futura.

## Operação antes de produção pública

- Rodar suíte contra PostgreSQL e validar migração em banco vazio e upgrade de saves da primeira versão. O SQL/mapeamento foi gerado e conferido, mas não executado contra PostgreSQL local porque Docker/PostgreSQL não estão disponíveis neste ambiente.
- Configurar recuperação de senha e verificação de e-mail com provedor transacional. Ainda não há envio de e-mail, recuperação ou exclusão de conta com política de dados.
- Configurar observabilidade, métricas, tracing, alertas, health check do banco e proxies confiáveis. O rate limit atual usa o IP observado pela API, agregado atrás de Nginx; usar edge/distribuição para abertura pública.
- Agendar backup/restore e testar retenção externa. O script de dump entregue continua manual.
- Executar CI remota e promover imagens para um provedor escolhido. Redis e workers distribuídos entram conforme a carga; o worker atual apenas limpa sessões.
- Revisar acessibilidade, foco de modais, i18n integral, Safari/Firefox/Edge, telas/GPUs diferentes e otimizar bundles grandes. Editor e partida 3D já usam lazy loading.
- Publicação, domínio, CDN/S3 e assets licenciados continuam dependentes de infraestrutura e materiais ainda não fornecidos.
- Separar arquivos históricos da coluna JSON da carreira antes de ampliar para muitas temporadas/usuários. A API já fornece arquivos sob demanda, mas a persistência atual ainda serializa o snapshot completo no backend.
