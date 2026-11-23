# Dados versionados e reprodução de partidas

## Catálogo

`DataReleases` identifica versão, temporada, origem/licença, revisão e publicação. `ClubData` e `PlayerData` têm chaves compostas com `ReleaseId`, permitindo que o mesmo atleta/clube apareça em várias temporadas sem sobrescrever outras versões. O vínculo vazio de um jogador representa agente livre. Identificadores de jogador são únicos dentro de uma versão.

`POST /api/admin/catalogs/validate` analisa `format`, `content`, `version`, `season`, `source`, `license`. A análise não grava dados. `POST /api/admin/catalogs/` cria um rascunho. `POST /{id}/edit` exige a revisão corrente; `POST /{id}/publish` valida novamente, incrementa a revisão e torna a versão imutável. Essas rotas exigem a política Admin e registram auditoria. O EF aplica a gravação relacional com concorrência otimista; testes locais InMemory não substituem o teste transacional em PostgreSQL.

O CSV usa cabeçalho `kind,id,clubId,name,short,color,strength,position,age,rating,value,wage`. Há uma linha `club` por clube e uma linha `player` por jogador. Campos não usados em um tipo de linha ficam vazios. O parser aceita vírgula ou ponto-e-vírgula, aspas escapadas (`""`) e campos entre aspas com quebras de linha. Os templates podem ser baixados no editor.

Limites: 20 clubes, 18–40 jogadores por clube, pelo menos dois goleiros e dezesseis jogadores de linha, 1.500 jogadores por catálogo, conteúdo de até um milhão de caracteres e requisição de até 2 MB. A validação rejeita IDs repetidos, vínculos inexistentes, posições inválidas, valores negativos e metadados ausentes. O editor recebe o limite mais conservador de 1 MB para arquivos locais.

`GET /api/catalogs` lista somente versões publicadas. `GET /api/clubs?releaseId=...` retorna clubes da versão escolhida. `POST /api/careers/` recebe `releaseId`; sem ele, usa a última versão publicada. O estado da carreira contém uma cópia independente. Uma nova publicação nunca atualiza automaticamente uma carreira antiga.

## Motor posicional

Uma partida nova captura `MatchSetup`: versão do motor, escalações, posições, habilidade, condição, formação e mentalidade das duas equipes. O snapshot e a seed são persistidos no fixture. O mesmo `MatchSimulation.Run` produz o resultado da rodada e os frames do replay, sem decisões esportivas no JavaScript.

O tick é de dois segundos de jogo. O motor determina alvos de posicionamento por formação, deslocamento com limite de velocidade, portador da bola, passes, interceptações, chutes, defesas e gols. Probabilidades consideram habilidade/condição e mentalidade. A geometria/IA ainda é simplificada: faltam colisões, impedimento, faltas e várias outras regras reais.

O resumo do save mantém os lances decisivos e o snapshot. Passes/interceptações e os frames são regenerados somente ao abrir `GET /api/careers/{id}/matches/{round}/{home}/replay`. A rota verifica o proprietário da carreira antes de retornar os dados e confere os gols reproduzidos contra o placar persistido. A timeline contém um frame a cada seis segundos, além dos instantes de gol; portanto tem pelo menos 901 frames para 90 minutos.

O frontend localiza os dois frames vizinhos por busca binária, interpola posição e faz interpolação angular pelo arco mais curto. Apenas a representação geométrica, a câmera e o áudio são locais. Pausa e busca não enviam alterações de resultado ao servidor. O replay inclui a formação de cada lado, mesmo que a formação atual da carreira tenha mudado.

Não alterar o algoritmo `positional-v1` depois de utilizá-lo com saves reais sem manter sua implementação anterior. Futuras mudanças do motor devem introduzir uma nova versão com dispatch de reprodução para a versão armazenada. Jogos da primeira entrega, sem snapshot, preservam seus placares e continuam com reprodução ilustrativa dos eventos.

## Sessões

O JWT inclui `session_id`; cada requisição autenticada verifica se essa sessão ainda existe e não expirou. A rotação de refresh troca o hash do segredo aleatório e mantém o mesmo identificador de sessão. O hash é concurrency token do EF. Uma corrida de refresh pode retornar 409 e deve ser tratada como sessão atualizada, sem criar sessões paralelas.

`GET /api/auth/sessions/` lista apenas as sessões do usuário. `POST /api/auth/sessions/{id}/revoke` também filtra pelo proprietário. Revogar uma sessão remove seu acesso imediatamente; não há espera pelos quinze minutos do JWT. Um worker remove até mil sessões expiradas por hora. Segredos de refresh nunca são retornados pela listagem nem armazenados em texto puro.

JWTs antigos da instalação 0.1 não contêm `session_id` e deixam de ser aceitos nas rotas protegidas. Um refresh cookie ainda válido pode renová-los automaticamente; caso ele tenha expirado, é necessário entrar novamente. Nas sessões legadas, a data de criação passa a ser o momento da migração, pois o registro anterior não a armazenava. Os saves continuam preservados. Em produção, backups e execução das migrações devem preceder o restart.
