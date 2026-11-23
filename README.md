# futbrx

Versão incremental 0.16 de um manager de futebol 100% web. React 19 + TypeScript + Vite, API ASP.NET Core 10, EF Core e PostgreSQL. O jogador usa somente o navegador; as ferramentas abaixo são para desenvolvimento e hospedagem.

Carreira de treinador com contratos, reputação, ultimatos, desemprego, vagas, entrevistas, propostas, negociações e mercado ativo da IA. A troca de clube exige uma proposta oficial válida. [Fluxos, persistência e balanceamento da versão 0.16](RELEASE-0.16.md).

## Sistemas de um clube vivo

Diretoria com confiança e demissão, personalidades e promessas, empresários e comissões, tratamentos e recaídas, categorias Sub-15/17/20, olheiros, dívidas e patrocínios, torcida, imprensa e comissão técnica. As copas geradas têm transmissões obrigatórias e disputas de pênaltis próprias. [Comportamento, formatos e limites da versão 0.15](RELEASE-0.15.md).

## Comando durante a partida

Multiplicadores de 1× a 96×, substituições e mudanças táticas ao vivo. O intervalo abre o vestiário e aguarda **Ir para o segundo tempo**. Laterais, impedimentos, pênaltis, cartões e assistentes estão no novo motor V7. [Notas e limites da versão](RELEASE-0.14.md).

## Jogador FBX fornecido

O modelo `soccer_player_rigged.fbx` fornecido pelo usuário foi integrado como jogador das partidas e do Estúdio. O rig e as texturas originais são preservados, com 29 animações adaptadas, escala de 1,83 m e ajuste de contato com o gramado. Camisa, calção e meiões recebem as cores da equipe; goleiros e árbitro têm paletas distintas. Equipes com cores muito próximas usam alternativa visitante. O mesmo mesh aparece nos três perfis de qualidade.

No [Estúdio local](http://127.0.0.1:5173/visual), escolha o clube e marque Uniforme de goleiro e luvas para conferir. Detalhes: [integração do FBX](docs/USER-PLAYER-FBX.md).

## Executar

Pré-requisitos do desenvolvedor: Node.js 22.12+ e SDK .NET 10. O modo local usa SQLite persistente; Docker/PostgreSQL são alternativas de hospedagem.

No PowerShell, dentro desta pasta:

```powershell
# Local persistente em .runtime/local-game.db:
./scripts/start-local.ps1

# Demonstração descartável em memória:
./scripts/dev.ps1 -InMemory

# Com banco persistente existente:
./scripts/dev.ps1 -ConnectionString 'Host=localhost;Database=brasileirao;Username=manager;Password=SUA_SENHA'
```

Abra **http://127.0.0.1:5173**, crie uma conta (senha com pelo menos 12 caracteres), escolha um clube e avance a rodada. O script mantém os serviços ativos até pressionar Enter. Não há conta ou senha padrão. Logs em `.runtime/`. O JWT fica somente em memória do navegador; o refresh token fica em cookie HttpOnly.

Alternativa com persistência em containers:

```powershell
Copy-Item .env.example .env
# Edite .env e substitua as duas credenciais por valores aleatórios.
docker compose up --build -d
```

Abra **http://127.0.0.1:8080**. Esse Compose é para desenvolvimento, expõe somente loopback e mantém PostgreSQL em volume. Não execute `docker compose down -v` se quiser preservar os saves.

## O que funciona nesta entrega

- Cadastro, login, refresh com rotação, logout, limite de tentativas e hashing de senha pelo PasswordHasher do ASP.NET.
- Até dez carreiras por conta, cada uma com seu estado independente, salvamento a cada operação e controle de concorrência por revisão.
- Base demonstrativa com vinte clubes, jogadores fictícios, 380 jogos em turno e returno e classificação calculada no servidor.
- Motor posicional C# determinístico, com ticks de dois segundos, deslocamento dos 22 jogadores, decisões de passe, interceptação, finalização, gol e fim de jogo. Formação, mentalidade, habilidade e condição física influenciam a partida.
- Escalação de onze titulares, formações, mentalidade, foco de treino, contratações e vendas com valores conferidos no servidor.
- Calendário completo, caixa, salários, receitas por rodada, gráficos, filtros, ordenação, histórico e início da temporada seguinte.
- Replay Three.js/R3F carregado sob demanda: posições dos jogadores e da bola vêm do servidor, com interpolação de frames, busca na linha do tempo, quatro câmeras, áudio espacial com gravações CC0 e síntese de apoio, pausa, velocidade e qualidade gráfica. Personagens humanos estilizados com biblioteca esquelética de futebol; fallback geométrico em falha do carregamento.
- Catálogo relacional de clubes e jogadores por versão/temporada; importadores CSV/JSON com validação, fonte e licença obrigatórias; editor administrativo com tabelas de clubes e jogadores, rascunhos, revisão concorrente, publicação e duplicação de versões.
- Seleção de catálogo publicado ao criar carreira. Publicar uma nova base não altera saves existentes, incluindo os elencos dos adversários e os replays disputados.
- Tela Conta e sessões: identificação dos dispositivos e revogação com invalidação imediata dos JWTs; worker para remover sessões expiradas.
- Painéis de disponibilidade, jovens do elenco e objetivos da diretoria, com limites da etapa indicados na interface.
- Contratos com vencimento, duração de 1–5 temporadas, salário por rodada e luvas; propostas, contrapropostas e confirmação final com conferência de caixa e revisão do save.
- Mercado dos 19 clubes adversários e agentes livres, com filtros/paginação no servidor; lista de venda, ofertas recebidas e transferência do atleta/dinheiro entre os clubes da carreira.
- Janelas demonstrativas nas rodadas 1–6 e 20–23; renovações e agentes livres durante toda a temporada. Contratos vencidos liberam os atletas ao iniciar o ano seguinte.
- Histórico de temporadas com classificação final, campeão e todas as partidas/replays preservados. O painel baixa jogos antigos somente ao abrir o arquivo do ano escolhido.

A folha salarial é debitada a cada rodada. O modelo inclui empréstimos, parcelamentos e multas rescisórias; ainda não inclui agentes, opções de compra ou bônus condicionais; os clubes controlados pela IA mantêm seus contratos com uma política simplificada. O motor tem posicionamento e decisões autoritativos, mas suas regras usam impedimento por partes válidas do corpo com participação e separação cinemática; ainda faltam física de corpos articulados e arbitragem completa. Não é uma simulação física completa. Todos os movimentos das novas partidas, resultados e eventos são calculados na API, nunca em vídeos pré-gravados. Jogos antigos sem snapshot posicional continuam com reprodução ilustrativa de seus eventos; seus resultados não são recalculados.

## Contato e apresentação — 0.13

Novas partidas usam o motor v6, com disputa pela posse, carrinhos, proteção de bola, desequilíbrio, queda/recuperação e disputa aérea. Os vinte clipes do jogador são inspecionáveis no Estúdio. A passada acompanha a distância percorrida; o controle de bola usa a orientação do portador. Mãos, costuras e acabamento dos uniformes receberam detalhes. A entrada usa caminhada a partir do túnel e corte de câmera para o posicionamento inicial.

A arbitragem foi ampliada com vantagem e retorno à falta original, impedimento no toque do receptor, obstrução da visão do goleiro e checagem do gol antes da confirmação do placar. Há detecção simplificada de braço antinatural em contatos aéreos; não equivale à aplicação integral das leis. Replays e transmissões v5 existentes continuam no motor antigo. Elenco, contratos, departamento médico, estatísticas, formação de jovens, relatórios de treino, extrato financeiro e transferências têm cartões e apresentação esportiva; classificação continua em tabela.

Testes de domínio/API, vinte clipes e compatibilidade aprovados; conferência em larguras de computador/celular. Ainda não há certificação em diferentes GPUs físicas. O catálogo real 2026 continua sem publicação: nova consulta retornou HTTP 429, e seis escudos permanecem incluídos. Detalhes e limites: [entrega 0.13](docs/RELEASE-0.13.md).

## Dia de jogo — 0.13

**Jogar partida** abre uma transmissão em tela inteira: entrada das equipes, câmera de TV, placar progressivo, relógio com acréscimos, lances e áudio opcional. A transmissão usa ritmo fixo de 12× (cerca de oito minutos por jogo), sem pular, adiantar ou encerrar antes do apito final. Pausar é permitido; tempo offline não conta. Ao recarregar, a carreira retoma a transmissão. Resultados, finanças e classificação entram na carreira somente ao voltar ao vestiário após o final.

O motor positional-v6 calcula decisões incrementalmente enquanto o relógio avança; não prepara o resultado antes da transmissão. Reiniciar a API reconstrói somente o trecho já assistido a partir do snapshot e seed persistidos. Isso não representa um novo motor físico ou uma simulação interativa com comandos durante os lances. A gestão fica congelada durante a transmissão. Replays de partidas já concluídas mantêm seus controles de reprodução.

A apresentação foi revista em todas as telas: estádio noturno original, tipografia esportiva, paleta com verde-lima, cartões e navegação de vestiário. O 3D recebeu gramado com textura procedural, redes, marcações, bandeirinhas, túnel e estruturas; a câmera acompanha a bola. Em falha de WebGL, há transmissão tática com o mesmo relógio e resultado.

Detalhes e validação: [transmissão e visual](docs/LIVE-BROADCAST.md).

Estádio 0.12: arquibancadas contínuas, cobertura e iluminação, torcida instanciada com braços/comemoração, bandeiras de tecido animado, bola de 22 cm e gols com boca aberta e rede flexível. Cinco gravações CC0 reproduzem ambiente, gol, quase gol, chute e apito. A pressão da torcida cresce perto da área. Créditos em Dados e licenças. Elenco, prancheta e mercado receberam cartões e interação visual. Detalhes: [entrega 0.12](docs/RELEASE-0.12.md).

## Regras e gestão — 0.9

**Gestão avançada** inclui empréstimos de entrada e saída com retorno automático e vaga reservada, multas rescisórias e parcelas de compras em até 24 rodadas. **Competições** permite duas divisões demonstrativas, antes da primeira rodada, com quatro descensos, dois acessos diretos e playoffs da Série B. Os clubes complementares são fictícios.

Novas partidas usam `positional-v6`, com impedimentos, separação cinemática, reposições, pênaltis, cinco trocas em até três paradas e acréscimos. Replays v1/v2/v3 permanecem nos motores anteriores. A arbitragem ainda é simplificada; não há protocolo VAR nem aplicação integral de todas as leis IFAB.

O jogo gera 380 confrontos e datas próprios em cada temporada, inclusive nos anos futuros. As datas são simuladas: avançar a rodada não depende do relógio real. Saves existentes preservam jogos e resultados. A tabela oficial e suas remarcações não são requisitos do projeto. Seis escudos foram incorporados com créditos, dezoito têm licença revisada, doze downloads estão limitados pelo provedor e dois arquivos seguem sem aprovação de origem/licença. A coleta de atletas alcançou 1.820 linhas candidatas dos vinte clubes; **os elencos reais completos continuam sem publicação**.

Consulte [regras, gestão e fontes 2026](docs/RULES-MARKET-AND-2026.md) para funcionamento, fontes, compatibilidade e limites.

## Visual 3D e dados de 2026

O replay tem jogador humano original estilizado, rosto, três cabelos, quatro tons de pele, uniforme numerado, chuteiras e luvas. São 20 clipes esqueléticos: 17 movimentos autorais de futebol e contato e três de locomoção CC0. Inclui passe, chute, condução, cruzamento, cabeceio, encaixe e defesa para ambos os lados. Há detalhes adicionais no estádio, telão, áudio espacial HRTF com gravações CC0 e volume ajustável. Há perfis Leve/Equilibrada/Alta e seleção automática com redução de qualidade, além de FPS e chamadas de desenho observados. Os três perfis usam agora o FBX fornecido; o perfil Leve reduz público e sombras. Não há garantia de 30/60 FPS em dispositivos não medidos.

**Dados e licenças** mostra a lista de participantes de 2026 conferida na CBF, evidências abertas do Wikidata e créditos dos materiais. Os elencos reais não estão publicados: os vínculos disponíveis exigem conferência atual e a coleta completa encontrou limites/indisponibilidade do provedor. Cinco arquivos de escudos foram incorporados; a situação por clube aparece na tela, com créditos e pendências. As carreiras jogáveis continuam identificadas com atletas fictícios.

Consulte [o relatório de visual e fontes](docs/VISUAL-AND-REAL-DATA.md) para licenças, comandos, limites e pendências. O motor e os snapshots de partidas anteriores permanecem inalterados.

Abra **Estúdio do jogador** para inspecionar os 14 movimentos, examinar o rosto, variar aparência e avançar quadro a quadro. O replay começa em 30× e oferece 1× para ver os contatos em tempo real. Detalhes em [arte humana e animações](docs/HUMAN-AND-FOOTBALL-ANIMATION.md).

## Categorias de base

Abra **Categorias de base** para acompanhar o grupo sub-20, solicitar observações por região/posição, incorporar candidatos e promover atletas. Cada operação mostra seus custos antes da confirmação. A observação leva duas rodadas; os jovens treinam com o clube e preservam XP e atributos ao virar profissionais.

A base começa com seis jovens fictícios, tem dezoito vagas e custa R$ 6.000 por atleta por rodada. Ao completar 21 anos na virada do ano, o jovem não promovido sai como agente livre. Entram até três novos atletas por temporada, conforme as vagas. Competições de base ainda não estão implementadas. Consulte [o guia de formação e captação](docs/YOUTH-AND-SCOUTING.md) para valores, prazos, contratos e limites.

## Treino semanal e evolução

Abra **Treinamentos** para planejar seis dias de preparação, escolher intensidade ou usar modelos técnico, físico e recuperação. O sétimo dia é a partida. Cada atleta pode seguir o treino coletivo ou receber um foco individual em passe, finalização, defesa, físico ou goleiro (apenas goleiros).

O avanço da rodada aplica a carga e a evolução antes de montar os snapshots da partida. Cada 100 XP rende um ponto de atributo, com limite de 99. Idade, condição e intensidade afetam o ganho; lesionados ficam fora e suspensos podem treinar. Os últimos doze relatórios mostram XP, evolução e condição antes do jogo. Reabrir a página ou salvar planos não gera progresso.

As regras e os pesos por posição estão no [guia de treino e desenvolvimento](docs/TRAINING-AND-DEVELOPMENT.md). Saves antigos recebem atributos derivados da habilidade existente, sem evolução retroativa. Novas partidas usam o motor v4; os replays já disputados são preservados. Esta entrega não altera o esquema relacional.

## Disponibilidade e substituições

Em **Táticas**, configure até cinco trocas para a próxima partida, consulte o banco e ative as decisões automáticas. Em **Departamento médico**, acompanhe lesões, amarelos e suspensões; **Elenco** impede escalar atletas indisponíveis. **Estatísticas** reúne números individuais e de clubes, e o histórico arquiva a artilharia de cada ano.

Novas partidas usam `positional-v6`, com faltas, cartões, lesões, trocas e alternância de lados no intervalo. Os replays `positional-v1`, `positional-v2` e `positional-v3` continuam usando suas implementações originais. As regras são demonstrativas; consulte [gestão esportiva e disciplina](docs/SPORTS-AND-DISCIPLINE.md) para limites, compatibilidade e rotas. A versão 0.4 amplia o snapshot JSON e não altera o esquema relacional.

## Contratos e mercado

Abra **Contratos** para ver vencimentos, renovar vínculos e colocar atletas à venda. **Transferências** reúne quatro abas: Mercado, Negociações, Contratos e Movimentações. Uma proposta não debita nem reserva dinheiro. Você confere a resposta, os valores e as luvas antes de confirmar; nessa confirmação, o servidor revalida saldo, janela, vínculo e revisão da carreira.

Propostas entre clubes recebem resposta ao avançar uma rodada; na última rodada da janela, a resposta é imediata. Renovações e agentes livres respondem imediatamente. Uma contraproposta pode aumentar taxa e/ou salário. Propostas expiram após três rodadas, na virada da temporada ou ao fechar a janela entre clubes. Agentes livres não têm taxa de transferência, mas exigem luvas.

Ao iniciar a temporada seguinte, os contratos vencidos são encerrados sem gerar receita. Antes de avançar o ano, revise os avisos na tela Competições e renove os atletas que quiser manter. O caixa não é reiniciado e a escalação é reorganizada após as saídas. Caso venda um titular durante o ano, ajuste a escalação antes da próxima partida. As regras de janela e duração são do jogo demonstrativo, sem alegação de reproduzir regulamentos oficiais.

O [guia de contratos e transferências](docs/CONTRACTS-AND-TRANSFERS.md) detalha regras, rotas, compatibilidade e limites. Saves 0.1/0.2 recebem vínculos ao serem carregados; o novo estado é persistido na operação seguinte. Não há alteração de esquema PostgreSQL na versão 0.3, pois os novos campos pertencem ao snapshot JSON da carreira.

## Administrar bases de temporada

Crie sua conta normalmente. Em **Conta e sessões**, copie o ID da conta. O operador do servidor autoriza IDs de contas existentes; cadastro público não concede administração.

Em desenvolvimento, com a API ainda ativa:

```powershell
./scripts/grant-dev-admin.ps1 -UserId 'ID-DA-SUA-CONTA'
```

Atualize a página para ver **Dados de temporada** ou **Administrar dados**. O script escreve somente `appsettings.Development.json`, ignorado pelo Git e pelo Docker. Isso permite testar o editor em memória sem perder a conta ao reiniciar. Para revogar acesso de desenvolvimento, execute o mesmo comando com `-Revoke`. Em produção, defina `ADMIN_USER_IDS` no `.env` do Compose ou `Admin__UserIds` no ambiente da API e reinicie o serviço.

No editor, baixe os modelos JSON/CSV, importe os dados e valide-os. São exigidos vinte clubes, 18–40 jogadores por clube, dois goleiros e pelo menos dezesseis atletas de linha, IDs únicos, valores válidos e informação de fonte/licença. O CSV contém as linhas `club` e `player`, com aspas/acentos/quebras de linha suportados; as colunas estão no modelo. O JSON admite agentes livres com `clubId` vazio. Metadados preenchidos no editor prevalecem sobre os do arquivo.

Salve um rascunho antes de publicar. Clubes e jogadores podem ser editados em tabelas; arquivos CSV são convertidos para JSON ao abrir o rascunho salvo. Versões publicadas são imutáveis e ficam disponíveis para novas carreiras. Para atualizar um catálogo, use **Duplicar** e um identificador de versão novo. A documentação da importação e do replay está em [docs/DATA-AND-REPLAY.md](docs/DATA-AND-REPLAY.md).

## Verificação

```powershell
Push-Location frontend
npm ci
npm run build
Pop-Location
dotnet run --project backend/tests/SimulationTests
dotnet run --project backend/tests/ApiTests
node --experimental-strip-types frontend/tests/playback.test.mjs
# Com a API ativa em 5080, em um banco exclusivo para testes:
node backend/tests/api-smoke.mjs
```

O smoke HTTP cria contas fictícias e carreiras e verifica isolamento, transmissão sem salto, proteção de resultados futuros, retomada, congelamento das táticas e rotação/revogação do refresh token. Temporadas completas e viradas são verificadas pela suíte da simulação. Use `API_URL` para outro endereço. A suíte ApiTests executa em TestServer, testa importação e publicação administrativa, preservação de snapshots e replays, autorização e revogação imediata de sessões. Os testes do motor verificam calendário, CSV com aspas/quebras, validação do catálogo, determinismo dos frames, limites do campo e consistência entre eventos e placares. Há testes JavaScript da busca/interpolação dos frames. O workflow de CI inclui integração com PostgreSQL real; esse workflow precisa ser executado no repositório remoto.

Verificação realizada: builds TypeScript/Vite e C#, suites do motor, catálogo, TestServer e HTTP com EF InMemory, geração/consistência da migração e inspeção no Chromium. Docker, PostgreSQL real, CI remota, Safari/Firefox/Edge, GPUs diferentes e metas de FPS **não foram verificados neste ambiente**. EF InMemory serve exclusivamente para desenvolvimento e não comprova garantias transacionais de PostgreSQL. O build ainda alerta sobre bundles grandes; a cena 3D e o editor são carregados sob demanda.

## Organização

```text
frontend/src/
  app/                 Rotas, autenticação e telas de gerenciamento
  components/ui/       Button no padrão shadcn/ui (Radix, CVA)
  features/match3d/    Replay lazy com Three.js/R3F/Drei
  features/catalog/    Importação e editor administrativo
  features/account/    Gestão de sessões
  features/transfers/  Propostas, renovações e mercado entre clubes
  features/career/     Arquivo de temporadas
  services/            Contratos tipados e cliente da API
  stores/              Zustand: carreira selecionada e navegação
  styles/              Design system e responsividade
backend/src/
  Domain/              Entidades e contratos de estado
  Simulation/          Motor C# sem dependência da renderização ou EF
  Infrastructure/      DbContext PostgreSQL
  Api/                 Autenticação, autorização, operações e migrações EF
backend/tests/         Testes do motor e integração HTTP
infrastructure/        Containers, Nginx e HTTPS com Caddy
docs/                  Arquitetura, segurança e próximas etapas
```

As telas de gerenciamento ainda estão concentradas no shell React; a extração em features menores será feita ao aprofundar os módulos. TanStack Query gerencia dados remotos; Zustand guarda apenas escolhas de interface. Tabelas usam TanStack Table e finanças usam Recharts. i18next está configurado para pt-BR; a tradução integral para outros idiomas ainda não está implementada.

## Publicar com HTTPS

É necessário um servidor com Docker, domínio e DNS apontando para ele. Defina `DOMAIN` no `.env`, além das credenciais fortes. Então:

```powershell
docker compose -f compose.yml -f compose.production.yml up --build -d
```

Caddy emite os certificados e encaminha para Nginx, que serve o frontend e faz proxy da API na mesma origem. Portas 80/443 devem estar acessíveis. Em Production os cookies de refresh exigem HTTPS. O banco e a API não têm portas públicas. As migrações EF são aplicadas no início nesta implantação de uma instância; antes de múltiplas réplicas, extraia a migração para um job de release. **Esta entrega não foi publicada e não está certificada para produção.**

Backups manuais: `./scripts/backup.ps1`. Agende esse script no servidor e envie os dumps para armazenamento externo com retenção e teste de restauração antes de hospedar dados reais. Sem esse agendamento, não existem backups automáticos. A configuração atual de rate limiting usa o IP observado pela API; atrás do proxy ele é agregado. Configure proxies confiáveis e limites por IP no edge antes de abrir o serviço publicamente.

## Pendências explícitas

Consulte [docs/ROADMAP.md](docs/ROADMAP.md). Esta entrega implementa carreiras com contratos, negociações e múltiplas temporadas, mas ainda não corresponde ao simulador profundo completo descrito no pedido. Elencos reais completos/licenciados, todos os escudos, arbitragem integral, regras adicionais de negociação e infraestrutura de nuvem ainda têm pendências. Modelos GLB humanos e animações esqueléticas de futebol estão implementados na 0.8. O “prompt mestre anterior” mencionado no anexo não foi fornecido.

Referência técnica da autenticação: [documentação oficial de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).
